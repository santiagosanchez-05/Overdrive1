using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overdrive.EditorTools
{
    /// <summary>
    /// Encaja un modelo de escalera (por ejemplo "stairsOpen" del kit de Kenney) sobre la rampa del juego (StairsN):
    /// lo gira para que suba hacia el mismo lado, lo escala para que ocupe lo mismo y lo coloca encima.
    /// La rampa sigue siendo la que se pisa (su collider), pero se oculta para que solo se vea el modelo.
    /// </summary>
    internal static class StairsModelFitter
    {
        private static readonly Regex RampName = new Regex(@"^Stairs\d+$");

        [MenuItem("TopDown/Ajustar modelo de escalera a la rampa")]
        public static void FitMenu()
        {
            Scene scene = SceneManager.GetActiveScene();
            int fitted = FitAll(scene);
            if (fitted == 0)
            {
                Debug.LogWarning("[Escalera] No se encontró ninguna rampa StairsN con un modelo de escalera cerca.");
                return;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log($"[Escalera] {fitted} modelo(s) de escalera ajustados a su rampa. Guarda la escena.");
        }

        /// <summary>Ajusta a cada rampa de la escena el modelo de escalera más cercano. Devuelve cuántos ajustó.</summary>
        public static int FitAll(Scene scene)
        {
            List<Transform> all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
            List<Transform> ramps = all.Where(t => RampName.IsMatch(t.name) && t.GetComponent<BoxCollider>() != null).ToList();
            List<Transform> models = all.Where(IsStairsModel).ToList();

            int fitted = 0;
            foreach (Transform ramp in ramps)
            {
                Vector3 center = ramp.position;
                Transform model = models
                    .Where(m => Mathf.Abs(m.position.y - center.y) < Core.Floors.Height)
                    .OrderBy(m => Vector2.Distance(new Vector2(m.position.x, m.position.z), new Vector2(center.x, center.z)))
                    .FirstOrDefault();
                if (model == null) continue;
                models.Remove(model);
                Fit(model, ramp);
                fitted++;
            }
            return fitted;
        }

        /// <summary>Raíz de un modelo de escalera: se llama "stairs…" (no "StairsN") y tiene mallas.</summary>
        private static bool IsStairsModel(Transform t)
        {
            if (RampName.IsMatch(t.name) || !t.name.StartsWith("stairs", System.StringComparison.OrdinalIgnoreCase)) return false;
            if (t.parent != null && t.parent.name.StartsWith("stairs", System.StringComparison.OrdinalIgnoreCase)) return false;
            return t.GetComponentsInChildren<MeshFilter>(true).Any(mf => mf.sharedMesh != null);
        }

        public static void Fit(Transform model, Transform ramp)
        {
            // Superficie de la rampa (cubo unidad girado): borde de abajo y de arriba de su cara superior.
            Vector3 bottom = ramp.TransformPoint(new Vector3(-0.5f, 0.5f, 0f));
            Vector3 top = ramp.TransformPoint(new Vector3(0.5f, 0.5f, 0f));
            if (top.y < bottom.y) (bottom, top) = (top, bottom);
            Vector3 ascend = top - bottom;
            ascend.y = 0f;
            float run = ascend.magnitude;
            float rise = top.y - bottom.y;
            float width = ramp.lossyScale.z;
            ascend.Normalize();

            Undo.RecordObject(model, "Ajustar escalera");

            // 1. Girar para que el modelo suba hacia el mismo lado que la rampa (en pasos de 90°).
            Vector3 modelAscend = AscendDirection(model);
            float yaw = Vector3.SignedAngle(modelAscend, ascend, Vector3.up);
            yaw = Mathf.Round(yaw / 90f) * 90f;
            model.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * model.rotation;

            // 2. Escalar para que ocupe lo mismo que la rampa: largo (subida), alto y ancho.
            Bounds b = WorldBounds(model);
            bool alongX = Mathf.Abs(ascend.x) >= Mathf.Abs(ascend.z);
            var target = alongX ? new Vector3(run, rise, width) : new Vector3(width, rise, run);
            var factor = new Vector3(target.x / b.size.x, target.y / b.size.y, target.z / b.size.z);
            Vector3 scale = model.localScale;
            for (int axis = 0; axis < 3; axis++)
            {
                // Eje del mundo al que apunta cada eje local del modelo tras el giro.
                Vector3 world = model.rotation * AxisVector(axis);
                int worldAxis = DominantAxis(world);
                scale[axis] *= factor[worldAxis];
            }
            model.localScale = scale;

            // 3. Colocar: que la caja del modelo coincida con la de la rampa.
            b = WorldBounds(model);
            Vector3 rampCenter = (bottom + top) * 0.5f;
            var targetCenter = new Vector3(rampCenter.x, bottom.y + rise * 0.5f, rampCenter.z);
            model.position += targetCenter - b.center;

            // La rampa se sigue pisando, pero ya no se ve: el modelo hace de escalera.
            var renderer = ramp.GetComponent<Renderer>();
            if (renderer != null)
            {
                Undo.RecordObject(renderer, "Ajustar escalera");
                renderer.enabled = false;
            }

            // El modelo no debe bloquear la subida: fuera las cajas de colisión que se le hubieran añadido.
            foreach (Collider c in model.GetComponentsInChildren<Collider>(true))
                if (PrefabUtility.IsAddedComponentOverride(c)) Undo.DestroyObjectImmediate(c);
        }

        /// <summary>Hacia dónde sube el modelo: del centro de su caja hacia sus vértices más altos (en horizontal).</summary>
        private static Vector3 AscendDirection(Transform model)
        {
            List<Vector3> vertices = WorldVertices(model);
            float maxY = vertices.Max(v => v.y);
            float minY = vertices.Min(v => v.y);
            float threshold = maxY - (maxY - minY) * 0.1f;
            Vector3 high = Vector3.zero;
            int count = 0;
            foreach (Vector3 v in vertices)
            {
                if (v.y < threshold) continue;
                high += v;
                count++;
            }
            Vector3 dir = high / count - WorldBounds(model).center;
            dir.y = 0f;
            // Al eje del mundo más cercano.
            return Mathf.Abs(dir.x) >= Mathf.Abs(dir.z)
                ? new Vector3(Mathf.Sign(dir.x), 0f, 0f)
                : new Vector3(0f, 0f, Mathf.Sign(dir.z));
        }

        private static List<Vector3> WorldVertices(Transform model)
        {
            var result = new List<Vector3>();
            foreach (MeshFilter mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Matrix4x4 m = mf.transform.localToWorldMatrix;
                foreach (Vector3 v in mf.sharedMesh.vertices) result.Add(m.MultiplyPoint3x4(v));
            }
            return result;
        }

        private static Bounds WorldBounds(Transform model)
        {
            List<Vector3> vertices = WorldVertices(model);
            var b = new Bounds(vertices[0], Vector3.zero);
            foreach (Vector3 v in vertices) b.Encapsulate(v);
            return b;
        }

        private static Vector3 AxisVector(int axis) => axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;

        private static int DominantAxis(Vector3 v)
        {
            float x = Mathf.Abs(v.x), y = Mathf.Abs(v.y), z = Mathf.Abs(v.z);
            return x >= y && x >= z ? 0 : y >= z ? 1 : 2;
        }
    }
}
