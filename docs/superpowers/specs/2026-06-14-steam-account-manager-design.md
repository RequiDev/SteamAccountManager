# Steam Account Manager — Design Spec

- **Date:** 2026-06-14
- **Status:** Approved (design phase complete; implementation plan next)
- **Platform:** Windows 10/11, desktop

## 1. Purpose

A Windows desktop app that parses every Steam account previously logged in on
this machine and lets the user switch between them with one click, manage an
unlimited number of them (bypassing Steam's 5-account login dropdown), organize
them into categories/groups, and add new accounts. It lives in the system tray
so switching is always one right-click away.

## 2. Key technical reality (from research)

These facts are load-bearing; the architecture is shaped around them.

- **The passwordless-login credential is a JWT refresh token minted by Valve's
  servers and stored DPAPI-encrypted on disk** (reverse-engineered location:
  `%LocalAppData%\Steam\local.vdf`). It is *not* the registry `RememberPassword`
  flag and *not* a password.
- **A switcher only flips selectors and restarts Steam.** The selectors are the
  registry value `HKCU\Software\Valve\Steam\AutoLoginUser` (+ `RememberPassword`
  DWORD) and the `MostRecent` field in `loginusers.vdf`. We **never** touch,
  decrypt, copy, or store the token.
- **We cannot fabricate tokens.** Only a real interactive Steam login mints one.
  Therefore "add account" means *orchestrating one real login*; afterwards the
  account is silently switchable until its token expires (~200 days, version
  dependent) or is revoked (sign-out, password change, Valve revocation).
- **The 5-account limit is a UI cap on Steam's login dropdown, not on storage.**
  `loginusers.vdf` holds many accounts. By driving switching ourselves we make
  the managed count effectively unlimited.
- **Steam rewrites `loginusers.vdf`/`config.vdf` on shutdown.** Any edit made
  while Steam is running gets clobbered. All edits must happen with Steam fully
  closed.
- **Tokens are machine + Windows-user bound (DPAPI).** Config is not portable to
  another PC/user; this is not a backup/migration tool.

See §13 for items that must be verified against a live install before relied on.

## 3. Goals / Non-goals

**Goals**
- Auto-discover all accounts from `loginusers.vdf`.
- One-click switch (auto-close Steam → apply → relaunch logged in).
- Add new accounts via an orchestrated one-time Steam login.
- Unlimited managed accounts.
- Per-account custom labels/notes; show persona name, account name, avatar,
  last-login, and active badge.
- Categories/groups (many-to-many membership) with an "Ungrouped" bucket.
- System tray with group submenus for fast switching; left-click opens the app.
- Autostart with Windows (per-user); start hidden in tray when launched at login.
- Close (X) minimizes to tray; Exit only via tray menu.
- Single-instance.

**Non-goals (v1)**
- Storing or managing passwords / generating Steam Guard 2FA codes.
- Decrypting, reading, copying, or backing up Steam tokens.
- Cross-machine account migration.
- Game launching / library management.
- Non-Windows platforms.

## 4. Tech stack

- **.NET 10 (LTS)**, `net10.0-windows`.
- **WPF**, MVVM via **CommunityToolkit.Mvvm**.
- **WPF-UI** for Fluent theming.
- **Hardcodet.NotifyIcon.Wpf** for the system tray.
- **ValveKeyValue** (SteamDatabase) for VDF parse/write.
- **xUnit** for tests.

## 5. Solution structure (standard Visual Studio layout)

```
SteamAccountManager.sln
├─ src/
│  ├─ SteamAccountManager.Core   (class library, net10.0-windows, no UI deps)
│  └─ SteamAccountManager.App    (WPF app, net10.0-windows)
└─ tests/
   └─ SteamAccountManager.Core.Tests   (xUnit)
```

All Steam logic, group/settings storage, and autostart live in **Core** behind
interfaces so they are unit-testable without the UI. **App** is WPF + tray + MVVM.

## 6. Core components

Each has a single responsibility and an interface (`ISteamLocator`, etc.) so it
can be faked in tests.

| Component | Responsibility |
|---|---|
| `SteamLocator` | Locate the Steam dir + `steam.exe` via registry (`HKCU\…\SteamPath`, `HKLM\…\WOW6432Node\Valve\Steam\InstallPath`, 32-bit fallback), normalizing slashes. |
| `LoginUsersStore` | Parse/write `loginusers.vdf` with ValveKeyValue; tolerant of old/new casing & structure; mutate only target keys; produce `SteamAccount` list. |
| `SteamRegistry` | Read/write `AutoLoginUser` and `RememberPassword`. |
| `SteamProcessController` | Detect running Steam; graceful shutdown via `steam.exe -shutdown`; poll until process fully exits; relaunch (optionally `-silent`). |
| `BackupService` | Back up `loginusers.vdf` and the relevant registry values before any write; support restore. |
| `AtomicFile` | Temp-file-then-rename writer used for every file mutation. |
| `AccountSwitcher` | Orchestrate the safe switch sequence (§8). |
| `AccountMetadataStore` | JSON store (per SteamID64): custom label, notes, group membership. |
| `GroupStore` | JSON store of group definitions (id, name, order). |
| `SettingsStore` | JSON store of app settings (autostart, start-minimized). |
| `AvatarService` | Resolve avatar/persona; keyless community XML endpoint + local `avatarcache` if present; cache images locally; default avatar fallback. |
| `AutostartService` | Read/write the per-user `HKCU\…\CurrentVersion\Run` entry. |

## 7. Data model & storage locations

**Domain types**
- `SteamAccount`: SteamId64, AccountName, PersonaName, MostRecent, Timestamp,
  RememberPassword, AllowAutoLogin (read from `loginusers.vdf`).
- `AccountMetadata`: SteamId64, CustomLabel, Notes, GroupIds[].
- `Group`: Id, Name, SortOrder.
- `AppSettings`: AutostartEnabled, StartMinimized.

**App data** (JSON, `%AppData%\SteamAccountManager\`):
`metadata.json`, `groups.json`, `settings.json`, plus `avatars/` cache and
`backups/` for VDF/registry backups. All written atomically.

**Steam files/registry** (read, and selectors written):
`<SteamInstall>\config\loginusers.vdf`; registry under
`HKCU\Software\Valve\Steam`. The token store (`local.vdf`) is **never** touched.

## 8. Switch sequence (the core engine)

1. Resolve target account.
2. If Steam is running, confirm, then graceful shutdown via `steam.exe -shutdown`
   and **poll until the process is fully gone**.
3. Back up `loginusers.vdf` + registry selector values.
4. Set registry `AutoLoginUser` = target `AccountName`, `RememberPassword` = 1.
5. In `loginusers.vdf`: target `MostRecent` = 1 and `RememberPassword` = 1; set
   all other accounts' `MostRecent` = 0. Write atomically.
6. Re-parse the written file to validate; restore from backup on failure.
7. Relaunch `steam.exe`. Steam reads the selector + the account's stored token
   and logs in silently.
8. Refresh UI/tray; mark the new active account.

If the account's token has expired/been revoked, Steam will show a normal login
+ Steam Guard prompt — expected, surfaced to the user as info, not an error.

## 9. Add-account flow

1. Ensure Steam is closed (graceful shutdown + wait).
2. Clear `AutoLoginUser` so Steam presents a fresh login screen.
3. Launch `steam.exe`.
4. User logs in once with **Remember Password** checked + completes Steam Guard.
5. App watches `loginusers.vdf` for the new SteamID64 entry; on detection,
   refresh the account list. The account is now one-click switchable.

## 10. Groups, tray, autostart, lifecycle

**Groups** — many-to-many; accounts with no group appear under "Ungrouped".
Left pane filters the dashboard by group; per-card checkbox editor assigns
groups; create/rename/delete groups inline.

**Tray** (`TrayIconService`) — context menu rebuilt whenever accounts/groups
change:
```
Steam Account Manager
  <Group>      ▸  [ account ] [ account ] …   (click = switch)
  …
  Ungrouped    ▸  [ … ]
  ──────────
  Open
  Start with Windows   ✓
  Exit
```
Active account is checked. Left-click the tray icon opens the main window.

**Autostart** — `AutostartService` toggles the `HKCU\…\CurrentVersion\Run` entry
(no admin). When launched at login, start hidden in the tray.

**Lifecycle** — Close (X) hides to tray; the app keeps running. Exit only via
tray "Exit". Single-instance enforced with a named mutex; a second launch
surfaces the existing window.

## 11. Safety & error handling (no shortcuts)

- Never edit Steam files/registry while Steam is running.
- Atomic writes (temp-then-rename) for every mutation.
- Back up `loginusers.vdf` + registry selectors before writing; restore on
  failure.
- Validate VDF re-parses before and after writing; tolerate Steam's
  casing/whitespace/structure variants.
- Never store, read, transmit, or decrypt credentials/tokens.
- Graceful handling when Steam is not installed / files missing.
- Clear, non-alarming messaging when a switch triggers Steam Guard (expired
  token), and when an operation needs Steam closed.

## 12. UI

WPF + WPF-UI Fluent. Main window: left group-filter pane + a dashboard of
account cards (avatar, persona name, account name, custom label, last-login,
active badge). Click a card to switch; per-card edit-label and group editor.
"Add account" button. Settings (autostart, start-minimized). Non-blocking
status/toasts for the shutdown→switch→relaunch sequence.

## 13. Must-verify-live items

Verify against a real Steam install during implementation before depending on:
1. `loginusers.vdf` exact structure/casing on the target machine.
2. `avatarcache` filename scheme/location (or rely on the community XML endpoint).
3. Graceful-shutdown timing / reliable "fully exited" detection.
4. That silent switching works on the current Steam client version (there are
   reported regressions where every switch re-prompts Steam Guard).

## 14. Testing

xUnit over Core: `loginusers.vdf` parse/round-trip against sample fixtures (old
& new formats), `AccountSwitcher` orchestration with faked process/registry/
filesystem, metadata/group/settings stores, autostart toggle (faked registry).
Real-Steam integration checks are manual/opt-in.

## 15. Distribution

Standard `dotnet build` / `dotnet publish`. Release: self-contained single-file
exe for `win-x64`. No installer required for v1.
