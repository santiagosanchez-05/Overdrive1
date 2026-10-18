using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Overdrive.EditorTools
{
    /// <summary>
    /// Modelo 3D del maletín (02_Modelos/Props/Briefcase.fbx con su textura): sustituye a los cubos del prefab,
    /// apoyado en el suelo, con el lado largo de frente y un poco de brillo dorado para que se vea como objetivo.
    /// </summary>
    internal static class BriefcaseModel
    {
        public const string ModelName = "Model";
        /// <summary>Largo del maletín en metros (un poco más grande que el real, como los personajes).</summary>
        public const float Length = 0.7f;
        public static string PropsPath => AssetFactory.NumberedFolder("Modelos", "02_Modelos") + "/Props";
        public static string FbxPath => PropsPath + "/Briefcase.fbx";
        public static string TexturePath => PropsPath + "/Briefcase_basecolor.PNG";

        public static bool Available => AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath) != null;

        /// <summary>Pone el modelo en la raíz del prefab (quitando los cubos). Falso si no está el FBX.</summary>
        public static bool Apply(GameObject root)
        {
            if (!Available) return false;
            // Malla legible antes de instanciarla: reimportar con el modelo ya puesto deshace su nombre.
            if (AssetImporter.GetAtPath(FbxPath) is ModelImporter importer && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
            }
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            foreach (string old in new[] { "Body", "Handle", ModelName })
            {
                Transform t = root.transform.Find(old);
                if (t != null) Object.DestroyImmediate(t.gameObject);
            }

            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
            model.name = ModelName;
            // Lado largo en X (de frente en la vista cenital), en el suelo y centrado.
            VehicleFactory.FitModel(model.transform, new VehicleFactory.Spec("Briefcase.fbx", null, Length, false), 1f);
            model.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * model.transform.localRotation;
            model.transform.localPosition = Quaternion.Euler(0f, 90f, 0f) * model.transform.localPosition;

            Material mat = EnsureMaterial();
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterials = Enumerable.Repeat(mat, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            foreach (Collider c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            model.name = ModelName;
            return true;
        }

        private static Material EnsureMaterial()
        {
            string path = $"{AssetFactory.MaterialsPath}/M_BriefcaseModel.mat";
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = mat == null;
            if (created) mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetTexture("_BaseMap", texture);
            mat.SetTexture("_MainTex", texture);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Metallic", 0.35f);
            mat.SetFloat("_Smoothness", 0.6f);
            // Brillo suave con su propia textura: se distingue en los niveles oscuros sin perder el detalle.
            mat.EnableKeyword("_EMISSION");
            mat.SetTexture("_EmissionMap", texture);
            mat.SetColor("_EmissionColor", new Color(1f, 0.85f, 0.45f) * 0.45f);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (created) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
