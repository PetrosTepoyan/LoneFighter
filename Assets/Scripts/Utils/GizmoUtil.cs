using UnityEngine;

namespace LoneFighter.Utils
{
    public static class GizmoUtil
    {
        public static void DrawCircle(Vector3 center, float radius, int segments = 32)
        {
            Vector3 prev = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments * Mathf.PI * 2f;
                Vector3 next = center + new Vector3(Mathf.Cos(t) * radius, Mathf.Sin(t) * radius, 0f);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }
    }
}
