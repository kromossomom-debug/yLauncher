# yLauncher

<div align="center">

**Uma biblioteca de jogos em tela cheia para Windows, feita para mouse, teclado e controle.**

[![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?style=flat-square&logo=windows11&logoColor=white)](https://www.microsoft.com/windows)
[![Vers├úo](https://img.shields.io/badge/vers├úo-2.1-6C5CE7?style=flat-square)](CHANGELOG.md)
[![Controle](https://img.shields.io/badge/controle-XInput-107C10?style=flat-square&logo=xbox&logoColor=white)](#controles)
[![Status](https://img.shields.io/badge/status-beta-F59E0B?style=flat-square)](#status-do-projeto)

[**Baixar instalador 2.1**](Instalador_yLauncher_v2.1.exe) ┬À [**Baixar vers├úo port├ítil**](yLauncher_v2.1_Portable.zip) ┬À [Ver mudan├ºas](CHANGELOG.md)

![Demonstra├º├úo do yLauncher](https://raw.githubusercontent.com/kromossomom-debug/yLauncher/16d24e753c9e5d3f5aff624c07ec26fcc47eeb93/ylauncher_preview.gif)

</div>

## O que h├í de novo na vers├úo 2.1

- Ponte local protegida por token tempor├írio; p├íginas externas n├úo conseguem mais acionar execut├íveis pelo launcher.
- Protocolo `ylauncher://` limitado a a├º├Áes seguras e sem execu├º├úo arbitr├íria de caminhos.
- Proxy do SteamGridDB restrito ao dom├¡nio oficial e sem chave de API incorporada.
- Pacote publicado com biblioteca vazia, sem jogos ou dados pertencentes ├á m├íquina de desenvolvimento.
- Conte├║do importado e textos personalizados s├úo normalizados antes de aparecer na interface.
- Inicializa├º├úo mais r├ípida: a varredura de jogos acontece depois que a interface abre.
- Instalador renovado, atualiza├º├úo que preserva a biblioteca e desinstalador que mant├®m dados pessoais por padr├úo.
- Build reproduz├¡vel do launcher, instalador, desinstalador e pacote port├ítil.

## Principais recursos

- Navega├º├úo por controle XInput, teclado ou mouse.
- Detec├º├úo de bibliotecas Steam, Epic Games, Battle.net e EA App.
- Inclus├úo manual de jogos, aplicativos e emuladores.
- Busca, filtros, favoritos, avalia├º├Áes e tempo de jogo.
- Capas e banners personalizados, com integra├º├úo opcional ao SteamGridDB.
- Interface adapt├ível para desktops e port├íteis como Lenovo Legion Go, ASUS ROG Ally, AYANEO, GPD e OneXPlayer.

## Instala├º├úo

1. Baixe o [instalador 2.1](Instalador_yLauncher_v2.1.exe).
2. Compare o SHA-256 com o valor da se├º├úo [Integridade](#integridade).
3. Abra o instalador, escolha a pasta e as integra├º├Áes desejadas.
4. Clique em **Instalar**.

Tamb├®m ├® poss├¡vel extrair a [vers├úo port├ítil](yLauncher_v2.1_Portable.zip) e abrir `yLauncher.exe` diretamente.

> Os execut├íveis ainda n├úo possuem assinatura digital. O Windows pode mostrar um aviso do SmartScreen; confirme a origem e o hash antes de execut├í-los.

### Requisitos

- Windows 10 vers├úo 1809 ou mais recente, ou Windows 11.
- Microsoft Edge instalado, usado para exibir a interface em modo aplicativo.
- Controle XInput opcional.
- Internet apenas para capas e outros recursos online.

## Primeiros passos

Ao abrir o yLauncher, use **Verificar jogos instalados** para localizar bibliotecas no computador. Jogos que n├úo forem detectados podem ser inclu├¡dos em **Adicionar jogo**.

Para usar artes da comunidade, crie sua pr├│pria chave no SteamGridDB e salve-a nas configura├º├Áes do launcher. Nenhuma chave ├® distribu├¡da dentro do aplicativo.

Ao desinstalar, capas, prefer├¬ncias e cache s├úo preservados por padr├úo. Marque a op├º├úo de exclus├úo de dados somente se quiser uma limpeza completa.

## Controles

| Comando | A├º├úo |
| --- | --- |
| D-pad ou anal├│gico esquerdo | Navegar pela biblioteca |
| **A** ou **Enter** | Abrir o jogo selecionado |
| **B** ou **Esc** | Voltar |
| **Y** | Alternar favorito |
| **LB / RB** ou **Q / E** | Alternar categorias |
| **Start / X** | Personalizar a arte |
| **View / Select** | Abrir configura├º├Áes |

## Integridade

Os hashes completos est├úo em [checksums.sha256](checksums.sha256).

| Arquivo | SHA-256 |
| --- | --- |
| `Instalador_yLauncher_v2.1.exe` | `5B4EB55B691586E386A470F4F12BDFC9FEAD3525F18263706F3CD8730952F97C` |
| `yLauncher_v2.1_Portable.zip` | `36FD44B956B956F6CC63B5829041CE384C86CB7A7EAEE321DE38B919364A0092` |

Verifica├º├úo no PowerShell:

```powershell
Get-FileHash .\Instalador_yLauncher_v2.1.exe -Algorithm SHA256
```

## Compilar a partir do c├│digo-fonte

O c├│digo recuperado e organizado est├í em `src/`. Com o Visual Studio 2022 Build Tools e o compilador C# instalados, execute:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

O script recompila os tr├¬s execut├íveis, confere vers├Áes e recursos incorporados e grava a distribui├º├úo em `dist/`.

```text
src/
Ôö£ÔöÇÔöÇ runtime/       # launcher e ponte nativa local
Ôö£ÔöÇÔöÇ installer/     # interface HTML e instalador
ÔööÔöÇÔöÇ uninstaller/   # remo├º├úo segura do aplicativo
```

## Vers├Áes anteriores

- [yLauncher 2.0](https://github.com/kromossomom-debug/yLauncher/raw/16d24e753c9e5d3f5aff624c07ec26fcc47eeb93/Instalador_yLauncher_v2.exe)
- [yLauncher 0.1 cl├íssica](https://github.com/kromossomom-debug/yLauncher/raw/16d24e753c9e5d3f5aff624c07ec26fcc47eeb93/Instalador_yLauncher.exe)

## Status do projeto

O yLauncher est├í em **beta**. Encontrou um problema? [Abra uma issue](https://github.com/kromossomom-debug/yLauncher/issues) com a vers├úo do Windows e os passos para reproduzir.

---

<div align="center"><sub>Feito para jogar mais e configurar menos.</sub></div>
