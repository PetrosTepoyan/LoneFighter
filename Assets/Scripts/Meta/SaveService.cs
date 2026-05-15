using System;
using System.IO;
using UnityEngine;

namespace LoneFighter.Meta
{
    // SaveService owns the single on-disk JSON blob and the in-memory cache for it.
    //
    // Why static: every system that needs save state reaches in by name (SaveService.Current),
    // and there is no scenario where two saves coexist — Wipe/Reload are the only mutators
    // beyond Save().
    //
    // Atomic write: we serialize to <path>.tmp first, then File.Replace into the real path.
    // This avoids torn writes if the process dies mid-save (power loss on mobile, app kill).
    //
    // Defensive read: a corrupt or empty JSON file logs a warning and resets to a fresh
    // SaveData rather than throwing. Players never lose access to the menu over bad bytes.
    public static class SaveService
    {
        private const string SaveFileName = "savegame.json";
        private const string TempSuffix = ".tmp";
        private const string BackupSuffix = ".bak";

        private static SaveData _current;
        private static bool _loaded;

        public static SaveData Current
        {
            get
            {
                if (!_loaded) Reload();
                return _current;
            }
        }

        public static string SavePath => Path.Combine(Application.persistentDataPath, SaveFileName);
        private static string TempPath => SavePath + TempSuffix;
        private static string BackupPath => SavePath + BackupSuffix;

        // Force a re-read from disk. Used after Wipe() and on first access.
        public static void Reload()
        {
            _loaded = true;
            try
            {
                if (!File.Exists(SavePath))
                {
                    _current = new SaveData();
                    return;
                }

                string json = File.ReadAllText(SavePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    Debug.LogWarning($"[SaveService] Empty save file at {SavePath}; using defaults.");
                    _current = new SaveData();
                    return;
                }

                var data = JsonUtility.FromJson<SaveData>(json);
                if (data == null)
                {
                    Debug.LogWarning($"[SaveService] Corrupt save (FromJson returned null) at {SavePath}; using defaults.");
                    _current = new SaveData();
                    return;
                }

                data.EnsureDefaults();
                _current = data;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveService] Failed to read save: {ex.Message}. Using defaults.");
                _current = new SaveData();
            }
        }

        public static void Save()
        {
            if (_current == null) _current = new SaveData();
            _current.EnsureDefaults();

            try
            {
                bool pretty =
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                    true;
#else
                    false;
#endif
                string json = JsonUtility.ToJson(_current, pretty);

                // Ensure target dir exists (persistentDataPath should always exist, but
                // belt-and-braces for unusual platforms).
                string dir = Path.GetDirectoryName(SavePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(TempPath, json);

                if (File.Exists(SavePath))
                {
                    // File.Replace gives us an atomic swap on Win/macOS/Linux and keeps a
                    // .bak around in case the new file later turns out to be unreadable.
                    File.Replace(TempPath, SavePath, BackupPath);
                }
                else
                {
                    File.Move(TempPath, SavePath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveService] Failed to write save: {ex.Message}");
            }
        }

        public static void Wipe()
        {
            try
            {
                if (File.Exists(SavePath)) File.Delete(SavePath);
                if (File.Exists(TempPath)) File.Delete(TempPath);
                if (File.Exists(BackupPath)) File.Delete(BackupPath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SaveService] Failed to wipe save: {ex.Message}");
            }
            _current = new SaveData();
            _loaded = true;
        }
    }
}
