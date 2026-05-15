using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace LoneFighter.Debugging.Diagnostics
{
    /// F5-toggled visualizer for enemy AI state.
    ///
    /// For every enemy in EnemyRegistry:
    ///   - Draws a line from the enemy to PlayerController.Instance (the chase target).
    ///   - If the enemy has a DasherAI, draws an arrow along its current dash direction.
    ///   - If the enemy has an EnemyShockwave child / sibling, draws the expanding ring.
    ///
    /// Two render paths:
    ///   - OnDrawGizmos for the Scene view in the Editor.
    ///   - LineRenderer-based fallback so it also renders in runtime builds.
    ///
    /// All registry / component access uses reflection so this overlay compiles even when the
    /// referenced gameplay scripts move namespaces or are stripped from a stub build.
    [ExecuteAlways]
    public class EnemyAiVisualizer : MonoBehaviour
    {
        [SerializeField] private Color chaseLineColor    = new Color(0.20f, 0.80f, 1.00f, 0.85f);
        [SerializeField] private Color dashArrowColor    = new Color(1.00f, 0.55f, 0.20f, 0.95f);
        [SerializeField] private Color shockwaveColor    = new Color(1.00f, 0.25f, 0.25f, 0.85f);
        [SerializeField] private int   shockwaveSegments = 48;

        // Reflection cache.
        private Type _registryType;
        private PropertyInfo _enemiesProp;
        private Type _playerControllerType;
        private PropertyInfo _playerInstanceProp;
        private Type _dasherType;
        private FieldInfo _dashDirField;
        private Type _shockwaveType;
        private FieldInfo _elapsedField;
        private FieldInfo _expandDurationField;
        private FieldInfo _maxRadiusField;
        private bool _reflectionResolved;

        // Runtime line-renderer pool for builds. Each enemy can contribute multiple line segments
        // per frame; we recycle a flat pool and toggle unused renderers off at the end.
        private readonly List<LineRenderer> _linePool = new();
        private int _lineCursor;
        private Material _lineMaterial;
        private Transform _lineRoot;

        private void OnEnable()
        {
            EnsureReflection();
        }

        private void OnDisable()
        {
            // Hide every spawned LineRenderer.
            for (int i = 0; i < _linePool.Count; i++)
            {
                if (_linePool[i] != null) _linePool[i].enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (_lineRoot != null) Destroy(_lineRoot.gameObject);
            if (_lineMaterial != null) Destroy(_lineMaterial);
        }

        /// Scene-view rendering. Active only when the overlay is enabled and the user has the
        /// AI-gizmos preference on.
        private void OnDrawGizmos()
        {
            if (!enabled) return;
            if (!Application.isPlaying) return;
            if (!DiagnosticsSettings.DiagnosticsAllowed) return;
            if (!DiagnosticsSettings.AiGizmos) return;

            EnsureReflection();
            ForEachEnemy((enemyGo, enemyPos, playerPos) =>
            {
                Gizmos.color = chaseLineColor;
                Gizmos.DrawLine(enemyPos, playerPos);

                var dash = enemyGo.GetComponent(_dasherType);
                if (dash != null && _dashDirField != null)
                {
                    Vector2 dir = (Vector2)_dashDirField.GetValue(dash);
                    if (dir.sqrMagnitude > 0.0001f)
                    {
                        Gizmos.color = dashArrowColor;
                        Vector3 tip = enemyPos + (Vector3)(dir.normalized * 1.5f);
                        Gizmos.DrawLine(enemyPos, tip);
                        DrawArrowHead(enemyPos, tip, dashArrowColor);
                    }
                }
            });

            DrawShockwavesGizmo();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying) return;
            if (!DiagnosticsSettings.DiagnosticsAllowed) return;
            if (!DiagnosticsSettings.AiGizmos)
            {
                HideAllLines();
                return;
            }

            EnsureReflection();
            _lineCursor = 0;

            ForEachEnemy((enemyGo, enemyPos, playerPos) =>
            {
                DrawRuntimeLine(enemyPos, playerPos, chaseLineColor);

                var dash = enemyGo.GetComponent(_dasherType);
                if (dash != null && _dashDirField != null)
                {
                    Vector2 dir = (Vector2)_dashDirField.GetValue(dash);
                    if (dir.sqrMagnitude > 0.0001f)
                    {
                        Vector3 tip = enemyPos + (Vector3)(dir.normalized * 1.5f);
                        DrawRuntimeLine(enemyPos, tip, dashArrowColor);
                    }
                }
            });

            DrawShockwavesRuntime();
            HideUnusedLines();
        }

        private delegate void EnemyVisitor(GameObject enemy, Vector3 enemyPos, Vector3 playerPos);

        private void ForEachEnemy(EnemyVisitor visit)
        {
            if (_registryType == null || _enemiesProp == null) return;

            var enemiesObj = _enemiesProp.GetValue(null, null);
            if (enemiesObj is not IEnumerable enemies) return;

            Vector3? playerPos = ResolvePlayerPos();
            if (!playerPos.HasValue) return;

            foreach (var e in enemies)
            {
                if (e is not Component c || c == null) continue;
                if (!c.gameObject.activeInHierarchy) continue;
                visit(c.gameObject, c.transform.position, playerPos.Value);
            }
        }

        private Vector3? ResolvePlayerPos()
        {
            if (_playerInstanceProp == null) return null;
            var inst = _playerInstanceProp.GetValue(null, null);
            if (inst is Component pc && pc != null) return pc.transform.position;
            return null;
        }

        private void DrawShockwavesGizmo()
        {
            if (_shockwaveType == null) return;

            // Shockwaves are not in EnemyRegistry, so scan the scene. This is dev-only; cost is fine.
            var waves = FindObjectsByType(_shockwaveType, FindObjectsSortMode.None);
            if (waves == null) return;

            Gizmos.color = shockwaveColor;
            for (int i = 0; i < waves.Length; i++)
            {
                if (waves[i] is not Component c || c == null) continue;
                if (!c.gameObject.activeInHierarchy) continue;
                float r = ReadShockwaveRadius(c);
                if (r <= 0f) continue;
                DrawCircleGizmo(c.transform.position, r);
            }
        }

        private void DrawShockwavesRuntime()
        {
            if (_shockwaveType == null) return;
            var waves = FindObjectsByType(_shockwaveType, FindObjectsSortMode.None);
            if (waves == null) return;

            for (int i = 0; i < waves.Length; i++)
            {
                if (waves[i] is not Component c || c == null) continue;
                if (!c.gameObject.activeInHierarchy) continue;
                float r = ReadShockwaveRadius(c);
                if (r <= 0f) continue;
                DrawRuntimeCircle(c.transform.position, r, shockwaveColor);
            }
        }

        private float ReadShockwaveRadius(Component wave)
        {
            try
            {
                if (_elapsedField == null || _expandDurationField == null || _maxRadiusField == null)
                    return 0f;
                float elapsed  = (float)_elapsedField.GetValue(wave);
                float duration = (float)_expandDurationField.GetValue(wave);
                float maxR     = (float)_maxRadiusField.GetValue(wave);
                if (duration <= 0f) return maxR;
                float t = Mathf.Clamp01(elapsed / duration);
                return Mathf.Lerp(0f, maxR, t);
            }
            catch
            {
                return 0f;
            }
        }

        private static UnityEngine.Object[] FindObjectsByType(Type t, FindObjectsSortMode sort)
        {
            // Wrapper so we can call the generic API by type at runtime.
            var method = typeof(UnityEngine.Object)
                .GetMethod("FindObjectsByType",
                    BindingFlags.Public | BindingFlags.Static,
                    binder: null,
                    types: new[] { typeof(Type), typeof(FindObjectsSortMode) },
                    modifiers: null);
            if (method == null) return null;
            return (UnityEngine.Object[])method.Invoke(null, new object[] { t, sort });
        }

        private static void DrawCircleGizmo(Vector3 center, float radius)
        {
            const int segments = 48;
            Vector3 prev = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float a = (i / (float)segments) * Mathf.PI * 2f;
                Vector3 next = center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        private static void DrawArrowHead(Vector3 from, Vector3 to, Color color)
        {
            Vector3 dir = (to - from).normalized;
            Vector3 right = Quaternion.Euler(0f, 0f, 25f) * -dir * 0.3f;
            Vector3 left  = Quaternion.Euler(0f, 0f, -25f) * -dir * 0.3f;
            Gizmos.color = color;
            Gizmos.DrawLine(to, to + right);
            Gizmos.DrawLine(to, to + left);
        }

        // ---------------------------------------------------------------- runtime LineRenderers

        private void DrawRuntimeLine(Vector3 a, Vector3 b, Color color)
        {
            var lr = GetLine();
            lr.enabled = true;
            lr.positionCount = 2;
            lr.SetPosition(0, a);
            lr.SetPosition(1, b);
            ApplyColor(lr, color);
        }

        private void DrawRuntimeCircle(Vector3 center, float radius, Color color)
        {
            var lr = GetLine();
            lr.enabled = true;
            int segs = Mathf.Max(8, shockwaveSegments);
            lr.positionCount = segs + 1;
            for (int i = 0; i <= segs; i++)
            {
                float a = (i / (float)segs) * Mathf.PI * 2f;
                lr.SetPosition(i, center + new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
            }
            ApplyColor(lr, color);
        }

        private void ApplyColor(LineRenderer lr, Color color)
        {
            lr.startColor = color;
            lr.endColor   = color;
        }

        private LineRenderer GetLine()
        {
            if (_lineRoot == null)
            {
                var go = new GameObject("[DiagAiLines]");
                go.hideFlags = HideFlags.DontSave;
                _lineRoot = go.transform;
            }
            if (_lineMaterial == null)
            {
                // URP-compatible unlit material via the always-present "Sprites/Default" shader,
                // which renders without lighting and works in URP 2D scenes.
                var shader = Shader.Find("Sprites/Default");
                if (shader != null) _lineMaterial = new Material(shader);
            }

            if (_lineCursor < _linePool.Count)
            {
                var existing = _linePool[_lineCursor++];
                if (existing == null)
                {
                    existing = CreateLine();
                    _linePool[_lineCursor - 1] = existing;
                }
                return existing;
            }

            var fresh = CreateLine();
            _linePool.Add(fresh);
            _lineCursor = _linePool.Count;
            return fresh;
        }

        private LineRenderer CreateLine()
        {
            var go = new GameObject("DiagLine");
            go.hideFlags = HideFlags.DontSave;
            go.transform.SetParent(_lineRoot, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.widthMultiplier = 0.04f;
            lr.numCapVertices = 0;
            lr.numCornerVertices = 0;
            lr.alignment = LineAlignment.View;
            lr.textureMode = LineTextureMode.Stretch;
            lr.sortingOrder = 32760; // Render on top of gameplay sprites.
            if (_lineMaterial != null) lr.sharedMaterial = _lineMaterial;
            return lr;
        }

        private void HideUnusedLines()
        {
            for (int i = _lineCursor; i < _linePool.Count; i++)
            {
                if (_linePool[i] != null) _linePool[i].enabled = false;
            }
        }

        private void HideAllLines()
        {
            for (int i = 0; i < _linePool.Count; i++)
            {
                if (_linePool[i] != null) _linePool[i].enabled = false;
            }
        }

        // ---------------------------------------------------------------- reflection setup

        private void EnsureReflection()
        {
            if (_reflectionResolved) return;

            _registryType = FindType("LoneFighter.Enemies.EnemyRegistry") ?? FindType("EnemyRegistry");
            if (_registryType != null)
            {
                _enemiesProp = _registryType.GetProperty("Enemies",
                    BindingFlags.Public | BindingFlags.Static);
            }

            _playerControllerType = FindType("LoneFighter.Player.PlayerController") ?? FindType("PlayerController");
            if (_playerControllerType != null)
            {
                _playerInstanceProp = _playerControllerType.GetProperty("Instance",
                    BindingFlags.Public | BindingFlags.Static);
            }

            _dasherType = FindType("LoneFighter.Enemies.DasherAI") ?? FindType("DasherAI");
            if (_dasherType != null)
            {
                _dashDirField = _dasherType.GetField("_dashDirection",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }

            _shockwaveType = FindType("LoneFighter.Enemies.EnemyShockwave") ?? FindType("EnemyShockwave");
            if (_shockwaveType != null)
            {
                _elapsedField        = _shockwaveType.GetField("_elapsed",        BindingFlags.NonPublic | BindingFlags.Instance);
                _expandDurationField = _shockwaveType.GetField("expandDuration",  BindingFlags.NonPublic | BindingFlags.Instance);
                _maxRadiusField      = _shockwaveType.GetField("maxRadius",       BindingFlags.NonPublic | BindingFlags.Instance);
            }

            _reflectionResolved = true;
        }

        private static Type FindType(string fullName)
        {
            var asms = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < asms.Length; i++)
            {
                var t = asms[i].GetType(fullName, throwOnError: false);
                if (t != null) return t;
            }
            return null;
        }
    }
}
