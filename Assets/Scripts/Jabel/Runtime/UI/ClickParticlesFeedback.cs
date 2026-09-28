using Jabel.Localization;
using Jabel.Numbers;
using UnityEngine;

namespace Jabel.UI
{
    /// <summary>
    /// Bursts particles from the click point. The count grows with the logarithm of the click value,
    /// so big clicks feel big without flooding the screen.
    /// </summary>
    [AddComponentMenu("Jabel/UI/Click Particles Feedback")]
    public class ClickParticlesFeedback : MonoBehaviour, IClickFeedback
    {
        [SerializeField] private ParticleSystem particles;
        [SerializeField] private int minCount = 4;
        [SerializeField] private int maxCount = 60;
        [Tooltip("Extra particles per order of magnitude of the click value.")]
        [SerializeField] private float perMagnitude = 7f;
        [SerializeField] private float criticalMultiplier = 2.5f;
        [Tooltip("Distance in front of the camera for Screen Space - Overlay canvases.")]
        [SerializeField] private float overlayDepth = 5f;

        public void Play(ClickFeedbackData data)
        {
            if (particles == null) return;
            double magnitude = data.Value.IsZero ? 0 : System.Math.Max(0, data.Value.Log10() + 1);
            float count = minCount + (float)magnitude * perMagnitude;
            if (data.IsCritical) count *= criticalMultiplier;
            count = Mathf.Clamp(count, minCount, maxCount);

            var position = data.WorldPosition;
            if (data.Camera != null && data.Camera.orthographic)
                position.z = particles.transform.position.z;
            else if (data.Camera != null && position == Vector3.zero)
                position = data.Camera.ScreenToWorldPoint(new Vector3(data.ScreenPosition.x, data.ScreenPosition.y, overlayDepth));

            var emit = new ParticleSystem.EmitParams { position = position, applyShapeToPosition = true };
            particles.Emit(emit, Mathf.RoundToInt(count));
        }

#if UNITY_EDITOR
        public void EditorSetup(ParticleSystem system) => particles = system;
#endif
    }
}
