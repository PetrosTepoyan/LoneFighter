using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace LoneFighter.Debugging.Diagnostics
{
    /// F4-toggled IMGUI panel listing the live state of every PoolService pool.
    ///
    /// PoolService.Instance._pools is private, so we read it via reflection. The dictionary
    /// values are UnityEngine.Pool.ObjectPool<GameObject>, which expose CountActive / CountInactive
    /// public properties. We grab both via reflection too so we don't take a hard compile-time
    /// dependency on the pool type's exact namespace.
    ///
    /// If reflection fails (PoolService is missing, fields renamed, etc.) we fall back to showing
    /// PoolService.Instance.gameObject.transform.childCount as a rough proxy for "stuff in the pool".
    public class PoolDiagnosticsOverlay : MonoBehaviour
    {
        private const float PANEL_WIDTH  = 320f;
        private const float ROW_HEIGHT   = 18f;
        private const float PANEL_MARGIN = 12f;
        private const float REFRESH_INTERVAL = 0.25f; // Re-poll the pool dictionary 4x/sec.

        private GUIStyle _headerStyle;
        private GUIStyle _rowStyle;
        private GUIStyle _bgStyle;

        // Cached reflection handles. Resolved lazily and cached for the lifetime of the overlay.
        private Type _poolServiceType;
        private PropertyInfo _poolServiceInstanceProp;
        private FieldInfo _poolsField;
        private PropertyInfo _countActiveProp;
        private PropertyInfo _countInactiveProp;
        private bool _reflectionReady;
        private bool _reflectionFailed;

        private readonly List<PoolRow> _rows = new();
        private string _fallbackText;
        private float _nextRefreshTime;

        private struct PoolRow
        {
            public string Name;
            public int Active;
            public int Inactive;
        }

        private void OnEnable()
        {
            _nextRefreshTime = 0f;
        }

        private void OnGUI()
        {
            if (!DiagnosticsSettings.DiagnosticsAllowed) return;
            if (!DiagnosticsSettings.PoolOverlay) return;

            EnsureStyles();
            if (Time.unscaledTime >= _nextRefreshTime)
            {
                RefreshSnapshot();
                _nextRefreshTime = Time.unscaledTime + REFRESH_INTERVAL;
            }

            // Anchor: upper-left, but offset down past the F3 PerformanceMonitor block.
            float x = PANEL_MARGIN;
            float y = 100f;
            float rowCount = Mathf.Max(1, _rows.Count);
            float height = ROW_HEIGHT * (rowCount + 2) + 8f;

            var panelRect = new Rect(x, y, PANEL_WIDTH, height);
            GUI.Box(panelRect, GUIContent.none, _bgStyle);

            float cursorY = y + 4f;
            GUI.Label(new Rect(x + 6f, cursorY, PANEL_WIDTH - 12f, ROW_HEIGHT),
                "<b>Pools (F4)</b>", _headerStyle);
            cursorY += ROW_HEIGHT;

            if (_reflectionFailed)
            {
                GUI.Label(new Rect(x + 6f, cursorY, PANEL_WIDTH - 12f, ROW_HEIGHT),
                    _fallbackText ?? "PoolService unavailable", _rowStyle);
                return;
            }

            if (_rows.Count == 0)
            {
                GUI.Label(new Rect(x + 6f, cursorY, PANEL_WIDTH - 12f, ROW_HEIGHT),
                    "(no pools registered)", _rowStyle);
                return;
            }

            for (int i = 0; i < _rows.Count; i++)
            {
                var r = _rows[i];
                string line = $"{Truncate(r.Name, 22),-22}  act {r.Active,4}  free {r.Inactive,4}";
                GUI.Label(new Rect(x + 6f, cursorY, PANEL_WIDTH - 12f, ROW_HEIGHT), line, _rowStyle);
                cursorY += ROW_HEIGHT;
            }
        }

        private void RefreshSnapshot()
        {
            _rows.Clear();
            _fallbackText = null;

            if (!EnsureReflection())
            {
                _reflectionFailed = true;
                return;
            }

            object instance = _poolServiceInstanceProp.GetValue(null, null);
            if (instance == null)
            {
                _reflectionFailed = true;
                _fallbackText = "PoolService.Instance == null";
                return;
            }

            var poolsObj = _poolsField.GetValue(instance);
            if (poolsObj is not IDictionary dict)
            {
                // Fall back to childCount proxy if the dictionary layout changed.
                _reflectionFailed = true;
                if (instance is MonoBehaviour mb && mb != null)
                {
                    _fallbackText = $"~ {mb.transform.childCount} pooled instances (proxy)";
                }
                else
                {
                    _fallbackText = "Pool layout unrecognized";
                }
                return;
            }

            _reflectionFailed = false;

            foreach (DictionaryEntry entry in dict)
            {
                string name = entry.Key is UnityEngine.Object o && o != null ? o.name : "<?>";
                int active = 0, inactive = 0;
                TryReadPoolCounts(entry.Value, ref active, ref inactive);
                _rows.Add(new PoolRow { Name = name, Active = active, Inactive = inactive });
            }
        }

        private void TryReadPoolCounts(object pool, ref int active, ref int inactive)
        {
            if (pool == null) return;
            try
            {
                if (_countActiveProp == null || _countActiveProp.DeclaringType != pool.GetType())
                {
                    _countActiveProp   = pool.GetType().GetProperty("CountActive",   BindingFlags.Public | BindingFlags.Instance);
                    _countInactiveProp = pool.GetType().GetProperty("CountInactive", BindingFlags.Public | BindingFlags.Instance);
                }
                if (_countActiveProp != null)   active   = (int)_countActiveProp.GetValue(pool, null);
                if (_countInactiveProp != null) inactive = (int)_countInactiveProp.GetValue(pool, null);
            }
            catch
            {
                // Swallow: leaves counts at 0 for that row.
            }
        }

        private bool EnsureReflection()
        {
            if (_reflectionReady) return true;

            // PoolService is in the LoneFighter.Systems namespace per spec.
            _poolServiceType = FindType("LoneFighter.Systems.PoolService") ?? FindType("PoolService");
            if (_poolServiceType == null) return false;

            _poolServiceInstanceProp = _poolServiceType.GetProperty("Instance",
                BindingFlags.Public | BindingFlags.Static);
            _poolsField = _poolServiceType.GetField("_pools",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (_poolServiceInstanceProp == null || _poolsField == null) return false;

            _reflectionReady = true;
            return true;
        }

        private static Type FindType(string fullName)
        {
            // Scan loaded assemblies; AppDomain works under Mono + IL2CPP in dev builds.
            var asms = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < asms.Length; i++)
            {
                var t = asms[i].GetType(fullName, throwOnError: false);
                if (t != null) return t;
            }
            return null;
        }

        private static string Truncate(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
        }

        private void EnsureStyles()
        {
            if (_headerStyle != null) return;

            _headerStyle = new GUIStyle(GUI.skin != null ? GUI.skin.label : new GUIStyle())
            {
                fontSize = 13,
                richText = true,
                alignment = TextAnchor.UpperLeft,
            };
            _headerStyle.normal.textColor = Color.white;

            _rowStyle = new GUIStyle(_headerStyle)
            {
                fontSize = 12,
                richText = false,
            };

            _bgStyle = new GUIStyle(GUI.skin != null ? GUI.skin.box : new GUIStyle());
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.55f));
            tex.Apply();
            _bgStyle.normal.background = tex;
        }
    }
}
