using System.Collections.Generic;
using UnityEngine;
using LoneFighter.Enemies;

namespace LoneFighter.Effects.HitFeedback
{
    /// <summary>
    /// Exaggerated damage popups. The bigger the hit, the more violent the
    /// shake — and very-high-damage hits get a screaming "BIG HIT!" banner.
    ///
    /// We can't extend the project's existing <c>UI.DamagePopup</c> behavior
    /// (no modifying existing files), so this component owns its own popup
    /// pipeline:
    ///   - Each popup is a child <see cref="GameObject"/> with a
    ///     <c>TextMesh</c> (built-in 3D text, no extra package required).
    ///   - We pool popups internally to avoid per-hit allocations.
    ///   - Shake amplitude scales linearly with damage; over
    ///     <see cref="bigHitThreshold"/>, we additionally spawn a second
    ///     "BIG HIT!" popup nearby with stronger shake.
    /// </summary>
    public class HitDamageNumbers : MonoBehaviour
    {
        [Header("Sizing & Color")]
        [Tooltip("Base font size for normal hits.")]
        [SerializeField, Min(1f)] private float baseFontSize = 4f;
        [Tooltip("Font size at the high-damage cap.")]
        [SerializeField, Min(1f)] private float maxFontSize = 9f;
        [Tooltip("Damage value at or above which we use max font / BIG HIT.")]
        [SerializeField, Min(1f)] private float bigHitThreshold = 60f;
        [Tooltip("Color used for normal enemy-damage popups.")]
        [SerializeField] private Color enemyHitColor = new Color(1f, 0.95f, 0.4f);
        [Tooltip("Color used for player-damage popups.")]
        [SerializeField] private Color playerHitColor = new Color(1f, 0.25f, 0.25f);
        [Tooltip("Color used for the BIG HIT! tag.")]
        [SerializeField] private Color bigHitColor = new Color(1f, 0.4f, 0.05f);

        [Header("Animation")]
        [Tooltip("Lifetime of a popup in unscaled seconds.")]
        [SerializeField, Min(0.05f)] private float lifetime = 0.7f;
        [Tooltip("World units the popup rises over its life.")]
        [SerializeField] private float riseDistance = 1.2f;
        [Tooltip("Max shake amplitude in world units at the big-hit threshold.")]
        [SerializeField] private float maxShakeAmplitude = 0.25f;
        [Tooltip("Shake frequency in Hz.")]
        [SerializeField, Min(1f)] private float shakeHz = 30f;

        [Header("Big Hit Banner")]
        [Tooltip("Text shown for damage >= bigHitThreshold.")]
        [SerializeField] private string bigHitText = "BIG HIT!";
        [Tooltip("Vertical offset of the BIG HIT tag relative to the damage value.")]
        [SerializeField] private float bigHitYOffset = 0.6f;
        [Tooltip("Extra shake amplitude multiplier applied to the BIG HIT tag.")]
        [SerializeField, Min(1f)] private float bigHitShakeMultiplier = 1.8f;

        private readonly Queue<Popup> _pool = new();
        private readonly List<Popup> _live = new();

        /// <summary>
        /// Show a damage popup. If <paramref name="amount"/> is at or above
        /// <see cref="bigHitThreshold"/>, an additional "BIG HIT!" banner spawns.
        /// </summary>
        public void Show(Vector3 worldPos, float amount, Vector2 hitDirection, bool isPlayer = false)
        {
            if (amount <= 0f) return;
            float shake = Mathf.Lerp(
                0f,
                maxShakeAmplitude,
                Mathf.Clamp01(amount / Mathf.Max(0.0001f, bigHitThreshold))
            );
            float fontSize = Mathf.Lerp(
                baseFontSize,
                maxFontSize,
                Mathf.Clamp01(amount / Mathf.Max(0.0001f, bigHitThreshold))
            );

            // Bias the popup slightly along the hit direction so multi-hit
            // bursts don't pile up on the same pixel.
            Vector3 driftBias = (Vector3)(hitDirection.normalized * 0.15f);
            // And random scatter so adjacent hits don't overlap exactly.
            Vector3 scatter = new Vector3(
                Random.Range(-0.15f, 0.15f),
                Random.Range(0f, 0.15f),
                0f);

            var color = isPlayer ? playerHitColor : enemyHitColor;
            Spawn(worldPos + driftBias + scatter, Mathf.RoundToInt(amount).ToString(),
                color, fontSize, shake);

            if (amount >= bigHitThreshold)
            {
                Spawn(worldPos + new Vector3(0f, bigHitYOffset, 0f) + driftBias,
                    bigHitText, bigHitColor, maxFontSize * 1.1f,
                    shake * bigHitShakeMultiplier);
            }
        }

