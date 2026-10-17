using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Overdrive.Level;
using Overdrive.Core;
using Object = UnityEngine.Object;

namespace Overdrive.EditorTools
{
    internal static class Level02Environment
    {
        private const string ModelPath = "Assets/00_Scenes/huge-nightclub/source/elysium/source/Elysium/Elysium.fbx";
        // Original entrance is at y=.02 and the centre of the dance floor at z=-39.16.
        private const float Scale = 0.8f;
        private static readonly string[] OmittedGroups = { "LowerLevel_Group", "SecondLevel_Group", "ThirdLevel_Group", "VIPLoungeGroup01", "VIPLoungeGroup02", "VIPLoungeGroup03", "VIPLoungeGroup04", "VIPLoungeGroup05", "Holo" };

        public static GameObject Build(Transform level)
        {
            Directory.CreateDirectory(Level02Builder.GeneratedPath);
            AssetDatabase.Refresh();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) throw new InvalidOperationException("No se encontró Elysium.fbx.");
            var environment = (GameObject)PrefabUtility.InstantiatePrefab(model, level);
            environment.name = "Elysium";
            // Scene-only copy: the imported model and its shared materials stay intact.
            PrefabUtility.UnpackPrefabInstance(environment, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            environment.transform.localScale = Vector3.one * Scale;
            environment.transform.localPosition = new Vector3(0f, -0.02f * Scale, 39.16f * Scale);
            foreach (string group in OmittedGroups)
            {
                var child = environment.transform.Find(group);
                if (child != null) Object.DestroyImmediate(child.gameObject);
            }
            PrepareGeometry(environment);
            PrepareMaterials(environment);
            AddDanceFloor(level);
            AddFloorSeams(level);
            AddSeatingRamps(level);
            AddNeonAccents(level);
            AddClubCables(level);
            return environment;
        }

        private static void PrepareGeometry(GameObject environment)
        {
            foreach (var filter in environment.GetComponentsInChildren<MeshFilter>(true))
            {
                string name = filter.name;
                var renderer = filter.GetComponent<MeshRenderer>();
                bool unusedPlatform = filter.transform.parent.name == "DancePlatform_Group" && name != "Dance_Platform01";
                bool closedDoor = name.Contains("Door") && !name.Contains("Frame") && !name.StartsWith("WC_Door");
                bool overhead = name.Contains("Ceiling") || name == "EntranceCap" || name == "EntranceInside";
                bool unusedUpperDecor = renderer.bounds.min.y > 2.8f;
                if (unusedPlatform || closedDoor || overhead || unusedUpperDecor || name == "FenchMetal")
                {
                    Object.DestroyImmediate(filter.gameObject);
                    continue;
                }
                if (name == "Dance_Platform01") filter.transform.position -= Vector3.up * 1.4f;
                if (name == "DressingCloset01") filter.transform.position += Vector3.left * 3.3f;
                // Cut walls down to a readable height instead of hiding their collision.
                float cut = float.PositiveInfinity;
                if (name.StartsWith("HallWalls")) cut = 0.45f;
                if (name == "StripWalls") cut = -0.45f;
                if (name == "DressingWalls") cut = 0.45f;
                if (name == "Floor" && filter.transform.parent.name == "GroundLevel_Group") cut = 2.4f;
                if (name == "FirstLevel") cut = 2.9f;
                if (!float.IsPositiveInfinity(cut))
                {
                    Mesh clipped = ClipAbove(filter, cut);
                    string path = Level02Builder.GeneratedPath + "/Cut_" + name + ".asset";
                    filter.sharedMesh = StoreAsset(clipped, path);
                }
                Bounds bounds = renderer.bounds;
                bool tinyDecoration = filter.transform.parent.name.StartsWith("Drinks") || name.StartsWith("DressLights") || name.StartsWith("Lock") || name == "Elysium";
                if (tinyDecoration) continue;
                // Large combined meshes need exact concave collision: boxes would fill rooms and doorways.
                bool architecture = name.Contains("Frame") || name.Contains("Stairs") || bounds.size.x > 6f || bounds.size.z > 6f || bounds.size.y < 0.08f;
                if (architecture)
                {
                    var collider = filter.gameObject.AddComponent<MeshCollider>();
                    // Keep the posts solid; decorative low lintels would reject the 2m navigation agent.
                    collider.sharedMesh = name.Contains("DoorFrame")
                        ? StoreAsset(ClipAbove(filter, bounds.min.y + 1.75f), Level02Builder.GeneratedPath + "/Collision_" + name + ".asset")
                        : filter.sharedMesh;
                }
                else
                {
                    var collider = filter.gameObject.AddComponent<BoxCollider>();
                    collider.center = filter.sharedMesh.bounds.center;
                    collider.size = Vector3.Max(filter.sharedMesh.bounds.size, Vector3.one * 0.03f);
                }
                filter.gameObject.isStatic = true;
            }
        }

