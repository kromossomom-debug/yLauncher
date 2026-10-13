[CmdletBinding()]
param(
    [switch]$SkipPortable
)

$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$sourceRoot = Join-Path $projectRoot 'src'
$buildRoot = Join-Path $projectRoot 'build\release'
$distRoot = Join-Path $projectRoot 'dist'
$portableRoot = Join-Path $distRoot 'yLauncher_v2.1_Portable'

function Reset-ProjectDirectory([string]$Path) {
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $rootPrefix = $projectRoot.TrimEnd('\') + '\'
    if (-not $fullPath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Caminho de saída fora do projeto: $fullPath"
    }
    if (Test-Path -LiteralPath $fullPath) {
        Remove-Item -LiteralPath $fullPath -Recurse -Force
    }
    New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
}

function Find-CSharpCompiler {
    $knownCompiler = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe'
    if (Test-Path -LiteralPath $knownCompiler) {
        return $knownCompiler
    }

    $searchRoot = 'C:\Program Files (x86)\Microsoft Visual Studio'
    $candidate = Get-ChildItem -LiteralPath $searchRoot -Filter csc.exe -Recurse -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -like '*\MSBuild\Current\Bin\Roslyn\csc.exe' } |
        Select-Object -First 1
    if ($candidate) {
        return $candidate.FullName
    }

    throw 'Compilador C# do Visual Studio Build Tools não encontrado.'
}

function Invoke-Compiler([string[]]$Arguments) {
    & $compiler @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Falha de compilação (código $LASTEXITCODE)."
    }
}

