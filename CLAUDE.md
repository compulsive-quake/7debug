# 7debug - Remote Debug HTTP Server for 7 Days to Die

## What this is
A 7 Days to Die mod that starts an HTTP server on port 7860 when the game loads. Other mods, tools, and AI agents can connect to it to run commands, inspect game state, and capture screenshots for automated debugging/bug fixing.

## Knowledge Base
There is a shared KB for 7 Days to Die modding at `../7KB/`. Use it to learn about the game's modding API, block systems, XUi, Harmony patching, etc. Keep it up to date as you learn new things.

## Build
```
dotnet build src/7debug.csproj -c Release /p:GameDir="D:\SteamLibrary\steamapps\common\7 Days To Die"
```
Or use `build.ps1` / `build.sh`.

## Deploy
`deploy.ps1` or `deploy.sh` — builds and copies to the game's Mods folder.

## Project structure
- `ModInfo.xml` — mod metadata
- `src/ModApi.cs` — entry point, starts the HTTP server
- `src/DebugHttpServer.cs` — HTTP listener with all API endpoints
- `src/ScreenshotCapture.cs` — MonoBehaviour for main-thread screenshot capture
- `src/ScreenshotRequest.cs` — thread-safe request object for screenshot queue
- `src/LogCapture.cs` — ring buffer capturing game log messages via `Log.LogCallbacks`
- `src/CaptureConsoleConnection.cs` — `IConsoleConnection` impl for capturing command output

## HTTP API (port 7860)
- `GET /` — lists all endpoints
- `GET /api/status` — FPS, memory, game version, uptime, game time
- `GET /api/players` — all connected players with position, health, level
- `GET /api/world` — world name, seed, size, day/time, difficulty, entity count
- `GET /api/entities` — active entities (capped at 200)
- `GET /api/mods` — all loaded mods with name, version, path
- `GET /api/console` — last 100 log messages (errors include stack traces)
- `GET /api/screenshot` — returns a PNG screenshot of the current frame
- `POST /api/command` — execute a console command, body: `{"command": "cmd"}`
- `GET /api/saves` — list saved games sorted by most recent
- `POST /api/loadgame` — load a save, body: `{"world": "name", "game": "name"}` or empty for most recent
- `POST /api/quit` — gracefully quit the game (empty body)

## Key design decisions
- Uses `System.Net.HttpListener` (no external dependencies)
- Screenshots are queued from the HTTP thread and captured on Unity's main thread via a MonoBehaviour coroutine (required because `ReadPixels` must run on main thread)
- Falls back to localhost-only binding if all-interfaces binding fails (admin privileges)
- CORS enabled for browser-based tool access
- Log capture uses `Log.LogCallbacks` (7DTD's LogLibrary), not Unity's `Application.logMessageReceived`
- Command output captured via `IConsoleConnection` implementation, not log interception
- Main-thread dispatch queue on ScreenshotCapture for operations like `StartGame` that must run on Unity's main thread

## Skills
- `/try7` — launches the game, waits for 7debug to come online, and loads the most recent save