        private static void PrepareMaterials(GameObject environment)
        {
            var copies = new Dictionary<Material, Material>();
            foreach (var renderer in environment.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(source =>
                {
                    if (source == null) throw new InvalidOperationException("Material ausente: " + renderer.name);
                    if (copies.TryGetValue(source, out var existing)) return existing;
                    var material = new Material(source) { name = "L02_" + source.name };
                    if (material.shader == null || !material.shader.name.StartsWith("Universal Render Pipeline/"))
                        material.shader = Shader.Find("Universal Render Pipeline/Lit");
                    // Keep the texture set assembled by ElysiumMaterialFixer; moderate glossy floors.
                    if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.42f);
                    if (material.HasProperty("_EmissionMap") && material.GetTexture("_EmissionMap") != null)
                    {
                        material.EnableKeyword("_EMISSION");
                        material.SetColor("_EmissionColor", Color.white * 1.8f);
                        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    }
                    var saved = StoreAsset(material, Level02Builder.GeneratedPath + "/" + material.name + ".mat");
                    copies.Add(source, saved);
                    return saved;
                }).ToArray();
            }
        }

        private static void AddDanceFloor(Transform level)
        {
            // Covers the original shaft underneath the stage; no invisible void during combat.
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "DanceFloorSafetySlab";
            floor.transform.SetParent(level, false);
            floor.transform.localPosition = new Vector3(0f, -0.17f, 0f);
            floor.transform.localScale = new Vector3(16.2f, 0.15f, 16.2f);
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            floor.AddComponent<MeshCollider>().sharedMesh = floor.GetComponent<MeshFilter>().sharedMesh;
            floor.GetComponent<Renderer>().sharedMaterial = AssetFactory.Mat(AssetFactory.MatFloor);
        }

        private static void AddFloorSeams(Transform level)
        {
            // The imported room floors do not meet exactly at these open doorways.
            AddSeam(level, "EntranceThreshold", new Vector3(0f, -0.1f, 22.2f), new Vector3(4.1f, 0.2f, 2.2f));
            AddSeam(level, "StageThreshold", new Vector3(0f, -2.35f, -37f), new Vector3(4.1f, 0.2f, 2f));
            AddSeam(level, "BackstageThreshold", new Vector3(-6.7f, -1.6f, -69f), new Vector3(10f, 0.2f, 2.2f));
            AddSeam(level, "StageSafetySlab", new Vector3(-6.7f, -2.45f, -51.8f), new Vector3(22.3f, 0.2f, 28.5f));
            AddSeam(level, "DressingSafetySlab", new Vector3(-6.7f, -1.7f, -77f), new Vector3(12.1f, 0.2f, 20.4f));
        }

