using UnityEngine;

namespace LoneFighter.Debugging.Cheats
{
    /// <summary>
    /// IMGUI cheat panel. Toggled on by:
    ///  * Shift+F1 on desktop
    ///  * Shake (|Input.acceleration| > 2.0) on mobile
    /// The window is only drawn when <see cref="BuildGuard.CheatsAllowed"/> is true,
    /// so it cannot appear in a shipped Release build.
    /// </summary>
    [DefaultExecutionOrder(10001)]
    public class CheatUi : MonoBehaviour
    {
        private const float ShakeMagnitudeThreshold = 2.0f;
        private const float ShakeCooldown = 1.0f;

        private bool _visible;
        private float _lastShakeTime = -10f;
        private Rect _windowRect = new Rect(16f, 16f, 280f, 360f);
        private GUIStyle _toggleStyle;
        private Vector2 _scroll;

        private void Update()
        {
            if (!BuildGuard.CheatsAllowed) return;

            // Desktop toggle: Shift+F1.
            if ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                && Input.GetKeyDown(KeyCode.F1))
            {
                _visible = !_visible;
            }

            // Mobile shake toggle, with cooldown so a single shake doesn't bounce.
            if (Time.unscaledTime - _lastShakeTime > ShakeCooldown
                && Input.acceleration.sqrMagnitude > ShakeMagnitudeThreshold * ShakeMagnitudeThreshold)
            {
                _visible = !_visible;
                _lastShakeTime = Time.unscaledTime;
            }
        }

        private void OnGUI()
        {
            if (!BuildGuard.CheatsAllowed) return;
            if (!_visible) return;
            if (_toggleStyle == null)
            {
                _toggleStyle = new GUIStyle(GUI.skin.toggle) { fontSize = 14 };
            }
            _windowRect = GUILayout.Window(0xC4EA7, _windowRect, DrawWindow, "Cheats (debug only)");
        }

        private void DrawWindow(int id)
        {
            var svc = CheatService.Instance;
            _scroll = GUILayout.BeginScrollView(_scroll);

            svc.GodMode          = GUILayout.Toggle(svc.GodMode,          " God Mode",          _toggleStyle);
            svc.OneHitKills      = GUILayout.Toggle(svc.OneHitKills,      " One Hit Kills",     _toggleStyle);
            svc.InfiniteXp       = GUILayout.Toggle(svc.InfiniteXp,       " Infinite XP",       _toggleStyle);
            svc.NoSpawn          = GUILayout.Toggle(svc.NoSpawn,          " No Spawn",          _toggleStyle);
            svc.AutoFire         = GUILayout.Toggle(svc.AutoFire,         " Auto Fire (no-op)", _toggleStyle);
            svc.FreezeEnemies    = GUILayout.Toggle(svc.FreezeEnemies,    " Freeze Enemies",    _toggleStyle);
            svc.InstantCooldowns = GUILayout.Toggle(svc.InstantCooldowns, " Instant Cooldowns", _toggleStyle);
            svc.BigDamage        = GUILayout.Toggle(svc.BigDamage,        " Big Damage (1000x)", _toggleStyle);

            GUILayout.Space(8f);
            if (GUILayout.Button("Disable All")) svc.DisableAll();
            if (GUILayout.Button("Close")) _visible = false;

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }
    }
}
