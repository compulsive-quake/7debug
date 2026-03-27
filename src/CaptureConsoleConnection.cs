using System;
using System.Collections.Generic;
using UnityEngine;

namespace SevenDebug
{
    /// <summary>
    /// IConsoleConnection implementation that captures command output lines.
    /// </summary>
    public class CaptureConsoleConnection : IConsoleConnection
    {
        public List<string> Lines { get; } = new List<string>();

        public void SendLines(List<string> _output)
        {
            if (_output != null)
                Lines.AddRange(_output);
        }

        public void SendLine(string _text)
        {
            Lines.Add(_text);
        }

        public void SendLog(string _formattedMessage, string _plainMessage, string _trace,
            LogType _type, DateTime _timestamp, long _uptime)
        {
        }

        public void EnableLogLevel(LogType _type, bool _enable)
        {
        }

        public string GetDescription()
        {
            return "7debug";
        }
    }
}
