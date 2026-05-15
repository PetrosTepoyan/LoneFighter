using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LoneFighter.Abilities
{
    /// <summary>
    /// Bottom-corner UI that renders one small filled circle per dash charge.
    /// Spent charges show as outlined / dim; the next-to-recharge pulses while
    /// filling. Auto-rebuilds its icon row whenever <see cref="DashCharges.Max"/>
    /// changes (so charge-upgrade pickups update the HUD live).
    ///
    /// This script can sit on a RectTransform inside any existing Canvas. If no
    /// sprite is assigned it falls back to Unity's built-in UISprite knot, which
    /// is round-ish at small sizes - fine for a prototype.
    /// </summary>
    [DisallowMultipleComponent]
    public class DashChargeUi : MonoBehaviour
    {
        [Header("Source (auto-found if left null)")]
        [SerializeField] private DashCharges chargesSource;

        [Header("Icon")]
        [Tooltip("Sprite used for each charge icon. If null, Unity's built-in 'Knob' UISprite is used.")]
        [SerializeField] private Sprite iconSprite;
        [SerializeField] private float iconSize = 22f;
        [SerializeField] private float iconSpacing = 6f;

        [Header("Colors")]
        [SerializeField] private Color filledColor = new Color(0.4f, 0.9f, 1f, 1f);
        [SerializeField] private Color emptyColor = new Color(0.4f, 0.9f, 1f, 0.18f);
        [SerializeField] private Color pulseColor = new Color(0.85f, 1f, 1f, 1f);

        [Header("Pulse")]
        [Tooltip("Hertz of the pulse animation while a charge is regenerating.")]
        [SerializeField] private float pulseHz = 2.2f;
        [SerializeField] private float pulseScale = 0.18f;

        private readonly List<Image> _icons = new List<Image>();

        private void Awake()
        {
            if (chargesSource == null) chargesSource = FindCharges();
        }

        private void OnEnable()
        {
            TryHookCharges();
        }

        private void OnDisable()
        {
            if (chargesSource != null) chargesSource.OnChanged -= HandleChargesChanged;
        }

        private void Update()
        {
            if (chargesSource == null)
            {
                chargesSource = FindCharges();
                if (chargesSource != null) TryHookCharges();
                return;
            }
            RefreshPulse();
        }

        private void TryHookCharges()
        {
            if (chargesSource == null) return;
            chargesSource.OnChanged -= HandleChargesChanged;
            chargesSource.OnChanged += HandleChargesChanged;
            RebuildIcons(chargesSource.Max);
            HandleChargesChanged(chargesSource);
        }

        private DashCharges FindCharges()
        {
            // Find the first DashCharges in the scene. Works fine on the Player.
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<DashCharges>();
#else
            return Object.FindObjectOfType<DashCharges>();
#endif
        }

        private void RebuildIcons(int count)
        {
            // grow
            while (_icons.Count < count) _icons.Add(CreateIcon(_icons.Count));
            // shrink
            for (int i = _icons.Count - 1; i >= count; i--)
            {
                if (_icons[i] != null) Destroy(_icons[i].gameObject);
                _icons.RemoveAt(i);
            }
            LayoutIcons();
        }

        private Image CreateIcon(int index)
        {
            var go = new GameObject("DashCharge_" + index, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, worldPositionStays: false);
            var img = go.GetComponent<Image>();
            img.sprite = iconSprite != null ? iconSprite : Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
            img.color = emptyColor;
            img.raycastTarget = false;
            return img;
        }

        private void LayoutIcons()
        {
            for (int i = 0; i < _icons.Count; i++)
            {
                var rt = _icons[i].rectTransform;
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(iconSize, iconSize);
                rt.anchoredPosition = new Vector2(i * (iconSize + iconSpacing), 0f);
                rt.localScale = Vector3.one;
            }
        }

        private void HandleChargesChanged(DashCharges c)
        {
            if (_icons.Count != c.Max) RebuildIcons(c.Max);

            for (int i = 0; i < _icons.Count; i++)
            {
                bool filled = i < c.Current;
                _icons[i].color = filled ? filledColor : emptyColor;
                _icons[i].rectTransform.localScale = Vector3.one;
            }
        }

        private void RefreshPulse()
        {
            if (chargesSource == null) return;
            if (chargesSource.IsFull) return;

            int nextIndex = chargesSource.Current; // the slot currently regenerating
            if (nextIndex < 0 || nextIndex >= _icons.Count) return;

            float t = chargesSource.RechargeProgress01;
            // pulse amplitude grows as the charge approaches full
            float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseHz * Mathf.PI * 2f);
            float amp = pulseScale * (0.35f + 0.65f * t);

            var img = _icons[nextIndex];
            Color mixed = Color.Lerp(emptyColor, pulseColor, Mathf.Lerp(0.25f, 0.95f, t) * wave);
            img.color = mixed;
            img.rectTransform.localScale = Vector3.one * (1f + amp * wave);
        }
    }
}
