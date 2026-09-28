using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Jabel.Audio
{
    /// <summary>
    /// Binds a UI Slider to one volume channel, shows the value in percent and plays a tick while dragging,
    /// so the player hears the new loudness immediately.
    /// </summary>
    [RequireComponent(typeof(Slider))]
    [AddComponentMenu("Jabel/Audio/Volume Slider")]
    public class VolumeSlider : MonoBehaviour, IPointerUpHandler
    {
        [SerializeField] private AudioChannel channel = AudioChannel.Master;
        [SerializeField] private TMP_Text valueLabel;
        [Tooltip("Played while the value changes (throttled).")]
        [SerializeField] private SoundCue tickSound;
        [SerializeField] private float tickInterval = 0.08f;

        private Slider _slider;
        private bool _updating;
        private float _nextTick;

        public AudioChannel Channel => channel;

        private void Awake()
        {
            _slider = GetComponent<Slider>();
            _slider.minValue = 0;
            _slider.maxValue = 1;
            _slider.wholeNumbers = false;
            _slider.onValueChanged.AddListener(OnValueChanged);
        }

        private void OnEnable()
        {
            AudioVolumes.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            AudioVolumes.Changed -= Refresh;
            PlayerPrefs.Save();
        }

        private void Refresh()
        {
            if (_slider == null) return;
            _updating = true;
            _slider.SetValueWithoutNotify(AudioVolumes.Get(channel));
            _updating = false;
            UpdateLabel();
        }

        private void OnValueChanged(float value)
        {
            if (_updating) return;
            // Written to prefs now, flushed to disk when the drag ends.
            AudioVolumes.Set(channel, value, save: false);
            UpdateLabel();
            if (tickSound != null && Time.unscaledTime >= _nextTick)
            {
                _nextTick = Time.unscaledTime + tickInterval;
                JabelAudio.Play(tickSound);
            }
        }

        public void OnPointerUp(PointerEventData eventData) => PlayerPrefs.Save();

        private void UpdateLabel()
        {
            if (valueLabel != null) valueLabel.text = Mathf.RoundToInt(AudioVolumes.Get(channel) * 100) + "%";
        }

#if UNITY_EDITOR
        public void EditorSetup(AudioChannel audioChannel, TMP_Text label, SoundCue tick)
        {
            channel = audioChannel;
            valueLabel = label;
            tickSound = tick;
        }
#endif
    }
}
