// Auto-authored content seed for LoneFighter.
//
// Plain C# data only — no MonoBehaviours, no ScriptableObject references, no Unity API calls.
// An Editor tool (a separate agent's job) consumes these arrays and creates the matching
// WeaponData / EnemyData / UpgradeData / WaveConfig ScriptableObject instances under Assets/Data/*.
//
// Numbers here are the source of truth and match BALANCE.md. Update both together.

namespace LoneFighter.Data
{
    public static class ContentSeed
    {
        // ----------------------------------------------------------------
        // Nested spec structs
        // ----------------------------------------------------------------

        public struct PlayerBaselineSpec
        {
            public float maxHealth;
            public float moveSpeed;
            public float magnetRadius;
            public float pickupRadius;
            public float magnetForce;
            public float invulnerabilityWindow;
            public int baseXpToLevel;
            public float xpGrowthPerLevel;
        }

        public struct EnemySpec
        {
            public string name;
            public float hp;
            public float speed;
            public float damage;
            public int xp;
            public float contactCooldown;
            public float gemDropChance;
            public int concurrentCap;
            public float spawnWeight;
            public string behavior;
        }

        public struct WeaponSpec
        {
            public string name;
            public float damage;
            public float cooldown;
            public float projectileSpeed;
            public float projectileLifetime;
            public int pierce;
            public float range;
            public string behavior;

            // True if this weapon needs a new implementation rather than reusing the existing
            // ProjectileWeapon. The scaffold's ProjectileWeapon handles Pistol/Shotgun-style fire;
            // Orbital Blade (orbiting hitbox) and Lightning Chain (instant chain) need new classes.
            public bool requiresCustomImplementation;
        }

        public struct UpgradeSpec
        {
            public string name;
            public string description;
            public UpgradeKind kind;
            public float magnitude;
            public int maxStacks;
            public float weight;

            // For GrantWeapon upgrades — name of the weapon (matches Weapons[].name) to assign.
            // Empty for non-grant upgrades.
            public string grantsWeaponName;
        }

        public struct WaveEntrySpec
        {
            public float startTime;
            public float endTime;
            public string enemyName; // matches Enemies[].name
            public float spawnRate;
            public int concurrentCap;
        }

        // ----------------------------------------------------------------
        // Player baseline
        // ----------------------------------------------------------------

        public static readonly PlayerBaselineSpec PlayerBaseline = new PlayerBaselineSpec
        {
            maxHealth = 100f,
            moveSpeed = 5.0f,
            magnetRadius = 2.5f,
            pickupRadius = 0.5f,
            magnetForce = 18f,
            invulnerabilityWindow = 0.4f,
            baseXpToLevel = 5,
            xpGrowthPerLevel = 1.35f,
        };

        // ----------------------------------------------------------------
        // Enemies (canonical names — other agents key off these exact strings)
        // ----------------------------------------------------------------

        public static readonly EnemySpec[] Enemies = new EnemySpec[]
        {
            new EnemySpec {
                name = "Grunt",
                hp = 10f, speed = 2.2f, damage = 8f, xp = 1,
                contactCooldown = 0.5f, gemDropChance = 1f,
                concurrentCap = 120, spawnWeight = 10f,
                behavior = "Walks straight at the player; baseline filler.",
            },
            new EnemySpec {
                name = "Runner",
                hp = 6f, speed = 4.4f, damage = 6f, xp = 1,
                contactCooldown = 0.5f, gemDropChance = 1f,
                concurrentCap = 50, spawnWeight = 6f,
                behavior = "Sprints in fast, low HP, dies to one Pistol shot mid-build.",
            },
            new EnemySpec {
                name = "Tank",
                hp = 80f, speed = 1.4f, damage = 18f, xp = 5,
                contactCooldown = 0.6f, gemDropChance = 1f,
                concurrentCap = 12, spawnWeight = 2f,
                behavior = "Slow, heavy contact damage, soaks pierce and splits the swarm.",
            },
            new EnemySpec {
                name = "Spitter",
                hp = 14f, speed = 1.8f, damage = 7f, xp = 2,
                contactCooldown = 0.5f, gemDropChance = 1f,
                concurrentCap = 18, spawnWeight = 3f,
                behavior = "Stops at ~5 u and lobs a 10-damage projectile every ~1.8 s.",
            },
            new EnemySpec {
                name = "Dasher",
                hp = 22f, speed = 2.6f, damage = 14f, xp = 3,
                contactCooldown = 0.5f, gemDropChance = 1f,
                concurrentCap = 16, spawnWeight = 3f,
                behavior = "Telegraphs for 0.6 s then dashes 4 u forward at 9 u/s.",
            },
            new EnemySpec {
                name = "Bomber",
                hp = 18f, speed = 2.0f, damage = 6f, xp = 3,
                contactCooldown = 0.5f, gemDropChance = 1f,
                concurrentCap = 14, spawnWeight = 2f,
                behavior = "Detonates on death in a 1.8 u radius for 28 damage.",
            },
            new EnemySpec {
                name = "Warden",
                hp = 1200f, speed = 1.6f, damage = 30f, xp = 80,
                contactCooldown = 0.8f, gemDropChance = 1f,
                concurrentCap = 1, spawnWeight = 0f, // boss — spawned by wave entry, not weighted random
                behavior = "Mini-boss. Slow chase, periodic 3 u ground-slam ring with 1.0 s wind-up.",
            },
        };

