using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Overdrive.Level;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Overdrive.EditorTools
{
    /// <summary>
    /// Techos para la primera persona. En vista cenital estorbarían a la cámara, así que cada techo es un objeto
    /// apagado (grupo "Roof") que <see cref="FirstPersonRoof"/> enciende al pasar a primera persona (V).
    /// Los techos no tienen colliders ni proyectan sombra: no cambian la física, el NavMesh ni la luz de los niveles.
    /// Nivel 1: losas sobre la planta baja y sobre el piso 1, con tramo inclinado sobre la escalera.
    /// Nivel 2: los techos del club y sus paredes a altura completa (en cenital se quitaron o recortaron).
    /// Nivel 3: una losa sobre el loft.
    /// </summary>
    internal static class RoofFactory
    {
        public const string RootName = "Roof";
        public const string ControllerName = "Roof Controller";
        public const float Thickness = 0.2f;
        private const string Level03LoftName = "Scene Level03";

        [MenuItem("TopDown/Techos/Agregar techos de primera persona a los 3 niveles")]
        public static void AddAll()
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[Techos] Detén Play Mode primero."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { Debug.LogWarning("[Techos] Cancelado: no se guardaron los cambios de la escena abierta."); return; }
            Debug.Log("[Techos] Empezando…");
            var steps = new (string path, Action<Scene> build)[]
            {
                (SceneFactory.ScenePath, s =>
                {
                    SceneFactory.RefreshLedCables(s); // incluye los cables nuevos por el borde de las paredes
                    BuildLevel01(s, Level01Map.Create());
                }),
                (Level02Builder.ScenePath, BuildLevel02),
                (Level03Builder.ScenePath, BuildLevel03),
            };
            foreach (var (path, build) in steps)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null) continue;
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                build(scene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        [MenuItem("TopDown/Nivel 01/Agregar techos (primera persona)")]
        public static void AddLevel01() => AddOne(SceneFactory.ScenePath, s => BuildLevel01(s, Level01Map.Create()));

        [MenuItem("TopDown/Nivel 02/Agregar techos (primera persona)")]
        public static void AddLevel02() => AddOne(Level02Builder.ScenePath, BuildLevel02);

        [MenuItem("TopDown/Nivel 03/Agregar techo (primera persona)")]
        public static void AddLevel03() => AddOne(Level03Builder.ScenePath, BuildLevel03);

        private static void AddOne(string path, Action<Scene> build)
        {
            if (EditorApplication.isPlaying) { Debug.LogError("[Techos] Detén Play Mode primero."); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) { Debug.LogWarning("[Techos] Cancelado: no se guardaron los cambios de la escena abierta."); return; }
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            build(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // ---------------------------------------------------------------- Nivel 1

        internal static void BuildLevel01(Scene scene, LevelLayout layout)
        {
            GameObject levelGo = BuilderUtil.FindRoot(scene, SceneFactory.LevelRoot);
            if (levelGo == null) { Debug.LogError("[Techos] Level01 no tiene objeto Level. Usa TopDown/Construir nivel."); return; }
            Transform level = levelGo.transform;
            RemoveOld(level);

            Material material = AssetFactory.Mat(AssetFactory.MatWall);
            var roofs = new List<GameObject>();
            int slabs = 0;
            for (int i = 0; i < layout.Floors.Length; i++)
            {
                LevelMap map = layout.Floors[i];
                Transform parent = SceneFactory.FloorParent(level, i);
                if (parent == null) continue;
                LevelMap upper = i + 1 < layout.Floors.Length ? layout.Floors[i + 1] : null;
                Transform roof = NewRoot(parent);

                // Techo plano a la altura de las paredes; abierto sobre la escalera (tiene su tramo inclinado) y bajo los huecos del piso de arriba.
                List<LevelMap.Block> blocks = map.Blocks((c, r) =>
                    map.At(c, r) != LevelMap.Void && map.At(c, r) != LevelMap.Stairs &&
                    !(upper != null && upper.At(c, r) == LevelMap.Hole));
                foreach (LevelMap.Block block in blocks)
                    Slab(roof, block.NameWith("Roof"),
                        map.BlockCenter(block) + new Vector3(0f, SceneFactory.WallHeight + Thickness * 0.5f, 0f),
                        new Vector3(block.Width * map.CellSize, Thickness, block.Height * map.CellSize), Quaternion.identity, material);
                slabs += blocks.Count;

                // Sobre la escalera, el techo sube con la rampa (misma pendiente, a la altura de las paredes por encima).
                LevelMap.Block? stairs = map.BoundsOf(LevelMap.Stairs);
                if (stairs != null && upper != null)
                {
                    SceneFactory.RampGeometry ramp = SceneFactory.RampFor(map, stairs.Value, upper.BaseY);
                    float angle = ramp.AngleDegrees * Mathf.Deg2Rad;
                    var up = new Vector3(-Mathf.Sin(angle), Mathf.Cos(angle), 0f);
                    Vector3 middle = (ramp.Bottom + ramp.Top) * 0.5f;
                    Slab(roof, "Roof_Stairs", middle + Vector3.up * SceneFactory.WallHeight + up * (Thickness * 0.5f),
                        new Vector3(ramp.Scale.x, Thickness, ramp.Scale.z), Quaternion.Euler(0f, 0f, ramp.AngleDegrees), material);
                    slabs++;
                }

                roof.gameObject.SetActive(false);
                roofs.Add(roof.gameObject);
            }
            AddController(scene, roofs);
            Debug.Log($"[Techos] Nivel 1: {slabs} losas de techo en {roofs.Count} pisos (apagadas hasta primera persona).");
        }

        // ---------------------------------------------------------------- Nivel 2

        internal static void BuildLevel02(Scene scene)
        {
            GameObject levelGo = BuilderUtil.FindRoot(scene, "Level");
            Transform environment = levelGo != null ? levelGo.transform.Find("Elysium") : null;
            if (environment == null) { Debug.LogError("[Techos] Level02 no tiene Level/Elysium."); return; }
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Level02Environment.ModelPath);
            if (model == null) { Debug.LogError("[Techos] Falta " + Level02Environment.ModelPath); return; }
            RemoveOld(levelGo.transform);

            // Otra copia del club, con la misma posición y escala que la del nivel, de la que solo se conservan los techos.
            var roof = (GameObject)PrefabUtility.InstantiatePrefab(model, levelGo.transform);
            roof.name = RootName;
            PrefabUtility.UnpackPrefabInstance(roof, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            roof.transform.SetLocalPositionAndRotation(environment.localPosition, environment.localRotation);
            roof.transform.localScale = environment.localScale;

            // El nivel descarta los grupos LowerLevel, SecondLevel y ThirdLevel enteros, pero ahí están las paredes y el techo que
            // cierran la sala grande (laterales de la entrada, anillo del segundo nivel, anillo exterior del tercero y techo principal).
            // Se sacan de su grupo, sin moverlas, antes de borrarlo (los pisos y muebles de esos niveles se quedan fuera).
            foreach (MeshFilter saved in roof.GetComponentsInChildren<MeshFilter>(true).Where(m => IsSavedFromOmitted(m.name)).ToList())
                saved.transform.SetParent(roof.transform, true);

            foreach (string group in Level02Environment.OmittedGroups)
            {
                Transform omitted = roof.transform.Find(group);
                if (omitted != null) Object.DestroyImmediate(omitted.gameObject);
            }

            int kept = 0, ceilings = 0;
            var keptNames = new List<string>();
            foreach (MeshFilter filter in roof.GetComponentsInChildren<MeshFilter>(true).ToList())
            {
                if (KeepInFirstPerson(filter))
                {
                    if (filter.name == "FirstLevel" || filter.name == "Floor")
                    {
                        // Con las mismas aperturas que en el nivel (rampas, escalera del hall) pero sin recortar la altura:
                        // aquí están los muros altos y las pantallas de los lados de la sala.
                        Mesh full = Level02Environment.ClipAbove(filter, 10000f);
                        filter.sharedMesh = Level02Environment.StoreAsset(full, $"{Level02Builder.GeneratedPath}/FP_{filter.name}.asset");
                    }
                    UseLevel02Materials(filter.GetComponent<Renderer>());
                    var kr = filter.GetComponent<Renderer>();
                    if (kr != null)
                    {
                        Bounds kb = kr.bounds;
                        keptNames.Add($"{filter.name} (x {kb.min.x:0} a {kb.max.x:0}, y {kb.min.y:0.0} a {kb.max.y:0.0}, z {kb.min.z:0} a {kb.max.z:0})");
                    }
                    else keptNames.Add(filter.name);
                    kept++;
                    if (Level02Environment.IsOverhead(filter.name)) ceilings++;
                    continue;
                }
                var renderer = filter.GetComponent<Renderer>();
                if (renderer != null) Object.DestroyImmediate(renderer);
                Object.DestroyImmediate(filter);
            }
            foreach (Collider collider in roof.GetComponentsInChildren<Collider>(true).ToList()) Object.DestroyImmediate(collider);
            Prune(roof.transform, roof.transform);

            if (kept == 0)
            {
                Object.DestroyImmediate(roof);
                Debug.LogWarning("[Techos] El modelo del club no tiene mallas de techo (Ceiling*, EntranceCap, EntranceInside).");
                return;
            }
            roof.SetActive(false);
            AddController(scene, new List<GameObject> { roof });
            Debug.Log($"[Techos] Nivel 2: {kept} piezas del club para primera persona ({ceilings} techos y {kept - ceilings} paredes/suelo), apagadas hasta que se active: {string.Join(", ", keptNames)}");
        }

        /// <summary>Paredes que en vista cenital se recortan a ~0,45 m: la primera persona las necesita enteras.</summary>
        private static readonly string[] CutWalls = { "HallWalls01", "HallWalls02", "HallWalls03", "HallWalls04", "StripWalls", "DressingWalls" };

        /// <summary>
        /// Paredes y techo de los grupos descartados que sí se quieren en primera persona. De abajo arriba: GroundSideWalls (laterales
        /// de la entrada, hasta 6,2 m), WallsandStairs (anillo del segundo nivel, 3,4 a 6,2 m), ThirdLevelWall01-06 (anillo exterior,
        /// 6,2 a 9,9 m) y CeilingMain (techo a 9,9 m).
        /// </summary>
        private static bool IsSavedFromOmitted(string name) =>
            name == "GroundSideWalls" || name == "CeilingMain" || name == "WallsandStairs" || name.StartsWith("ThirdLevelWall");

        /// <summary>
        /// Qué piezas del club se enseñan en primera persona: todos los techos, las paredes de la sala grande y las
        /// recortadas en su altura original, el primer nivel (muros altos y pantallas) y el suelo de la planta baja entero. Los muebles de los pisos altos no vuelven:
        /// sin su suelo quedarían flotando.
        /// </summary>
        private static bool KeepInFirstPerson(MeshFilter filter)
        {
            string name = filter.name;
            if (Level02Environment.IsOverhead(name)) return true;
            if (CutWalls.Contains(name) || IsSavedFromOmitted(name) || name == "FirstLevel") return true;
            return name == "Floor" && filter.transform.parent != null && filter.transform.parent.name == "GroundLevel_Group";
        }

        private static void UseLevel02Materials(Renderer renderer)
        {
            if (renderer == null) return;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.sharedMaterials = renderer.sharedMaterials.Select(source =>
            {
                if (source == null) return null;
                var level = AssetDatabase.LoadAssetAtPath<Material>($"{Level02Builder.GeneratedPath}/L02_{source.name}.mat");
                return DoubleSided(level != null ? level : source);
            }).ToArray();
        }

        /// <summary>
        /// Copia del material que se ve por las dos caras: algunas paredes del modelo solo tienen cara hacia fuera
        /// y desde dentro del club serían invisibles. Solo para estas piezas de primera persona.
        /// </summary>
        private static Material DoubleSided(Material source)
        {
            if (!source.HasProperty("_Cull")) return source;
            var copy = new Material(source) { name = "L02FP_" + source.name.Replace("L02_", "") };
            copy.SetFloat("_Cull", (float)CullMode.Off);
            return Level02Environment.StoreAsset(copy, $"{Level02Builder.GeneratedPath}/{copy.name}.mat");
        }

        /// <summary>Quita las ramas del modelo que se quedaron sin mallas.</summary>
        private static void Prune(Transform t, Transform root)
        {
            for (int i = t.childCount - 1; i >= 0; i--) Prune(t.GetChild(i), root);
            if (t != root && t.childCount == 0 && t.GetComponent<MeshFilter>() == null) Object.DestroyImmediate(t.gameObject);
        }

        // ---------------------------------------------------------------- Nivel 3

        internal static void BuildLevel03(Scene scene)
        {
            GameObject loft = BuilderUtil.FindRoot(scene, Level03LoftName);
            if (loft == null) { Debug.LogError($"[Techos] Level03 no tiene '{Level03LoftName}'."); return; }
            var renderers = loft.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) { Debug.LogError("[Techos] El loft no tiene mallas."); return; }
            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers) b.Encapsulate(r.bounds);

            GameObject old = BuilderUtil.FindRoot(scene, RootName);
            if (old != null) Object.DestroyImmediate(old);
            var roof = new GameObject(RootName).transform;

            // Losa justo encima del loft: si el modelo ya trae techo queda oculta por él, y si no, cierra el loft.
            Material material = RoofMaterial(Level03Builder.DataPath + "/L03_Roof.mat", new Color(0.14f, 0.13f, 0.15f));
            Slab(roof, "Roof_Loft", new Vector3(b.center.x, b.max.y + Thickness * 0.5f, b.center.z),
                new Vector3(b.size.x + 0.4f, Thickness, b.size.z + 0.4f), Quaternion.identity, material);
            roof.gameObject.SetActive(false);
            AddController(scene, new List<GameObject> { roof.gameObject });
            Debug.Log($"[Techos] Nivel 3: techo sobre el loft a y={b.max.y:0.0} (apagado hasta primera persona).");
        }

        private static Material RoofMaterial(string path, Color color)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // ---------------------------------------------------------------- Utilidades

        private static void RemoveOld(Transform level)
        {
            foreach (Transform t in level.GetComponentsInChildren<Transform>(true).Where(t => t.name == RootName).ToList())
                if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        private static Transform NewRoot(Transform parent)
        {
            var go = new GameObject(RootName);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        /// <summary>Losa visual: sin collider (no cambia la física ni el NavMesh) y sin sombra (no tapa la luz direccional).</summary>
        private static void Slab(Transform parent, string name, Vector3 position, Vector3 scale, Quaternion rotation, Material material)
        {
            GameObject go = BuilderUtil.EnsurePrimitiveChild(parent, name, PrimitiveType.Cube, false);
            BuilderUtil.SetLocal(go, position, scale, rotation);
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>Objeto siempre activo que enciende y apaga los techos según la vista.</summary>
        private static void AddController(Scene scene, List<GameObject> roofs)
        {
            GameObject go = BuilderUtil.EnsureRoot(scene, ControllerName);
            var controller = BuilderUtil.EnsureComponent<FirstPersonRoof>(go);
            var so = new SerializedObject(controller);
            SerializedProperty list = so.FindProperty("roofs");
            list.arraySize = roofs.Count;
            for (int i = 0; i < roofs.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = roofs[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
