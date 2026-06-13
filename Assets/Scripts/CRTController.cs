using System.Collections;
using UnityEngine;

namespace Seeman
{
    /// <summary>Drives the CRT fullscreen material: power-on animation, channel static bursts.</summary>
    public class CRTController : MonoBehaviour
    {
        public Material CrtMaterial;

        static readonly int PowerOnId = Shader.PropertyToID("_PowerOn");
        static readonly int StaticId = Shader.PropertyToID("_Static");

        float _staticLevel;

        void Start()
        {
            if (CrtMaterial != null)
            {
                CrtMaterial.SetFloat(StaticId, 0f);
                StartCoroutine(PowerOn());
            }
        }

        IEnumerator PowerOn()
        {
            CrtMaterial.SetFloat(PowerOnId, 0.1f);
            yield return new WaitForSeconds(0.25f);
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 0.7f;
                CrtMaterial.SetFloat(PowerOnId, Mathf.SmoothStep(0.02f, 1f, t));
                yield return null;
            }
            CrtMaterial.SetFloat(PowerOnId, 1f);
            // a burst of static right after power-on, like a channel tuning in
            StaticBurst(0.85f, 0.5f);
        }

        Coroutine _decay;

        public void StaticBurst(float strength, float decaySeconds)
        {
            _staticLevel = Mathf.Max(_staticLevel, strength);
            if (_decay != null) StopCoroutine(_decay);
            _decay = StartCoroutine(DecayStatic(decaySeconds));
        }

        IEnumerator DecayStatic(float seconds)
        {
            while (_staticLevel > 0.005f)
            {
                _staticLevel = Mathf.MoveTowards(_staticLevel, 0f, Time.deltaTime / Mathf.Max(0.05f, seconds));
                if (CrtMaterial != null) CrtMaterial.SetFloat(StaticId, _staticLevel);
                yield return null;
            }
            _staticLevel = 0f;
            if (CrtMaterial != null) CrtMaterial.SetFloat(StaticId, 0f);
            _decay = null;
        }

        /// <summary>Small blip when the user taps the glass.</summary>
        public void Tap() { StaticBurst(0.18f, 0.15f); }

        void OnDestroy()
        {
            // keep the shared material asset clean in the editor
            if (CrtMaterial != null)
            {
                CrtMaterial.SetFloat(PowerOnId, 1f);
                CrtMaterial.SetFloat(StaticId, 0f);
            }
        }
    }
}