function New-DeterministicZip([string]$SourceDirectory, [string]$DestinationPath) {
    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    if (Test-Path -LiteralPath $DestinationPath) {
        Remove-Item -LiteralPath $DestinationPath -Force
    }

    $archive = [System.IO.Compression.ZipFile]::Open(
        $DestinationPath,
        [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        $fixedTimestamp = [System.DateTimeOffset]::Parse('2026-01-01T00:00:00Z')
        $files = Get-ChildItem -LiteralPath $SourceDirectory -File | Sort-Object Name
        foreach ($file in $files) {
            $entry = $archive.CreateEntry($file.Name, [System.IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = $fixedTimestamp
            $entryStream = $entry.Open()
            $fileStream = [System.IO.File]::OpenRead($file.FullName)
            try {
                $fileStream.CopyTo($entryStream)
            }
            finally {
                $fileStream.Dispose()
                $entryStream.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}

Reset-ProjectDirectory $buildRoot
Reset-ProjectDirectory $distRoot
$compiler = Find-CSharpCompiler

$launcher = Join-Path $buildRoot 'yLauncher.exe'
$uninstaller = Join-Path $buildRoot 'Desinstalar_yLauncher.exe'
$installer = Join-Path $distRoot 'Instalador_yLauncher_v2.1.exe'

Invoke-Compiler @(
    '/nologo', '/target:winexe', '/platform:x86', '/optimize+', '/deterministic+', '/langversion:latest',
    "/win32icon:$(Join-Path $sourceRoot 'runtime\app.ico')",
    "/out:$launcher",
    '/reference:System.Windows.Forms.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Management.dll',
    (Join-Path $sourceRoot 'runtime\Program.cs'),
    (Join-Path $sourceRoot 'runtime\Properties\AssemblyInfo.cs')
)

Invoke-Compiler @(
    '/nologo', '/target:winexe', '/platform:x86', '/optimize+', '/deterministic+', '/langversion:latest',
    "/win32icon:$(Join-Path $sourceRoot 'uninstaller\app.ico')",
    "/out:$uninstaller",
    '/reference:System.Windows.Forms.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Management.dll',
    (Join-Path $sourceRoot 'uninstaller\UninstallProgram.cs'),
    (Join-Path $sourceRoot 'uninstaller\UninstallForm.cs'),
    (Join-Path $sourceRoot 'uninstaller\Properties\AssemblyInfo.cs')
)

$index = Join-Path $sourceRoot 'installer\index.html'
$icon = Join-Path $sourceRoot 'installer\yLauncher.ico'
$manifest = Join-Path $sourceRoot 'installer\yLauncher.manifest'
$emptyLibrary = Join-Path $sourceRoot 'installer\installed_games.json'

Invoke-Compiler @(
    '/nologo', '/target:winexe', '/platform:x86', '/optimize+', '/deterministic+', '/langversion:latest',
    "/win32icon:$(Join-Path $sourceRoot 'installer\app.ico')",
    "/out:$installer",
    '/reference:System.Windows.Forms.dll',
    '/reference:System.Drawing.dll',
    '/reference:Microsoft.CSharp.dll',
    "/resource:$launcher,yLauncher.exe",
    "/resource:$index,index.html",
    "/resource:$icon,yLauncher.ico",
    "/resource:$manifest,yLauncher.manifest",
    "/resource:$uninstaller,Desinstalar_yLauncher.exe",
    "/resource:$emptyLibrary,installed_games.json",
    (Join-Path $sourceRoot 'installer\InstallerProgram.cs'),
    (Join-Path $sourceRoot 'installer\InstallerForm.cs'),
    (Join-Path $sourceRoot 'installer\Properties\AssemblyInfo.cs')
)

$installerAssembly = [System.Reflection.Assembly]::ReflectionOnlyLoadFrom($installer)
$embeddedResources = @($installerAssembly.GetManifestResourceNames())
$requiredResources = @(
    'yLauncher.exe',
    'index.html',
    'yLauncher.ico',
    'yLauncher.manifest',
    'Desinstalar_yLauncher.exe',
    'installed_games.json'
)
foreach ($requiredResource in $requiredResources) {
    if ($embeddedResources -notcontains $requiredResource) {
        throw "Recurso ausente no instalador: $requiredResource"
    }
}

$libraryStream = $installerAssembly.GetManifestResourceStream('installed_games.json')
if (-not $libraryStream) {
    throw 'Não foi possível validar a biblioteca vazia incorporada.'
}
try {
    $libraryReader = New-Object System.IO.StreamReader($libraryStream, [System.Text.Encoding]::UTF8)
    $embeddedLibrary = $libraryReader.ReadToEnd()
    if ($embeddedLibrary -notmatch '"games"\s*:\s*\[\s*\]') {
        throw 'O instalador contém uma biblioteca de jogos que não está vazia.'
    }
}
finally {
    if ($libraryReader) { $libraryReader.Dispose() }
    else { $libraryStream.Dispose() }
}

$launcherVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($launcher).FileVersion
$uninstallerVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($uninstaller).FileVersion
$installerVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($installer).FileVersion
if (@($launcherVersion, $uninstallerVersion, $installerVersion) | Where-Object { $_ -ne '2.1.0.0' }) {
    throw 'Um ou mais executáveis foram gerados com versão incorreta.'
}

if (-not $SkipPortable) {
    New-Item -ItemType Directory -Path $portableRoot -Force | Out-Null
    Copy-Item -LiteralPath $launcher -Destination $portableRoot
    Copy-Item -LiteralPath $uninstaller -Destination $portableRoot
    Copy-Item -LiteralPath $index -Destination $portableRoot
    Copy-Item -LiteralPath $icon -Destination $portableRoot
    Copy-Item -LiteralPath $manifest -Destination $portableRoot
    Copy-Item -LiteralPath $emptyLibrary -Destination $portableRoot

    $portableZip = Join-Path $distRoot 'yLauncher_v2.1_Portable.zip'
    for ($attempt = 1; $attempt -le 4; $attempt++) {
        try {
            New-DeterministicZip $portableRoot $portableZip
            break
        }
        catch {
            if (Test-Path -LiteralPath $portableZip) {
                Remove-Item -LiteralPath $portableZip -Force
            }
            if ($attempt -eq 4) { throw }
            Start-Sleep -Milliseconds 750
        }
    }
}

$artifacts = Get-ChildItem -LiteralPath $distRoot -File
$checksumPath = Join-Path $distRoot 'checksums.sha256'
$checksumLines = foreach ($artifact in $artifacts) {
    $hash = Get-FileHash -LiteralPath $artifact.FullName -Algorithm SHA256
    '{0}  {1}' -f $hash.Hash.ToLowerInvariant(), $artifact.Name
}
[System.IO.File]::WriteAllLines($checksumPath, $checksumLines, (New-Object System.Text.UTF8Encoding($false)))

Write-Host ''
Write-Host 'Build concluído:' -ForegroundColor Green
Get-ChildItem -LiteralPath $distRoot | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