        /// <summary>
        /// Convenience overload used directly by combat code that has an enemy
        /// reference handy.
        /// </summary>
        public void ShowEnemy(EnemyBase enemy, float amount, Vector2 hitDirection)
        {
            if (enemy == null) return;
            Show(enemy.transform.position, amount, hitDirection, isPlayer: false);
        }

        private void Spawn(Vector3 worldPos, string text, Color color, float fontSize, float shakeAmplitude)
        {
            Popup p = _pool.Count > 0 ? _pool.Dequeue() : Build();
            p.go.SetActive(true);
            p.origin = worldPos;
            p.elapsed = 0f;
            p.shakeAmplitude = shakeAmplitude;
            p.shakeSeed = Random.value * 100f;
            p.transform.position = worldPos;
            p.transform.localScale = Vector3.one;

            p.mesh.text = text;
            p.mesh.color = color;
            p.mesh.fontSize = Mathf.RoundToInt(fontSize * 10f); // TextMesh uses an arbitrary "fontSize" unit
            p.transform.localScale = Vector3.one * (fontSize * 0.1f);

            _live.Add(p);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var p = _live[i];
                p.elapsed += dt;
                float t = Mathf.Clamp01(p.elapsed / lifetime);

                // Rise vertically.
                Vector3 pos = p.origin + new Vector3(0f, riseDistance * t, 0f);
                // Add shake that decays with t (snappy at start, calms down).
                float shake = p.shakeAmplitude * (1f - t);
                pos.x += Mathf.Sin((p.elapsed + p.shakeSeed) * shakeHz) * shake;
                pos.y += Mathf.Cos((p.elapsed + p.shakeSeed) * shakeHz * 1.13f) * shake * 0.5f;
                p.transform.position = pos;

                // Fade alpha out over the back half of life.
                var c = p.mesh.color;
                c.a = t < 0.5f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.5f) / 0.5f);
                p.mesh.color = c;

                if (t >= 1f)
                {
                    p.go.SetActive(false);
                    _live.RemoveAt(i);
                    _pool.Enqueue(p);
                }
            }
        }

        private Popup Build()
        {
            // TextMesh is a built-in 3D text renderer. It needs a MeshRenderer
            // (auto-added by AddComponent<TextMesh>()). It renders against the
            // built-in font automatically, so no asset configuration required.
            var go = new GameObject("HitDamagePopup");
            go.transform.SetParent(transform, false);
            var tm = go.AddComponent<TextMesh>();
            tm.alignment = TextAlignment.Center;
            tm.anchor = TextAnchor.LowerCenter;
            tm.characterSize = 0.1f;
            tm.color = enemyHitColor;
            // Force the text to face the camera (sprites are XY-plane in 2D).
            tm.transform.rotation = Quaternion.identity;

            var mr = go.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sortingOrder = 1000;
            }

            return new Popup
            {
                go = go,
                transform = go.transform,
                mesh = tm,
                elapsed = 0f,
            };
        }

        private class Popup
        {
            public GameObject go;
            public Transform transform;
            public TextMesh mesh;
            public Vector3 origin;
            public float elapsed;
            public float shakeAmplitude;
            public float shakeSeed;
        }
    }
}
