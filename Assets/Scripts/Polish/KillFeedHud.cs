using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LoneFighter.Polish
{
    /// Pop-in stack of recent kill labels. Subscribes to ComboCounter.OnKillRecorded.
    /// Labels fade after ~1.2s. Limited to maxVisible at once. Uses object reuse for cheap re-firing.
    public class KillFeedHud : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private ComboCounter counter;
        [Tooltip("Parent transform that holds the spawned labels. Add a VerticalLayoutGroup for stacking.")]
        [SerializeField] private RectTransform container;
        [Tooltip("Prefab with a TMP_Text on root. Will be instantiated/reused under container.")]
        [SerializeField] private TMP_Text labelPrefab;

        [Header("Behavior")]
        [SerializeField, Min(1)] private int maxVisible = 5;
        [SerializeField, Min(0.2f)] private float visibleSeconds = 1.2f;
        [SerializeField, Min(0.05f)] private float fadeSeconds = 0.4f;
        [SerializeField] private string fallbackName = "Kill";

        private class Entry
        {
            public TMP_Text Text;
            public float SpawnUnscaledTime;
        }

        private readonly List<Entry> _active = new();
        private readonly Stack<TMP_Text> _pool = new();

        private void OnEnable()
        {
            if (counter == null) counter = ComboCounter.Instance;
            if (counter != null) counter.OnKillRecorded += HandleKill;
        }

        private void OnDisable()
        {
            if (counter != null) counter.OnKillRecorded -= HandleKill;
        }

        private void Start()
        {
            if (counter == null)
            {
                counter = ComboCounter.Instance;
                if (counter != null) counter.OnKillRecorded += HandleKill;
            }
        }

        private void HandleKill(string enemyName)
        {
            if (labelPrefab == null || container == null) return;

            // Evict oldest if at cap.
            while (_active.Count >= maxVisible)
            {
                Retire(_active[0]);
                _active.RemoveAt(0);
            }

            var text = _pool.Count > 0 ? _pool.Pop() : Instantiate(labelPrefab, container);
            text.gameObject.SetActive(true);
            text.transform.SetAsLastSibling();
            text.text = string.IsNullOrEmpty(enemyName) ? fallbackName : enemyName;
            var c = text.color;
            c.a = 1f;
            text.color = c;

            _active.Add(new Entry { Text = text, SpawnUnscaledTime = Time.unscaledTime });
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var entry = _active[i];
                if (entry.Text == null)
                {
                    _active.RemoveAt(i);
                    continue;
                }
                float age = now - entry.SpawnUnscaledTime;
                if (age >= visibleSeconds + fadeSeconds)
                {
                    Retire(entry);
                    _active.RemoveAt(i);
                    continue;
                }
                if (age > visibleSeconds)
                {
                    float k = Mathf.Clamp01((age - visibleSeconds) / fadeSeconds);
                    var c = entry.Text.color;
                    c.a = 1f - k;
                    entry.Text.color = c;
                }
            }
        }

        private void Retire(Entry entry)
        {
            if (entry == null || entry.Text == null) return;
            entry.Text.gameObject.SetActive(false);
            _pool.Push(entry.Text);
        }
    }
}
