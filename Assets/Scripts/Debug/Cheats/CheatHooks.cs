using UnityEngine;
using LoneFighter.Enemies;
using LoneFighter.Player;

namespace LoneFighter.Debugging.Cheats
{
    /// <summary>
    /// Runtime enforcement for every cheat flag. Place one of these in the scene
    /// (or let <see cref="CheatBootstrap"/> spawn one automatically). It polls the
    /// flags every frame and applies the effect directly — no upstream code needs
    /// to know cheats exist.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class CheatHooks : MonoBehaviour
    {
        private static CheatHooks _instance;
        public static CheatHooks Instance => _instance;

        // Cache lookups so per-frame work stays cheap.
        private PlayerHealth _cachedPlayerHealth;
        private PlayerController _cachedPlayerForHealth;
        private PlayerLevel _cachedPlayerLevel;
        private PlayerController _cachedPlayerForLevel;
        private EnemySpawner _cachedSpawner;
        private bool _spawnerDisabledByUs;

        // Snapshot of EnemyRegistry size to detect "something changed" for OneHitKills.
        // This is a best-effort scan: we one-shot every live enemy on the frame their
        // count or membership changes. We cannot subscribe to per-enemy hit events
        // because EnemyHealth does not expose an OnDamaged hook; the limit is
        // documented in the README in this folder.
        private int _lastEnemyCount = -1;

        // Cached subscribed PlayerLevel for the InfiniteXp kill hook.
        private PlayerLevel _xpHookedLevel;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            UnhookXp();
            RestoreSpawnerIfNeeded();
        }

        // ----------------------------------------------------------------- Update

        private void Update()
        {
            // If the build guard refused cheats, do nothing.
            if (!BuildGuard.CheatsAllowed) return;

            var svc = CheatService.Instance;

            EnforceGodMode(svc);
            EnforceOneHitKills(svc);
            EnforceInfiniteXp(svc);
            EnforceNoSpawn(svc);
        }

        private void FixedUpdate()
        {
            if (!BuildGuard.CheatsAllowed) return;
            if (!CheatService.Instance.FreezeEnemies) return;
            FreezeAllEnemies();
        }

        // ----------------------------------------------------------------- GodMode

        private void EnforceGodMode(CheatService svc)
        {
            if (!svc.GodMode) return;
            var health = ResolvePlayerHealth();
            if (health == null) return;
            float missing = health.Max - health.Current;
            if (missing > 0f) health.Heal(missing);
        }

        private PlayerHealth ResolvePlayerHealth()
        {
            var player = PlayerController.Instance;
            if (player == null) { _cachedPlayerHealth = null; _cachedPlayerForHealth = null; return null; }
            if (_cachedPlayerForHealth != player || _cachedPlayerHealth == null)
            {
                _cachedPlayerForHealth = player;
                _cachedPlayerHealth = player.GetComponent<PlayerHealth>();
            }
            return _cachedPlayerHealth;
        }

        // ----------------------------------------------------------------- OneHitKills

        private void EnforceOneHitKills(CheatService svc)
        {
            var enemies = EnemyRegistry.Enemies;
            int count = enemies.Count;
            if (!svc.OneHitKills) { _lastEnemyCount = count; return; }

            // Best-effort: any frame where the registry membership changed (a new
            // enemy spawned, or an existing one moved into the list) we punch every
            // live enemy with overkill damage. EnemyHealth ignores damage on dead
            // enemies, so this is safe to spam.
            if (count != _lastEnemyCount)
            {
                for (int i = enemies.Count - 1; i >= 0; i--)
                {
                    var e = enemies[i];
                    if (e == null || !e.isActiveAndEnabled) continue;
                    e.ApplyDamage(CheatService.OneHitDamage);
                }
                // Re-read after kills; surviving entries (e.g. spawned mid-frame) are
                // captured next frame.
                _lastEnemyCount = EnemyRegistry.Enemies.Count;
            }
        }

        // ----------------------------------------------------------------- InfiniteXp

