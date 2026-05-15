using System.Collections.Generic;
using UnityEngine;

namespace LoneFighter.Enemies
{
    public static class EnemyRegistry
    {
        private static readonly List<EnemyBase> _enemies = new();

        public static IReadOnlyList<EnemyBase> Enemies => _enemies;
        public static int Count => _enemies.Count;

        public static void Register(EnemyBase enemy)
        {
            if (enemy != null && !_enemies.Contains(enemy)) _enemies.Add(enemy);
        }

        public static void Unregister(EnemyBase enemy)
        {
            if (enemy != null) _enemies.Remove(enemy);
        }

        public static void Clear() => _enemies.Clear();

        public static EnemyBase FindNearest(Vector2 position, float maxRange = float.PositiveInfinity)
        {
            EnemyBase best = null;
            float bestSq = maxRange * maxRange;
            for (int i = 0; i < _enemies.Count; i++)
            {
                var e = _enemies[i];
                if (e == null || !e.isActiveAndEnabled) continue;
                float sq = ((Vector2)e.transform.position - position).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = e;
                }
            }
            return best;
        }
    }
}
