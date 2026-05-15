using System.Collections.Generic;
using UnityEngine;
using LoneFighter.Systems;

namespace LoneFighter.Combat.Status
{
    /// <summary>
    /// Per-effect VFX layer that listens to a sibling <see cref="StatusController"/> and
    /// spawns / despawns a particle system *attached to the victim* while the effect is active.
    ///
    /// Inspector lets you assign one prefab per effect Id. Examples (recommended):
    ///   burn   → orange additive particle (flame motes)
    ///   slow   → blue ice particle (frost drift)
    ///   stun   → yellow stars circling overhead
    ///   poison → green bubble particle
    ///
    /// Behavior:
    ///   - On OnEffectAdded: if no live VFX for that effect, instantiate the configured prefab
    ///     as a child of the victim. If a VFX is already live (re-application), it's left alone.
    ///   - On OnEffectRemoved: stop emitting and release back to the pool / destroy.
    ///   - The component also calls into <see cref="FxService"/> for "punchy" one-shots on critical
    ///     transitions (e.g. stun applied → light shake), if FxService is present.
    /// </summary>
    [RequireComponent(typeof(StatusController))]
    public class StatusVisualHooks : MonoBehaviour
    {
        [System.Serializable]
        public struct EffectFx
        {
            [Tooltip("Status effect Id this prefab pairs with (e.g. 'burn', 'slow', 'stun', 'poison').")]
            public string effectId;
            [Tooltip("Particle prefab to attach to the victim for the duration of the effect.")]
            public GameObject prefab;
            [Tooltip("Local-space offset from the victim's pivot.")]
            public Vector3 localOffset;
        }

        [Header("Per-effect particle prefabs")]
        [SerializeField] private List<EffectFx> effectFx = new();

        [Header("One-shot juice")]
        [Tooltip("Trigger a light camera shake when a stun is applied (via FxService, if present).")]
        [SerializeField] private bool stunShake = true;

        private StatusController _controller;
        // Live FX keyed by effect Id; instance is parented to this transform.
        private readonly Dictionary<string, GameObject> _liveByEffectId = new();

        private void Awake() => _controller = GetComponent<StatusController>();

        private void OnEnable()
        {
            if (_controller == null) _controller = GetComponent<StatusController>();
            _controller.OnEffectAdded += HandleAdded;
            _controller.OnEffectRemoved += HandleRemoved;
        }

        private void OnDisable()
        {
            if (_controller != null)
            {
                _controller.OnEffectAdded -= HandleAdded;
                _controller.OnEffectRemoved -= HandleRemoved;
            }
            // Tear down every live FX so a re-pooled enemy doesn't bring particles with it.
            foreach (var kv in _liveByEffectId)
            {
                if (kv.Value != null) ReleaseInstance(kv.Value);
            }
            _liveByEffectId.Clear();
        }

        private void HandleAdded(StatusController controller, StatusEffect effect)
        {
            if (effect == null) return;

            // One-shot punch for impactful effects.
            if (effect.Id == StunEffect.EffectId && stunShake && FxService.Instance != null)
            {
                FxService.Instance.LightShake();
            }

            // If already live, this was a refresh / stack — keep current emitter.
            if (_liveByEffectId.ContainsKey(effect.Id)) return;

            if (!TryGetPrefab(effect.Id, out var prefab, out var offset) || prefab == null) return;

            GameObject go;
            if (PoolService.Instance != null)
            {
                go = PoolService.Instance.Get(prefab, transform.position + transform.TransformVector(offset), Quaternion.identity);
                go.transform.SetParent(transform, worldPositionStays: true);
            }
            else
            {
                go = Instantiate(prefab, transform);
                go.transform.localPosition = offset;
            }

            // Make sure any particle systems are running.
            var ps = go.GetComponent<ParticleSystem>();
            if (ps != null)
            {
                ps.Clear(true);
                ps.Play(true);
            }

            _liveByEffectId[effect.Id] = go;
        }

        private void HandleRemoved(StatusController controller, StatusEffect effect)
        {
            if (effect == null) return;
            if (!_liveByEffectId.TryGetValue(effect.Id, out var live) || live == null)
            {
                _liveByEffectId.Remove(effect.Id);
                return;
            }

            // Stop emission so particles fade out naturally if the prefab is configured that way.
            var ps = live.GetComponent<ParticleSystem>();
            if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            ReleaseInstance(live);
            _liveByEffectId.Remove(effect.Id);
        }

        private bool TryGetPrefab(string effectId, out GameObject prefab, out Vector3 offset)
        {
            for (int i = 0; i < effectFx.Count; i++)
            {
                if (effectFx[i].effectId == effectId)
                {
                    prefab = effectFx[i].prefab;
                    offset = effectFx[i].localOffset;
                    return true;
                }
            }
            prefab = null;
            offset = Vector3.zero;
            return false;
        }

        private static void ReleaseInstance(GameObject go)
        {
            if (go == null) return;
            // Unparent before releasing — pooled prefabs live under PoolService's transform.
            go.transform.SetParent(null, worldPositionStays: true);
            if (PoolService.Instance != null) PoolService.Instance.Release(go);
            else Destroy(go);
        }
    }
}
