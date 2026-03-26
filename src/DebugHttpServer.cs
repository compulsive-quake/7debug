using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using UnityEngine;

namespace SevenDebug
{
    public class DebugHttpServer
    {
        private readonly int _port;
        private HttpListener _listener;
        private Thread _listenerThread;
        private volatile bool _running;

        // Screenshot queue: requests come from HTTP thread, captures happen on main thread
        private readonly ConcurrentQueue<ScreenshotRequest> _screenshotQueue = new ConcurrentQueue<ScreenshotRequest>();
        private GameObject _screenshotHelper;

        public DebugHttpServer(int port)
        {
            _port = port;
        }

        public void Start()
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://+:{_port}/");
            _running = true;

            try
            {
                _listener.Start();
            }
            catch (HttpListenerException)
            {
                // Fallback to localhost only if we can't bind to all interfaces
                Log.Warning("[7debug] Cannot bind to all interfaces, falling back to localhost only");
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{_port}/");
                _listener.Start();
            }

            _listenerThread = new Thread(ListenLoop)
            {
                IsBackground = true,
                Name = "7debug-http"
            };
            _listenerThread.Start();

            // Create a GameObject to handle screenshot captures on the main thread
            _screenshotHelper = new GameObject("7debug_ScreenshotHelper");
            _screenshotHelper.AddComponent<ScreenshotCapture>();
            _screenshotHelper.GetComponent<ScreenshotCapture>().RequestQueue = _screenshotQueue;
            UnityEngine.Object.DontDestroyOnLoad(_screenshotHelper);
        }

        public void Stop()
        {
            _running = false;
            _listener?.Stop();
            _listener?.Close();
            if (_screenshotHelper != null)
                UnityEngine.Object.Destroy(_screenshotHelper);
        }

        private void ListenLoop()
        {
            while (_running)
            {
                try
                {
                    var context = _listener.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => HandleRequest(context));
                }
                catch (HttpListenerException)
                {
                    if (_running) Log.Warning("[7debug] Listener exception");
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            // CORS headers for browser-based tools
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 204;
                response.Close();
                return;
            }

            try
            {
                var path = request.Url.AbsolutePath.TrimEnd('/');
                string result;

                switch (path)
                {
                    case "":
                    case "/":
                        result = HandleIndex();
                        break;
                    case "/api/status":
                        result = HandleStatus();
                        break;
                    case "/api/command":
                        result = HandleCommand(request);
                        break;
                    case "/api/console":
                        result = HandleConsoleLog();
                        break;
                    case "/api/players":
                        result = HandlePlayers();
                        break;
                    case "/api/world":
                        result = HandleWorld();
                        break;
                    case "/api/screenshot":
                        HandleScreenshot(response);
                        return;
                    case "/api/mods":
                        result = HandleMods();
                        break;
                    case "/api/entities":
                        result = HandleEntities();
                        break;
                    default:
                        response.StatusCode = 404;
                        result = Json("error", "Not found. GET / for available endpoints.");
                        break;
                }

                SendJson(response, result);
            }
            catch (Exception ex)
            {
                Log.Error($"[7debug] Request error: {ex.Message}");
                response.StatusCode = 500;
                SendJson(response, Json("error", ex.Message));
            }
        }

        // ── Handlers ───────────────────────────────────────────────

