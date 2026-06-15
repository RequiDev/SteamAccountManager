# Steam Account Manager

A lightweight Windows tray app for switching between **unlimited** Steam accounts with a single click — silent auto-login, no re-typing passwords, and no bumping into Steam's "remembered accounts" limit.

> ### 🤖 Authorship
> **This project — its design, code, tests, CI, and this README — was written end-to-end by [Claude](https://www.anthropic.com/claude) (Anthropic's *Claude Code*, model *Claude Opus*), not by the repository owner ([@RequiDev](https://github.com/RequiDev)).**
> It was produced through an AI-driven development session, including the live reverse-engineering of Steam's login behaviour that the one-click switching relies on. The repository owner directed the work and tested it; the implementation is Claude's. Use at your own risk.

---

## Features

- **One-click account switching** from a system-tray menu — the chosen account is *silently* signed in using Steam's own cached login token. No password, no Steam Guard prompt (for accounts you've logged into at least once with "Remember me").
- **Unlimited accounts** — preserves every account's cached token across switches, so Steam never prunes you down to its usual handful of "remembered" accounts.
- **Groups / categories** (many-to-many) with per-group submenus in the tray.
- **Tray-first UX** — left-click to open, right-click to switch; close-to-tray, start-minimized, start-with-Windows, single-instance.
- **Built-in auto-update** — checks GitHub Releases on launch and offers to download and restart into the new version.

## How the silent switching works

Modern Steam keeps a per-account, DPAPI-encrypted **JWT refresh token** in `%LocalAppData%\Steam\local.vdf` (its `ConnectCache`). On launch it auto-signs-in the account that is marked as the **most recent** in `config\loginusers.vdf` (by timestamp) and pointed at by the `AutoLoginUser` registry value — *provided a valid cached token exists*.

When you pick an account, this app:

1. preserves **every** account's cached token (so none get pruned),
2. sets the registry + `loginusers.vdf` selectors and makes the target the newest entry, and
3. relaunches Steam, which then signs in silently.

It only ever **copies** the already-encrypted token blobs between Steam's own files — it never decrypts, reads, or transmits your tokens or password. Everything stays on your machine.

## Install

1. Download `SteamAccountManager.App.exe` from the [latest release](../../releases/latest).
2. Run it. It's a small single-file exe that runs on the **[.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)** — if that runtime isn't installed, Windows shows a prompt with a download link on first launch.

The app lives in the system tray. Right-click the tray icon to switch accounts, manage groups, or open the main window.

## Auto-update

On startup the app checks the public GitHub Releases for a newer version. If one is found it asks whether to download and restart; on confirmation it downloads the new exe, swaps itself, and relaunches. You can disable the startup check in **Settings → Check for updates on startup**, or trigger it manually from the tray's **Check for updates**.

## Build from source

Requires the **.NET 10 SDK**.

```bash
dotnet build SteamAccountManager.slnx -c Release
dotnet test  SteamAccountManager.slnx -c Release
```

Publish the framework-dependent single-file exe (what CI ships — requires the .NET 10 Desktop Runtime on the target machine):

```bash
dotnet publish src/SteamAccountManager.App/SteamAccountManager.App.csproj \
  -c Release -r win-x64 --self-contained false \
  -p:PublishSingleFile=true
```

### Project layout

| Project | Purpose |
| --- | --- |
| `SteamAccountManager.Core` | Switching engine, token preservation, update service — no UI dependencies. |
| `SteamAccountManager.App`  | WPF tray application (WPF-UI, CommunityToolkit.Mvvm, H.NotifyIcon). |
| `*.Tests`                  | xUnit unit tests for both projects. |

Releases are cut automatically by GitHub Actions when a `v*` tag is pushed.

## Disclaimer

Not affiliated with or endorsed by Valve. This tool manipulates local Steam configuration the same way Steam's own "switch account" feature does. It never handles your password or tokens in plaintext. Provided as-is, without warranty.