        // ----------------------------------------------------------------
        // Weapons
        // ----------------------------------------------------------------

        public static readonly WeaponSpec[] Weapons = new WeaponSpec[]
        {
            new WeaponSpec {
                name = "Pistol",
                damage = 10f, cooldown = 0.55f, projectileSpeed = 12f,
                projectileLifetime = 2.0f, pierce = 0, range = 10f,
                behavior = "Auto-targets nearest enemy, single projectile.",
                requiresCustomImplementation = false,
            },
            new WeaponSpec {
                name = "Spread Shotgun",
                damage = 6f, cooldown = 0.95f, projectileSpeed = 11f,
                projectileLifetime = 0.9f, pierce = 1, range = 7f,
                behavior = "Fires 5 projectiles in a 35 deg cone; close-range crowd clear.",
                requiresCustomImplementation = false,
            },
            new WeaponSpec {
                name = "Orbital Blade",
                damage = 14f, cooldown = 0f, projectileSpeed = 0f,
                projectileLifetime = 0f, pierce = 999, range = 1.8f,
                behavior = "Two blades orbit the player at 360 deg/s, 0.25 s per-target hit cooldown.",
                requiresCustomImplementation = true,
            },
            new WeaponSpec {
                name = "Lightning Chain",
                damage = 22f, cooldown = 1.45f, projectileSpeed = 0f,
                projectileLifetime = 0f, pierce = 0, range = 6f,
                behavior = "Strikes nearest enemy, chains to up to 3 within 3.5 u at 70% damage per jump.",
                requiresCustomImplementation = true,
            },
        };

        // ----------------------------------------------------------------
        // Upgrades (15-entry pool, mixed offense/defense/utility per DESIGN.md)
        // ----------------------------------------------------------------

        public static readonly UpgradeSpec[] Upgrades = new UpgradeSpec[]
        {
            new UpgradeSpec {
                name = "Bigger Bullets",
                description = "+25% weapon damage.",
                kind = UpgradeKind.WeaponDamage,
                magnitude = 0.25f, maxStacks = 5, weight = 3.0f,
                grantsWeaponName = "",
            },
            new UpgradeSpec {
                name = "Quick Hands",
                description = "-12% weapon cooldown.",
                kind = UpgradeKind.WeaponCooldown,
                magnitude = 0.12f, maxStacks = 5, weight = 3.0f,
                grantsWeaponName = "",
            },
            new UpgradeSpec {
                name = "Hot Lead",
                description = "+20% projectile speed.",
                kind = UpgradeKind.WeaponProjectileSpeed,
                magnitude = 0.20f, maxStacks = 4, weight = 2.0f,
                grantsWeaponName = "",
            },
            new UpgradeSpec {
                name = "Drillshot",
                description = "+1 projectile pierce.",
                kind = UpgradeKind.WeaponPierce,
                magnitude = 1f, maxStacks = 4, weight = 2.5f,
                grantsWeaponName = "",
            },
            new UpgradeSpec {
                name = "Heavy Slugs",
                description = "+35% weapon damage (rare big hitter).",
                kind = UpgradeKind.WeaponDamage,
                magnitude = 0.35f, maxStacks = 3, weight = 1.5f,
                grantsWeaponName = "",
            },
            new UpgradeSpec {
                name = "Trigger Discipline",
                description = "-18% weapon cooldown (rare).",
                kind = UpgradeKind.WeaponCooldown,
                magnitude = 0.18f, maxStacks = 3, weight = 1.5f,
                grantsWeaponName = "",
            },
            new UpgradeSpec {
                name = "Unlock: Spread Shotgun",
                description = "Grants the Spread Shotgun weapon.",
                kind = UpgradeKind.GrantWeapon,
                magnitude = 1f, maxStacks = 1, weight = 1.2f,
                grantsWeaponName = "Spread Shotgun",
            },
            new UpgradeSpec {
                name = "Unlock: Orbital Blade",
                description = "Grants two orbiting blades.",
                kind = UpgradeKind.GrantWeapon,
                magnitude = 1f, maxStacks = 1, weight = 1.2f,
                grantsWeaponName = "Orbital Blade",
            },
            new UpgradeSpec {
                name = "Unlock: Lightning Chain",
                description = "Grants a chain-lightning strike.",
                kind = UpgradeKind.GrantWeapon,
                magnitude = 1f, maxStacks = 1, weight = 1.2f,
                grantsWeaponName = "Lightning Chain",
            },
            new UpgradeSpec {
                name = "Vitality",
                description = "+20% max HP (heals 20% of max).",
                kind = UpgradeKind.PlayerMaxHealth,
                magnitude = 0.20f, maxStacks = 5, weight = 2.5f,
                grantsWeaponName = "",
            },
            new UpgradeSpec {
                name = "Thick Skin",
                description = "+12% max HP.",
                kind = UpgradeKind.PlayerMaxHealth,
                magnitude = 0.12f, maxStacks = 5, weight = 2.0f,
                grantsWeaponName = "",
            },
            new UpgradeSpec {
                name = "Fleet Foot",
                description = "+8% move speed.",
                kind = UpgradeKind.PlayerMoveSpeed,
                magnitude = 0.08f, maxStacks = 5, weight = 2.5f,
                grantsWeaponName = "",
            },
            new UpgradeSpec {
                name = "Adrenaline",
                description = "+15% move speed (rare).",
                kind = UpgradeKind.PlayerMoveSpeed,
                magnitude = 0.15f, maxStacks = 3, weight = 1.5f,
                grantsWeaponName = "",
            },
            new UpgradeSpec {
                name = "Lodestone",
                description = "+30% magnet radius.",
                kind = UpgradeKind.PlayerMagnetRadius,
                magnitude = 0.30f, maxStacks = 5, weight = 2.0f,
                grantsWeaponName = "",
            },
            new UpgradeSpec {
                name = "Greed",
                description = "+15% XP gain.",
                kind = UpgradeKind.XpGainMultiplier,
                magnitude = 0.15f, maxStacks = 5, weight = 1.8f,
                grantsWeaponName = "",
            },
        };

