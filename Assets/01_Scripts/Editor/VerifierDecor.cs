using System.Collections.Generic;
using System.Linq;
using Overdrive.Combat;
using Overdrive.Level;
using Overdrive.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Overdrive.EditorTools
{
    /// <summary>Verificación de los shaders propios (estelas, halos, haz, disolución) y de los muebles.</summary>
    internal static partial class Verifier
    {
        private static void VerifyShadersAndMaterials(Checker c)
        {
            foreach (string name in new[] { AssetFactory.ShaderTrail, AssetFactory.ShaderAura, AssetFactory.ShaderLightBeam,
                         AssetFactory.ShaderDissolve, AssetFactory.ShaderWall, AssetFactory.ShaderLedLine })
            {
                Shader shader = Shader.Find(name);
                c.NotNull(shader, $"Existe el shader {name}");
                if (shader != null) c.Check(!ShaderUtil.ShaderHasError(shader), $"El shader {name} compila sin errores");
            }
            foreach (var (name, shaderName, _) in AssetFactory.AllShaderMaterialDefs)
            {
                Material m = AssetFactory.Mat(name);
                c.Check(m != null && m.shader != null && m.shader.name == shaderName, $"M_{name} usa {shaderName}");
            }

            // Estela de las balas.
            var bullet = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.BulletPrefabPath);
            var projectile = bullet != null ? bullet.GetComponent<Projectile>() : null;
            TrailRenderer bulletTrail = bullet != null ? bullet.GetComponent<TrailRenderer>() : null;
            c.Check(bulletTrail != null && projectile != null && projectile.Trail == bulletTrail &&
                    bulletTrail.sharedMaterial == AssetFactory.Mat(AssetFactory.MatBulletTrail),
                "La bala tiene estela con M_BulletTrail");

            // Estela de las armas lanzadas.
            foreach (WeaponType type in AssetFactory.AllWeaponTypes)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.WeaponPrefabPath(type));
                var item = prefab != null ? prefab.GetComponent<WeaponItem>() : null;
                TrailRenderer trail = prefab != null ? prefab.GetComponent<TrailRenderer>() : null;
                c.Check(item != null && trail != null && item.Trail == trail && !trail.emitting &&
                        trail.sharedMaterial == AssetFactory.Mat(AssetFactory.MatThrowTrail),
                    $"Weapon_{type}: estela apagada hasta que se lanza");
            }

            // Halo del maletín.
            var briefcase = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.BriefcasePrefabPath);
            var pickup = briefcase != null ? briefcase.GetComponent<BriefcasePickup>() : null;
            c.Check(pickup != null && pickup.Glow != null && pickup.Glow.transform.parent == briefcase.transform &&
                    pickup.Glow.GetComponent<Renderer>().sharedMaterial == AssetFactory.Mat(AssetFactory.MatAuraBriefcase) &&
                    pickup.Glow.GetComponent<Collider>() == null,
                "El maletín tiene un halo dorado sin collider");
        }

        private static void VerifyDecorScene(Checker c, LevelLayout layout)
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject level = BuilderUtil.FindRoot(scene, SceneFactory.LevelRoot);
            if (level == null) return;

            // --- Haz de la salida ---
            var exit = level.transform.Find(SceneFactory.ExitName)?.GetComponent<ExitZone>();
            Transform beam = level.transform.Find(SceneFactory.ExitBeamName);
            c.Check(exit != null && beam != null && exit.Beam == beam.gameObject, "La salida tiene su haz de luz");
            if (beam != null)
            {
                c.Check(!beam.gameObject.activeSelf, "El haz de la salida empieza apagado");
                c.Check(beam.GetComponent<Collider>() == null, "El haz no tiene collider (no entra en el NavMesh ni bloquea)");
                c.Check(beam.GetComponent<Renderer>().sharedMaterial == AssetFactory.Mat(AssetFactory.MatExitBeam), "El haz usa M_ExitBeam");
            }

            // --- Puerta que se disuelve ---
            Transform door = BuilderUtil.FindRoot(scene, SceneFactory.DoorsName)?.transform.Find(SceneFactory.DoorName);
            var dissolve = door != null ? door.GetComponent<DoorDissolve>() : null;
            c.NotNull(dissolve, "La puerta de los refuerzos tiene DoorDissolve");
            if (dissolve != null)
            {
                c.Check(dissolve.DoorRenderer == door.GetComponent<Renderer>() && dissolve.DoorCollider == door.GetComponent<Collider>() &&
                        dissolve.Obstacle == door.GetComponent<NavMeshObstacle>(), "DoorDissolve tiene sus referencias");
                c.Check(dissolve.DoorRenderer.sharedMaterial == AssetFactory.Mat(AssetFactory.MatDoorDissolve), "La puerta usa M_DoorDissolve");
                c.Check(dissolve.DoorCollider.enabled && dissolve.Obstacle.enabled, "La puerta empieza sólida");
            }

            // --- Muebles ---
            foreach (LevelMap map in layout.Floors)
            {
                Transform parent = map.FloorIndex == 0 ? level.transform : level.transform.Find(SceneFactory.FloorRootName(map.FloorIndex));
                Transform props = parent != null ? parent.Find(SceneFactory.PropsName) : null;
                Transform walls = parent != null ? parent.Find(SceneFactory.WallsName) : null;
                var cells = map.Cells().Where(x => PropCatalog.IsProp(x.c)).ToList();
                c.Check(props != null && props.childCount == cells.Count,
                    $"Piso {map.FloorIndex}: hay {cells.Count} muebles (hay {(props != null ? props.childCount : 0)})");
                if (props == null || walls == null) continue;

                List<Bounds> wallBounds = walls.Cast<Transform>().Select(w => w.GetComponent<Collider>().bounds).ToList();
                foreach (var (col, row, ch) in cells)
                {
                    PropCatalog.Entry entry = PropCatalog.Get(ch);
                    string name = SceneFactory.PropName(entry, col, row);
                    Transform prop = props.Find(name);
                    c.NotNull(prop, $"Falta el mueble {name}");
                    if (prop == null) continue;

                    c.Check(PrefabUtility.IsPartOfPrefabInstance(prop.gameObject), $"{name}: es instancia del prefab de Mnostva_Art");
                    c.Check(prop.GetComponentsInChildren<Collider>().Any(col2 => col2.gameObject.name != SceneFactory.HaloName),
                        $"{name}: tiene collider (bloquea el paso y las balas)");
                    Bounds b = SceneFactory.PropBounds(prop.gameObject);
                    c.Check(Mathf.Max(b.size.x, b.size.z) <= entry.Footprint + 0.02f, $"{name}: mide como mucho {entry.Footprint} m");
                    c.Near(b.min.y, map.BaseY, $"{name}: apoyado en el suelo de su piso", 0.02f);
                    c.Check(map.WallSideOf(col, row) != (0, 0), $"{name}: está junto a una pared");

                    var shrunk = new Bounds(b.center, b.size - new Vector3(0.04f, 0.04f, 0.04f));
                    c.Check(!wallBounds.Any(w => w.Intersects(shrunk)), $"{name}: no se mete dentro de una pared");

                    if (entry.HasHalo)
                    {
                        Transform halo = prop.Find(SceneFactory.HaloName);
                        c.Check(halo != null && halo.GetComponent<Renderer>().sharedMaterial == AssetFactory.Mat(AssetFactory.MatAuraNeonPink) &&
                                halo.position.y > b.max.y, $"{name}: tiene halo neón encima");
                    }
                }

                // Con los muebles como obstáculo, todo lo importante sigue conectado.
                (int, int) start;
                if (map.FloorIndex == 0) start = map.FindAll(LevelMap.PlayerSpawn)[0];
                else
                {
                    LevelMap.Block s = layout.Ground.BoundsOf(LevelMap.Stairs).Value;
                    start = (s.Col + s.Width, s.Row);
                }
                HashSet<(int, int)> reach = FloodFillWhere(map, start.Item1, start.Item2,
                    (col, row) => map.IsWalkable(col, row) && !PropCatalog.IsProp(map.At(col, row)));
                foreach (var (col, row, ch) in map.Cells())
                {
                    bool important = LevelMap.IsEnemy(ch) || LevelMap.IsFloorWeapon(ch) || ch == LevelMap.Exit || ch == LevelMap.Briefcase ||
                                     ch == LevelMap.Door || ch == LevelMap.Reinforcement || ch == LevelMap.Stairs;
                    if (important) c.Check(reach.Contains((col, row)), $"Piso {map.FloorIndex}: los muebles no tapan '{ch}' en ({col},{row})");
                }
            }
        }

        /// <summary>
        /// Muebles colocados a mano: ninguno suelto en la raíz de la escena, todos bajo Decor de su piso,
        /// y ningún mueble apoyado en el suelo (del mapa o a mano) se puede atravesar: no hay NavMesh en su centro.
        /// También las luces de ambiente.
        /// </summary>
        private static void VerifyDecorAndAmbience(Checker c, LevelLayout layout)
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject level = BuilderUtil.FindRoot(scene, SceneFactory.LevelRoot);
            if (level == null) return;

            int loose = scene.GetRootGameObjects().Count(SceneFactory.IsDecorInstance);
            c.Check(loose == 0, $"No quedan muebles sueltos en la raíz de la escena (hay {loose})");

            Physics.SyncTransforms();
            List<Bounds> ramps = level.transform.Cast<Transform>().Where(t => t.name.StartsWith("Stairs"))
                .Select(t => t.GetComponent<Collider>().bounds).ToList();
            var walkThrough = new HashSet<GameObject>(); // alfombras y decoración sobre la rampa: se pisan a propósito
            var furniture = new List<(GameObject go, LevelMap map)>();
            foreach (LevelMap map in layout.Floors)
            {
                Transform parent = SceneFactory.FloorParent(level.transform, map.FloorIndex);
                foreach (string container in new[] { SceneFactory.DecorName, SceneFactory.PropsName })
                {
                    Transform t = parent != null ? parent.Find(container) : null;
                    if (t == null) continue;
                    foreach (Transform child in t)
                    {
                        if (container == SceneFactory.DecorName)
                        {
                            c.Check(SceneFactory.IsDecorInstance(child.gameObject), $"Decor/{child.name}: es un mueble del equipo (Mnostva, Kenney o Modelos)");
                            Bounds db = SceneFactory.PropBounds(child.gameObject);
                            int floor = Core.Floors.IndexOf(db.min.y);
                            c.Check(floor == map.FloorIndex, $"Decor/{child.name}: está bajo el piso {map.FloorIndex} y es del piso {floor}");

                            bool flat = db.size.y < SceneFactory.FlatDecorHeight;
                            bool onRamp = ramps.Any(r => r.Intersects(db));
                            if (flat || onRamp) walkThrough.Add(child.gameObject);
                            else
                                c.Check(child.GetComponentsInChildren<Collider>(true).Any(col2 => !col2.isTrigger),
                                    $"Decor/{child.name}: tiene colisión (bloquea el paso y las balas)");
                        }
                        furniture.Add((child.gameObject, map));
                    }
                }
            }
            c.Check(furniture.Count(f => f.go.transform.parent.name == SceneFactory.DecorName) > 0, "Los muebles de Jhael están bajo Decor");

            // Nadie atraviesa los muebles: en el centro de los que están apoyados en el suelo no hay NavMesh.
            foreach (var (go, map) in furniture)
            {
                if (walkThrough.Contains(go)) continue;
                Bounds b = SceneFactory.PropBounds(go);
                bool onFloor = b.min.y < map.BaseY + 0.2f;
                bool bigEnough = Mathf.Min(b.size.x, b.size.z) >= 0.35f;
                if (!onFloor || !bigEnough) continue;
                var center = new Vector3(b.center.x, map.BaseY, b.center.z);
                bool walkable = NavMesh.SamplePosition(center, out NavMeshHit hit, 0.15f, NavMesh.AllAreas);
                c.Check(!walkable, $"Los enemigos no atraviesan {go.name} (piso {map.FloorIndex})");
            }

            // Luces de ambiente.
            foreach (AmbientLight light in layout.Ambience)
            {
                Transform container = SceneFactory.FloorParent(level.transform, light.Floor)?.Find(SceneFactory.AmbienceName);
                Transform t = container != null ? container.Find(light.Name) : null;
                c.NotNull(t, $"Falta la luz de ambiente {light.Name}");
                if (t == null) continue;
                Renderer r = t.GetComponent<Renderer>();
                c.Check(r != null && r.sharedMaterial == AssetFactory.Mat(AssetFactory.AmbientMaterial(light.Kind)),
                    $"{light.Name}: usa {AssetFactory.AmbientMaterial(light.Kind)}");
                c.Check(t.GetComponent<Collider>() == null, $"{light.Name}: sin collider (no bloquea ni entra en el NavMesh)");
                float floorY = layout.Floors[light.Floor].BaseY;
                if (light.IsBeam)
                    c.Check(Vector3.Angle(t.up, Vector3.down) < 1f && Mathf.Abs(t.position.y - (floorY + light.Height * 0.5f)) < 0.01f,
                        $"{light.Name}: columna boca abajo, del techo al suelo");
                else if (light.Kind == AmbientKind.Ember)
                    c.Check(t.position.y > floorY + 0.4f && t.position.y < floorY + 1.6f, $"{light.Name}: apoyada sobre una mesa ({t.position.y - floorY:F2} m)");
                else
                    c.Near(t.position.y, floorY + light.Height, $"{light.Name}: cuelga a su altura", 0.01f);
            }
            int total = layout.Floors.Sum(m => SceneFactory.FloorParent(level.transform, m.FloorIndex)?.Find(SceneFactory.AmbienceName)?.childCount ?? 0);
            c.Check(total == layout.Ambience.Length, $"Hay {layout.Ambience.Length} luces de ambiente (hay {total})");
        }

        /// <summary>
        /// Ningún objeto sólido (muebles del mapa o colocados a mano, de quien sea) tapa una celda importante:
        /// inicio, enemigos, armas, maletín, salida o entrada de refuerzos.
        /// </summary>
        private static void VerifyNothingBlocksKeyCells(Checker c, LevelLayout layout)
        {
            Physics.SyncTransforms();
            foreach (var (map, col, row, ch) in layout.AllCells())
            {
                bool important = ch == LevelMap.PlayerSpawn || LevelMap.IsEnemy(ch) || LevelMap.IsFloorWeapon(ch) ||
                                 ch == LevelMap.Briefcase || ch == LevelMap.Exit || ch == LevelMap.Reinforcement;
                if (!important) continue;

                // Caja a la altura del cuerpo: no toca el suelo ni la losa, sí cualquier mueble encima de la celda.
                Vector3 center = map.CellToWorld(col, row) + Vector3.up;
                Collider[] hits = Physics.OverlapBox(center, new Vector3(0.3f, 0.4f, 0.3f), Quaternion.identity, ~0,
                    QueryTriggerInteraction.Ignore);
                var blockers = hits.Where(h => h.GetComponentInParent<Enemies.Enemy>() == null &&
                                               h.GetComponentInParent<Player.PlayerLife>() == null)
                                   .Select(h => h.transform.root.name + "/" + h.name).Distinct().ToList();
                c.Check(blockers.Count == 0,
                    $"Nada tapa '{ch}' en ({col},{row}) del piso {map.FloorIndex}{(blockers.Count > 0 ? ": " + string.Join(", ", blockers) : "")}");
            }
        }

        /// <summary>Estelas, halo, haz y puerta funcionando (en la escena real; se deja todo como estaba).</summary>
        private static void VerifyDecorSimulation(Checker c)
        {
            var created = new List<GameObject>();
            try
            {
                // Estela de la bala: al lanzarla no se dibuja una raya desde el origen.
                var bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.BulletPrefabPath);
                GameObject bullet = Object.Instantiate(bulletPrefab);
                created.Add(bullet);
                var projectile = bullet.GetComponent<Projectile>();
                projectile.Launch(new Vector3(900f, 1f, 900f), Vector3.forward, 40f, Faction.Player);
                c.Check(projectile.Trail.positionCount == 0, "Al disparar, la estela empieza en el arma (no en el origen)");

                // Estela del arma lanzada.
                var knifePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.WeaponPrefabPath(WeaponType.Knife));
                GameObject knifeGo = Object.Instantiate(knifePrefab);
                created.Add(knifeGo);
                var knife = knifeGo.GetComponent<WeaponItem>();
                knife.Throw(new Vector3(-900f, 1f, -900f), Vector3.forward, Faction.Player);
                c.Check(knife.Trail.emitting, "Un arma lanzada deja estela");
                knife.DropAt(new Vector3(-900f, 0f, -890f), 0f);
                c.Check(!knife.Trail.emitting, "Al caer, el arma deja de dejar estela");

                // Halo del maletín: se apaga al recogerlo y vuelve al rebobinar.
                var briefcasePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.BriefcasePrefabPath);
                GameObject briefcaseGo = Object.Instantiate(briefcasePrefab, new Vector3(950f, 0f, 950f), Quaternion.identity);
                created.Add(briefcaseGo);
                var collector = new GameObject("Collector");
                created.Add(collector);
                collector.transform.position = briefcaseGo.transform.position;
                var pickup = briefcaseGo.GetComponent<BriefcasePickup>();
                object before = pickup.Capture();
                c.Check(pickup.Glow.activeSelf, "El maletín brilla en el suelo");
                pickup.TryCollect(collector.transform);
                c.Check(!pickup.Glow.activeSelf, "Recogido, el maletín deja de brillar");
                pickup.Restore(before);
                c.Check(pickup.Glow.activeSelf, "Al rebobinar, el maletín vuelve a brillar");

                Scene scene = SceneManager.GetActiveScene();

                // Haz de la salida.
                var exit = BuilderUtil.FindRoot(scene, SceneFactory.LevelRoot)?.transform.Find(SceneFactory.ExitName)?.GetComponent<ExitZone>();
                if (exit != null && exit.Beam != null)
                {
                    exit.SetOpen(true);
                    c.Check(exit.Beam.activeSelf, "Simulación: al abrirse la salida se enciende el haz");
                    exit.SetOpen(false);
                    c.Check(!exit.Beam.activeSelf, "Cerrada, el haz se apaga");
                }

                // Puerta que se disuelve.
                var door = BuilderUtil.FindRoot(scene, SceneFactory.DoorsName)?.transform.Find(SceneFactory.DoorName)?.GetComponent<DoorDissolve>();
                if (door != null)
                {
                    door.Play();
                    c.Check(door.gameObject.activeSelf && !door.DoorCollider.enabled && !door.Obstacle.enabled,
                        "Simulación: al abrirse, la puerta deja pasar al instante");
                    door.Tick(door.Duration * 0.5f);
                    c.Check(door.gameObject.activeSelf && door.CurrentAmount > 0.4f && door.CurrentAmount < 0.6f,
                        $"A mitad de tiempo la puerta está medio disuelta ({door.CurrentAmount:F2})");
                    door.Tick(door.Duration);
                    c.Check(!door.gameObject.activeSelf && !door.IsDissolving, "Al terminar, la puerta desaparece");
                    door.ResetClosed();
                    c.Check(door.gameObject.activeSelf && door.DoorCollider.enabled && door.Obstacle.enabled && Mathf.Approximately(door.CurrentAmount, DoorDissolve.ClosedAmount),
                        "Al rebobinar, la puerta vuelve a estar cerrada y entera");
                }
            }
            finally
            {
                foreach (GameObject go in created)
                    if (go != null) Object.DestroyImmediate(go);
            }
        }
    }
}
