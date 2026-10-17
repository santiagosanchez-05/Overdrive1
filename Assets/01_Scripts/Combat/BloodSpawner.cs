using Overdrive.Core;
using UnityEngine;

namespace Overdrive.Combat
{
    /// <summary>Manchas de sangre (quads rojos) en el suelo cuando alguien muere.</summary>
    public class BloodSpawner : MonoBehaviour, IRewindable
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

        /// <summary>Guarda cuántas manchas había; al rebobinar se borran las posteriores.</summary>
        public object Capture() => transform.childCount;

        public void Restore(object snapshot)
        {
            int keep = (int)snapshot;
            // Recorrido de longitud fija: en Play Mode Destroy() es diferido y childCount no baja hasta el final del fotograma.
            for (int i = transform.childCount - 1; i >= keep; i--) Remove(transform.GetChild(i).gameObject);
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
            // Borra las más viejas si hay demasiadas (longitud fija, ver Restore).
            int excess = transform.childCount - maxSplats;
            for (int i = excess - 1; i >= 0; i--) Remove(transform.GetChild(i).gameObject);
        }

        private void SpawnOne(Vector3 position, float size)
        {
            if (!GroundProbe.TrySurface(position, out var surface)) return;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, surface.normal)
                * Quaternion.Euler(90f, Random.Range(0f, 360f), 0f);
            // Reduce las manchas junto a un escalón para que no queden suspendidas sobre el piso inferior.
            bool fits = false;
            for (int attempt = 0; attempt < 6; attempt++)
            {
                if (FitsSurface(surface.point, rotation, size)) { fits = true; break; }
                size *= .6f;
            }
            if (!fits) return;
            // Cada mancha un poco más alta que la anterior para que no parpadeen al solaparse.
            position = surface.point + surface.normal * (0.01f + (spawned % 40) * 0.0005f);
            spawned++;
            GameObject splat = Instantiate(bloodPrefab, position, rotation, transform);
            splat.transform.localScale = Vector3.one * size;
        }

        private static bool FitsSurface(Vector3 center, Quaternion rotation, float size)
        {
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0) continue;
                    Vector3 corner = center + rotation * new Vector3(x * size * .5f, y * size * .5f, 0f);
                    if (!GroundProbe.TrySurface(corner, out var hit) || Mathf.Abs(hit.point.y - corner.y) > .06f)
                        return false;
                }
            return true;
        }

        /// <summary>
        /// Quita la mancha. Se desengancha primero para que deje de contar como hija ya mismo,
        /// aunque en Play Mode Destroy() no la borre hasta el final del fotograma.
        /// </summary>
        private static void Remove(GameObject go)
        {
            go.transform.SetParent(null, true);
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
    }
}
