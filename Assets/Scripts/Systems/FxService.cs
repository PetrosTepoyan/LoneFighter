using UnityEngine;
using Unity.Cinemachine;

namespace LoneFighter.Systems
{
    /// Centralized juice: pooled particle bursts, hit-flash, camera shake, hit-stop.
    /// Tuned for many simultaneous effects without burning frame budget on mobile.
    public class FxService : MonoBehaviour
    {
        public static FxService Instance { get; private set; }

        [Header("Particle Prefabs (each should be a ParticleSystem with PlayOnAwake = true, StopAction = Disable)")]
        [SerializeField] private GameObject enemyExplosionPrefab;
        [SerializeField] private GameObject projectileImpactPrefab;
        [SerializeField] private GameObject playerHitPrefab;
        [SerializeField] private GameObject xpPickupPrefab;
        [SerializeField] private GameObject levelUpBurstPrefab;

        [Header("Floating Numbers")]
        [SerializeField] private GameObject damagePopupPrefab;

        [Header("Camera Shake (Cinemachine 3.x ImpulseSource on this object)")]
        [SerializeField] private CinemachineImpulseSource impulseSource;
        [SerializeField] private float lightShakeAmplitude = 0.15f;
        [SerializeField] private float heavyShakeAmplitude = 0.45f;

        [Header("Hit-Stop")]
        [Range(0f, 0.2f)] [SerializeField] private float bigHitFreezeSeconds = 0.05f;

        private float _hitStopRemaining;
        private float _hitStopOriginalTimeScale = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (_hitStopRemaining > 0f)
            {
                _hitStopRemaining -= Time.unscaledDeltaTime;
                if (_hitStopRemaining <= 0f)
                {
                    Time.timeScale = _hitStopOriginalTimeScale;
                }
            }
        }

        public void EnemyExplosion(Vector3 position) => Spawn(enemyExplosionPrefab, position);
        public void ProjectileImpact(Vector3 position) => Spawn(projectileImpactPrefab, position);
        public void PlayerHit(Vector3 position) => Spawn(playerHitPrefab, position);
        public void XpPickup(Vector3 position) => Spawn(xpPickupPrefab, position);
        public void LevelUpBurst(Vector3 position) => Spawn(levelUpBurstPrefab, position);

        public void DamagePopup(Vector3 position, float amount)
        {
            if (damagePopupPrefab == null) return;
            var go = PoolService.Instance != null
                ? PoolService.Instance.Get(damagePopupPrefab, position + new Vector3(Random.Range(-0.15f, 0.15f), 0.2f, 0f), Quaternion.identity)
                : Instantiate(damagePopupPrefab, position, Quaternion.identity);
            var popup = go.GetComponent<UI.DamagePopup>();
            if (popup != null) popup.Show(Mathf.RoundToInt(amount));
        }

        public void LightShake()
        {
            if (impulseSource != null) impulseSource.GenerateImpulse(lightShakeAmplitude);
        }

        public void HeavyShake()
        {
            if (impulseSource != null) impulseSource.GenerateImpulse(heavyShakeAmplitude);
        }

        public void HitStop(float seconds = -1f)
        {
            float duration = seconds < 0f ? bigHitFreezeSeconds : seconds;
            if (duration <= 0f) return;
            if (_hitStopRemaining <= 0f) _hitStopOriginalTimeScale = Time.timeScale;
            _hitStopRemaining = duration;
            Time.timeScale = 0f;
        }

        private void Spawn(GameObject prefab, Vector3 position)
        {
            if (prefab == null) return;
            if (PoolService.Instance != null)
            {
                var instance = PoolService.Instance.Get(prefab, position, Quaternion.identity);
                var ps = instance.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    ps.Clear(true);
                    ps.Play(true);
                }
                var autoRelease = instance.GetComponent<ParticleAutoRelease>();
                if (autoRelease == null) instance.AddComponent<ParticleAutoRelease>();
            }
            else
            {
                Instantiate(prefab, position, Quaternion.identity);
            }
        }
    }
}