        // ----------------------------------------------------------------
        // Wave timeline (300 s run; ramps from ~1/s to ~6/s, Warden at 270)
        // ----------------------------------------------------------------

        public const float RunDurationSeconds = 300f;

        public static readonly WaveEntrySpec[] WaveTimeline = new WaveEntrySpec[]
        {
            new WaveEntrySpec { startTime =   0f, endTime =  60f, enemyName = "Grunt",   spawnRate = 1.0f,  concurrentCap = 40  },
            new WaveEntrySpec { startTime =  60f, endTime = 120f, enemyName = "Grunt",   spawnRate = 1.8f,  concurrentCap = 70  },
            new WaveEntrySpec { startTime =  60f, endTime = 150f, enemyName = "Runner",  spawnRate = 0.4f,  concurrentCap = 20  },
            new WaveEntrySpec { startTime =  90f, endTime = 180f, enemyName = "Spitter", spawnRate = 0.3f,  concurrentCap = 12  },
            new WaveEntrySpec { startTime = 120f, endTime = 210f, enemyName = "Grunt",   spawnRate = 2.2f,  concurrentCap = 90  },
            new WaveEntrySpec { startTime = 150f, endTime = 240f, enemyName = "Runner",  spawnRate = 0.6f,  concurrentCap = 30  },
            new WaveEntrySpec { startTime = 150f, endTime = 240f, enemyName = "Tank",    spawnRate = 0.12f, concurrentCap = 6   },
            new WaveEntrySpec { startTime = 180f, endTime = 270f, enemyName = "Spitter", spawnRate = 0.5f,  concurrentCap = 16  },
            new WaveEntrySpec { startTime = 180f, endTime = 270f, enemyName = "Dasher",  spawnRate = 0.35f, concurrentCap = 10  },
            new WaveEntrySpec { startTime = 210f, endTime = 300f, enemyName = "Grunt",   spawnRate = 2.6f,  concurrentCap = 120 },
            new WaveEntrySpec { startTime = 210f, endTime = 300f, enemyName = "Bomber",  spawnRate = 0.25f, concurrentCap = 12  },
            new WaveEntrySpec { startTime = 240f, endTime = 300f, enemyName = "Tank",    spawnRate = 0.18f, concurrentCap = 10  },
            new WaveEntrySpec { startTime = 240f, endTime = 300f, enemyName = "Runner",  spawnRate = 0.8f,  concurrentCap = 40  },
            new WaveEntrySpec { startTime = 270f, endTime = 300f, enemyName = "Dasher",  spawnRate = 0.5f,  concurrentCap = 14  },
            new WaveEntrySpec { startTime = 270f, endTime = 300f, enemyName = "Spitter", spawnRate = 0.6f,  concurrentCap = 18  },
            new WaveEntrySpec { startTime = 270f, endTime = 271f, enemyName = "Warden",  spawnRate = 1.0f,  concurrentCap = 1   },
        };
    }
}
