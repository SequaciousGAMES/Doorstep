# Doorstep

An unofficial Windows window for the [Ring dashboard](https://account.ring.com/account/dashboard). Ring discontinued its desktop app and left the account site as the replacement. Doorstep hosts that site in its own window so the browser can keep system notifications off.

Doorstep is not affiliated with Ring or Amazon. Sign-in happens on Ring’s website. Doorstep does not store the password, and it does not talk to a separate Ring API.

Alerts can be missed. Doorstep shows a Windows notification only when the Ring site itself raises one, and that can fail to happen. Doorstep is not a security or monitoring service.

Doorstep does not change the Ring page's scripts, requests, or controls. Light appearance leaves the page as Ring rendered it. Dark appearance is available from the tray menu.

## Download

The Windows installer is published on the [Releases](https://github.com/SequaciousGAMES/Doorstep/releases/latest) page. Download `DoorstepSetup.exe` and run it. Setup downloads the latest Doorstep from GitHub, so it needs a network connection. Windows SmartScreen will warn because the installer is not signed.

The tray menu can check for a newer version. The check runs at startup until turned off, and Check for updates runs it immediately. It asks GitHub for the latest release and sends no account information. When a newer version exists, the notification offers Install update, and the tray menu has the same command. Doorstep installs that release over the current copy, then opens again. The saved sign-in stays in place. A Program Files install asks for administrator approval. Windows SmartScreen may warn again because the installer is not signed.

Setup installs for all users by default, into `C:\Program Files\Doorstep`, and asks for administrator rights. You can choose to install for your account only, which uses `%LocalAppData%\Programs\Doorstep` and does not require administrator rights. Setup installs the Microsoft Edge WebView2 Runtime when that runtime is missing. Uninstall removes Doorstep’s program files, its private data under `%LocalAppData%\Doorstep`, shortcuts that point at that install, the startup entry, and its notification registration. The WebView2 Runtime stays installed because other apps share it.

## What it does

- Opens the Ring dashboard in its own window.
- Stays on `ring.com`. Other links open in the default browser.
- Closes to the notification area and keeps running. Quit from the tray icon to stop it.
- Shows a Windows notification labeled Doorstep when the Ring site raises one.
- Offers light, dark, and follow-the-system appearance from the tray menu.
- Can start with Windows, also from the tray menu.
- Can check for a newer release at startup, or on request, from the tray menu, and can install that release over the current copy.

The first launch asks you to acknowledge that Doorstep is unofficial. The session is kept in `%LocalAppData%\Doorstep\WebView2`.

## Requirements

Windows 10 version 1809 or later, 64-bit. The installer provides the [WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) when it is not already present.

## License

Doorstep is free software under the [GNU General Public License v3.0](LICENSE).

Disclaimer: Doorstep was implemented with agentic coding tools.
The creative decisions were made by people, not by AI.
