# yLauncher

<div align="center">

**Uma biblioteca de jogos em tela cheia para Windows, feita para mouse, teclado e controle.**

[![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?style=flat-square&logo=windows11&logoColor=white)](https://www.microsoft.com/windows)
[![Versão](https://img.shields.io/badge/versão-2.1-6C5CE7?style=flat-square)](CHANGELOG.md)
[![Controle](https://img.shields.io/badge/controle-XInput-107C10?style=flat-square&logo=xbox&logoColor=white)](#controles)
[![Status](https://img.shields.io/badge/status-beta-F59E0B?style=flat-square)](#status-do-projeto)

[**Baixar instalador 2.1**](Instalador_yLauncher_v2.1.exe) · [**Baixar versão portátil**](yLauncher_v2.1_Portable.zip) · [Ver mudanças](CHANGELOG.md)

![Demonstração do yLauncher](https://raw.githubusercontent.com/kromossomom-debug/yLauncher/16d24e753c9e5d3f5aff624c07ec26fcc47eeb93/ylauncher_preview.gif)

</div>

## O que há de novo na versão 2.1

- Ponte local protegida por token temporário; páginas externas não conseguem mais acionar executáveis pelo launcher.
- Protocolo `ylauncher://` limitado a ações seguras e sem execução arbitrária de caminhos.
- Proxy do SteamGridDB restrito ao domínio oficial e sem chave de API incorporada.
- Pacote publicado com biblioteca vazia, sem jogos ou dados pertencentes à máquina de desenvolvimento.
- Conteúdo importado e textos personalizados são normalizados antes de aparecer na interface.
- Inicialização mais rápida: a varredura de jogos acontece depois que a interface abre.
- Instalador renovado, atualização que preserva a biblioteca e desinstalador que mantém dados pessoais por padrão.
- Build reproduzível do launcher, instalador, desinstalador e pacote portátil.

## Principais recursos

- Navegação por controle XInput, teclado ou mouse.
- Detecção de bibliotecas Steam, Epic Games, Battle.net e EA App.
- Inclusão manual de jogos, aplicativos e emuladores.
- Busca, filtros, favoritos, avaliações e tempo de jogo.
- Capas e banners personalizados, com integração opcional ao SteamGridDB.
- Interface adaptável para desktops e portáteis como Lenovo Legion Go, ASUS ROG Ally, AYANEO, GPD e OneXPlayer.

## Instalação

1. Baixe o [instalador 2.1](Instalador_yLauncher_v2.1.exe).
2. Compare o SHA-256 com o valor da seção [Integridade](#integridade).
3. Abra o instalador, escolha a pasta e as integrações desejadas.
4. Clique em **Instalar**.

Também é possível extrair a [versão portátil](yLauncher_v2.1_Portable.zip) e abrir `yLauncher.exe` diretamente.

> Os executáveis ainda não possuem assinatura digital. O Windows pode mostrar um aviso do SmartScreen; confirme a origem e o hash antes de executá-los.

### Requisitos

- Windows 10 versão 1809 ou mais recente, ou Windows 11.
- Microsoft Edge instalado, usado para exibir a interface em modo aplicativo.
- Controle XInput opcional.
- Internet apenas para capas e outros recursos online.

## Primeiros passos

Ao abrir o yLauncher, use **Verificar jogos instalados** para localizar bibliotecas no computador. Jogos que não forem detectados podem ser incluídos em **Adicionar jogo**.

Para usar artes da comunidade, crie sua própria chave no SteamGridDB e salve-a nas configurações do launcher. Nenhuma chave é distribuída dentro do aplicativo.

Ao desinstalar, capas, preferências e cache são preservados por padrão. Marque a opção de exclusão de dados somente se quiser uma limpeza completa.

## Controles

| Comando | Ação |
| --- | --- |
| D-pad ou analógico esquerdo | Navegar pela biblioteca |
| **A** ou **Enter** | Abrir o jogo selecionado |
| **B** ou **Esc** | Voltar |
| **Y** | Alternar favorito |
| **LB / RB** ou **Q / E** | Alternar categorias |
| **Start / X** | Personalizar a arte |
| **View / Select** | Abrir configurações |

## Integridade

Os hashes completos estão em [checksums.sha256](checksums.sha256).

| Arquivo | SHA-256 |
| --- | --- |
| `Instalador_yLauncher_v2.1.exe` | `A27C8DB909D3C2D16C97F1CE17BC5E1538D07F7C68E936013BCC9F704B128806` |
| `yLauncher_v2.1_Portable.zip` | `9ED9BB1350D12A74F17438E3469C112A55A6D5E4570C7686D0C92DB95E331F63` |

Verificação no PowerShell:

```powershell
Get-FileHash .\Instalador_yLauncher_v2.1.exe -Algorithm SHA256
```

## Compilar a partir do código-fonte

O código recuperado e organizado está em `src/`. Com o Visual Studio 2022 Build Tools e o compilador C# instalados, execute:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

O script recompila os três executáveis, confere versões e recursos incorporados e grava a distribuição em `dist/`.

```text
src/
├── runtime/       # launcher e ponte nativa local
├── installer/     # interface HTML e instalador
└── uninstaller/   # remoção segura do aplicativo
```

## Versões anteriores

- [yLauncher 2.0](https://github.com/kromossomom-debug/yLauncher/raw/16d24e753c9e5d3f5aff624c07ec26fcc47eeb93/Instalador_yLauncher_v2.exe)
- [yLauncher 0.1 clássica](https://github.com/kromossomom-debug/yLauncher/raw/16d24e753c9e5d3f5aff624c07ec26fcc47eeb93/Instalador_yLauncher.exe)

## Status do projeto

O yLauncher está em **beta**. Encontrou um problema? [Abra uma issue](https://github.com/kromossomom-debug/yLauncher/issues) com a versão do Windows e os passos para reproduzir.

---

<div align="center"><sub>Feito para jogar mais e configurar menos.</sub></div>
