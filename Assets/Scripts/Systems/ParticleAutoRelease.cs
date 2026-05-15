using UnityEngine;

namespace LoneFighter.Systems
{
    /// Returns a pooled ParticleSystem GameObject to the pool once it finishes playing.
    [RequireComponent(typeof(ParticleSystem))]
    public class ParticleAutoRelease : MonoBehaviour
    {
        private ParticleSystem _ps;

        private void Awake()
        {
            _ps = GetComponent<ParticleSystem>();
        }

        private void OnEnable()
        {
            if (_ps != null)
            {
                _ps.Clear(true);
                _ps.Play(true);
            }
        }

        private void Update()
        {
            if (_ps == null) return;
            if (!_ps.IsAlive(true))
            {
                if (PoolService.Instance != null) PoolService.Instance.Release(gameObject);
                else Destroy(gameObject);
            }
        }
    }
}
