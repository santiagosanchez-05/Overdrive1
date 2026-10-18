using System.Collections.Generic;
using System.Linq;
using Overdrive.Weapons;
using UnityEditor;
using UnityEngine;

namespace Overdrive.EditorTools
{
    /// <summary>
    /// Modelos 3D de las armas y de las balas (02_Modelos/Weapons). Cada modelo se coloca dentro del
    /// prefab con su textura, tumbado a lo largo de +Z local (hacia donde dispara o golpea) y escalado
    /// a la longitud de juego. Si falta un modelo, el arma conserva sus piezas de primitivas.
    /// </summary>
    internal static class WeaponModelFactory
    {
        public const string MeshName = "Mesh";

        public static string WeaponsPath => AssetFactory.NumberedFolder("Modelos", "02_Modelos") + "/Weapons";

        /// <summary>Cómo se reconoce la parte delantera del modelo.</summary>
        public enum Kind
        {
            /// <summary>Arma de fuego: la culata/empuñadura (la parte más alta) va detrás y hacia abajo.</summary>
            Gun,
            /// <summary>La punta es la parte más gruesa (bate).</summary>
            ThickTip,
            /// <summary>La punta es la parte más fina (cuchillo, bala).</summary>
            ThinTip,
        }

        /// <summary>Un modelo: archivo, textura, longitud en metros y giro extra por si el modelo viene al revés.</summary>
        public readonly struct Spec
        {
            public readonly string Fbx;
            public readonly string Texture;
            public readonly float Length;
            /// <summary>Giro que se aplica después de tumbar el modelo (p. ej. (0, 180, 0) si apunta hacia atrás).</summary>
            public readonly Vector3 Fix;
            /// <summary>Fracción del modelo que queda detrás del origen (la empuñadura en la mano). 0.5 = centrado.</summary>
            public readonly float Grip;
            public readonly Kind Shape;
            /// <summary>Alinear por el eje principal de la malla (modelos exportados en diagonal). Si es false, por su caja.</summary>
            public readonly bool Pca;

            public Spec(string fbx, string texture, float length, float grip, Kind shape, Vector3 fix = default, bool pca = true)
            {
                Fbx = fbx; Texture = texture; Length = length; Grip = grip; Shape = shape; Fix = fix; Pca = pca;
            }
        }

        // Los modelos del bate y el cuchillo se exportaron con nombre de Tripo: se identifican por su textura.
        private static readonly Dictionary<WeaponType, Spec> Weapons = new Dictionary<WeaponType, Spec>
        {
            { WeaponType.Bat, new Spec("tripo_convert_88b500fe-b66f-4f90-a6b0-3becf9f81350.fbx",
                "tripo_convert_88b500fe-b66f-4f90-a6b0-3becf9f81350.fbm/Bate_basecolor.PNG", 0.95f, 0.12f, Kind.ThickTip) },
            { WeaponType.Knife, new Spec("tripo_convert_5b9e13f7-c119-4491-8ffe-740d61fec89a.fbx",
                "tripo_convert_5b9e13f7-c119-4491-8ffe-740d61fec89a.fbm/Cuchillo_basecolor.PNG", 0.42f, 0.2f, Kind.ThinTip) },
            { WeaponType.Shotgun, new Spec("Escopeta.fbx", "escopeta_basecolor.PNG", 0.95f, 0.3f, Kind.Gun) },
            { WeaponType.Smg, new Spec("subfusil.fbx", "subfusil_basecolor.PNG", 0.6f, 0.45f, Kind.Gun, new Vector3(0f, 180f, 0f)) },
            { WeaponType.Revolver, new Spec("Revolver.fbx", "revolver_basecolor.PNG", 0.38f, 0.25f, Kind.Gun, default, false) },
        };

        private static readonly Dictionary<WeaponType, Spec> Bullets = new Dictionary<WeaponType, Spec>
        {
            { WeaponType.Shotgun, new Spec("BalaEscopeta.fbx", "Bala_Escopeta_basecolor.PNG", 0.22f, 0.5f, Kind.ThinTip) },
            { WeaponType.Smg, new Spec("BalaSubfusil.fbx", "Bala_Subfusil_basecolor.PNG", 0.25f, 0.5f, Kind.ThinTip) },
            { WeaponType.Revolver, new Spec("BalaRevolver.fbx", "Bala_Revolver_basecolor.PNG", 0.25f, 0.5f, Kind.ThinTip) },
        };

