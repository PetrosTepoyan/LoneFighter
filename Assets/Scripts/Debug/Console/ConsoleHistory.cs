using System.Collections.Generic;

namespace LoneFighter.Debugging.Console
{
    /// <summary>
    /// Up/Down arrow command history. Stores up to 20 most recent submitted lines and exposes
    /// <see cref="MoveUp"/> / <see cref="MoveDown"/> that mimic typical shell behaviour: Up walks
    /// backward in time, Down walks forward toward the empty prompt.
    /// </summary>
    public sealed class ConsoleHistory
    {
        public const int MaxEntries = 20;

        private readonly List<string> _entries = new(MaxEntries);
        // -1 means "not browsing" (cursor sits at the empty prompt past the newest entry).
        private int _cursor = -1;

        public int Count => _entries.Count;

        public void Add(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return;
            // Drop a duplicate of the most recent entry to keep history useful.
            if (_entries.Count > 0 && _entries[_entries.Count - 1] == command)
            {
                _cursor = -1;
                return;
            }

            _entries.Add(command);
            while (_entries.Count > MaxEntries) _entries.RemoveAt(0);
            _cursor = -1;
        }

        /// <summary>Older entry. Returns the previous command, or <paramref name="current"/> if at the oldest.</summary>
        public string MoveUp(string current)
        {
            if (_entries.Count == 0) return current;
            if (_cursor == -1) _cursor = _entries.Count - 1;
            else if (_cursor > 0) _cursor--;
            return _entries[_cursor];
        }

        /// <summary>Newer entry. Walks toward the empty prompt; returns "" once past the newest.</summary>
        public string MoveDown(string current)
        {
            if (_entries.Count == 0 || _cursor == -1) return current;
            if (_cursor < _entries.Count - 1)
            {
                _cursor++;
                return _entries[_cursor];
            }
            _cursor = -1;
            return string.Empty;
        }

        /// <summary>Resets the browsing cursor (call after the user manually edits the input field).</summary>
        public void ResetCursor() => _cursor = -1;

        public string Get(int index)
        {
            if (index < 0 || index >= _entries.Count) return string.Empty;
            return _entries[index];
        }

        public void Clear()
        {
            _entries.Clear();
            _cursor = -1;
        }
    }
}
