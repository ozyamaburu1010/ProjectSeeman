using System.Collections;
using UnityEngine;

namespace Seeman
{
    public class FishLipSync : MonoBehaviour
    {
        public SkinnedMeshRenderer Mesh;
        public AudioSource Voice;
        public float Gain = 14f;

        int _bsOpen = -1, _bsBlink = -1;
        readonly int[] _bsVowels = { -1, -1, -1, -1, -1 };
        readonly float[] _vowelW = new float[5];
        int _curVowel;
        float _vowelTimer;
        float _amp;
        float _blinkW;
        float _fakeUntil = -1f;
        readonly float[] _samples = new float[256];

        void Start()
        {
            if (Mesh == null) Mesh = GetComponentInChildren<SkinnedMeshRenderer>();
            if (Voice == null) Voice = GetComponent<AudioSource>();
            if (Mesh == null) { enabled = false; return; }
            var m = Mesh.sharedMesh;
            string[] vnames = { "Mouth_A", "Mouth_I", "Mouth_U", "Mouth_E", "Mouth_O" };
            for (int i = 0; i < m.blendShapeCount; i++)
            {
                string n = m.GetBlendShapeName(i);
                if (n == "Mouth_Open") _bsOpen = i;
                if (n == "Blink") _bsBlink = i;
                for (int v = 0; v < 5; v++) if (n == vnames[v]) _bsVowels[v] = i;
            }
            StartCoroutine(BlinkLoop());
        }

        public void FakeTalk(float duration) { _fakeUntil = Time.time + duration; }
        public void StopTalk() { _fakeUntil = -1f; }

        void Update()
        {
            float target = 0f;
            if (Voice != null && Voice.isPlaying)
            {
                Voice.GetOutputData(_samples, 0);
                float sum = 0f;
                for (int i = 0; i < _samples.Length; i++) sum += _samples[i] * _samples[i];
                target = Mathf.Clamp01(Mathf.Sqrt(sum / _samples.Length) * Gain);
            }
            else if (Time.time < _fakeUntil)
            {
                target = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.time * 9f) * Mathf.Sin(Time.time * 3.7f));
            }
            _amp = Mathf.Lerp(_amp, target, Time.deltaTime * 18f);

            _vowelTimer -= Time.deltaTime;
            if (_vowelTimer <= 0f)
            {
                _curVowel = Random.Range(0, 5);
                _vowelTimer = Random.Range(0.07f, 0.16f);
            }

            for (int v = 0; v < 5; v++)
            {
                float w = (v == _curVowel ? 70f : 0f) * _amp;
                _vowelW[v] = Mathf.Lerp(_vowelW[v], w, Time.deltaTime * 14f);
            }
        }

        // The Swim animation clip contains curves for all 7 blendshapes, so the
        // Animator resets them every frame. Weights must be written AFTER the
        // animator evaluates, i.e. in LateUpdate.
        void LateUpdate()
        {
            for (int v = 0; v < 5; v++)
            {
                if (_bsVowels[v] < 0) continue;
                Mesh.SetBlendShapeWeight(_bsVowels[v], _vowelW[v]);
            }
            if (_bsOpen >= 0) Mesh.SetBlendShapeWeight(_bsOpen, _amp * 55f);
            if (_bsBlink >= 0) Mesh.SetBlendShapeWeight(_bsBlink, _blinkW);
        }

        IEnumerator BlinkLoop()
        {
            while (true)
            {
                yield return new WaitForSeconds(Random.Range(2.2f, 6.5f));
                if (_bsBlink < 0) continue;
                float t = 0f;
                while (t < 1f)
                {
                    t += Time.deltaTime / 0.07f;
                    _blinkW = Mathf.Lerp(0f, 100f, t);
                    yield return null;
                }
                while (t > 0f)
                {
                    t -= Time.deltaTime / 0.09f;
                    _blinkW = Mathf.Lerp(0f, 100f, t);
                    yield return null;
                }
                _blinkW = 0f;
            }
        }
    }
}
