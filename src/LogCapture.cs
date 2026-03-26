using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SevenDebug
{
    /// <summary>
    /// Captures recent Unity log messages into a ring buffer so they
    /// can be served via the /api/console endpoint.
    /// </summary>
    public static class LogCapture
    {
        private const int MaxEntries = 500;
        private static readonly object Lock = new object();
        private static readonly LinkedList<LogEntry> Entries = new LinkedList<LogEntry>();
        private static bool _registered;

        public struct LogEntry
        {
            public string Time;
            public string Type;
            public string Message;
        }

        public static void EnsureRegistered()
        {
            if (_registered) return;
            _registered = true;
            Application.logMessageReceived += OnLogMessage;
        }

        private static void OnLogMessage(string message, string stackTrace, LogType type)
        {
            var entry = new LogEntry
            {
                Time = DateTime.Now.ToString("HH:mm:ss.fff"),
                Type = type.ToString(),
                Message = message
            };

            // Include stacktrace for errors/exceptions
            if ((type == LogType.Error || type == LogType.Exception) && !string.IsNullOrEmpty(stackTrace))
            {
                entry.Message = message + "\n" + stackTrace;
            }

            lock (Lock)
            {
                Entries.AddLast(entry);
                while (Entries.Count > MaxEntries)
                    Entries.RemoveFirst();
            }
        }

        public static List<LogEntry> GetRecentEntries(int count = 100)
        {
            var result = new List<LogEntry>();
            lock (Lock)
            {
                var node = Entries.Last;
                while (node != null && result.Count < count)
                {
                    result.Add(node.Value);
                    node = node.Previous;
                }
            }
            result.Reverse();
            return result;
        }
    }

    /// <summary>
    /// Harmony patch to register our log capture early.
    /// </summary>
    [HarmonyPatch(typeof(GameManager), "Awake")]
    public static class GameManagerAwakePatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            LogCapture.EnsureRegistered();
        }
    }
}
