using System;
using System.Reflection;
using HarmonyLib;

namespace SevenDebug
{
    public class ModApi : IModApi
    {
        public static Mod ModInstance { get; private set; }
        public static string ModPath { get; private set; }

        private DebugHttpServer _server;

        public void InitMod(Mod _modInstance)
        {
            ModInstance = _modInstance;
            ModPath = _modInstance.Path;

            Log.Out("[7debug] Initializing...");

            LogCapture.EnsureRegistered();

            var harmony = new Harmony("com.richard.7debug");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            int port = GameManager.IsDedicatedServer ? 7861 : 7860;
            _server = new DebugHttpServer(port: port);
            _server.Start();

            ModEvents.GameShutdown.RegisterHandler(OnGameShutdown);

            Log.Out("[7debug] HTTP debug server started on port {0}", port);
        }

        private void OnGameShutdown(ref ModEvents.SGameShutdownData data)
        {
            Log.Out("[7debug] Shutting down HTTP server...");
            _server?.Stop();
        }
    }
}
