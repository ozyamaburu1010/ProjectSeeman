using UnityEngine;

namespace Seeman
{
    public class FeedingSystem : MonoBehaviour
    {
        public Material PelletMaterial;
        float _cooldown;

        void Update() { _cooldown -= Time.deltaTime; }

        public void Feed()
        {
            if (_cooldown > 0f) return;
            _cooldown = 2.5f;

            var g = GameManager.I;
            Bounds b = g != null ? g.TankBounds : new Bounds(Vector3.up * 0.5f, new Vector3(2f, 0.8f, 1f));

            var pellet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pellet.name = "FoodPellet";
            Object.Destroy(pellet.GetComponent<Collider>());
            pellet.transform.localScale = Vector3.one * 0.03f;
            pellet.transform.position = new Vector3(
                Random.Range(b.min.x * 0.7f, b.max.x * 0.7f),
                b.max.y + 0.1f,
                Random.Range(b.min.z, b.max.z));
            if (PelletMaterial != null)
                pellet.GetComponent<MeshRenderer>().sharedMaterial = PelletMaterial;
            var fp = pellet.AddComponent<FoodPellet>();

            if (g != null && g.Fish != null) g.Fish.EatFood(fp);
        }
    }
}
