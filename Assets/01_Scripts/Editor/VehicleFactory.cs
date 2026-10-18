using System.Collections.Generic;
using System.Linq;
using Overdrive.Driving;
using UnityEditor;
using UnityEngine;

namespace Overdrive.EditorTools
{
    /// <summary>
    /// Coches del nivel 4: el del jugador (con Nina) y las Hilux de los dealers (normal y blindada), a partir de los
    /// modelos de 02_Modelos/Vehicles. El modelo se tumba con su eje largo hacia +Z, se le da la vuelta si mira hacia
    /// atrás (se reconoce por dónde queda la cabina) y se escala a la longitud de un coche real.
    /// </summary>
    internal static class VehicleFactory
    {
        public static string VehiclesPath => AssetFactory.NumberedFolder("Modelos", "02_Modelos") + "/Vehicles";
        public static string PlayerCarPrefabPath => AssetFactory.PrefabsPath + "/PlayerCar.prefab";
        public static string DealerPrefabPath => AssetFactory.PrefabsPath + "/DealerHilux.prefab";
        public static string ArmoredPrefabPath => AssetFactory.PrefabsPath + "/DealerHiluxBlindada.prefab";
        public const string ModelName = "Model";

        /// <summary>Un vehículo: archivo, textura, largo en metros y si su cabina está en la mitad delantera (pickup).</summary>
        public readonly struct Spec
        {
            public readonly string Fbx, Texture;
            public readonly float Length;
            public readonly bool CabinTowardFront;

            public Spec(string fbx, string texture, float length, bool cabinTowardFront)
            {
                Fbx = fbx; Texture = texture; Length = length; CabinTowardFront = cabinTowardFront;
            }
        }

        // Personajes ~1.3 veces más grandes que en la realidad: los coches, a juego.
        public static readonly Spec PlayerCar = new Spec("PlayerCar.fbx", "PlayerCar_basecolor.PNG", 4.6f * PlayerModelFactory.CharacterScale, false);
        public static readonly Spec Hilux = new Spec("Hilux.fbx", "Hilux_basecolor.PNG", 5.3f * PlayerModelFactory.CharacterScale, true);

        public static bool Available => File(PlayerCar) != null && File(Hilux) != null;
        private static GameObject File(Spec s) => AssetDatabase.LoadAssetAtPath<GameObject>($"{VehiclesPath}/{s.Fbx}");

        public static void EnsurePrefabs()
        {
            if (!Available) return;
            Material explosion = EnsureFlatMaterial("Explosion", new Color(1f, 0.55f, 0.15f), 4f);
            Material wreck = EnsureFlatMaterial("CarWreck", new Color(0.06f, 0.05f, 0.05f), 0f);
            Material playerMat = EnsureCarMaterial("PlayerCar", PlayerCar, Color.white);
            Material hiluxMat = EnsureCarMaterial("Hilux", Hilux, Color.white);
            Material armoredMat = EnsureCarMaterial("HiluxBlindada", Hilux, new Color(0.45f, 0.2f, 0.22f));

            // Al jugador no le sirve embestir (las camionetas se destruyen a tiros); los volantazos de los dealers sí le hacen daño.
            EnsureCar(PlayerCarPrefabPath, "PlayerCar", PlayerCar, playerMat, true, 1f, 140f, 0f, 34f, explosion, wreck);
            EnsureCar(DealerPrefabPath, "DealerHilux", Hilux, hiluxMat, false, 1f, 60f, 3.5f, 33f, explosion, wreck);
            EnsureCar(ArmoredPrefabPath, "DealerHiluxBlindada", Hilux, armoredMat, false, 1.15f, 260f, 5f, 33f, explosion, wreck);
        }

