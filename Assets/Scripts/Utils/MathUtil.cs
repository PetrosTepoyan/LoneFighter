using UnityEngine;

namespace LoneFighter.Utils
{
    public static class MathUtil
    {
        public static Vector2 RandomPointOnRing(Vector2 center, float radius)
        {
            float angle = Random.value * Mathf.PI * 2f;
            return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        }

        public static Vector2 RandomPointOnRing(Vector2 center, float minRadius, float maxRadius)
        {
            float angle = Random.value * Mathf.PI * 2f;
            float r = Random.Range(minRadius, maxRadius);
            return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * r;
        }
    }
}
