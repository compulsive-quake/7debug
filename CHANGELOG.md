# Changelog

## 1.1.0 - 2026-03-26

### Added
- `GET /api/saves` — list saved games sorted by most recent
- `POST /api/loadgame` — load a saved game by world/game name, or empty body to load the most recent save
- `QueueMainThreadAction` on ScreenshotCapture — general-purpose main-thread dispatch for operations that require Unity's main thread (like StartGame)
- `/try7` Claude Code skill — launches the game, waits for 7debug to come online, and loads into the most recent save automatically

## 1.0.2 - 2026-03-26

### Fixed
- Log capture was empty — switched from `Application.logMessageReceived` (Unity) to `Log.LogCallbacks` (7DTD's LogLibrary), which is what the game actually uses
- Command output was empty — implemented `IConsoleConnection` (`CaptureConsoleConnection`) to receive output from `SdtdConsole.ExecuteAsync` instead of trying to intercept Unity log events
- Added direct `LogCapture.EnsureRegistered()` call in `InitMod` so log capture starts immediately, not just from the Harmony patch on `GameManager.Awake`

## 1.0.1 - 2026-03-26

### Fixed
- Build errors from incorrect game API usage:
  - `GameShutdown` handler signature — delegate requires `ref SGameShutdownData` parameter
  - `Entity.EntityName` and `Entity.Health` only exist on `EntityAlive` — added cast with `as EntityAlive`
  - `World.GetWorldName()` / `World.GetGamePrefs()` don't exist — replaced with `GamePrefs.GetString(EnumGamePrefs.GameWorld)`
  - `Application.logMessageReceived` can't be assigned to a variable (it's an event) — removed the save
- PowerShell `param()` must be the first statement — moved above `$ErrorActionPreference` in build.ps1 and deploy.ps1
- Build/deploy scripts now auto-detect game install path from Steam registry + `libraryfolders.vdf` instead of hardcoding

## 1.0.0 - 2026-03-26

### Added
- HTTP debug server on port 7860, starts automatically when game loads
- `GET /api/status` — FPS, memory, game version, uptime, game time
- `GET /api/players` — connected players with position, health, level
- `GET /api/world` — world name, seed, size, day/time, difficulty
- `GET /api/entities` — active entities (capped at 200)
- `GET /api/mods` — all loaded mods
- `GET /api/console` — recent log messages with ring buffer (500 entries)
- `GET /api/screenshot` — capture current frame as PNG (client only)
- `POST /api/command` — execute console commands with output capture
- CORS support for browser-based tools
- Fallback to localhost binding if all-interfaces binding fails
- Clean shutdown via `ModEvents.GameShutdown`
