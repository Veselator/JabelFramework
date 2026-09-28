using UnityEngine;

namespace Jabel.UI
{
    /// <summary>Emits a burst from a particle system. Wire Burst(float) to FunctionListener / UnityEvents.</summary>
    [AddComponentMenu("Jabel/UI/Particle Burst")]
    public class ParticleBurst : MonoBehaviour
    {
        [SerializeField] private ParticleSystem particles;
        [SerializeField] private int baseCount = 30;
        [SerializeField] private float countPerUnit = 2f;
        [SerializeField] private int maxCount = 200;

        public void Burst(float amount)
        {
            if (particles == null) return;
            int count = Mathf.Clamp(Mathf.RoundToInt(baseCount + amount * countPerUnit), 1, maxCount);
            particles.Emit(count);
        }

        public void Burst() => Burst(0);

#if UNITY_EDITOR
        public void EditorSetup(ParticleSystem system, int count) { particles = system; baseCount = count; }
#endif
    }
}
