using System;
using System.IO;
using Overdrive.Core;
using Overdrive.Combat;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Overdrive.EditorTools
{
    internal static class BloodSurfaceChecks
    {
        public static void Run()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
            floor.name = "Temporary blood regression floor";
            floor.transform.position = new Vector3(500f, -1.5f, 500f);
            floor.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            floor.transform.localScale = Vector3.one * 6f;
            var blood = new GameObject("Temporary blood regression spawner").AddComponent<BloodSpawner>();
            var serialized = new SerializedObject(blood);
            serialized.FindProperty("bloodPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.BloodPrefabPath);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var randomState = UnityEngine.Random.state;
            UnityEngine.Random.InitState(42);
            try
            {
                Physics.SyncTransforms();
                Require(Mathf.Abs(GroundProbe.GroundY(floor.transform.position + Vector3.up * .2f) + 1.5f) < .02f,
                    "MeshCollider a altura negativa debe recibir la sangre, sin recurrir a y=0.");
                blood.Spawn(floor.transform.position, 1f);
                Require(blood.SplatCount == 3, "Las tres manchas deben aparecer sobre el suelo de malla.");
                foreach (Transform splat in blood.transform)
                    Require(Mathf.Abs(splat.position.y + 1.49f) < .003f, "La sangre no puede flotar a y=0 sobre el camerino.");
                blood.Restore(0);
                floor.transform.rotation = Quaternion.Euler(75f, 0f, 0f);
                Physics.SyncTransforms();
                Vector3 point = floor.transform.position + Vector3.forward;
                Require(Physics.Raycast(point + Vector3.up, Vector3.down, out var hit, 3f), "Falta suelo inclinado de prueba.");
                Require(Mathf.Abs(GroundProbe.GroundY(point) - hit.point.y) < .02f, "La sangre debe seguir la pendiente del MeshCollider.");
                blood.Spawn(point, 1f);
                Require(blood.SplatCount == 3, "La pendiente debe recibir las salpicaduras.");
                foreach (Transform splat in blood.transform)
                    Require(Vector3.Dot(-splat.forward, hit.normal) > .99f, "Las manchas deben orientarse sobre la pendiente.");
                blood.Restore(0);
                Object.DestroyImmediate(floor.GetComponent<MeshCollider>());
                floor.transform.rotation = Quaternion.identity;
                floor.AddComponent<BoxCollider>().size = new Vector3(1f, .02f, 1f);
                Physics.SyncTransforms();
                Require(Mathf.Abs(GroundProbe.GroundY(floor.transform.position + Vector3.up * .2f) + 1.44f) < .02f,
                    "Los BoxCollider del nivel 1 deben conservar su altura.");
                blood.Spawn(floor.transform.position + Vector3.right * 2.85f, 1f);
                Require(blood.SplatCount > 0, "Debe quedar sangre en el borde del escalón.");
                foreach (Transform splat in blood.transform)
                    for (int x = -1; x <= 1; x += 2)
                        for (int y = -1; y <= 1; y += 2)
                        {
                            Vector3 corner = splat.TransformPoint(new Vector3(x * .5f, y * .5f, 0f));
                            Require(Mathf.Abs(GroundProbe.GroundY(corner) - corner.y) < .04f, "Las manchas no deben sobresalir flotando del escalón.");
                        }
                File.WriteAllText("Temp/BloodSurface.checks.txt", "PASS MeshCollider negativo, sangre apoyada, orientación en pendiente, BoxCollider y borde del escalón.");
            }
            finally
            {
                UnityEngine.Random.state = randomState;
                Object.DestroyImmediate(blood.gameObject);
                Object.DestroyImmediate(floor);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
