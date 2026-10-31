# Development

USBWatcher requests administrator privileges during a normal application launch
because renaming a serial device writes protected Windows registry values.

### VS Code tasks

Open the repository root in VS Code and use **Terminal > Run Task**.
The tasks require Windows, the .NET 8 SDK, the Windows SDK used by the WinUI
project, and PowerShell 7 (`pwsh`) on PATH.

- **Restore** restores dependencies for the solution.
- **Build: All (Debug)** builds both editions and is the default task for **Ctrl+Shift+B**.
- **Build: All (Release)** builds both editions in Release mode.
- **Build: WinUI (Debug)** and **Build: WinForms (Debug)** build an individual edition.
- **Run: WinUI (Administrator)** and **Run: WinForms (Administrator)** build the selected edition, then launch it through UAC. The task finishes after launch; the app remains running.
- **Clean** prompts for Debug or Release and cleans that configuration's build output.
- **Package: Local release** prompts for a `vX.Y.Z.A` version and generates both Setup installers and portable ZIPs. It recreates `artifacts/local-release` without creating a Git tag or publishing a release.

### WinUI 3 frontend

`USBWatcher.WinUI` is the modern Windows 11 frontend. It uses the same
`USBWatcher.Core` device watcher as the WinForms application and targets the
stable Windows App SDK 2.5.1 release.

```powershell
dotnet build src\USBWatcher.WinUI\USBWatcher.WinUI.csproj -c Release
```

The executable is produced under
`src\USBWatcher.WinUI\bin\x64\Release\net8.0-windows10.0.19041.0\win-x64` when
the project is built directly.

### Test release packages locally

Build the same portable ZIPs and Velopack Setup executables used by the release
workflow without creating a tag or GitHub release:

```powershell
pwsh -NoProfile -File .\scripts\build-local-release.ps1 -ReleaseTag v2.1.1.8
```

The files are written to `artifacts\local-release\release`. Run either Setup
normally, not as administrator. Local packages use a prerelease version suffix
so a later stable build can replace them; uninstall them before testing the
final release.
