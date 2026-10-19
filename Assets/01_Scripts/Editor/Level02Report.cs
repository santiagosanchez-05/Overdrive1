using System.IO;
using System.Linq;
using System.Text;
using Overdrive.Enemies;
using Overdrive.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overdrive.EditorTools
{
    /// <summary>
    /// Informe de diagnóstico del nivel 2: medidas de marcos de puerta, paredes (dibujo y colisión),
    /// suelos y personajes, para ajustar alturas sin adivinar. Escribe Temp/Level02_Report.txt.
    /// No modifica la escena.
    /// </summary>
    internal static class Level02Report
    {
        public const string OutputPath = "Temp/Level02_Report.txt";

        [MenuItem("TopDown/Nivel 02/Informe de puertas y paredes")]
        public static void Write()
        {
            Scene scene = SceneManager.GetActiveScene();
            var sb = new StringBuilder();
            sb.AppendLine($"Escena: {scene.path}");
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();

            // Personajes.
            var player = all.Select(t => t.GetComponent<PlayerController>()).FirstOrDefault(p => p != null);
            if (player != null)
            {
                var cc = player.GetComponent<CharacterController>();
                sb.AppendLine($"Jugador: pos {V(player.transform.position)} escala {V(player.transform.lossyScale)} " +
                              $"cc alto {(cc ? cc.height * player.transform.lossyScale.y : 0f):0.00} radio {(cc ? cc.radius : 0f):0.00} " +
                              $"modelo alto {Height(player.transform):0.00}");
            }
            foreach (Enemy e in all.Select(t => t.GetComponent<Enemy>()).Where(e => e != null).Take(40))
                sb.AppendLine($"Enemigo {Path(e.transform)}: pie {V(e.transform.position)} modelo alto {Height(e.transform):0.00}");

            // Marcos de puerta y puertas.
            sb.AppendLine();
            sb.AppendLine("== Marcos y puertas (renderer: min..max y | colisión: min..max y) ==");
            foreach (Transform t in all.Where(t => t.name.Contains("Frame") || t.name.StartsWith("Door")))
                sb.AppendLine(Describe(t));

            // Paredes.
            sb.AppendLine();
            sb.AppendLine("== Paredes ==");
            foreach (Transform t in all.Where(t => t.name.ToLowerInvariant().Contains("wall")))
                sb.AppendLine(Describe(t));

            // Suelos.
            sb.AppendLine();
            sb.AppendLine("== Suelos / pisos ==");
            foreach (Transform t in all.Where(t => t.name.Contains("Floor") || t.name.Contains("Level") || t.name.Contains("Slab") || t.name.Contains("Threshold")))
                if (t.GetComponent<Renderer>() != null || t.GetComponent<Collider>() != null) sb.AppendLine(Describe(t));

            Directory.CreateDirectory("Temp");
            File.WriteAllText(OutputPath, sb.ToString());
            Debug.Log($"[Level02Report] Informe escrito en {System.IO.Path.GetFullPath(OutputPath)}");
            EditorUtility.RevealInFinder(OutputPath);
        }

        private static string Describe(Transform t)
        {
            var r = t.GetComponent<Renderer>();
            var c = t.GetComponent<Collider>();
            string rs = r != null ? $"{r.bounds.min.y:0.00}..{r.bounds.max.y:0.00} x[{r.bounds.min.x:0.0},{r.bounds.max.x:0.0}] z[{r.bounds.min.z:0.0},{r.bounds.max.z:0.0}]" : "-";
            string cs = c != null ? $"{c.GetType().Name} {c.bounds.min.y:0.00}..{c.bounds.max.y:0.00}{(c.isTrigger ? " trigger" : "")}{(c.enabled ? "" : " OFF")}" : "-";
            return $"{Path(t)} | activo {t.gameObject.activeInHierarchy} | render {rs} | colisión {cs} | escala {V(t.lossyScale)}";
        }

        private static float Height(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return 0f;
            Bounds b = rs[0].bounds;
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            return b.size.y;
        }

        private static string Path(Transform t)
        {
            string p = t.name;
            for (Transform x = t.parent; x != null; x = x.parent) p = x.name + "/" + p;
            return p;
        }

        private static string V(Vector3 v) => $"({v.x:0.00}, {v.y:0.00}, {v.z:0.00})";
    }
}