        /// <summary>
        /// Escala de juego: las longitudes de arriba son las reales, pero vistas desde la cámara cenital, en la mano
        /// de personajes de ~1.8 m, se pierden. Se exageran (como en los juegos de vista cenital) para que se lean bien.
        /// </summary>
        public const float WeaponGameScale = 1.6f * PlayerModelFactory.CharacterScale; // crecen con los personajes
        public const float BulletGameScale = 1.5f * PlayerModelFactory.CharacterScale;

        public static bool TryGetWeapon(WeaponType type, out Spec spec) =>
            TryGetScaled(Weapons, type, WeaponGameScale, out spec);

        public static bool TryGetBullet(WeaponType type, out Spec spec) =>
            TryGetScaled(Bullets, type, BulletGameScale, out spec);

        private static bool TryGetScaled(Dictionary<WeaponType, Spec> table, WeaponType type, float scale, out Spec spec)
        {
            if (!table.TryGetValue(type, out Spec raw) || !Available(raw))
            {
                spec = default;
                return false;
            }
            spec = new Spec(raw.Fbx, raw.Texture, raw.Length * scale, raw.Grip, raw.Shape, raw.Fix, raw.Pca);
            return true;
        }

        /// <summary>Longitud real del modelo (sin la escala de juego), para las comprobaciones.</summary>
        public static float RealLength(WeaponType type) => Weapons.TryGetValue(type, out Spec raw) ? raw.Length : 0f;

        private static bool Available(Spec spec) => AssetDatabase.LoadAssetAtPath<GameObject>($"{WeaponsPath}/{spec.Fbx}") != null;

