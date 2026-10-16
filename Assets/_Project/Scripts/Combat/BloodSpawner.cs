using UnityEngine;

namespace Overdrive.Combat
{
    /// <summary>Manchas de sangre (quads rojos) en el suelo cuando alguien muere.</summary>
    public class BloodSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject bloodPrefab;
        [SerializeField] private int maxSplats = 200;
        [SerializeField] private float spread = 0.6f;

        private static BloodSpawner instance;
        private int spawned;

        public GameObject BloodPrefab => bloodPrefab;
        public int SplatCount => transform.childCount;

        public static BloodSpawner Instance
        {
            get
            {
                if (instance == null) instance = FindAnyObjectByType<BloodSpawner>();
                return instance;
            }
        }

        /// <summary>Mancha grande y un par de salpicaduras alrededor del punto.</summary>
        public static void SpawnAt(Vector3 position, float size = 1f)
        {
            BloodSpawner spawner = Instance;
            if (spawner != null) spawner.Spawn(position, size);
        }

        public void Spawn(Vector3 position, float size)
        {
            if (bloodPrefab == null) return;
            SpawnOne(position, size * Random.Range(1f, 1.4f));
            for (int i = 0; i < 2; i++)
            {
                Vector2 offset = Random.insideUnitCircle * spread * size;
                SpawnOne(position + new Vector3(offset.x, 0f, offset.y), size * Random.Range(0.3f, 0.6f));
            }
            while (transform.childCount > maxSplats) Remove(transform.GetChild(0).gameObject);
        }

        private void SpawnOne(Vector3 position, float size)
        {
            // Cada mancha un poco más alta que la anterior para que no parpadeen al solaparse.
            position.y = 0.01f + (spawned % 40) * 0.0005f;
            spawned++;
            GameObject splat = Instantiate(bloodPrefab, position, Quaternion.Euler(90f, Random.Range(0f, 360f), 0f), transform);
            splat.transform.localScale = Vector3.one * size;
        }

        private static void Remove(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
    }
}
