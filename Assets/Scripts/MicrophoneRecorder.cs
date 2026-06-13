using UnityEngine;

namespace Seeman
{
    public class MicrophoneRecorder : MonoBehaviour
    {
        public float CurrentLevel { get; private set; }
        public bool IsRecording { get; private set; }
        public bool HasMic { get; private set; }

        [Tooltip("Open-mic level that summons the fish")]
        public float SummonThreshold = 0.10f;
        public System.Action OnVoiceDetected;

        public string DeviceName { get; private set; } = "";

        AudioClip _monitorClip;
        // null = OS default microphone (devices[0] can be a virtual/unused device)
        readonly string _device = null;
        int _recStart;
        float _voiceCooldown;
        readonly float[] _levelBuf = new float[256];

        const int MonitorLen = 12;
        const int Freq = 16000;

        void Start()
        {
            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[Seeman] No microphone found.");
                HasMic = false;
                return;
            }
            DeviceName = Microphone.devices[0];
            _monitorClip = Microphone.Start(_device, true, MonitorLen, Freq);
            HasMic = _monitorClip != null;
            Debug.Log("[Seeman] Mic monitoring started: " + DeviceName + " ok=" + HasMic);
        }

        void Update()
        {
            if (_monitorClip == null) return;
            int pos = Microphone.GetPosition(_device);
            if (pos < _levelBuf.Length) return;
            _monitorClip.GetData(_levelBuf, pos - _levelBuf.Length);
            float sum = 0f;
            for (int i = 0; i < _levelBuf.Length; i++) sum += _levelBuf[i] * _levelBuf[i];
            CurrentLevel = Mathf.Sqrt(sum / _levelBuf.Length);

            _voiceCooldown -= Time.deltaTime;
            if (!IsRecording && CurrentLevel > SummonThreshold && _voiceCooldown <= 0f)
            {
                _voiceCooldown = 4f;
                OnVoiceDetected?.Invoke();
            }
        }

        public void StartRecording()
        {
            if (!HasMic || IsRecording) return;
            IsRecording = true;
            _recStart = Microphone.GetPosition(_device);
        }

        public AudioClip StopRecording()
        {
            if (!IsRecording) return null;
            IsRecording = false;
            int end = Microphone.GetPosition(_device);
            int total = _monitorClip.samples;
            int len = end - _recStart;
            if (len < 0) len += total;
            if (len < Freq / 4) return null;

            float[] all = new float[total];
            _monitorClip.GetData(all, 0);
            float[] data = new float[len];
            for (int i = 0; i < len; i++) data[i] = all[(_recStart + i) % total];

            var clip = AudioClip.Create("rec", len, 1, Freq, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