        private void EnforceInfiniteXp(CheatService svc)
        {
            var level = ResolvePlayerLevel();
            if (svc.InfiniteXp)
            {
                if (level != null && _xpHookedLevel != level)
                {
                    UnhookXp();
                    _xpHookedLevel = level;
                    // We can't subscribe to "enemy died" globally, so we piggyback on
                    // EnemyRegistry shrinkage: each Update tick, if the enemy count
                    // dropped while the flag is set, treat that as kills and pump XP.
                }
            }
            else
            {
                UnhookXp();
            }

            // Drive XP from registry shrinkage so it works regardless of damage source.
            if (svc.InfiniteXp && level != null)
            {
                int current = EnemyRegistry.Enemies.Count;
                if (_lastXpCount < 0) _lastXpCount = current;
                int delta = _lastXpCount - current;
                if (delta > 0)
                {
                    // Award enough XP to force at least one level-up per kill.
                    for (int i = 0; i < delta; i++)
                    {
                        level.AddXp(Mathf.Max(1, level.XpToNext));
                    }
                }
                _lastXpCount = current;
            }
            else
            {
                _lastXpCount = -1;
            }
        }

        private int _lastXpCount = -1;

        private PlayerLevel ResolvePlayerLevel()
        {
            var player = PlayerController.Instance;
            if (player == null) { _cachedPlayerLevel = null; _cachedPlayerForLevel = null; return null; }
            if (_cachedPlayerForLevel != player || _cachedPlayerLevel == null)
            {
                _cachedPlayerForLevel = player;
                _cachedPlayerLevel = player.GetComponent<PlayerLevel>();
            }
            return _cachedPlayerLevel;
        }

        private void UnhookXp()
        {
            _xpHookedLevel = null;
        }

        // ----------------------------------------------------------------- NoSpawn

        private void EnforceNoSpawn(CheatService svc)
        {
            var spawner = ResolveSpawner();
            if (spawner == null) return;

            if (svc.NoSpawn)
            {
                if (spawner.gameObject.activeSelf)
                {
                    spawner.gameObject.SetActive(false);
                    _spawnerDisabledByUs = true;
                }
            }
            else if (_spawnerDisabledByUs)
            {
                spawner.gameObject.SetActive(true);
                _spawnerDisabledByUs = false;
            }
        }

        private EnemySpawner ResolveSpawner()
        {
            if (_cachedSpawner != null) return _cachedSpawner;
            // Find inactive too — required so NoSpawn off can re-enable.
            #if UNITY_2023_1_OR_NEWER
            _cachedSpawner = FindFirstObjectByType<EnemySpawner>(FindObjectsInactive.Include);
            #else
            _cachedSpawner = FindObjectOfType<EnemySpawner>(true);
            #endif
            return _cachedSpawner;
        }

        private void RestoreSpawnerIfNeeded()
        {
            if (!_spawnerDisabledByUs) return;
            var spawner = ResolveSpawner();
            if (spawner != null && !spawner.gameObject.activeSelf) spawner.gameObject.SetActive(true);
            _spawnerDisabledByUs = false;
        }

        // ----------------------------------------------------------------- FreezeEnemies

        private void FreezeAllEnemies()
        {
            var enemies = EnemyRegistry.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (e == null) continue;
                var rb = e.GetComponent<Rigidbody2D>();
                if (rb != null) rb.linearVelocity = Vector2.zero;
            }
        }

        // ----------------------------------------------------------------- helpers exposed to UI / persistence

        /// <summary>Effective damage multiplier given current cheat state.</summary>
        public static float DamageMultiplier =>
            BuildGuard.CheatsAllowed && CheatService.Instance.BigDamage
                ? CheatService.BigDamageMultiplier
                : 1f;

        /// <summary>Effective cooldown override given current cheat state, or -1 if not active.</summary>
        public static float CooldownOverride =>
            BuildGuard.CheatsAllowed && CheatService.Instance.InstantCooldowns
                ? CheatService.InstantCooldownSeconds
                : -1f;
    }
}