        private string HandleIndex()
        {
            return @"{
  ""name"": ""7debug"",
  ""version"": ""1.0.0"",
  ""endpoints"": {
    ""GET /api/status"":      ""Game status, FPS, memory, time"",
    ""GET /api/players"":     ""Connected players and their stats"",
    ""GET /api/world"":       ""World info (seed, size, day, time)"",
    ""GET /api/entities"":    ""Active entities in the world"",
    ""GET /api/mods"":        ""Loaded mods"",
    ""GET /api/console"":     ""Recent console log output"",
    ""GET /api/screenshot"":  ""Capture and return a PNG screenshot"",
    ""POST /api/command"":    ""Execute a console command (body: {\""command\"": \""cmd\""})"",
  }
}";
        }

        private string HandleStatus()
        {
            var sb = new StringBuilder();
            sb.Append("{");

            var world = GameManager.Instance?.World;
            bool inGame = world != null;

            sb.Append($"\"inGame\":{B(inGame)},");
            sb.Append($"\"fps\":{(1f / Time.unscaledDeltaTime):F1},");
            sb.Append($"\"memoryMB\":{(GC.GetTotalMemory(false) / 1048576f):F1},");
            sb.Append($"\"unityMemoryMB\":{(UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576f):F1},");
            sb.Append($"\"uptime\":\"{TimeSpan.FromSeconds(Time.realtimeSinceStartup):hh\\:mm\\:ss}\",");
            sb.Append($"\"platform\":\"{Application.platform}\",");
            sb.Append($"\"gameVersion\":\"{Constants.cVersionInformation.LongString}\"");

            if (inGame)
            {
                sb.Append($",\"gameTime\":{{\"day\":{world.worldTime / 24000UL},\"hour\":{(world.worldTime % 24000UL) / 1000UL}}}");
            }

            sb.Append("}");
            return sb.ToString();
        }

        private string HandleCommand(HttpListenerRequest request)
        {
            if (request.HttpMethod != "POST")
                return Json("error", "Use POST with body: {\"command\": \"your command\"}");

            string body;
            using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                body = reader.ReadToEnd();

            // Simple JSON parse for {"command": "..."}
            var cmd = ExtractJsonString(body, "command");
            if (string.IsNullOrEmpty(cmd))
                return Json("error", "Missing 'command' field in JSON body");

            Log.Out($"[7debug] Executing command: {cmd}");

            // Capture output
            var output = new List<string>();

            void LogHandler(string msg, string stack, LogType type)
            {
                output.Add(msg);
            }

            Application.logMessageReceived += LogHandler;

            try
            {
                // Execute via the console command system
                SdtdConsole.Instance.ExecuteSync(cmd, null);
            }
            finally
            {
                Application.logMessageReceived -= LogHandler;
            }

            var sb = new StringBuilder();
            sb.Append("{\"command\":");
            sb.Append(JsonString(cmd));
            sb.Append(",\"output\":[");
            for (int i = 0; i < output.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(JsonString(output[i]));
            }
            sb.Append("]}");
            return sb.ToString();
        }

        private string HandleConsoleLog()
        {
            var sb = new StringBuilder();
            sb.Append("{\"log\":[");

            var entries = LogCapture.GetRecentEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                if (i > 0) sb.Append(",");
                var e = entries[i];
                sb.Append("{\"time\":");
                sb.Append(JsonString(e.Time));
                sb.Append(",\"type\":");
                sb.Append(JsonString(e.Type));
                sb.Append(",\"message\":");
                sb.Append(JsonString(e.Message));
                sb.Append("}");
            }

            sb.Append("]}");
            return sb.ToString();
        }

        private string HandlePlayers()
        {
            var sb = new StringBuilder();
            sb.Append("{\"players\":[");

            var world = GameManager.Instance?.World;
            if (world?.Players != null)
            {
                bool first = true;
                foreach (var kvp in world.Players.dict)
                {
                    var player = kvp.Value;
                    if (!first) sb.Append(",");
                    first = false;

                    sb.Append("{");
                    sb.Append($"\"entityId\":{player.entityId},");
                    sb.Append($"\"name\":{JsonString(player.EntityName)},");
                    sb.Append($"\"position\":{{\"x\":{player.position.x:F1},\"y\":{player.position.y:F1},\"z\":{player.position.z:F1}}},");
                    sb.Append($"\"rotation\":{{\"x\":{player.rotation.x:F1},\"y\":{player.rotation.y:F1},\"z\":{player.rotation.z:F1}}},");
                    sb.Append($"\"health\":{player.Health},");
                    sb.Append($"\"maxHealth\":{player.GetMaxHealth()},");
                    sb.Append($"\"stamina\":{player.Stamina:F0},");
                    sb.Append($"\"level\":{player.Progression?.Level ?? 0},");
                    sb.Append($"\"isDead\":{B(player.IsDead())}");
                    sb.Append("}");
                }
            }

            sb.Append("]}");
            return sb.ToString();
        }

        private string HandleWorld()
        {
            var sb = new StringBuilder();
            sb.Append("{");

            var world = GameManager.Instance?.World;
            if (world != null)
            {
                sb.Append($"\"worldName\":{JsonString(GamePrefs.GetString(EnumGamePrefs.GameWorld))},");
                sb.Append($"\"seed\":{JsonString(GamePrefs.GetString(EnumGamePrefs.WorldGenSeed))},");
                sb.Append($"\"worldSize\":{GamePrefs.GetInt(EnumGamePrefs.WorldGenSize)},");
                sb.Append($"\"gameTime\":{{\"day\":{world.worldTime / 24000UL},\"hour\":{(world.worldTime % 24000UL) / 1000UL}}},");
                sb.Append($"\"difficulty\":{GamePrefs.GetInt(EnumGamePrefs.GameDifficulty)},");
                sb.Append($"\"entityCount\":{world.Entities.Count},");
                sb.Append($"\"chunkCacheCount\":{world.ChunkCache?.Count() ?? 0}");
            }
            else
            {
                sb.Append("\"error\":\"No world loaded\"");
            }

            sb.Append("}");
            return sb.ToString();
        }

        private string HandleMods()
        {
            var sb = new StringBuilder();
            sb.Append("{\"mods\":[");

            var mods = ModManager.GetLoadedMods();
            if (mods != null)
            {
                bool first = true;
                foreach (var mod in mods)
                {
                    if (!first) sb.Append(",");
                    first = false;

                    sb.Append("{");
                    sb.Append($"\"name\":{JsonString(mod.Name)},");
                    sb.Append($"\"displayName\":{JsonString(mod.DisplayName ?? mod.Name)},");
                    sb.Append($"\"version\":{JsonString(mod.VersionString ?? "unknown")},");
                    sb.Append($"\"path\":{JsonString(mod.Path)}");
                    sb.Append("}");
                }
            }

            sb.Append("]}");
            return sb.ToString();
        }

        private string HandleEntities()
        {
            var sb = new StringBuilder();
            sb.Append("{\"entities\":[");

            var world = GameManager.Instance?.World;
            if (world?.Entities != null)
            {
                bool first = true;
                int count = 0;
                foreach (var kvp in world.Entities.dict)
                {
                    if (count >= 200) break; // cap to avoid huge responses
                    var entity = kvp.Value;
                    if (!first) sb.Append(",");
                    first = false;

                    sb.Append("{");
                    sb.Append($"\"entityId\":{entity.entityId},");
                    sb.Append($"\"type\":{JsonString(entity.GetType().Name)},");
                    var alive = entity as EntityAlive;
                    sb.Append($"\"name\":{JsonString(alive?.EntityName ?? "")},");
                    sb.Append($"\"position\":{{\"x\":{entity.position.x:F1},\"y\":{entity.position.y:F1},\"z\":{entity.position.z:F1}}},");
                    sb.Append($"\"health\":{alive?.Health ?? 0},");
                    sb.Append($"\"isDead\":{B(entity.IsDead())}");
                    sb.Append("}");
                    count++;
                }
            }

            sb.Append("]}");
            return sb.ToString();
        }

        private void HandleScreenshot(HttpListenerResponse response)
        {
            if (GameManager.IsDedicatedServer)
            {
                response.StatusCode = 400;
                SendJson(response, Json("error", "Screenshots not available on dedicated server"));
                return;
            }

            var req = new ScreenshotRequest();
            _screenshotQueue.Enqueue(req);

            // Wait for the main thread to capture (up to 5 seconds)
            if (req.WaitHandle.WaitOne(5000))
            {
                if (req.PngData != null)
                {
                    response.ContentType = "image/png";
                    response.ContentLength64 = req.PngData.Length;
                    response.OutputStream.Write(req.PngData, 0, req.PngData.Length);
                    response.OutputStream.Close();
                    response.Close();
                }
                else
                {
                    response.StatusCode = 500;
                    SendJson(response, Json("error", "Screenshot capture failed"));
                }
            }
            else
            {
                response.StatusCode = 504;
                SendJson(response, Json("error", "Screenshot capture timed out"));
            }
        }

        // ── Helpers ────────────────────────────────────────────────

        private void SendJson(HttpListenerResponse response, string json)
        {
            response.ContentType = "application/json";
            var buffer = Encoding.UTF8.GetBytes(json);
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
            response.Close();
        }

        private static string Json(string key, string value) =>
            $"{{\"{key}\":{JsonString(value)}}}";

        private static string JsonString(string s)
        {
            if (s == null) return "null";
            return "\"" + s
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t") + "\"";
        }

        private static string B(bool b) => b ? "true" : "false";

        private static string ExtractJsonString(string json, string key)
        {
            // Minimal JSON string extractor — avoids bringing in a JSON library
            var search = $"\"{key}\"";
            int idx = json.IndexOf(search, StringComparison.Ordinal);
            if (idx < 0) return null;

            idx = json.IndexOf(':', idx + search.Length);
            if (idx < 0) return null;

            idx = json.IndexOf('"', idx + 1);
            if (idx < 0) return null;

            int start = idx + 1;
            int end = start;
            while (end < json.Length)
            {
                if (json[end] == '\\') { end += 2; continue; }
                if (json[end] == '"') break;
                end++;
            }
            return json.Substring(start, end - start);
        }
    }
}
