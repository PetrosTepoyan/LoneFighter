using TMPro;
using UnityEngine;
using LoneFighter.Systems;

namespace LoneFighter.UI
{
    [RequireComponent(typeof(TMP_Text))]
    public class DamagePopup : MonoBehaviour
    {
        [SerializeField] private float lifetime = 0.6f;
        [SerializeField] private float riseSpeed = 1.4f;
        [SerializeField] private Vector2 horizontalDrift = new Vector2(-0.4f, 0.4f);
        [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 0.15f, 1.15f);
        [SerializeField] private AnimationCurve alphaCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

        private TMP_Text _text;
        private float _t;
        private Vector3 _velocity;
        private Color _baseColor;

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
            _baseColor = _text.color;
        }

        private void OnEnable()
        {
            _t = 0f;
            float drift = Random.Range(horizontalDrift.x, horizontalDrift.y);
            _velocity = new Vector3(drift, riseSpeed, 0f);
        }

        public void Show(int amount)
        {
            if (_text == null) _text = GetComponent<TMP_Text>();
            _text.text = amount.ToString();
            transform.localScale = Vector3.zero;
        }

        private void Update()
        {
            _t += Time.unscaledDeltaTime;
            transform.position += _velocity * Time.unscaledDeltaTime;

            float scaleSample = scaleCurve.Evaluate(_t);
            transform.localScale = Vector3.one * (0.3f + scaleSample * 0.7f);

            var c = _baseColor;
            c.a = alphaCurve.Evaluate(_t / Mathf.Max(0.01f, lifetime));
            _text.color = c;

            if (_t >= lifetime)
            {
                if (PoolService.Instance != null) PoolService.Instance.Release(gameObject);
                else Destroy(gameObject);
            }
        }
    }
}
