using UnityEngine;

namespace Seeman
{
    /// <summary>Flickers a spot light to fake underwater caustics.</summary>
    public class CausticLight : MonoBehaviour
    {
        public float BaseIntensity = 2.2f;
        public float FlickerAmount = 0.5f;
        public float DriftAmount = 0.15f;

        Light _light;
        Vector3 _basePos;
        float _seed;

        void Start()
        {
            _light = GetComponent<Light>();
            _basePos = transform.position;
            _seed = Random.value * 100f;
        }

        void Update()
        {
            float t = Time.time;
            if (_light != null)
                _light.intensity = BaseIntensity +
                    (Mathf.PerlinNoise(_seed, t * 1.7f) - 0.5f) * 2f * FlickerAmount;
            transform.position = _basePos + new Vector3(
                (Mathf.PerlinNoise(_seed + 1f, t * 0.5f) - 0.5f) * DriftAmount,
                0f,
                (Mathf.PerlinNoise(_seed + 2f, t * 0.4f) - 0.5f) * DriftAmount);
        }
    }
}
