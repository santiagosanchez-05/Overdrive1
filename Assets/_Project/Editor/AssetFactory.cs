using System;
using Overdrive.Combat;
using Overdrive.Enemies;
using Overdrive.Player;
using UnityEditor;
using UnityEngine;

namespace Overdrive.EditorTools
{
    /// <summary>Crea o actualiza los materiales URP y los prefabs del juego.</summary>
    internal static class AssetFactory
    {
        public const string Root = "Assets/_Project";
        public const string MaterialsPath = Root + "/Materials";
        public const string PrefabsPath = Root + "/Prefabs";
        public const string PlayerPrefabPath = PrefabsPath + "/Player.prefab";
        public const string EnemyPrefabPath = PrefabsPath + "/Enemy.prefab";

        public const string MatFloor = "Floor";
        public const string MatWall = "Wall";
        public const string MatPlayer = "Player";
        public const string MatPlayerDetail = "PlayerDetail";
        public const string MatExit = "Exit";
        public const string MatEnemy = "Enemy";
        public const string MatEnemyDetail = "EnemyDetail";
        public const string MatEnemyDead = "EnemyDead";

        /// <summary>Nombre, color base e intensidad de emisión (0 = sin emisión).</summary>
        public static readonly (string name, Color color, float emission)[] MaterialDefs =
        {
            (MatFloor, new Color(0.45f, 0.08f, 0.50f), 0f),
            (MatWall, new Color(0.05f, 0.85f, 0.85f), 0.6f),
            (MatPlayer, new Color(1.00f, 0.85f, 0.10f), 0.3f),
            (MatPlayerDetail, Color.white, 0.5f),
            (MatExit, new Color(0.10f, 1.00f, 0.30f), 1.5f),
            (MatEnemy, new Color(1.00f, 0.15f, 0.25f), 0.4f),
            (MatEnemyDetail, new Color(0.15f, 0.05f, 0.05f), 0f),
            (MatEnemyDead, new Color(0.35f, 0.02f, 0.05f), 0f),
        };

        public static string MaterialPath(string name) => $"{MaterialsPath}/M_{name}.mat";

        public static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>(MaterialPath(name));

        public static void EnsureMaterials()
        {
            BuilderUtil.EnsureFolder(MaterialsPath);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("No se encontró el shader 'Universal Render Pipeline/Lit'.");

            foreach (var (name, color, emission) in MaterialDefs)
            {
                string path = MaterialPath(name);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                bool created = mat == null;
                if (created) mat = new Material(shader);

                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Smoothness", 0.15f);
                if (emission > 0f)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", color * emission);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                }
                else
                {
                    mat.DisableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", Color.black);
                }

                if (created) AssetDatabase.CreateAsset(mat, path);
                else EditorUtility.SetDirty(mat);
            }
        }

        public static GameObject EnsurePlayerPrefab()
        {
            BuilderUtil.EnsureFolder(PrefabsPath);
            return BuilderUtil.EditPrefab(PlayerPrefabPath, "Player", root =>
            {
                root.tag = "Player";

                var controller = BuilderUtil.EnsureComponent<CharacterController>(root);
                controller.center = new Vector3(0f, 0.9f, 0f);
                controller.height = 1.8f;
                controller.radius = 0.4f;
                controller.stepOffset = 0.3f;
                controller.skinWidth = 0.05f;

                var (body, nose) = EnsureCharacterBody(root, Mat(MatPlayer), Mat(MatPlayerDetail));

                var life = BuilderUtil.EnsureComponent<PlayerLife>(root);
                BuilderUtil.SetRef(life, "body", body.transform);

                var playerController = BuilderUtil.EnsureComponent<PlayerController>(root);
                BuilderUtil.SetRef(playerController, "controller", controller);
                BuilderUtil.SetRef(playerController, "life", life);

                var aim = BuilderUtil.EnsureComponent<PlayerAim>(root);
                BuilderUtil.SetRef(aim, "life", life);

                var finisher = BuilderUtil.EnsureComponent<PlayerFinisher>(root);
                BuilderUtil.SetRef(finisher, "controller", playerController);
                BuilderUtil.SetRef(finisher, "life", life);

                var melee = BuilderUtil.EnsureComponent<PlayerMelee>(root);
                BuilderUtil.SetRef(melee, "life", life);
                BuilderUtil.SetRef(melee, "finisher", finisher);
                BuilderUtil.SetRef(melee, "fist", nose.transform);
            });
        }

        public static GameObject EnsureEnemyPrefab()
        {
            BuilderUtil.EnsureFolder(PrefabsPath);
            return BuilderUtil.EditPrefab(EnemyPrefabPath, "Enemy", root =>
            {
                var collider = BuilderUtil.EnsureComponent<CapsuleCollider>(root);
                collider.center = new Vector3(0f, 0.9f, 0f);
                collider.height = 1.8f;
                collider.radius = 0.4f;

                var (body, _) = EnsureCharacterBody(root, Mat(MatEnemy), Mat(MatEnemyDetail));

                var enemy = BuilderUtil.EnsureComponent<Enemy>(root);
                BuilderUtil.SetRef(enemy, "body", body.transform);
                BuilderUtil.SetRef(enemy, "bodyCollider", collider);
                BuilderUtil.SetRef(enemy, "bodyRenderer", body.GetComponent<Renderer>());
                BuilderUtil.SetRef(enemy, "deadMaterial", Mat(MatEnemyDead));
            });
        }

        /// <summary>
        /// Cuerpo de cápsula con una "nariz" que indica hacia dónde mira.
        /// La nariz va dentro de Body para caer con él al ser derribado o morir.
        /// </summary>
        private static (GameObject body, GameObject nose) EnsureCharacterBody(GameObject root, Material bodyMat, Material noseMat)
        {
            Vector3 bodyScale = new Vector3(0.8f, 0.9f, 0.8f);
            GameObject body = BuilderUtil.EnsurePrimitiveChild(root.transform, "Body", PrimitiveType.Capsule, false);
            BuilderUtil.SetLocal(body, new Vector3(0f, 0.9f, 0f), bodyScale, Quaternion.identity);
            BuilderUtil.SetMaterial(body, bodyMat);

            GameObject nose = BuilderUtil.EnsurePrimitiveChild(body.transform, "Nose", PrimitiveType.Cube, false);
            BuilderUtil.SetLocal(nose,
                Divide(new Vector3(0f, 0.3f, 0.45f), bodyScale),
                Divide(new Vector3(0.2f, 0.2f, 0.4f), bodyScale),
                Quaternion.identity);
            BuilderUtil.SetMaterial(nose, noseMat);
            return (body, nose);
        }

        private static Vector3 Divide(Vector3 a, Vector3 b) => new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
    }
}