        /// <summary>
        /// Deja en <paramref name="modelRoot"/> solo el modelo indicado (borra primitivas o modelos anteriores),
        /// con su material, apuntando a +Z y con la longitud pedida.
        /// </summary>
        /// <returns>La boca del arma (centro del extremo delantero) en el espacio de <paramref name="modelRoot"/>.</returns>
        public static Vector3 Apply(Transform modelRoot, Spec spec, string materialName, bool castShadows = true)
        {
            for (int i = modelRoot.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(modelRoot.GetChild(i).gameObject);
            modelRoot.localPosition = Vector3.zero;
            modelRoot.localRotation = Quaternion.identity;
            modelRoot.localScale = Vector3.one;

            string fbxPath = $"{WeaponsPath}/{spec.Fbx}";
            // Hay que leer los vértices para saber hacia dónde apunta el modelo.
            if (AssetImporter.GetAtPath(fbxPath) is ModelImporter importer && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            var mesh = (GameObject)PrefabUtility.InstantiatePrefab(source, modelRoot);
            PrefabUtility.UnpackPrefabInstance(mesh, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            mesh.name = MeshName;
            mesh.transform.localPosition = Vector3.zero;
            mesh.transform.localRotation = Quaternion.identity;
            mesh.transform.localScale = Vector3.one;

            foreach (Collider col in mesh.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
            foreach (Animator anim in mesh.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(anim);

            Material mat = EnsureMaterial(materialName, spec);
            foreach (Renderer r in mesh.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterials = Enumerable.Repeat(mat, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
                r.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // Los modelos vienen exportados en diagonal: se usa el eje principal real de la malla (PCA de los
            // vértices), no la caja que la envuelve.
            // 0) Tumbar: el eje más largo del arma pasa a ser +Z.
            List<Vector3> pts = Points(mesh.transform, modelRoot);
            Quaternion lie = spec.Pca ? Quaternion.FromToRotation(MainAxis(pts), Vector3.forward) : BoxLie(BoundsOf(pts).size);
            mesh.transform.localRotation = lie;

            // 1) Giro sobre Z: el segundo eje (el alto: empuñadura, cargador, culata) pasa a ser Y.
            pts = Points(mesh.transform, modelRoot);
            Vector2 height = MainAxis2D(pts);
            float angle = 90f - Mathf.Atan2(height.y, height.x) * Mathf.Rad2Deg;
            Quaternion roll = spec.Pca ? Quaternion.Euler(0f, 0f, angle) : Quaternion.identity;
            mesh.transform.localRotation = roll * lie;

            // 2) Delante / detrás: la punta es el extremo más fino (cañón, filo, bala) o el más grueso (bate).
            pts = Points(mesh.transform, modelRoot);
            Section front = Slice(pts, true), back = Slice(pts, false);
            bool tipIsBack = spec.Shape == Kind.ThickTip ? back.Area > front.Area : back.Area < front.Area;
            Quaternion turn = tipIsBack ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
            mesh.transform.localRotation = turn * roll * lie;
            roll = turn * roll; // a partir de aquí "roll" acumula los giros ya aplicados
            turn = Quaternion.identity;
            Bounds all, b;

            // 3) Arriba / abajo (armas de fuego): el cañón va pegado a la parte de arriba, y la empuñadura
            //    y el cargador cuelgan hacia abajo. Si sobresale más por arriba, está boca abajo.
            if (spec.Shape == Kind.Gun)
            {
                pts = Points(mesh.transform, modelRoot);
                all = BoundsOf(pts);
                front = Slice(pts, true);
                if (all.max.y - front.CenterY > front.CenterY - all.min.y) roll = Quaternion.Euler(0f, 0f, 180f) * roll;
            }
            mesh.transform.localRotation = Quaternion.Euler(spec.Fix) * roll * turn * lie;

            // Escalar a la longitud de juego y colocar la empuñadura en el origen.
            b = LocalBounds(mesh.transform, modelRoot);
            float scale = b.size.z > 0.0001f ? spec.Length / b.size.z : 1f;
            mesh.transform.localScale = Vector3.one * scale;
            b = LocalBounds(mesh.transform, modelRoot);
            Vector3 anchor = new Vector3(b.center.x, b.center.y, b.min.z + b.size.z * spec.Grip);
            mesh.transform.localPosition -= anchor;

            // Boca: el centro del tercio delantero, en la punta.
            List<Vector3> tipPts = Points(mesh.transform, modelRoot);
            Bounds tipAll = BoundsOf(tipPts);
            float cut = tipAll.max.z - tipAll.size.z * 0.12f;
            Bounds tip = BoundsOf(tipPts.Where(p => p.z >= cut).ToList());
            return new Vector3(tip.center.x, tip.center.y, tipAll.max.z);
        }

        private readonly struct Section
        {
            public readonly float Width, Height, Area, CenterY;
            public Section(Bounds b) { Width = b.size.x; Height = b.size.y; Area = b.size.x * b.size.y; CenterY = b.center.y; }
        }

        /// <summary>Tercio delantero (+Z) o trasero del modelo.</summary>
        private static Section Slice(List<Vector3> pts, bool front)
        {
            Bounds all = BoundsOf(pts);
            float cut = all.size.z / 3f;
            var part = pts.Where(p => front ? p.z >= all.max.z - cut : p.z <= all.min.z + cut).ToList();
            return new Section(part.Count > 0 ? BoundsOf(part) : all);
        }

        /// <summary>Tumba el modelo para que el lado más largo de su caja quede en Z.</summary>
        private static Quaternion BoxLie(Vector3 size)
        {
            if (size.x >= size.y && size.x >= size.z) return Quaternion.Euler(0f, -90f, 0f);
            if (size.y >= size.x && size.y >= size.z) return Quaternion.Euler(90f, 0f, 0f);
            return Quaternion.identity;
        }

        /// <summary>Eje de mayor dispersión de los puntos (vector propio principal de la covarianza).</summary>
        private static Vector3 MainAxis(List<Vector3> pts)
        {
            Vector3 c = Vector3.zero;
            foreach (Vector3 p in pts) c += p;
            c /= Mathf.Max(1, pts.Count);
            float xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
            foreach (Vector3 q in pts)
            {
                Vector3 d = q - c;
                xx += d.x * d.x; xy += d.x * d.y; xz += d.x * d.z;
                yy += d.y * d.y; yz += d.y * d.z; zz += d.z * d.z;
            }
            Vector3 v = new Vector3(1f, 0.7f, 0.4f).normalized;
            for (int i = 0; i < 64; i++)
            {
                v = new Vector3(xx * v.x + xy * v.y + xz * v.z, xy * v.x + yy * v.y + yz * v.z, xz * v.x + yz * v.y + zz * v.z);
                if (v.sqrMagnitude < 1e-12f) return Vector3.forward;
                v.Normalize();
            }
            return v;
        }

        /// <summary>Eje de mayor dispersión en el plano XY.</summary>
        private static Vector2 MainAxis2D(List<Vector3> pts)
        {
            Vector2 c = Vector2.zero;
            foreach (Vector3 p in pts) c += new Vector2(p.x, p.y);
            c /= Mathf.Max(1, pts.Count);
            float xx = 0, xy = 0, yy = 0;
            foreach (Vector3 p in pts)
            {
                float dx = p.x - c.x, dy = p.y - c.y;
                xx += dx * dx; xy += dx * dy; yy += dy * dy;
            }
            float theta = 0.5f * Mathf.Atan2(2f * xy, xx - yy);
            return new Vector2(Mathf.Cos(theta), Mathf.Sin(theta));
        }

        private static Bounds BoundsOf(List<Vector3> pts)
        {
            if (pts.Count == 0) return new Bounds();
            var b = new Bounds(pts[0], Vector3.zero);
            foreach (Vector3 p in pts) b.Encapsulate(p);
            return b;
        }

        /// <summary>Vértices del modelo (muestreados) en el espacio de <paramref name="space"/>.</summary>
        private static List<Vector3> Points(Transform mesh, Transform space)
        {
            var pts = new List<Vector3>();
            void Add(Mesh m, Transform t)
            {
                if (m == null || !m.isReadable) return;
                Vector3[] v = m.vertices;
                int step = Mathf.Max(1, v.Length / 4000);
                for (int i = 0; i < v.Length; i += step) pts.Add(space.InverseTransformPoint(t.TransformPoint(v[i])));
            }
            foreach (MeshFilter mf in mesh.GetComponentsInChildren<MeshFilter>(true)) Add(mf.sharedMesh, mf.transform);
            foreach (SkinnedMeshRenderer smr in mesh.GetComponentsInChildren<SkinnedMeshRenderer>(true)) Add(smr.sharedMesh, smr.transform);
            return pts;
        }

        private static Bounds LocalBounds(Transform mesh, Transform space)
        {
            var bounds = new Bounds();
            bool first = true;
            foreach (MeshFilter mf in mesh.GetComponentsInChildren<MeshFilter>(true))
                Encapsulate(mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(), mf.transform, space, ref bounds, ref first);
            foreach (SkinnedMeshRenderer smr in mesh.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                Encapsulate(smr.sharedMesh != null ? smr.sharedMesh.bounds : new Bounds(), smr.transform, space, ref bounds, ref first);
            return bounds;
        }

        private static void Encapsulate(Bounds local, Transform from, Transform space, ref Bounds bounds, ref bool first)
        {
            Vector3 c = local.center, e = local.extents;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(i % 2 == 0 ? -e.x : e.x, (i / 2) % 2 == 0 ? -e.y : e.y, i < 4 ? -e.z : e.z) + c;
                Vector3 p = space.InverseTransformPoint(from.TransformPoint(corner));
                if (first) { bounds = new Bounds(p, Vector3.zero); first = false; }
                else bounds.Encapsulate(p);
            }
        }

        /// <summary>Material URP con la textura del modelo y algo de luz propia (el nivel es muy oscuro).</summary>
        private static Material EnsureMaterial(string name, Spec spec)
        {
            string path = $"{AssetFactory.MaterialsPath}/M_{name}.mat";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>($"{WeaponsPath}/{spec.Texture}");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = mat == null;
            if (created) mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetTexture("_BaseMap", texture);
            mat.SetTexture("_MainTex", texture);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.3f);
            mat.EnableKeyword("_EMISSION");
            mat.SetTexture("_EmissionMap", texture);
            mat.SetColor("_EmissionColor", PlayerModelFactory.ModelEmission);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (created) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
