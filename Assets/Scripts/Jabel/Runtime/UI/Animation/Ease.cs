using UnityEngine;

namespace Jabel.UI
{
    /// <summary>Common easing curves (t in 0..1). Tiny on purpose: no tween library dependency.</summary>
    public static class Ease
    {
        public static float OutCubic(float t) { t = Mathf.Clamp01(t); return 1 - Mathf.Pow(1 - t, 3); }
        public static float InCubic(float t) { t = Mathf.Clamp01(t); return t * t * t; }
        public static float InOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2;
        }

        public static float OutBack(float t, float overshoot = 1.70158f)
        {
            t = Mathf.Clamp01(t);
            float c3 = overshoot + 1;
            return 1 + c3 * Mathf.Pow(t - 1, 3) + overshoot * Mathf.Pow(t - 1, 2);
        }

        public static float OutElastic(float t)
        {
            t = Mathf.Clamp01(t);
            if (t == 0 || t == 1) return t;
            const float c4 = 2 * Mathf.PI / 3;
            return Mathf.Pow(2, -10 * t) * Mathf.Sin((t * 10 - 0.75f) * c4) + 1;
        }

        /// <summary>Damped oscillation from 1 to 0, used for punch/shake effects.</summary>
        public static float DampedWave(float t, float frequency = 18f, float damping = 6f) =>
            Mathf.Exp(-damping * t) * Mathf.Cos(frequency * t);
    }

    /// <summary>Punch helper: call <see cref="Kick"/>, read <see cref="Value"/> each frame (1 = rest).</summary>
    public struct Punch
    {
        private float _strength;
        private float _time;

        public void Kick(float strength)
        {
            _strength = Mathf.Min(0.6f, Mathf.Max(_strength * Mathf.Exp(-6 * _time), 0) + strength);
            _time = 0;
        }

        public float Value
        {
            get
            {
                if (_strength <= 0) return 1;
                return 1 + _strength * Ease.DampedWave(_time);
            }
        }

        public bool Active => _strength > 0 && _time < 1.2f;

        public void Update(float dt)
        {
            if (_strength <= 0) return;
            _time += dt;
            if (_time > 1.2f) _strength = 0;
        }
    }
}