        private static void EnsureCar(string path, string name, Spec spec, Material mat, bool player, float sizeMul,
            float health, float ramDamage, float maxSpeed, Material explosion, Material wreck)
        {
            BuilderUtil.EnsureFolder(AssetFactory.PrefabsPath);
            BuilderUtil.EditPrefab(path, name, root =>
            {
                root.tag = player ? "Player" : "Untagged";
                Transform existing = root.transform.Find(ModelName);
                if (existing != null) Object.DestroyImmediate(existing.gameObject);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(File(spec), root.transform);
                model.name = ModelName;
                Bounds size = FitModel(model.transform, spec, sizeMul);
                foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
                {
                    r.sharedMaterials = Enumerable.Repeat(mat, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }
                foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

                var rb = BuilderUtil.EnsureComponent<Rigidbody>(root);
                rb.mass = player ? 1400f : 1700f * sizeMul;
                rb.useGravity = false;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                // Sin física: lo mueve RailCar sobre la carretera (los choques solo lo desplazan un poco de lado).
                rb.isKinematic = true;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                rb.constraints = RigidbodyConstraints.None;
                rb.linearDamping = 0f;
                rb.angularDamping = 0.5f;

                var box = BuilderUtil.EnsureComponent<BoxCollider>(root);
                box.center = new Vector3(size.center.x, size.size.y * 0.5f, size.center.z);
                box.size = new Vector3(size.size.x * 0.95f, size.size.y, size.size.z * 0.97f);

                var car = BuilderUtil.EnsureComponent<Car>(root);
                var rail = BuilderUtil.EnsureComponent<RailCar>(root);
                rail.SetSize(box.size.x, box.size.z);
                EditorUtility.SetDirty(rail);
                var so = new SerializedObject(car);
                so.FindProperty("isPlayer").boolValue = player;
                so.FindProperty("maxHealth").floatValue = health;
                so.FindProperty("ramDamagePerMps").floatValue = ramDamage;
                so.FindProperty("maxSpeed").floatValue = maxSpeed;
                so.FindProperty("explosionMaterial").objectReferenceValue = explosion;
                so.FindProperty("wreckMaterial").objectReferenceValue = wreck;
                SerializedProperty renderers = so.FindProperty("bodyRenderers");
                Renderer[] rs = model.GetComponentsInChildren<Renderer>(true);
                renderers.arraySize = rs.Length;
                for (int i = 0; i < rs.Length; i++) renderers.GetArrayElementAtIndex(i).objectReferenceValue = rs[i];
                so.ApplyModifiedPropertiesWithoutUndo();

                if (player)
                {
                    BuilderUtil.SetRef(BuilderUtil.EnsureComponent<PlayerCarInput>(root), "car", car);
                    Object.DestroyImmediate(root.GetComponent<EnemyCarAI>());
                    var gun = BuilderUtil.EnsureComponent<CarGun>(root);
                    BuilderUtil.SetRef(gun, "car", car);
                    BuilderUtil.SetRef(gun, "tracerMaterial", EnsureFlatMaterial("Tracer", new Color(1f, 0.85f, 0.4f), 3f));
                }
                else
                {
                    var ai = BuilderUtil.EnsureComponent<EnemyCarAI>(root);
                    BuilderUtil.SetRef(ai, "car", car);
                    var aiSo = new SerializedObject(ai);
                    aiSo.FindProperty("hitDamage").floatValue = sizeMul > 1f ? 20f : 12f;
                    aiSo.FindProperty("push").floatValue = sizeMul > 1f ? 9f : 7f;
                    aiSo.ApplyModifiedPropertiesWithoutUndo();
                    Object.DestroyImmediate(root.GetComponent<PlayerCarInput>());
                    Object.DestroyImmediate(root.GetComponent<CarGun>());
                }

                // Faros: de noche se ve venir a cada coche (y el del jugador alumbra la carretera).
                float front = size.max.z, half = size.size.x * 0.32f, h = size.size.y * 0.45f;
                EnsureLight(root.transform, "Headlight_L", new Vector3(-half, h, front), player ? 2.6f : 1.6f, new Color(1f, 0.95f, 0.8f), true);
                EnsureLight(root.transform, "Headlight_R", new Vector3(half, h, front), player ? 2.6f : 1.6f, new Color(1f, 0.95f, 0.8f), true);
                EnsureLight(root.transform, "TailLight", new Vector3(0f, h, size.min.z - 0.2f), 1.2f, player ? new Color(1f, 0.2f, 0.6f) : new Color(1f, 0.08f, 0.08f), false);
            });
        }

        private static void EnsureLight(Transform root, string name, Vector3 pos, float intensity, Color color, bool spot)
        {
            GameObject go = BuilderUtil.EnsureChild(root, name);
            BuilderUtil.SetLocal(go, pos, Vector3.one, spot ? Quaternion.Euler(12f, 0f, 0f) : Quaternion.identity);
            var light = BuilderUtil.EnsureComponent<Light>(go);
            light.type = spot ? LightType.Spot : LightType.Point;
            light.spotAngle = 60f;
            light.range = spot ? 22f : 5f;
            light.intensity = intensity;
            light.color = color;
            light.shadows = LightShadows.None;
        }

        /// <summary>
        /// Tumba el modelo con el eje largo hacia +Z, lo gira si mira al revés, lo escala a su longitud y lo apoya en el
        /// suelo con el centro en el origen. Devuelve su caja en el espacio de la raíz.
        /// </summary>
        public static Bounds FitModel(Transform model, Spec spec, float sizeMul)
        {
            string path = AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(model.gameObject));
            if (AssetImporter.GetAtPath(path) is ModelImporter importer && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
            // Se conservan el giro y la escala de importación de la raíz del FBX (p. ej. de Z arriba a Y arriba).
            Quaternion baseRotation = model.localRotation;
            Vector3 baseScale = model.localScale;
            model.localPosition = Vector3.zero;

            Bounds b = BoundsIn(model.parent, model);
            // Eje largo horizontal hacia Z.
            if (b.size.x > b.size.z) model.localRotation = Quaternion.Euler(0f, 90f, 0f) * baseRotation;
            if (CabinOffset(model) > 0f != spec.CabinTowardFront) model.localRotation = Quaternion.Euler(0f, 180f, 0f) * model.localRotation;

            b = BoundsIn(model.parent, model);
            float scale = spec.Length * sizeMul / Mathf.Max(b.size.z, 0.01f);
            model.localScale = baseScale * scale;
            b = BoundsIn(model.parent, model);
            model.localPosition = new Vector3(-b.center.x, -b.min.y, -b.center.z);
            return BoundsIn(model.parent, model);
        }

        /// <summary>
        /// Dónde está la cabina respecto al centro del coche (en Z): la zona más alta del modelo.
        /// Positivo = hacia +Z. Así se sabe hacia dónde mira (sedán: cabina atrás; pickup: cabina delante).
        /// </summary>
        public static float CabinOffset(Transform model)
        {
            List<Vector3> pts = Points(model, model.parent);
            if (pts.Count == 0) return 0f;
            float minZ = pts.Min(p => p.z), maxZ = pts.Max(p => p.z), top = pts.Max(p => p.y), minY = pts.Min(p => p.y);
            float cut = minY + (top - minY) * 0.85f; // el 15 % más alto: el techo
            var roof = pts.Where(p => p.y >= cut).ToList();
            return roof.Average(p => p.z) - (minZ + maxZ) * 0.5f;
        }

        private static List<Vector3> Points(Transform model, Transform space)
        {
            var pts = new List<Vector3>();
            foreach (MeshFilter mf in model.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                Vector3[] v = mf.sharedMesh.vertices;
                int step = Mathf.Max(1, v.Length / 6000);
                for (int i = 0; i < v.Length; i += step) pts.Add(space.InverseTransformPoint(mf.transform.TransformPoint(v[i])));
            }
            foreach (SkinnedMeshRenderer sk in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (sk.sharedMesh == null || !sk.sharedMesh.isReadable) continue;
                Vector3[] v = sk.sharedMesh.vertices;
                int step = Mathf.Max(1, v.Length / 6000);
                for (int i = 0; i < v.Length; i += step) pts.Add(space.InverseTransformPoint(sk.transform.TransformPoint(v[i])));
            }
            return pts;
        }

        private static Bounds BoundsIn(Transform space, Transform model)
        {
            List<Vector3> pts = Points(model, space);
            if (pts.Count == 0) return new Bounds(Vector3.zero, Vector3.one);
            var b = new Bounds(pts[0], Vector3.zero);
            foreach (Vector3 p in pts) b.Encapsulate(p);
            return b;
        }

        private static Material EnsureCarMaterial(string name, Spec spec, Color tint)
        {
            string path = $"{AssetFactory.MaterialsPath}/M_{name}.mat";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{VehiclesPath}/{spec.Texture}");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = mat == null;
            if (created) mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetTexture("_BaseMap", texture);
            mat.SetTexture("_MainTex", texture);
            mat.SetColor("_BaseColor", tint);
            mat.SetFloat("_Metallic", 0.3f);
            mat.SetFloat("_Smoothness", 0.55f);
            mat.EnableKeyword("_EMISSION");
            mat.SetTexture("_EmissionMap", texture);
            mat.SetColor("_EmissionColor", tint * 0.25f);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (created) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);
            return mat;
        }

        public static Material EnsureFlatMaterial(string name, Color color, float emission)
        {
            string path = $"{AssetFactory.MaterialsPath}/M_{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = mat == null;
            if (created) mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.2f);
            if (emission > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * emission);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
            if (created) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
