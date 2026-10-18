using System.Linq;
using Overdrive.Combat;
using Overdrive.Enemies;
using Overdrive.Level;
using Overdrive.Weapons;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Overdrive.EditorTools
{
    /// <summary>Verificación de las puertas batientes: lógica, colocación en el mapa, escena y simulación.</summary>
    internal static partial class Verifier
    {
        private static void VerifyDoors(Checker c, LevelLayout layout)
        {
            VerifyDoorLogic(c);
            VerifyDoorMap(c, layout);
            VerifyDoorScene(c, layout);
            VerifyDoorSimulation(c);
        }

        private static void VerifyDoorLogic(Checker c)
        {
            c.Check(SwingDoorLogic.InZone(new Vector3(0.5f, 1f, -1f), 3f, 1.4f) && !SwingDoorLogic.InZone(new Vector3(0f, 1f, -2f), 3f, 1.4f) &&
                    !SwingDoorLogic.InZone(new Vector3(2.5f, 1f, 0f), 3f, 1.4f), "La puerta se abre solo con alguien delante o detrás, cerca");
            c.Check(SwingDoorLogic.SwingSide(new Vector3(0f, 0f, -1f)) == 1f && SwingDoorLogic.SwingSide(new Vector3(0f, 0f, 1f)) == -1f,
                "Se abre hacia el lado contrario del que llega");
            c.Near(SwingDoorLogic.Step(0f, true, 0.1f, 0.2f, 0.5f), 0.5f, "Se abre en 0.2 s");
            c.Near(SwingDoorLogic.Step(1f, false, 0.1f, 0.2f, 0.5f), 0.8f, "Se cierra más despacio (0.5 s)");
            c.Check(SwingDoorLogic.LeafAngle(true, 1f, 1f, 90f) < 0f && SwingDoorLogic.LeafAngle(false, 1f, 1f, 90f) > 0f &&
                    Mathf.Approximately(SwingDoorLogic.LeafAngle(true, 0f, 1f, 90f), 0f), "Las dos hojas giran en sentidos opuestos y cerradas no giran");
        }

        private static void VerifyDoorMap(Checker c, LevelLayout layout)
        {
            c.Check(layout.Doorways.Length >= 6, $"Hay puertas por todo el nivel ({layout.Doorways.Length})");
            foreach (Doorway d in layout.Doorways)
            {
                LevelMap map = layout.Floors[d.Floor];
                var (c2, r2) = d.Second;
                var (wb, rb) = d.WallBefore;
                var (wa, ra) = d.WallAfter;
                c.Check(map.IsWalkable(d.Col, d.Row) && map.IsWalkable(c2, r2), $"{d.Name}: el hueco es suelo");
                c.Check(map.IsWall(wb, rb) && map.IsWall(wa, ra), $"{d.Name}: tiene pared a los dos lados (encaja en el hueco)");
                // Las celdas a través del hueco son suelo: se puede pasar de una sala a otra.
                var (ax, az) = d.Vertical ? (-1, 0) : (0, -1);
                c.Check(map.IsWalkable(d.Col + ax, d.Row + az) && map.IsWalkable(d.Col - ax, d.Row - az), $"{d.Name}: comunica dos zonas");
            }
        }

        private static void VerifyDoorScene(Checker c, LevelLayout layout)
        {
            if (!DoorFactory.ModelAvailable) return;
            GameObject root = BuilderUtil.FindRoot(SceneManager.GetActiveScene(), DoorFactory.DoorsRootName);
            c.NotNull(root, "Hay puertas en la escena");
            if (root == null) return;
            c.Check(root.transform.childCount == layout.Doorways.Length, $"Una puerta por hueco ({root.transform.childCount}/{layout.Doorways.Length})");
            foreach (Doorway d in layout.Doorways)
            {
                Transform t = root.transform.Find(d.Name);
                c.NotNull(t, $"Falta la puerta {d.Name}");
                if (t == null) continue;
                var door = t.GetComponent<SwingDoor>();
                LevelMap map = layout.Floors[d.Floor];
                var (c2, r2) = d.Second;
                Vector3 center = (map.CellToWorld(d.Col, d.Row) + map.CellToWorld(c2, r2)) * 0.5f;
                c.Check(door != null && Vector3.Distance(t.position, center) < 0.01f && Mathf.Abs(door.Width - 2f * map.CellSize) < 0.01f,
                    $"{d.Name}: en el centro del hueco y de su ancho");
                c.Check(door != null && door.IsClosed && door.LeftHinge.localRotation == Quaternion.identity && door.RightHinge.localRotation == Quaternion.identity,
                    $"{d.Name}: empieza cerrada");
                Collider[] cols = t.GetComponentsInChildren<Collider>();
                c.Check(cols.Length == 2 && cols.All(col => !col.isTrigger), $"{d.Name}: sus dos hojas son sólidas (cobertura)");
                c.Check(t.GetComponentsInChildren<Renderer>().All(r => r.sharedMaterials.All(m => m != null)), $"{d.Name}: se ve con el modelo de Jhael");
                var modifier = t.GetComponent<NavMeshModifier>();
                c.Check(modifier != null && modifier.ignoreFromBuild, $"{d.Name}: no entra en el NavMesh");
                // Se puede cruzar andando (la IA pasa y la abre).
                var (ax, az) = d.Vertical ? (1, 0) : (0, 1);
                Vector3 a = map.CellToWorld(d.Col - ax, d.Row - az), b = map.CellToWorld(d.Col + ax, d.Row + az);
                var path = new NavMeshPath();
                c.Check(NavMesh.CalculatePath(a, b, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete,
                    $"{d.Name}: hay camino por el NavMesh de un lado al otro");
            }
        }

        /// <summary>Simulación con una puerta del nivel: se abre al acercarse, para balas y vista cerrada, y se cierra sola.</summary>
        private static void VerifyDoorSimulation(Checker c)
        {
            GameObject root = BuilderUtil.FindRoot(SceneManager.GetActiveScene(), DoorFactory.DoorsRootName);
            SwingDoor door = root != null ? root.GetComponentInChildren<SwingDoor>() : null;
            if (door == null) return;
            Transform t = door.transform;
            Vector3 before = t.position - t.forward * 1.2f + Vector3.up;
            Vector3 after = t.position + t.forward * 1.2f + Vector3.up;
            Physics.SyncTransforms();
            c.Check(ProjectileCast.TryCast(before, t.forward, 2.4f, 0.08f, Faction.Player, out ProjectileCast.Hit hit) && hit.IsObstacle,
                "Cerrada, la puerta para las balas");
            c.Check(!LineOfSight.IsClear(before, after, null), "Cerrada, la puerta tapa la vista (cobertura)");

            var visitors = new[] { t.position - t.forward * 1f };
            for (int i = 0; i < 10; i++) door.Tick(0.05f, visitors);
            c.Check(door.Openness > 0.99f && door.Side == 1f, "Al acercarse alguien se abre del todo, hacia el otro lado");
            Physics.SyncTransforms();
            c.Check(LineOfSight.IsClear(before, after, null), "Abierta, se ve (y se dispara) a través del hueco");
            Vector3 leafTip = door.LeftHinge.TransformPoint(Vector3.right * 0.5f);
            c.Check(Vector3.Dot(leafTip - t.position, t.forward) > 0.3f, "Las hojas se apartan hacia el lado contrario del que llega");

            var nobody = new Vector3[0];
            for (int i = 0; i < 60; i++) door.Tick(0.05f, nobody);
            c.Check(door.IsClosed, "Al quedar libre, se cierra sola");
            Physics.SyncTransforms();
            c.Check(!LineOfSight.IsClear(before, after, null), "Y vuelve a dar cobertura");
        }
    }
}
