using System;
using Overdrive.Combat;
using Overdrive.Enemies;
using Overdrive.Player;
using Overdrive.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

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
        public const string BulletPrefabPath = PrefabsPath + "/Bullet.prefab";
        public const string BloodPrefabPath = PrefabsPath + "/Blood.prefab";
        public const string HandName = "Hand";

        public static string WeaponPrefabPath(WeaponType type) => $"{PrefabsPath}/Weapon_{type}.prefab";

        public const string MatFloor = "Floor";
        public const string MatWall = "Wall";
        public const string MatPlayer = "Player";
        public const string MatPlayerDetail = "PlayerDetail";
        public const string MatExit = "Exit";
        public const string MatEnemy = "Enemy";
        public const string MatEnemyDetail = "EnemyDetail";
        public const string MatEnemyDead = "EnemyDead";
        public const string MatWeaponMelee = "WeaponMelee";
        public const string MatWeaponGun = "WeaponGun";
        public const string MatWeaponDark = "WeaponDark";
        public const string MatBlade = "Blade";
        public const string MatBullet = "Bullet";
        public const string MatBlood = "Blood";
        public const string MatExitLocked = "ExitLocked";

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
            (MatWeaponMelee, new Color(1.00f, 0.50f, 0.10f), 0.8f),
            (MatWeaponGun, new Color(0.55f, 0.80f, 1.00f), 0.8f),
            (MatWeaponDark, new Color(0.12f, 0.12f, 0.15f), 0f),
            (MatBlade, new Color(0.85f, 0.90f, 1.00f), 0.6f),
            (MatBullet, new Color(1.00f, 0.90f, 0.30f), 4f),
            (MatBlood, new Color(0.65f, 0.00f, 0.03f), 0.25f),
            (MatExitLocked, new Color(0.10f, 0.30f, 0.15f), 0.2f),
        };

        public static GameObject EnsureBloodPrefab()
        {
            BuilderUtil.EnsureFolder(PrefabsPath);
            return BuilderUtil.EditPrefab(BloodPrefabPath, "Blood", root =>
            {
                // El quad mira hacia -Z; el BloodSpawner gira la raíz 90° en X para tumbarlo en el suelo.
                GameObject quad = BuilderUtil.EnsurePrimitiveChild(root.transform, "Quad", PrimitiveType.Quad, false);
                BuilderUtil.SetLocal(quad, Vector3.zero, Vector3.one, Quaternion.identity);
                BuilderUtil.SetMaterial(quad, Mat(MatBlood));
                var renderer = quad.GetComponent<Renderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = true;
            });
        }

        /// <summary>Pieza visual de un arma: forma, posición, escala, rotación y material.</summary>
        private readonly struct Part
        {
            public readonly string Name;
            public readonly PrimitiveType Shape;
            public readonly Vector3 Position, Scale, Euler;
            public readonly string Material;

            public Part(string name, PrimitiveType shape, Vector3 position, Vector3 scale, Vector3 euler, string material)
            {
                Name = name; Shape = shape; Position = position; Scale = scale; Euler = euler; Material = material;
            }
        }

        // Todas las armas apuntan hacia +Z local. Los cilindros se tumban 90° en X.
        private static Part[] WeaponParts(WeaponType type)
        {
            Vector3 lie = new Vector3(90f, 0f, 0f);
            switch (type)
            {
                case WeaponType.Bat:
                    return new[]
                    {
                        new Part("Handle", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.15f), new Vector3(0.07f, 0.18f, 0.07f), lie, MatWeaponDark),
                        new Part("Barrel", PrimitiveType.Cylinder, new Vector3(0f, 0f, 0.6f), new Vector3(0.13f, 0.28f, 0.13f), lie, MatWeaponMelee),
                    };
                case WeaponType.Knife:
                    return new[]
                    {
                        new Part("Handle", PrimitiveType.Cube, new Vector3(0f, 0f, 0.05f), new Vector3(0.06f, 0.06f, 0.16f), Vector3.zero, MatWeaponMelee),
                        new Part("Blade", PrimitiveType.Cube, new Vector3(0f, 0f, 0.27f), new Vector3(0.05f, 0.03f, 0.3f), Vector3.zero, MatBlade),
                    };
                case WeaponType.Shotgun:
                    return new[]
                    {
                        new Part("Stock", PrimitiveType.Cube, new Vector3(0f, 0f, 0f), new Vector3(0.11f, 0.13f, 0.32f), Vector3.zero, MatWeaponDark),
                        new Part("Barrel", PrimitiveType.Cube, new Vector3(0f, 0.02f, 0.45f), new Vector3(0.09f, 0.09f, 0.6f), Vector3.zero, MatWeaponGun),
                    };
                case WeaponType.Smg:
                    return new[]
                    {
                        new Part("Body", PrimitiveType.Cube, new Vector3(0f, 0f, 0.1f), new Vector3(0.1f, 0.16f, 0.36f), Vector3.zero, MatWeaponGun),
                        new Part("Mag", PrimitiveType.Cube, new Vector3(0f, -0.13f, 0.12f), new Vector3(0.06f, 0.16f, 0.08f), Vector3.zero, MatWeaponDark),
                        new Part("Barrel", PrimitiveType.Cube, new Vector3(0f, 0.03f, 0.37f), new Vector3(0.05f, 0.05f, 0.2f), Vector3.zero, MatWeaponDark),
                    };
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        public static WeaponType[] AllWeaponTypes => (WeaponType[])Enum.GetValues(typeof(WeaponType));

        public static GameObject EnsureBulletPrefab()
        {
            BuilderUtil.EnsureFolder(PrefabsPath);
            return BuilderUtil.EditPrefab(BulletPrefabPath, "Bullet", root =>
            {
                BuilderUtil.EnsureComponent<Projectile>(root);
                GameObject model = BuilderUtil.EnsurePrimitiveChild(root.transform, "Model", PrimitiveType.Cube, false);
                BuilderUtil.SetLocal(model, Vector3.zero, new Vector3(0.1f, 0.1f, 0.45f), Quaternion.identity);
                BuilderUtil.SetMaterial(model, Mat(MatBullet));
                model.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            });
        }

        public static void EnsureWeaponPrefabs()
        {
            BuilderUtil.EnsureFolder(PrefabsPath);
            var bullet = AssetDatabase.LoadAssetAtPath<GameObject>(BulletPrefabPath).GetComponent<Projectile>();
            foreach (WeaponType type in AllWeaponTypes)
            {
                BuilderUtil.EditPrefab(WeaponPrefabPath(type), $"Weapon_{type}", root =>
                {
                    var item = BuilderUtil.EnsureComponent<WeaponItem>(root);
                    var so = new SerializedObject(item);
                    so.FindProperty("type").enumValueIndex = (int)type;
                    so.FindProperty("mode").enumValueIndex = (int)WeaponItem.Mode.Ground;
                    so.FindProperty("projectilePrefab").objectReferenceValue =
                        WeaponDefinition.Get(type).IsMelee ? null : bullet;
                    so.ApplyModifiedPropertiesWithoutUndo();

                    GameObject model = BuilderUtil.EnsureChild(root.transform, "Model");
                    foreach (Part part in WeaponParts(type))
                    {
                        GameObject go = BuilderUtil.EnsurePrimitiveChild(model.transform, part.Name, part.Shape, false);
                        BuilderUtil.SetLocal(go, part.Position, part.Scale, Quaternion.Euler(part.Euler));
                        BuilderUtil.SetMaterial(go, Mat(part.Material));
                    }
                });
            }
        }

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
                BuilderUtil.SetRef(melee, "fist", nose.transform);

                GameObject hand = EnsureHand(root);
                var weapons = BuilderUtil.EnsureComponent<PlayerWeapons>(root);
                BuilderUtil.SetRef(weapons, "hand", hand.transform);
                BuilderUtil.SetRef(weapons, "life", life);
                BuilderUtil.SetRef(weapons, "finisher", finisher);
                BuilderUtil.SetRef(weapons, "melee", melee);
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
                BuilderUtil.SetRef(enemy, "hand", EnsureHand(root).transform);

                var agent = BuilderUtil.EnsureComponent<NavMeshAgent>(root);
                agent.radius = 0.4f;
                agent.height = 1.8f;
                agent.baseOffset = 0f;
                agent.speed = 5f;
                agent.angularSpeed = 720f;
                agent.acceleration = 30f;
                agent.stoppingDistance = 0f;
                agent.autoBraking = true;

                var ai = BuilderUtil.EnsureComponent<EnemyAI>(root);
                BuilderUtil.SetRef(ai, "enemy", enemy);
                BuilderUtil.SetRef(ai, "agent", agent);
            });
        }

        /// <summary>Punto donde se sostiene el arma: a la derecha y delante del cuerpo.</summary>
        private static GameObject EnsureHand(GameObject root)
        {
            GameObject hand = BuilderUtil.EnsureChild(root.transform, HandName);
            BuilderUtil.SetLocal(hand, new Vector3(0.38f, 1.0f, 0.35f), Vector3.one, Quaternion.identity);
            return hand;
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
