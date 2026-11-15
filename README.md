<p align="center">
  <img src="src/WinDownloader/Assets/logo.png" width="96" alt="WinDownloader">
</p>

<h1 align="center">WinDownloader</h1>

<p align="center">
  One dashboard for <b>Windows updates</b>, <b>driver updates</b>, <b>USB &amp; device drivers</b> and <b>app updates</b> —
  with a single score that tells you how up to date your PC is.<br>
  <sub>by DarkerDawnFR</sub>
</p>

<p align="center">
  <a href="../../releases/latest"><b>⬇ Download WinSetup.exe (latest release)</b></a>
</p>

---

## What it does

| Area | How | Where it comes from |
|---|---|---|
| **Windows updates** | Finds, downloads and installs security, quality and feature updates | Windows Update Agent (the same engine as *Settings → Windows Update*), optionally through Microsoft Update |
| **Driver updates** | Graphics, chipset, network, audio, USB and other drivers published by hardware makers | Microsoft Update driver channel |
| **Devices & USB** | Lists every device with its driver version/date, flags problem devices and old drivers, pairs them with available updates | WMI (`Win32_PnPSignedDriver`, `Win32_PnPEntity`) + `pnputil` |
| **Apps** | Upgrades installed programs that have a newer version | `winget` (Windows Package Manager) |
| **History** | What has been installed on this PC | Windows Update history |

### The Update Score

Instead of a “protection” score, WinDownloader shows an **update score** out of 100 — the dial on the dashboard.
100 means everything it can see is installed. Points are deducted per missing item:

| Missing item | Points |
|---|---|
| Critical / security Windows update | −15 |
| Important Windows update | −8 |
| Optional Windows update | −3 |
| Driver update | −6 |
| App update | −2 |
| Restart pending | −5 |
| Device with a problem code | −4 each (max −12) |

## Install

1. Grab **`WinSetup.exe`** from the [latest release](../../releases/latest).
2. Run it. **Win Setup** opens: it detects your Windows version (10 or 11), lets you confirm or change it, choose the install folder and shortcuts, and installs the app.
3. WinDownloader starts and scans. You can change the Windows version later in **Settings**.

Both `WinSetup.exe` and the app are self-contained .NET 8 builds — there is nothing else to install.
They need administrator rights (installing updates requires it), so you will see a UAC prompt.
Because the build is not code-signed, SmartScreen may show a warning the first time: *More info → Run anyway*.

Uninstall from **Settings → Apps → Installed apps → WinDownloader**, or run `WinSetup.exe --uninstall` from the install folder.

## Build it yourself

Requirements: Windows 10/11 and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
git clone https://github.com/luaslucas422-lab/Windownloader.git
cd Windownloader

# 1) the app
dotnet publish src/WinDownloader/WinDownloader.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o out/app

# 2) zip it into the installer payload
Compress-Archive -Path out/app/* -DestinationPath src/WinSetup/Payload/app.zip -Force

# 3) the installer
dotnet publish src/WinSetup/WinSetup.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o out/setup
```

`out/setup/WinSetup.exe` is the file you give to people. `out/app/WinDownloader.exe` runs on its own.

Every push to `main` runs the same steps on GitHub Actions and publishes a new release automatically.

## Project layout

```
src/WinDownloader/          the app (WPF, .NET 8)
  Services/                 WindowsUpdateService (WUA COM), DeviceService (WMI), WingetService, SystemInfoService
  ViewModels/               MainViewModel — scanning, installing, score
  Views/                    Dashboard, update lists, Devices, History, Settings
  Controls/ScoreGauge.cs    the dial
src/WinSetup/               Win Setup — the installer wizard (WPF, .NET 8)
.github/workflows/build.yml builds both and publishes a release
```

## Notes

* Windows only offers drivers that manufacturers have submitted to Microsoft Update. For the absolute newest GPU driver, the maker's own installer may still be ahead.
* Settings live in `C:\ProgramData\WinDownloader\settings.json`.
* Windows 10 and Windows 11 are both supported; the version you pick in Win Setup is stored and shown on the dashboard.