        private static void AddSeatingRamps(Transform level)
        {
            float length = Mathf.Sqrt(6f * 6f + 1.6f * 1.6f);
            float angle = Mathf.Atan2(1.6f, 6f) * Mathf.Rad2Deg;
            foreach (float x in new[] { -10f, 10f })
            {
                var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ramp.name = x < 0f ? "WestSeatingRamp" : "EastSeatingRamp";
                ramp.transform.SetParent(level, false);
                ramp.transform.position = new Vector3(x, 0.7f, 3f);
                ramp.transform.localScale = new Vector3(3f, 0.2f, length);
                ramp.transform.rotation = Quaternion.Euler(-angle, 0f, 0f);
                ramp.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Level02Builder.GeneratedPath + "/L02_Threshold.mat");
                AddSeam(level, ramp.name + "Landing", new Vector3(x, 1.5f, 8f), new Vector3(3f, 0.2f, 4.2f));
            }
        }

        private static void AddNeonAccents(Transform level)
        {
            var accents = new GameObject("Neon Accents").transform;
            accents.SetParent(level, false);
            Material Neon(string name, Color color)
            {
                var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                material.SetColor("_BaseColor", color);
                material.SetColor("_EmissionColor", color * 3f);
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                return StoreAsset(material, Level02Builder.GeneratedPath + "/" + name + ".mat");
            }
            var pink = Neon("L02_NeonPink", new Color(1f, 0.025f, 0.26f));
            var cyan = Neon("L02_NeonCyan", new Color(0.025f, 0.65f, 1f));
            foreach (float x in new[] { -11.45f, -8.55f, 8.55f, 11.45f })
            {
                NeonStrip(accents, new Vector3(x, 0.15f, 0.1f), new Vector3(x, 1.72f, 6f), cyan);
                NeonStrip(accents, new Vector3(x, 1.72f, 6f), new Vector3(x, 1.72f, 10f), cyan);
            }
            for (int ring = 0; ring < 2; ring++)
            {
                var line = new GameObject("DanceRing_" + ring).AddComponent<LineRenderer>();
                line.transform.SetParent(accents, false);
                line.sharedMaterial = ring == 0 ? cyan : pink;
                line.useWorldSpace = false;
                line.widthMultiplier = 0.055f;
                line.loop = true;
                line.positionCount = 96;
                for (int i = 0; i < 96; i++)
                {
                    float angle = i * Mathf.PI * 2f / 96;
                    line.SetPosition(i, new Vector3(Mathf.Cos(angle) * (ring == 0 ? 6.2f : 7.5f), 0.2f, Mathf.Sin(angle) * (ring == 0 ? 6.2f : 7.5f)));
                }
            }
            foreach (float x in new[] { -18f, 4.65f })
                NeonStrip(accents, new Vector3(x, -1.75f, -40f), new Vector3(x, -1.75f, -64f), pink);
            foreach (float x in new[] { -12.3f, -0.9f })
                NeonStrip(accents, new Vector3(x, -0.9f, -73f), new Vector3(x, -0.9f, -86f), cyan);
        }

        internal static void AddClubCables(Transform level)
        {
            var old = level.Find("Club Neon Cables");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("Club Neon Cables").transform;
            root.SetParent(level, false);
            var pink = AssetDatabase.LoadAssetAtPath<Material>(Level02Builder.GeneratedPath + "/L02_NeonPink.mat");
            var cyan = AssetDatabase.LoadAssetAtPath<Material>(Level02Builder.GeneratedPath + "/L02_NeonCyan.mat");
            // Hang beside the seating rather than across the centre of the camera's view.
            for (int side = -1; side <= 1; side += 2)
                for (int span = 0; span < 3; span++)
                {
                    float z = -13f + span * 10f;
                    var anchors = new[] { new Vector3(side * 20f, 3.1f, z), new Vector3(side * 13f, 3.1f, z + 4f), new Vector3(side * 19f, 3.1f, z + 8f) };
                    var points = LedPath.Hang(anchors, .65f, 18, .45f);
                    var cable = new GameObject("Hanging Neon " + side + "_" + span).AddComponent<LineRenderer>();
                    cable.transform.SetParent(root, false);
                    cable.sharedMaterial = span % 2 == 0 ? pink : cyan;
                    cable.widthMultiplier = .045f;
                    cable.positionCount = points.Length;
                    cable.SetPositions(points);
                    cable.shadowCastingMode = ShadowCastingMode.Off;
                    cable.receiveShadows = false;
                    foreach (Vector3 anchor in anchors) AddCableSupport(cable.transform, anchor);
                    for (int bulb = 5; bulb < points.Length; bulb += 9)
                    {
                        var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        glow.name = "LED Bulb";
                        glow.transform.SetParent(root, false);
                        glow.transform.position = points[bulb];
                        glow.transform.localScale = Vector3.one * .13f;
                        Object.DestroyImmediate(glow.GetComponent<Collider>());
                        glow.GetComponent<Renderer>().sharedMaterial = cable.sharedMaterial;
                    }
                    var light = new GameObject("Neon Wash").AddComponent<Light>();
                    light.transform.SetParent(root, false);
                    light.transform.position = anchors[1] - Vector3.up * .8f;
                    light.type = LightType.Point;
                    light.color = span % 2 == 0 ? new Color(1f, .035f, .32f) : new Color(.04f, .65f, 1f);
                    light.range = 7f;
                    light.intensity = 2.2f;
                    light.shadows = LightShadows.None;
                }
            foreach (float x in new[] { -17.7f, 4.4f })
                for (int i = 0; i < 4; i++)
                    NeonStrip(root, new Vector3(x, -1.2f, -42f - i * 5.5f), new Vector3(x, -.7f, -44.5f - i * 5.5f), i % 2 == 0 ? cyan : pink);
        }

        private static void AddCableSupport(Transform cable, Vector3 anchor)
        {
            if (!GroundProbe.TrySurface(anchor, out var ground))
                throw new InvalidOperationException("Anclaje de neón sin superficie de apoyo: " + anchor);
            float height = anchor.y - ground.point.y;
            var metal = AssetDatabase.LoadAssetAtPath<Material>(Level02Builder.GeneratedPath + "/L02_Threshold.mat");
            void Cylinder(string name, Vector3 position, Vector3 scale)
            {
                var support = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                support.name = name;
                support.transform.SetParent(cable, false);
                support.transform.position = position;
                support.transform.localScale = scale;
                // Slender decorative supports do not change the baked navigation.
                Object.DestroyImmediate(support.GetComponent<Collider>());
                support.GetComponent<Renderer>().sharedMaterial = metal;
            }
            Cylinder("Cable Support", ground.point + Vector3.up * (height * .5f), new Vector3(.075f, height * .5f + .025f, .075f));
            Cylinder("Anchor Base", ground.point + Vector3.up * .025f, new Vector3(.3f, .025f, .3f));
            Cylinder("Cable Clamp", anchor, new Vector3(.15f, .045f, .15f));
        }

        private static void NeonStrip(Transform parent, Vector3 from, Vector3 to, Material material)
        {
            var line = new GameObject("Wall Strip").AddComponent<LineRenderer>();
            line.transform.SetParent(parent, false);
            line.sharedMaterial = material;
            line.widthMultiplier = 0.06f;
            line.positionCount = 2;
            line.SetPositions(new[] { from, to });
        }

        private static void AddSeam(Transform level, string name, Vector3 position, Vector3 size)
        {
            var seam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seam.name = name;
            seam.transform.SetParent(level, false);
            seam.transform.position = position;
            seam.transform.localScale = size;
            var material = AssetDatabase.LoadAssetAtPath<Material>(Level02Builder.GeneratedPath + "/L02_Threshold.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "L02_Threshold" };
                material.SetColor("_BaseColor", new Color(0.055f, 0.065f, 0.1f));
                material.SetFloat("_Metallic", 0.45f);
                material.SetFloat("_Smoothness", 0.35f);
                AssetDatabase.CreateAsset(material, Level02Builder.GeneratedPath + "/L02_Threshold.mat");
            }
            seam.GetComponent<Renderer>().sharedMaterial = material;
        }

        public static void BakeNavigation(GameObject level)
        {
            var surface = level.GetComponent<NavMeshSurface>();
            if (surface == null) surface = level.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.1f;
            surface.overrideTileSize = true;
            surface.tileSize = 128;
            Physics.SyncTransforms();
            // The project default permits 75cm steps, but Player's CharacterController only climbs 30cm.
            // Bake a conservative copy for this level without changing Level01's agent settings.
            var settings = surface.GetBuildSettings();
            settings.agentClimb = 0.25f;
            settings.agentHeight = 1.8f;
            settings.agentRadius = 0.45f;
            var sources = new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(level.transform, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, new List<NavMeshBuildMarkup>(), sources);
            Bounds bounds = new Bounds(level.transform.position, Vector3.zero);
            foreach (var collider in level.GetComponentsInChildren<Collider>())
                if (collider.enabled && !collider.isTrigger) bounds.Encapsulate(collider.bounds);
            bounds.Expand(4f);
            var baked = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (baked == null) throw new InvalidOperationException("El horneado NavMesh no produjo datos.");
            surface.RemoveData();
            surface.navMeshData = StoreAsset(baked, Level02Builder.GeneratedPath + "/Level02_NavMesh.asset");
            surface.AddData();
        }

        internal static T StoreAsset<T>(T asset, string path) where T : Object
        {
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null) { AssetDatabase.CreateAsset(asset, path); return asset; }
            EditorUtility.CopySerialized(asset, existing);
            if (existing is Mesh mesh) RefreshMeshBuffers(mesh);
            Object.DestroyImmediate(asset);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private struct Vertex
        {
            public Vector3 Position, Normal;
            public Vector2 Uv;
            public static Vertex Lerp(Vertex a, Vertex b, float t) => new Vertex { Position = Vector3.Lerp(a.Position, b.Position, t), Normal = Vector3.Lerp(a.Normal, b.Normal, t).normalized, Uv = Vector2.Lerp(a.Uv, b.Uv, t) };
        }

        // Clip triangles rather than discard whole faces, preserving the textured wall below the cut.
        private static Mesh ClipAbove(MeshFilter filter, float worldHeight)
        {
            Mesh source = filter.sharedMesh;
            var vertices = source.vertices;
            var normals = source.normals;
            var uvs = source.uv;
            var output = new List<Vertex>();
            var submeshes = new List<int[]>();
            for (int submesh = 0; submesh < source.subMeshCount; submesh++)
            {
                var indices = new List<int>();
                int[] triangles = source.GetTriangles(submesh);
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    var polygon = new List<Vertex>();
                    for (int j = 0; j < 3; j++)
                    {
                        int index = triangles[i + j];
                        polygon.Add(new Vertex { Position = vertices[index], Normal = normals.Length > index ? normals[index] : Vector3.up, Uv = uvs.Length > index ? uvs[index] : Vector2.zero });
                    }
                    // Leave an open notch over the hall stairs, retaining the side seating.
                    Plane[][] regions = filter.name == "FirstLevel"
                        ? new[] { new[] { new Plane(Vector3.left, -3f) }, new[] { new Plane(Vector3.right, -3f) }, new[] { new Plane(Vector3.right, 3f), new Plane(Vector3.left, 3f), new Plane(Vector3.forward, 10f) } }
                        : new[] { Array.Empty<Plane>() };
                    foreach (Plane[] region in regions)
                    {
                        var clipped = new List<Vertex>(polygon);
                        foreach (Plane plane in region.Concat(new[] { new Plane(Vector3.down, worldHeight) }))
                        {
                            var input = clipped;
                            clipped = new List<Vertex>();
                            for (int j = 0; j < input.Count; j++)
                            {
                                Vertex a = input[j], b = input[(j + 1) % input.Count];
                                float da = plane.GetDistanceToPoint(filter.transform.TransformPoint(a.Position));
                                float db = plane.GetDistanceToPoint(filter.transform.TransformPoint(b.Position));
                                bool insideA = da >= 0f, insideB = db >= 0f;
                                if (insideA) clipped.Add(a);
                                if (insideA != insideB) clipped.Add(Vertex.Lerp(a, b, da / (da - db)));
                            }
                        }
                        var pieces = new List<List<Vertex>> { clipped };
                        if (filter.name == "FirstLevel" || filter.name == "Floor" && filter.transform.parent.name == "GroundLevel_Group")
                        {
                            // Open the original curved rail at each ramp's landing, keeping the floor below it.
                            foreach (float x in new[] { -10f, 10f })
                            {
                                Plane[] opening = { new Plane(Vector3.right, -(x - 1.8f)), new Plane(Vector3.left, x + 1.8f), new Plane(Vector3.forward, -5.5f), new Plane(Vector3.back, 12.5f), new Plane(Vector3.up, -1.65f) };
                                pieces = pieces.SelectMany(piece => SubtractVolume(piece, opening, filter.transform)).ToList();
                            }
                        }
                        if (filter.name == "FirstLevel")
                        {
                            var flattened = new List<List<Vertex>>();
                            foreach (Plane[] stairs in UnusedStairVolumes())
                            {
                                foreach (var piece in pieces)
                                {
                                    var footprint = piece;
                                    foreach (Plane plane in stairs) footprint = ClipPolygon(footprint, plane, filter.transform);
                                    if (footprint.Count < 3) continue;
                                    if (filter.transform.TransformDirection(footprint[0].Normal).y < .5f) continue;
                                    flattened.Add(footprint.Select(v =>
                                    {
                                        Vector3 world = filter.transform.TransformPoint(v.Position);
                                        world.y = 1.6f;
                                        v.Position = filter.transform.InverseTransformPoint(world);
                                        v.Normal = filter.transform.InverseTransformDirection(Vector3.up);
                                        return v;
                                    }).ToList());
                                }
                                pieces = pieces.SelectMany(piece => SubtractVolume(piece, stairs, filter.transform)).ToList();
                            }
                            pieces.AddRange(flattened);
                        }
                        foreach (var piece in pieces)
                        for (int j = 1; j + 1 < piece.Count; j++)
                        {
                            if (Vector3.Cross(piece[j].Position - piece[0].Position, piece[j + 1].Position - piece[0].Position).sqrMagnitude < 1e-12f) continue;
                            int start = output.Count;
                            output.Add(piece[0]); output.Add(piece[j]); output.Add(piece[j + 1]);
                            indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
                        }
                    }
                }
                submeshes.Add(indices.ToArray());
            }
            var mesh = new Mesh { name = "Cutaway_" + filter.name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(output.Select(v => v.Position).ToList());
            mesh.SetNormals(output.Select(v => v.Normal).ToList());
            mesh.SetUVs(0, output.Select(v => v.Uv).ToList());
            mesh.subMeshCount = submeshes.Count;
            for (int i = 0; i < submeshes.Count; i++) mesh.SetTriangles(submeshes[i], i);
            mesh.RecalculateBounds();
            if (filter.name == "FirstLevel") MatchFlattenedFloorTexture(mesh, filter.transform);
            mesh.RecalculateTangents();
            return mesh;
        }

        private static void MatchFlattenedFloorTexture(Mesh mesh, Transform transform)
        {
            var vertices = mesh.vertices;
            var uv = mesh.uv;
            var triangles = mesh.triangles;
            var regions = UnusedStairVolumes().ToArray();
            bool InStairFootprint(Vector3 p) => regions.Any(region => region.All(plane => plane.GetDistanceToPoint(new Vector3(p.x, 2f, p.z)) >= 0f));
            float largest = 0f;
            Vector2 floorUv = Vector2.zero;
            Vector3 floorCenter = Vector3.zero;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                var pa = transform.TransformPoint(vertices[a]); var pb = transform.TransformPoint(vertices[b]); var pc = transform.TransformPoint(vertices[c]);
                var normal = Vector3.Cross(pb - pa, pc - pa);
                if (pa.y < 1.5f || pa.y > 1.64f || Mathf.Abs(pb.y - pa.y) > .001f || Mathf.Abs(pc.y - pa.y) > .001f || normal.y <= 0f) continue;
                if (InStairFootprint(pa) || InStairFootprint(pb) || InStairFootprint(pc) || normal.sqrMagnitude <= largest) continue;
                largest = normal.sqrMagnitude;
                floorUv = (uv[a] + uv[b] + uv[c]) / 3f;
                floorCenter = (pa + pb + pc) / 3f;
            }
            if (largest == 0f) throw new InvalidOperationException("No se encontró la textura del piso de la terraza.");
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 world = transform.TransformPoint(vertices[i]);
                if (Mathf.Abs(world.y - 1.6f) < .005f && InStairFootprint(world))
                    uv[i] = floorUv + new Vector2(world.x - floorCenter.x, world.z - floorCenter.z) * .0002f;
            }
            mesh.uv = uv;
        }

        private static IEnumerable<Plane[]> UnusedStairVolumes()
        {
            // Four outer flights led to removed upper floors. Preserve the terrace slab beneath them.
            foreach (int side in new[] { -1, 1 })
                foreach (var area in new[] { new Vector4(19.7f, 25.4f, .2f, 4.1f), new Vector4(18.3f, 23.9f, -11.7f, -6.5f) })
                {
                    float left = side < 0 ? -area.y : area.x;
                    float right = side < 0 ? -area.x : area.y;
                    yield return new[]
                    {
                        new Plane(Vector3.right, -left), new Plane(Vector3.left, right),
                        new Plane(Vector3.forward, -area.z), new Plane(Vector3.back, area.w),
                        new Plane(Vector3.up, -1.64f)
                    };
                }
        }

        internal static void RemoveUnusedUpperStairs(Transform level)
        {
            var floor = level.GetComponentsInChildren<MeshFilter>(true).Single(f => f.name == "FirstLevel");
            var mesh = StoreAsset(ClipAbove(floor, 2.9f), Level02Builder.GeneratedPath + "/Cut_FirstLevel.asset");
            floor.sharedMesh = null;
            floor.sharedMesh = mesh;
            var collider = floor.GetComponent<MeshCollider>();
            collider.sharedMesh = null;
            collider.sharedMesh = mesh;
        }

        private static void RefreshMeshBuffers(Mesh mesh)
        {
            // CopySerialized preserves the asset GUID; explicitly refresh native render buffers too.
            mesh.vertices = mesh.vertices;
            mesh.normals = mesh.normals;
            mesh.uv = mesh.uv;
            for (int i = 0; i < mesh.subMeshCount; i++) mesh.SetTriangles(mesh.GetTriangles(i), i);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
        }

        private static List<List<Vertex>> SubtractVolume(List<Vertex> polygon, Plane[] volume, Transform transform)
        {
            var outside = new List<List<Vertex>>();
            var remaining = polygon;
            foreach (Plane plane in volume)
            {
                var piece = ClipPolygon(remaining, plane.flipped, transform);
                if (piece.Count >= 3) outside.Add(piece);
                remaining = ClipPolygon(remaining, plane, transform);
                if (remaining.Count < 3) break;
            }
            return outside;
        }

        private static List<Vertex> ClipPolygon(List<Vertex> polygon, Plane plane, Transform transform)
        {
            var result = new List<Vertex>();
            for (int i = 0; i < polygon.Count; i++)
            {
                Vertex a = polygon[i], b = polygon[(i + 1) % polygon.Count];
                float da = plane.GetDistanceToPoint(transform.TransformPoint(a.Position));
                float db = plane.GetDistanceToPoint(transform.TransformPoint(b.Position));
                if (da >= 0f) result.Add(a);
                if ((da >= 0f) != (db >= 0f)) result.Add(Vertex.Lerp(a, b, da / (da - db)));
            }
            return result;
        }
    }
}
