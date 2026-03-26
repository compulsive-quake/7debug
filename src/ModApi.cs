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

            var harmony = new Harmony("com.richard.7debug");
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            _server = new DebugHttpServer(port: 7860);
            _server.Start();

            ModEvents.GameShutdown.RegisterHandler(OnGameShutdown);

            Log.Out("[7debug] HTTP debug server started on port 7860");
        }

        private void OnGameShutdown(ref ModEvents.SGameShutdownData data)
        {
            Log.Out("[7debug] Shutting down HTTP server...");
            _server?.Stop();
        }
    }
}
