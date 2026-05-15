using UnityEngine;

namespace LoneFighter.Effects.Trails
{
    /// <summary>
    /// Drives a <see cref="TrailRenderer"/> from a <see cref="TrailProfile"/>.
    /// Designed to live on the same GameObject as (or a child of) a pooled
    /// projectile.
    ///
    /// Critically, this controller resets the trail when its GameObject is
    /// activated and clears it when it is deactivated, so projectiles that
    /// teleport from impact location back to a pool spawn point do not
    /// draw a streak across the screen on their next launch.
    ///
    /// Wire-up:
    /// - Drop a <c>TrailRenderer</c> on the projectile (any child works).
    /// - Add this component, assign the <c>TrailRenderer</c> and a
    ///   <see cref="TrailProfile"/>.
    /// - Optionally assign an additive material that the profile will swap
    ///   in when <see cref="TrailProfile.additive"/> is true.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BulletTrailController : MonoBehaviour
    {
        [Tooltip("The TrailRenderer to drive. If left null, the controller will look for one " +
                 "on this GameObject and then in its children at Awake.")]
        [SerializeField] private TrailRenderer trail;

        [Tooltip("Profile applied to the TrailRenderer at Awake / OnEnable.")]
        [SerializeField] private TrailProfile profile;

        [Tooltip("Optional additive material. Used when the profile has Additive = true. " +
                 "If null, the TrailRenderer's existing material is left alone.")]
        [SerializeField] private Material additiveMaterial;

        [Tooltip("Optional default (alpha-blended) material. Used when the profile has " +
                 "Additive = false. If null, the TrailRenderer's existing material is left alone.")]
        [SerializeField] private Material defaultMaterial;

#if UNITY_EDITOR
        private bool _initialized;
#endif

        private void Awake()
        {
            if (trail == null)
            {
                trail = GetComponent<TrailRenderer>();
                if (trail == null) trail = GetComponentInChildren<TrailRenderer>(includeInactive: true);
            }
            ApplyProfile();
        }

        private void OnEnable()
        {
            // Pool returns can re-activate the object far from where it was disabled.
            // Clear any leftover vertices so the trail doesn't snap across the arena.
            if (trail != null)
            {
                trail.Clear();
                trail.emitting = true;
            }
        }

        private void OnDisable()
        {
            if (trail != null)
            {
                trail.emitting = false;
                trail.Clear();
            }
        }

        /// <summary>
        /// Swap to a new profile at runtime (e.g. weapon upgrade). Re-applies
        /// gradient, width curve, fade time and material.
        /// </summary>
        public void SetProfile(TrailProfile newProfile)
        {
            profile = newProfile;
#if UNITY_EDITOR
            _initialized = false;
#endif
            ApplyProfile();
            if (isActiveAndEnabled && trail != null) trail.Clear();
        }

        private void ApplyProfile()
        {
            if (trail == null || profile == null) return;

            trail.time = profile.fadeTime;
            trail.minVertexDistance = profile.minVertexDistance;
            trail.colorGradient = profile.colorGradient;
            trail.widthCurve = profile.widthCurve;
            trail.widthMultiplier = profile.widthScale;

            // Material swap (only when an override is supplied; otherwise leave whatever
            // the prefab is using so we don't strip user-authored materials).
            Material desired = profile.additive ? additiveMaterial : defaultMaterial;
            if (desired != null) trail.sharedMaterial = desired;

            // Sorting.
            if (!string.IsNullOrEmpty(profile.sortingLayerName))
            {
                trail.sortingLayerName = profile.sortingLayerName;
            }
            trail.sortingOrder = profile.sortingOrder;

#if UNITY_EDITOR
            _initialized = true;
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Live-update in the editor when the profile changes.
            if (!Application.isPlaying || !_initialized) return;
            ApplyProfile();
        }
#endif
    }
}
