---
name: try7
description: Launch 7 Days to Die, connect to the 7debug mod, and load into the game world for testing
disable-model-invocation: true
---

# try7 — Launch and connect to 7 Days to Die

Launch the game, wait for the 7debug mod HTTP server to come online, and load the most recent saved game.

## Steps

1. **Check if 7debug is already reachable:**
   ```bash
   curl -s --max-time 2 http://localhost:7860/api/status
   ```
   If the server responds, skip to step 3.

2. **Launch the game if not running:**
   Find the game executable via Steam:
   ```powershell
   # Find Steam install path from registry
   $steamPath = (Get-ItemProperty -Path "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam" -Name InstallPath -ErrorAction SilentlyContinue).InstallPath
   if (-not $steamPath) { $steamPath = (Get-ItemProperty -Path "HKCU:\SOFTWARE\Valve\Steam" -Name InstallPath -ErrorAction SilentlyContinue).InstallPath }

   # Parse libraryfolders.vdf to find game
   $vdf = Get-Content "$steamPath\steamapps\libraryfolders.vdf" -Raw
   $libraries = [regex]::Matches($vdf, '"path"\s+"([^"]+)"') | ForEach-Object { $_.Groups[1].Value -replace '\\\\', '\' }
   foreach ($lib in $libraries) {
       $manifest = "$lib\steamapps\appmanifest_251570.acf"
       if (Test-Path $manifest) {
           $acf = Get-Content $manifest -Raw
           $m = [regex]::Match($acf, '"installdir"\s+"([^"]+)"')
           $gameDir = "$lib\steamapps\common\$($m.Groups[1].Value)"
           break
       }
   }
   ```
   Launch via Steam protocol so EAC is bypassed:
   ```bash
   powershell -Command "Start-Process 'steam://rungameid/251570'"
   ```

   Then poll for the 7debug server to come online (check every 5 seconds, up to 120 seconds):
   ```bash
   curl -s --max-time 2 http://localhost:7860/api/status
   ```
   Wait until you get a JSON response. Tell the user the game is loading and report progress.

3. **Check game state and load if needed:**
   Check `GET /api/status` — if `"inGame": true`, we're already loaded. Report the game state and done.

   If `"inGame": false` (at main menu), load the most recent save:
   ```bash
   curl -s -X POST http://localhost:7860/api/loadgame -d '{}'
   ```
   This loads the most recent saved game. An empty body means "pick the most recent save."

   Then poll `GET /api/status` every 5 seconds until `"inGame": true` (up to 120 seconds).

4. **Report final state:**
   Once in-game, fetch and display:
   - `GET /api/status` — FPS, memory, game time
   - `GET /api/players` — player position and health
   - `GET /api/world` — world name, day, loaded mods

   Report to the user that the game is ready for testing.

## Error handling

- If the game doesn't start within 120 seconds, tell the user and suggest checking if the game crashed.
- If loadgame returns an error, show the error and list available saves with `GET /api/saves`.
- If the game is already in-world, just report the current state — don't try to reload.

## Notes

- The 7debug HTTP server runs on port 7860
- All responses are JSON
- The server only becomes available after the mod has loaded (during game startup)
- Loading a game requires being on the main menu (`inGame: false`)
