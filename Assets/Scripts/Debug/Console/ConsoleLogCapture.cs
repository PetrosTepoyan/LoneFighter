using System;
using UnityEngine;

namespace LoneFighter.Debugging.Console
{
    /// <summary>
    /// Captures <see cref="Application.logMessageReceived"/> entries into a fixed-size ring buffer
    /// (capacity 200) for display inside the in-game developer console. Each entry is colour-coded
    /// by <see cref="LogType"/> using rich-text-friendly hex strings so an IMGUI label using
    /// <c>richText = true</c> can render them directly.
    /// </summary>
    public sealed class ConsoleLogCapture
    {
        public const int Capacity = 200;

        public readonly struct Entry
        {
            public readonly string Text;
            public readonly LogType Type;
            public readonly string ColorHex;

            public Entry(string text, LogType type, string colorHex)
            {
                Text = text;
                Type = type;
                ColorHex = colorHex;
            }

            public string Rich => string.IsNullOrEmpty(ColorHex)
                ? Text
                : $"<color=#{ColorHex}>{Text}</color>";
        }

        private readonly Entry[] _buffer = new Entry[Capacity];
        private int _head;       // next write index
        private int _count;      // number of valid entries (<= Capacity)

        public int Count => _count;
        public bool IsAttached { get; private set; }

        public event Action OnChanged;

        public void Attach()
        {
            if (IsAttached) return;
            Application.logMessageReceived += HandleLogMessage;
            IsAttached = true;
        }

        public void Detach()
        {
            if (!IsAttached) return;
            Application.logMessageReceived -= HandleLogMessage;
            IsAttached = false;
        }

        public void Clear()
        {
            for (int i = 0; i < _buffer.Length; i++) _buffer[i] = default;
            _head = 0;
            _count = 0;
            OnChanged?.Invoke();
        }

        /// <summary>Manually push a line without going through Unity's log system.</summary>
        public void Push(string text, LogType type = LogType.Log)
        {
            AppendInternal(text, type);
            OnChanged?.Invoke();
        }

        /// <summary>Copies the most recent <paramref name="maxCount"/> entries into <paramref name="dest"/> oldest-first.</summary>
        /// <returns>Number of entries written.</returns>
        public int CopyRecent(Entry[] dest, int maxCount)
        {
            if (dest == null || dest.Length == 0 || maxCount <= 0) return 0;
            int n = Mathf.Min(Mathf.Min(maxCount, _count), dest.Length);
            // Oldest entry sits at _head - _count (mod capacity). Walk forward from there.
            int start = (_head - _count + Capacity * 2) % Capacity;
            int firstShown = _count - n; // skip the oldest extras if dest is smaller
            int srcIndex = (start + firstShown) % Capacity;
            for (int i = 0; i < n; i++)
            {
                dest[i] = _buffer[srcIndex];
                srcIndex = (srcIndex + 1) % Capacity;
            }
            return n;
        }

        private void HandleLogMessage(string condition, string stackTrace, LogType type)
        {
            // Stack traces are appended only for exceptions/errors to keep the buffer readable.
            if (type == LogType.Exception || type == LogType.Error)
            {
                if (!string.IsNullOrEmpty(stackTrace))
                {
                    AppendInternal(condition, type);
                    AppendInternal(stackTrace.TrimEnd(), type);
                }
                else
                {
                    AppendInternal(condition, type);
                }
            }
            else
            {
                AppendInternal(condition, type);
            }
            OnChanged?.Invoke();
        }

        private void AppendInternal(string text, LogType type)
        {
            if (text == null) text = string.Empty;
            _buffer[_head] = new Entry(text, type, ColorForType(type));
            _head = (_head + 1) % Capacity;
            if (_count < Capacity) _count++;
        }

        public static string ColorForType(LogType type)
        {
            switch (type)
            {
                case LogType.Error:     return "ff6b6b";
                case LogType.Exception: return "ff3b3b";
                case LogType.Assert:    return "ff9f43";
                case LogType.Warning:   return "feca57";
                case LogType.Log:
                default:                return "dfe6e9";
            }
        }
    }
}
