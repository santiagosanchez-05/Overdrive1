using System.Collections.Generic;
using System.Linq;
using Overdrive.CameraRig;
using Overdrive.Enemies;
using Overdrive.Level;
using Overdrive.Player;
using Overdrive.Weapons;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Overdrive.EditorTools
{
    /// <summary>
    /// Ático del Jefe (nivel 3): una sala grande junto al loft para que el combate final tenga sitio. Suelo oscuro con
    /// cuadrícula de neón, paredes con tiras de luz, columnas y cajas de cobertura (paran balas y embestidas), armas
    /// por el suelo y luces violeta y carmesí. Se llega en ascensor desde el piso de arriba del loft.
    /// </summary>
    internal static class Level03Arena
    {
        public const string ArenaName = "Arena";
        public const string ElevatorName = "Elevator";
        public const float Width = 26f;
        public const float Depth = 20f;
        public const float WallHeight = 3f;
        public const float Gap = 15f;
        public const float CameraDistance = 15f;

        /// <summary>Puntos de juego del ático (en el mundo).</summary>
        public sealed class Points
        {
            public Vector3 Center, Start, Boss, Exit;
            public List<Vector3> Rage = new List<Vector3>();
            public List<Vector3> Final = new List<Vector3>();
            public List<(Vector3 pos, WeaponType type)> Weapons = new List<(Vector3, WeaponType)>();
            public List<Vector3> Covers = new List<Vector3>();
        }

        /// <summary>Puntos del ático con su centro (para las comprobaciones, sin construir nada).</summary>
        public static Points Layout(Vector3 center)
        {
            Vector3 P(float x, float z) => center + new Vector3(x, 0f, z);
            float hw = Width * 0.5f, hd = Depth * 0.5f;
            var p = new Points
            {
                Center = center,
                Start = P(0f, -hd + 2.5f),
                Boss = P(0f, hd - 4f),
                Exit = P(hw - 2.5f, hd - 2.5f),
            };
            p.Rage.AddRange(new[] { P(-hw + 1.5f, hd - 1.5f), P(hw - 1.5f, hd - 4.5f), P(-hw + 1.5f, 0f) });
            p.Final.AddRange(new[] { P(-hw + 1.5f, -hd + 1.5f), P(hw - 1.5f, -hd + 1.5f) });
            p.Weapons.Add((P(-hw + 2.5f, -hd + 3f), WeaponType.Shotgun));
            p.Weapons.Add((P(hw - 2.5f, -hd + 3f), WeaponType.Smg));
            p.Weapons.Add((P(-hw + 3f, 3f), WeaponType.Revolver));
            p.Weapons.Add((P(hw - 3f, 3f), WeaponType.Bat));
            p.Weapons.Add((P(0f, -1f), WeaponType.Shotgun));
            // Coberturas: dos columnas en el centro y cajas repartidas, con pasillos anchos entre ellas.
            p.Covers.AddRange(new[] { P(-4f, 1f), P(4f, 1f), P(-8.5f, -4f), P(8.5f, -4f), P(-7f, 6f), P(7f, 6f), P(0f, -5.5f) });
            return p;
        }

        /// <summary>Centro del ático: a un lado del loft, a la altura de su suelo.</summary>
        public static Vector3 CenterFor(Bounds loft, float floorY) =>
            new Vector3(loft.max.x + Gap + Width * 0.5f, floorY, loft.center.z);

        public static Points Build(Transform level, Bounds loft, float floorY)
        {
            Points p = Layout(CenterFor(loft, floorY));
            var root = new GameObject(ArenaName).transform;
            root.SetParent(level, false);
            root.position = p.Center;

            Material floor = VehicleFactory.EnsureFlatMaterial("ArenaFloor", new Color(0.05f, 0.04f, 0.08f), 0f);
            Material wall = VehicleFactory.EnsureFlatMaterial("ArenaWall", new Color(0.09f, 0.07f, 0.12f), 0f);
            Material crate = VehicleFactory.EnsureFlatMaterial("ArenaCrate", new Color(0.18f, 0.12f, 0.1f), 0f);
            Material pillar = VehicleFactory.EnsureFlatMaterial("ArenaPillar", new Color(0.12f, 0.1f, 0.16f), 0f);

            // Suelo: losa sólida con la cara de arriba a la altura del suelo del loft.
            Box(root, "Floor", new Vector3(0f, -0.1f, 0f), new Vector3(Width, 0.2f, Depth), floor);
            // Paredes.
            float hw = Width * 0.5f, hd = Depth * 0.5f, t = 0.6f;
            Box(root, "Wall_N", new Vector3(0f, WallHeight * 0.5f, hd + t * 0.5f), new Vector3(Width + 2 * t, WallHeight, t), wall);
            Box(root, "Wall_S", new Vector3(0f, WallHeight * 0.5f, -hd - t * 0.5f), new Vector3(Width + 2 * t, WallHeight, t), wall);
            Box(root, "Wall_E", new Vector3(hw + t * 0.5f, WallHeight * 0.5f, 0f), new Vector3(t, WallHeight, Depth), wall);
            Box(root, "Wall_W", new Vector3(-hw - t * 0.5f, WallHeight * 0.5f, 0f), new Vector3(t, WallHeight, Depth), wall);

            // Coberturas: columnas (las dos primeras) y cajas.
            for (int i = 0; i < p.Covers.Count; i++)
            {
                Vector3 local = p.Covers[i] - p.Center;
                if (i < 2)
                {
                    GameObject c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    c.name = $"Pillar_{i + 1}";
                    c.transform.SetParent(root, false);
                    c.transform.localPosition = local + Vector3.up * WallHeight * 0.5f;
                    c.transform.localScale = new Vector3(1.4f, WallHeight * 0.5f, 1.4f);
                    c.GetComponent<Renderer>().sharedMaterial = pillar;
                    c.isStatic = true;
                }
                else
                {
                    bool wide = i % 2 == 0;
                    Box(root, $"Crate_{i - 1}", local + Vector3.up * 0.7f, wide ? new Vector3(2.6f, 1.4f, 1.2f) : new Vector3(1.2f, 1.4f, 2.6f), crate);
                }
            }

            // Neón: cuadrícula en el suelo y tiras en lo alto de las paredes.
            Material grid = AssetFactory.Mat(AssetFactory.LedMaterial(LedColor.Violet, false));
            Material trim = AssetFactory.Mat(AssetFactory.LedMaterial(LedColor.Crimson, false));
            for (float x = -hw + 4f; x < hw; x += 4f) Line(root, $"GridX_{x:0}", new[] { new Vector3(x, 0.02f, -hd + 0.3f), new Vector3(x, 0.02f, hd - 0.3f) }, grid, 0.06f);
            for (float z = -hd + 4f; z < hd; z += 4f) Line(root, $"GridZ_{z:0}", new[] { new Vector3(-hw + 0.3f, 0.02f, z), new Vector3(hw - 0.3f, 0.02f, z) }, grid, 0.06f);
            Line(root, "Trim", new[]
            {
                new Vector3(-hw + 0.1f, WallHeight - 0.2f, -hd + 0.1f), new Vector3(-hw + 0.1f, WallHeight - 0.2f, hd - 0.1f),
                new Vector3(hw - 0.1f, WallHeight - 0.2f, hd - 0.1f), new Vector3(hw - 0.1f, WallHeight - 0.2f, -hd + 0.1f),
                new Vector3(-hw + 0.1f, WallHeight - 0.2f, -hd + 0.1f),
            }, trim, 0.25f);

            // Luces: violeta en las esquinas, carmesí sobre el trono del Jefe, blanca suave en la entrada.
            Light(root, "Light_SW", new Vector3(-hw + 3f, 4f, -hd + 3f), new Color(0.55f, 0.25f, 1f), 14f, 3f);
            Light(root, "Light_SE", new Vector3(hw - 3f, 4f, -hd + 3f), new Color(0.55f, 0.25f, 1f), 14f, 3f);
            Light(root, "Light_NW", new Vector3(-hw + 3f, 4f, hd - 3f), new Color(0.55f, 0.25f, 1f), 14f, 3f);
            Light(root, "Light_NE", new Vector3(hw - 3f, 4f, hd - 3f), new Color(0.55f, 0.25f, 1f), 14f, 3f);
            Light(root, "Light_Throne", (p.Boss - p.Center) + new Vector3(0f, 4f, 1f), new Color(1f, 0.1f, 0.25f), 12f, 4f);
            Light(root, "Light_Center", new Vector3(0f, 5f, 0f), new Color(0.9f, 0.8f, 1f), 18f, 1.6f);
            Light(root, "Light_Entry", (p.Start - p.Center) + Vector3.up * 3f, new Color(1f, 0.9f, 0.8f), 8f, 2f);
            var sign = Label(root, "ÁTICO DEL JEFE", (p.Start - p.Center) + new Vector3(0f, 0.03f, 2.2f), 0.9f, new Color(1f, 0.25f, 0.45f));
            var horror = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SignFontPath); // la fuente que eligió Mateo para el cartel
            if (horror != null) sign.font = horror;
            return p;
        }

        /// <summary>
        /// Ascensor en el piso de arriba del loft (donde antes esperaba el Jefe): se abre al no quedar guardias y lleva
        /// al jugador a la entrada del ático.
        /// </summary>
        public static ArenaElevator BuildElevator(Transform level, Vector3 spot, Points arena, PlayerLife player, Enemy[] guards, TopDownCamera rig)
        {
            var root = new GameObject(ElevatorName).transform;
            root.SetParent(level, false);
            root.position = spot;

            GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pad.name = "Pad";
            pad.transform.SetParent(root, false);
            pad.transform.localPosition = Vector3.up * 0.02f;
            pad.transform.localScale = new Vector3(1.6f, 0.02f, 1.6f);
            Object.DestroyImmediate(pad.GetComponent<Collider>());
            pad.GetComponent<Renderer>().sharedMaterial = AssetFactory.Mat(AssetFactory.MatExitLocked);

            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = "OpenBeam";
            beam.transform.SetParent(root, false);
            beam.transform.localPosition = Vector3.up * 1.2f;
            beam.transform.localScale = new Vector3(1.4f, 1.2f, 1.4f);
            Object.DestroyImmediate(beam.GetComponent<Collider>());
            beam.GetComponent<Renderer>().sharedMaterial = AssetFactory.Mat(AssetFactory.MatExitBeam);
            beam.SetActive(false);

            var label = Label(root, "ASCENSOR", new Vector3(0f, 0.04f, -1.2f), 0.45f, new Color(0.55f, 0.85f, 1f));

            var target = new GameObject("ArenaStart").transform;
            target.SetParent(level, false);
            target.position = arena.Start;
            target.rotation = Quaternion.LookRotation(arena.Boss - arena.Start);

            var elevator = root.gameObject.AddComponent<ArenaElevator>();
            var so = new SerializedObject(elevator);
            so.FindProperty("player").objectReferenceValue = player;
            SerializedProperty list = so.FindProperty("guards");
            list.arraySize = guards.Length;
            for (int i = 0; i < guards.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = guards[i];
            so.FindProperty("arenaStart").objectReferenceValue = target;
            so.FindProperty("cameraRig").objectReferenceValue = rig;
            so.FindProperty("arenaCameraDistance").floatValue = CameraDistance;
            so.FindProperty("openBeam").objectReferenceValue = beam;
            so.FindProperty("label").objectReferenceValue = label;
            so.ApplyModifiedPropertiesWithoutUndo();
            return elevator;
        }

        private static GameObject Box(Transform parent, string name, Vector3 local, Vector3 size, Material mat)
        {
            GameObject b = GameObject.CreatePrimitive(PrimitiveType.Cube);
            b.name = name;
            b.transform.SetParent(parent, false);
            b.transform.localPosition = local;
            b.transform.localScale = size;
            b.GetComponent<Renderer>().sharedMaterial = mat;
            b.isStatic = true;
            return b;
        }

        private static void Line(Transform parent, string name, Vector3[] localPoints, Material mat, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = localPoints.Length;
            lr.SetPositions(localPoints);
            lr.widthMultiplier = width;
            lr.alignment = LineAlignment.View;
            lr.sharedMaterial = mat;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static void Light(Transform parent, string name, Vector3 local, Color color, float range, float intensity)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.localPosition = local;
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        public const string SignFontPath = "Assets/TextMesh Pro/Fonts/horrendo SDF.asset";

        private static TextMeshPro Label(Transform parent, string text, Vector3 local, float size, Color color)
        {
            var label = new GameObject("Sign_" + text).AddComponent<TextMeshPro>();
            label.transform.SetParent(parent, false);
            label.transform.localPosition = local;
            label.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            label.text = text;
            label.fontSize = size * 10f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.rectTransform.sizeDelta = new Vector2(12f, 2f);
            return label;
        }
    }
}
