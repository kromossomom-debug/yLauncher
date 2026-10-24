# yLauncher

<div align="center">

**A modern, lightweight, console-style game launcher for Windows — built for mouse, keyboard, and gamepad.**

[![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?style=flat-square&logo=windows11&logoColor=white)](https://www.microsoft.com/windows)
[![Version](https://img.shields.io/badge/version-2.4.0-3B82F6?style=flat-square)](CHANGELOG.md)
[![Controller](https://img.shields.io/badge/controller-XInput-107C10?style=flat-square&logo=xbox&logoColor=white)](#controller-navigation)
[![Status](https://img.shields.io/badge/status-beta-F59E0B?style=flat-square)](#project-status)

[**Download latest release**](https://github.com/kromossomom-debug/yLauncher/releases/latest) &middot; [View Changelog](CHANGELOG.md)

<br/>

![yLauncher Interface](screenshot_ui.jpg)

</div>

---

## Overview

**yLauncher** is a lean, fast, full-screen gaming frontend designed to turn your Windows PC, TV setup, or gaming handheld into a dedicated gaming console. It organizes all your games from multiple stores and platforms into a single, cohesive, distraction-free interface.

Unlike bulky desktop clients, yLauncher launches in milliseconds, consumes minimal background resources, requires no mandatory accounts or cloud login, and offers native, console-quality gamepad navigation out of the box.

Optimized for desktop displays and handheld devices including **Lenovo Legion Go**, **ASUS ROG Ally**, **Steam Deck (Windows)**, **AYANEO**, **GPD**, and **OneXPlayer**.

---

## What's New in v2.4.0

- **7 library views**: Row, Grid, Compact, Tiles, List (sortable table), Side by side (list + details panel) and 3D Carousel.
- **Display options**: cover size, titles on covers, badges on/off and grouping by store, progress, install state or first letter.
- Controller: **L3** cycles the view, **R3** cycles cover size (keyboard **G** / **Z**).

## What's New in v2.3.0

- **Home screen** with console-style shelves (Continue playing, Favorites, Recently added, Installed, one shelf per store).
- **Real playtime tracking** for every store, plus Steam's own playtime history. "Now playing" indicator; the launcher hides while you play and comes back when the game closes.
- **Game page** with description, genres, developer, release date, Metacritic score and screenshots from the Steam store. Games from other stores get official Steam art automatically.
- **Quick menu** (Xbox/Guide button or F1): volume, brightness, battery, close running game, sleep/restart/shutdown.
- **8 themes, 17 accent colors + custom color, background art modes, UI scale (90–140%) and 6 navigation sound packs.**
- **Welcome assistant** on first run.

## What's New in v2.2.0

- **Automatic updates from GitHub Releases**: yLauncher checks for a new release at startup and every 6 hours, downloads the installer in the background, verifies its SHA-256 and installs it silently when you close the launcher (or right away with *Restart now*). Your library, covers and settings are preserved. Can be turned off in *Settings → Updates*.
- **More stores**: GOG Galaxy, Battle.net, EA app and Ubisoft Connect are now detected alongside Steam and Epic. Epic games launch through the official launcher.
- **Redesigned interface**: fluid 10-foot typography, vector icons, store filter chips, clearer controller focus, two-column settings, redesigned modals and locally generated covers for games without art.
- **Fixes**: demo library removed, manual games show up as installed, platinum status saved, battery/CPU telemetry, accent/radius selection, language switch error.

See the full list in the [CHANGELOG](CHANGELOG.md).

---

## Key Features

- **Console-Grade Fullscreen Experience**: Fluid horizontal game carousel with smooth scaling, dynamic high-res backdrop art, dark mode aesthetics, and digital clock telemetry.
- **Native Controller Support**: Full XInput and DirectInput support with deadzone hysteresis, eliminating stick drift and accidental multi-jumps.
- **SteamGridDB Integration**: Automatic cover and hero art suggestions with intelligent title matching and franchise disambiguation chips.
- **Multi-Store Library Detection**: Auto-detects installed games across **Steam**, **Epic Games**, **GOG Galaxy**, **Battle.net**, **EA app** and **Ubisoft Connect** without manual path configuration.
- **Automatic Updates**: New GitHub releases are downloaded, verified (SHA-256) and installed in the background.
- **Custom & Local Games**: Easily add standalone `.exe`, `.bat`, `.lnk` files, emulated games, or ROMs with integrated file browser and launch testing.
- **Gamer Stats & Telemetry**: Track hours played, session counts, personal 1-to-5 star ratings, backlog status (*Backlog*, *Playing*, *Completed*, *100% Platinado*), and notes.
- **Gamer Roulette ("What to play today?")**: Built-in wheel spinner to pick a random game from your library when you can't decide.
- **Privacy & Security**: Zero telemetry tracking, no mandatory cloud accounts, and a secured local native bridge.

---

## Controller Navigation

### In the Main Library:

| Button | Action |
| :--- | :--- |
| **D-Pad / Left Stick** | Navigate game carousel / grid |
| **(A) / Enter** | Launch selected game |
| **(B) / Esc** | Back / Clear search / View all |
| **(X) / START** | Customize game artwork (Cover / Background) |
| **(Y)** | Toggle Favorite |
| **(LB) / (RB)** | Switch category tabs (Installed, All, Recent, Favorites, Custom) |
| **(View / Select)** | Open Settings Hub & Customization |
| **(L3)** | Toggle quick sidebar |

### In the Artwork Studio:

| Button | Action |
| :--- | :--- |
| **D-Pad / Left Stick** | Navigate artwork cards (2 columns for Heroes, 4 columns for Covers) |
| **(A)** | Select & Preview; press again to **Apply** and close |
| **(X) / START** | **Apply** the focused artwork immediately |
| **(Y)** | Open fullscreen HD Lightbox Zoom |
| **(LT) / (RT)** | **Toggle between Cover (600x900) and Background (Widescreen)** |
| **(LB) / (RB)** | Previous / Next page of artworks |
| **(B)** | Close Studio |

---

## Installation

### Option 1: Installer (Recommended)
1. Download `Instalador_yLauncher_vX.Y.Z.exe` from the [latest release](https://github.com/kromossomom-debug/yLauncher/releases/latest).
2. *(Optional)* Verify the SHA-256 hash against `checksums.sha256` in the same release.
3. Run the installer and choose your installation path and shortcuts.
4. Click **Install**. From now on yLauncher updates itself automatically.

### Option 2: Portable Edition
1. Download `yLauncher_vX.Y.Z_Portable.zip` from the [latest release](https://github.com/kromossomom-debug/yLauncher/releases/latest).
2. Extract the archive into any folder of your choice.
3. Run `yLauncher.exe` directly. Portable copies notify you about new versions but do not replace themselves.

> **Note**: Because yLauncher is an independent open-source release without an expensive EV code signing certificate, Windows Defender SmartScreen may display an alert on first run. Click *"More info"* and *"Run anyway"*. You can always verify the binary checksums below.

---

## System Requirements

- **Operating System**: Windows 10 (version 1809 or later) or Windows 11 (64-bit / 32-bit).
- **Web Engine**: Microsoft Edge (preinstalled on modern Windows) for runtime web rendering.
- **Controller**: Any Xbox / XInput compatible controller, PlayStation controller via standard driver, or handheld gamepad. Mouse and keyboard are fully supported.
- **Network**: Internet connection required only for downloading SteamGridDB covers and banners.

---

## Integrity & Checksums

Every release publishes a `checksums.sha256` file. The built-in updater refuses to install anything whose SHA-256 does not match. To verify manually on PowerShell:

```powershell
Get-FileHash .\Instalador_yLauncher_v2.2.0.exe -Algorithm SHA256
```

---

## Building from Source

The complete source code is included in `src/`. With the Visual Studio 2022 Build Tools (or Visual Studio Community) and C# compiler installed, run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

The script compiles the runtime launcher, uninstaller, and self-extracting installer, verifies embedded manifest resources and versions, and outputs release packages to `dist/`.

```text
src/
├── shared/        # AppVersion.cs — single source of the version number and GitHub repo
├── runtime/       # yLauncher.exe: local HTTP bridge, store scanners (StoreScanners.cs), auto-updater (Updater.cs)
├── installer/     # index.html (UI) + self-extracting installer (supports /silent /update /relaunch)
└── uninstaller/   # Safe uninstaller with /purge support
```

### Publishing a release (auto-update)

1. Bump `Display` and `Assembly` in `src/shared/AppVersion.cs` (e.g. `2.2.1` / `2.2.1.0`).
2. Add the entry to `CHANGELOG.md`.
3. Commit, then tag and push: `git tag v2.2.1 && git push origin main --tags`.
4. The **Release** workflow (`.github/workflows/release.yml`) builds on Windows and publishes the installer, the portable ZIP and `checksums.sha256`. Installed copies pick it up within 6 hours (or on next launch).

---

## Project Status

yLauncher is in active **Beta** development. If you encounter an issue or have a feature suggestion, please [open an issue](https://github.com/kromossomom-debug/yLauncher/issues) with your Windows build and steps to reproduce.

---

<div align="center">
<sub>Play more, configure less. Crafted for the PC & Handheld gaming community.</sub>
</div>

## ☕ Support the project

Enjoying the project? Buy the author a coffee and help keep it updated.

<a href="https://buymeacoffee.com/kromossomoc"><img src="https://img.shields.io/badge/Buy%20Me%20a%20Coffee-kromossomoc-FFDD00?style=for-the-badge&logo=buymeacoffee&logoColor=black" alt="Buy Me a Coffee: kromossomoc" height="36"></a>
