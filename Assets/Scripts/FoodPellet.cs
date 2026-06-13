using UnityEngine;

namespace Seeman
{
    public class FoodPellet : MonoBehaviour
    {
        public float SinkSpeed = 0.07f;
        float _wobbleSeed;
        float _floorY = 0.08f;
        float _life = 60f;

        void Start() { _wobbleSeed = Random.value * 10f; }

        void Update()
        {
            if (transform.position.y > _floorY)
            {
                Vector3 p = transform.position;
                p.y -= SinkSpeed * Time.deltaTime;
                p.x += Mathf.Sin(Time.time * 2.1f + _wobbleSeed) * 0.012f * Time.deltaTime;
                transform.position = p;
            }
            _life -= Time.deltaTime;
            if (_life <= 0f) Destroy(gameObject);
        }

        public void Consume()
        {
            var g = GameManager.I;
            if (g != null && g.Personality != null) g.Personality.OnFed();
            if (g != null && g.Conversation != null && Random.value < 0.6f)
                g.Conversation.FishComment(CannedResponses.PickEating());
            Destroy(gameObject);
        }
    }
}
