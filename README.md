# Doorstep

An unofficial Windows window for the [Ring dashboard](https://account.ring.com/account/dashboard). Ring discontinued its desktop app and left the account site as the replacement. Doorstep hosts that site in its own window so the browser can keep system notifications off.

Doorstep is not affiliated with Ring or Amazon. Sign-in happens on Ring’s website. Doorstep does not store the password, and it does not talk to a separate Ring API.

Alerts can be missed. Doorstep shows a Windows notification only when the Ring site itself raises one, and that can fail to happen inside WebView2. Doorstep is not a security or monitoring service.

Doorstep does not change the Ring page's scripts, requests, or controls. Light appearance leaves the page as Ring rendered it. Dark appearance color-inverts the light page. Large photos, video, and snapshot backgrounds are restored to their real colors. Small icons are left inverted so dark artwork stays visible.

## Download

The Windows installer is published on the [Releases](https://github.com/SequaciousGAMES/Doorstep/releases/latest) page. Download `DoorstepSetup.exe` and run it. Setup downloads `Doorstep-win-x64.zip` from the latest release and installs that build. The setup program does not contain a copy of Doorstep, so publishing a new build does not require a new installer. The .NET SDK is not required. Setup needs a network connection to reach GitHub. Windows SmartScreen will warn because the installer is not signed.

The tray menu can check that page for a newer version. The check runs at startup until turned off, and Check for updates runs it immediately. It asks GitHub for the latest release and sends no account information. When a newer version exists, the notification offers Install update, and the tray menu has the same command. Doorstep downloads `DoorstepSetup.exe` and runs it over the current copy. That setup program downloads the latest release and installs it, then Doorstep opens again. The saved sign-in stays in place. A Program Files install asks for administrator approval. Windows SmartScreen may warn again because the installer is not signed.

Setup installs for all users by default, into `C:\Program Files\Doorstep`, and asks for administrator rights. You can choose to install for your account only, which uses `%LocalAppData%\Programs\Doorstep` and does not require administrator rights. Setup installs the Microsoft Edge WebView2 Runtime when that runtime is missing. Uninstall removes Doorstep’s program files, its private data under `%LocalAppData%\Doorstep`, shortcuts that point at that install, the startup entry, and its notification registration. The WebView2 Runtime stays installed because other apps share it.

## What it does

- Opens the Ring dashboard in a private WebView2 window.
- Stays on `ring.com`. Other links open in the default browser.
- Closes to the notification area and keeps running. Quit from the tray icon to stop it.
- Shows a Windows notification labeled Doorstep when the Ring site raises one.
- Offers light, dark, and follow-the-system appearance from the tray menu.
- Can start with Windows, also from the tray menu.
- Can check for a newer release at startup, or on request, from the tray menu, and can install that release over the current copy.

The first launch asks you to acknowledge that Doorstep is unofficial. The session is kept in `%LocalAppData%\Doorstep\WebView2`.

## Requirements

Windows 10 version 1809 or later, 64-bit. The installer provides the [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) when it is not already present.

Building from source also needs the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). Building the installer also needs [Inno Setup 6.5.2](https://jrsoftware.org/isinfo.php) or later.

## Run from source

```powershell
dotnet run --project src\Doorstep\Doorstep.csproj
```

## Build the installer

`installer\Build.ps1` publishes a self-contained 64-bit build, packs it as `artifacts\Doorstep-win-x64.zip`, downloads the WebView2 bootstrapper if it is not already in `installer\redist`, and compiles the setup program. The setup program does not contain that build. When it runs, it downloads the zip from the latest GitHub release.

```powershell
powershell -ExecutionPolicy Bypass -File installer\Build.ps1
```

The setup program is written to `artifacts\DoorstepSetup.exe`.

Pushing a version tag such as `v1.0.1` runs the release workflow, which publishes the zip and the setup program. The version in the project should match the tag. The workflow does not sign the installer. A new app build is published by replacing `Doorstep-win-x64.zip`; the setup program only needs to be replaced when the setup program itself changes.

## License

Doorstep is free software under the [GNU General Public License v3.0](LICENSE). Each GitHub release is built from the source at the matching version tag.

The Microsoft .NET runtime, the Microsoft Edge WebView2 SDK and Runtime, and the MIT-licensed libraries named in [NOTICE](NOTICE) remain under their own licenses. Doorstep includes an additional permission to combine with those components.
