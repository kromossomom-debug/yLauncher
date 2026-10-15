using System.Reflection;

// Fonte única da versão do yLauncher.
// O build.ps1 e o workflow de release leem a constante Display deste arquivo.
[assembly: AssemblyVersion(AppVersion.Assembly)]
[assembly: AssemblyFileVersion(AppVersion.Assembly)]
[assembly: AssemblyInformationalVersion(AppVersion.Display)]
[assembly: AssemblyCompany("yLauncher")]
[assembly: AssemblyCopyright("yLauncher contributors")]

internal static class AppVersion
{
	public const string Display = "2.4.0";
	public const string Assembly = "2.4.0.0";

	// Repositório monitorado pelo atualizador automático (GitHub Releases).
	public const string GitHubRepo = "kromossomom-debug/yLauncher";
}
