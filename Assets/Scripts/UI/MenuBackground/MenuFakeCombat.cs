// LoneFighter - Silent fake-combat sim for the main menu background.
//
// Pure visual: spawns lightweight enemy sprites that wander toward a fake
// "player" at center; the player occasionally fires a projectile that
// destroys an enemy with a tiny particle burst. No gameplay code involved -
// just Transform tweening, basic spawn/cleanup, and ParticleSystem bursts.
//
// Disabled automatically by MenuBackgroundController when the
// "LF_MenuFxIntensity" PlayerPref is set to "low".

using System.Collections.Generic;
using UnityEngine;

namespace LoneFighter.UI.MenuBackground
{
    /// <summary>
    /// Runs a silent, purely-cosmetic combat simulation behind the main menu.
    /// All entities are plain GameObjects animated by this script - no physics,
    /// no audio, no gameplay logic.
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuFakeCombat : MonoBehaviour
    {
        [Header("Fake Player")]
        [Tooltip("Transform that acts as the visual 'player'. If null, a placeholder is created at world origin under this object.")]
        [SerializeField] private Transform fakePlayer;
        [SerializeField] private Sprite fakePlayerSprite;
        [SerializeField] private Color fakePlayerColor = new Color(0.9f, 0.9f, 1f, 1f);

        [Header("Fake Enemies")]
        [SerializeField] private Sprite enemySprite;
        [SerializeField] private Color enemyColor = new Color(1f, 0.5f, 0.5f, 0.85f);
        [SerializeField] private int maxEnemies = 8;
        [SerializeField] private float spawnIntervalMin = 0.8f;
        [SerializeField] private float spawnIntervalMax = 1.6f;
        [SerializeField] private float enemySpeedMin = 0.5f;
        [SerializeField] private float enemySpeedMax = 1.2f;
        [SerializeField] private float enemyWanderAmplitude = 0.6f;
        [SerializeField] private float spawnRadius = 6f;

        [Header("Fake Projectiles")]
        [SerializeField] private Sprite projectileSprite;
        [SerializeField] private Color projectileColor = new Color(1f, 0.95f, 0.6f, 1f);
        [SerializeField] private float fireIntervalMin = 0.9f;
        [SerializeField] private float fireIntervalMax = 1.8f;
        [SerializeField] private float projectileSpeed = 6f;

        [Header("Hit FX")]
        [Tooltip("ParticleSystem prefab used for the small burst when an enemy is destroyed. If null, a runtime placeholder is generated.")]
        [SerializeField] private ParticleSystem hitBurstPrefab;

        [Header("Rendering")]
        [Tooltip("Sorting layer name applied to spawned sprites.")]
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder = -50;

        private readonly List<EnemyView> enemies = new List<EnemyView>(16);
        private float nextSpawnAt;
        private float nextFireAt;
        private ParticleSystem runtimeBurstPrefab;
        private Transform poolRoot;

        private class EnemyView
        {
            public Transform tf;
            public SpriteRenderer sr;
            public float speed;
            public float wanderPhase;
            public float wanderFreq;
        }

        private void OnEnable()
        {
            EnsurePoolRoot();
            EnsureFakePlayer();
            EnsureHitBurst();
            nextSpawnAt = Time.time + Random.Range(0.1f, 0.4f);
            nextFireAt = Time.time + Random.Range(fireIntervalMin, fireIntervalMax);
        }

        private void OnDisable()
        {
            ClearAllEnemies();
        }

        private void Update()
        {
            if (fakePlayer == null)
            {
                return;
            }

            float now = Time.time;

            if (now >= nextSpawnAt && enemies.Count < maxEnemies)
            {
                SpawnEnemy();
                nextSpawnAt = now + Random.Range(spawnIntervalMin, spawnIntervalMax);
            }

            TickEnemies(Time.deltaTime);

            if (now >= nextFireAt && enemies.Count > 0)
            {
                FireProjectile();
                nextFireAt = now + Random.Range(fireIntervalMin, fireIntervalMax);
            }
        }

        // ---------------------------------------------------------------
        // Setup helpers
        // ---------------------------------------------------------------

        private void EnsurePoolRoot()
        {
            if (poolRoot != null)
            {
                return;
            }
            var go = new GameObject("FakeCombatPool");
            go.transform.SetParent(transform, false);
            poolRoot = go.transform;
        }

        private void EnsureFakePlayer()
        {
            if (fakePlayer != null)
            {
                return;
            }
            var go = new GameObject("FakePlayer");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = fakePlayerSprite;
            sr.color = fakePlayerColor;
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = sortingOrder + 1;
            fakePlayer = go.transform;
        }

        private void EnsureHitBurst()
        {
            if (hitBurstPrefab != null)
            {
                runtimeBurstPrefab = hitBurstPrefab;
                return;
            }
            // Build a tiny default burst so the script works without authored assets.
            var go = new GameObject("FakeHitBurst");
            go.transform.SetParent(transform, false);
            go.SetActive(false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.4f;
            main.loop = false;
            main.startLifetime = 0.35f;
            main.startSpeed = 2.5f;
            main.startSize = 0.08f;
            main.startColor = new Color(1f, 0.8f, 0.4f, 1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;
            runtimeBurstPrefab = ps;
        }

        // ---------------------------------------------------------------
        // Enemy lifecycle
        // ---------------------------------------------------------------

        private void SpawnEnemy()
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 spawn = fakePlayer.position + new Vector3(
                Mathf.Cos(angle) * spawnRadius,
                Mathf.Sin(angle) * spawnRadius,
                0f);

            var go = new GameObject("FakeEnemy");
            go.transform.SetParent(poolRoot, true);
            go.transform.position = spawn;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = enemySprite;
            sr.color = enemyColor;
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = sortingOrder;

            enemies.Add(new EnemyView
            {
                tf = go.transform,
                sr = sr,
                speed = Random.Range(enemySpeedMin, enemySpeedMax),
                wanderPhase = Random.Range(0f, Mathf.PI * 2f),
                wanderFreq = Random.Range(1.2f, 2.4f),
            });
        }

        private void TickEnemies(float dt)
        {
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var e = enemies[i];
                if (e.tf == null)
                {
                    enemies.RemoveAt(i);
                    continue;
                }

                Vector3 to = fakePlayer.position - e.tf.position;
                float dist = to.magnitude;
                if (dist <= 0.25f)
                {
                    // Too close - despawn quietly (no gameplay hit).
                    Destroy(e.tf.gameObject);
                    enemies.RemoveAt(i);
                    continue;
                }

                Vector3 dir = to / dist;
                // Apply small sideways wander so enemies don't track in a straight line.
                e.wanderPhase += dt * e.wanderFreq;
                Vector3 perp = new Vector3(-dir.y, dir.x, 0f);
                Vector3 wander = perp * Mathf.Sin(e.wanderPhase) * enemyWanderAmplitude;

                e.tf.position += (dir * e.speed + wander * 0.5f) * dt;
            }
        }

        private void ClearAllEnemies()
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i].tf != null)
                {
                    Destroy(enemies[i].tf.gameObject);
                }
            }
            enemies.Clear();
        }

        // ---------------------------------------------------------------
        // Projectile + hit
        // ---------------------------------------------------------------

        private void FireProjectile()
        {
            // Pick a random live enemy as the target.
            int idx = Random.Range(0, enemies.Count);
            var target = enemies[idx];
            if (target == null || target.tf == null)
            {
                return;
            }

            var go = new GameObject("FakeProjectile");
            go.transform.SetParent(poolRoot, true);
            go.transform.position = fakePlayer.position;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = projectileSprite;
            sr.color = projectileColor;
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = sortingOrder + 2;

            var mover = go.AddComponent<FakeProjectileMover>();
            mover.Init(target, projectileSpeed, OnProjectileHit);
        }

        private void OnProjectileHit(EnemyView enemy, Vector3 hitPos)
        {
            if (enemy != null && enemy.tf != null)
            {
                SpawnBurst(hitPos);
                Destroy(enemy.tf.gameObject);
                enemies.Remove(enemy);
            }
        }

        private void SpawnBurst(Vector3 worldPos)
        {
            if (runtimeBurstPrefab == null)
            {
                return;
            }
            var inst = Instantiate(runtimeBurstPrefab, worldPos, Quaternion.identity, poolRoot);
            inst.gameObject.SetActive(true);
            inst.Play(true);
            float life = inst.main.duration + inst.main.startLifetime.constantMax;
            Destroy(inst.gameObject, life + 0.1f);
        }

        // ---------------------------------------------------------------
        // Internal projectile mover - keeps MenuFakeCombat self-contained.
        // ---------------------------------------------------------------

        private class FakeProjectileMover : MonoBehaviour
        {
            private EnemyView target;
            private float speed;
            private System.Action<EnemyView, Vector3> onHit;
            private float lifeRemaining = 2.5f;

            public void Init(EnemyView target, float speed, System.Action<EnemyView, Vector3> onHit)
            {
                this.target = target;
                this.speed = speed;
                this.onHit = onHit;
            }

            private void Update()
            {
                lifeRemaining -= Time.deltaTime;
                if (lifeRemaining <= 0f || target == null || target.tf == null)
                {
                    Destroy(gameObject);
                    return;
                }

                Vector3 to = target.tf.position - transform.position;
                float dist = to.magnitude;
                float step = speed * Time.deltaTime;
                if (dist <= step)
                {
                    Vector3 hit = target.tf.position;
                    onHit?.Invoke(target, hit);
                    Destroy(gameObject);
                    return;
                }

                transform.position += (to / dist) * step;
            }
        }
    }
}
