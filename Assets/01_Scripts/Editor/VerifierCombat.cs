using System.Collections.Generic;
using System.IO;
using Overdrive.Combat;
using Overdrive.Enemies;
using Overdrive.Level;
using Overdrive.Player;
using Overdrive.Weapons;
using UnityEditor;
using UnityEngine;

namespace Overdrive.EditorTools
{
    /// <summary>
    /// Vida, daño por zonas (cabeza / cuerpo), reglas de bate y cuchillo, lanzamientos, puntería de los
    /// enemigos, guardado automático y que no haya rebobinado. Con los prefabs reales.
    /// </summary>
    internal static partial class Verifier
    {
        private static void VerifyDamageAndHitZones(Checker c)
        {
            VerifyDamageLogic(c);
            VerifyHitZoneSimulation(c);
            VerifyCheckpointSave(c);
        }

        private static void VerifyDamageLogic(Checker c)
        {
            c.Check(!TimeRewinder.Enabled, "El rebobinado está quitado");
            c.Check(WeaponDefinition.Bat.IsMelee && !WeaponDefinition.Bat.HeadKills && WeaponDefinition.Bat.Damage < 100f,
                "El bate solo quita vida, también en la cabeza");
            c.Check(WeaponDefinition.Knife.HeadKills && WeaponDefinition.Knife.Damage < 100f && WeaponDefinition.Knife.ThrownHeadKills,
                "El cuchillo mata solo en la cabeza (golpe o lanzado)");
            foreach (WeaponDefinition gun in new[] { WeaponDefinition.Shotgun, WeaponDefinition.Smg, WeaponDefinition.Revolver })
                c.Check(gun.HeadKills && gun.Damage < 100f, $"{gun.DisplayName}: en la cabeza mata, en el cuerpo quita vida");

            var flight = new LinearFlight();
            flight.Launch(Vector3.zero, new Vector3(0f, -0.5f, 1f), 10f, 5f, horizontal: false);
            c.Check(flight.Direction.y < -0.3f, "En primera persona las balas y lanzamientos conservan la inclinación (abajo/arriba)");
        }

        private static void VerifyHitZoneSimulation(Checker c)
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.PlayerPrefabPath);
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.EnemyPrefabPath);
            var bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.BulletPrefabPath);
            if (playerPrefab == null || enemyPrefab == null || bulletPrefab == null) return;

            var created = new List<GameObject>();
            Vector3 basePos = new Vector3(-700f, 0f, 700f);
            T Spawn<T>(GameObject prefab, Vector3 offset, float yaw) where T : Component
            {
                GameObject go = Object.Instantiate(prefab, basePos + offset, Quaternion.Euler(0f, yaw, 0f));
                created.Add(go);
                HeadHitbox.Ensure(go.transform); // en el juego lo crean Start() del jugador y del enemigo
                return go.GetComponent<T>();
            }
            bool Shoot(Vector3 from, Vector3 at, Faction owner, WeaponDefinition def)
            {
                var p = Object.Instantiate(bulletPrefab).GetComponent<Projectile>();
                created.Add(p.gameObject);
                Physics.SyncTransforms();
                p.Launch(from, at - from, def.ProjectileSpeed, owner, 30f, def.Damage, def.HeadKills);
                for (int i = 0; i < 200 && p != null && p.Active; i++) p.Step(0.02f);
                return true;
            }

            try
            {
                // --- Zonas de impacto ajustadas al modelo ---
                var e1 = Spawn<Enemy>(enemyPrefab, new Vector3(0f, 0f, 0f), 180f);
                Physics.SyncTransforms();
                Transform head = e1.transform.Find(HeadHitbox.ObjectName);
                Transform body = e1.transform.Find(HeadHitbox.BodyName);
                c.Check(head != null && body != null, "Cada personaje tiene zona de cabeza y de cuerpo");
                if (head != null && body != null)
                {
                    var capsule = body.GetComponent<CapsuleCollider>();
                    float top = capsule.bounds.max.y - e1.transform.position.y;
                    c.Check(top > 1.85f, $"La zona del cuerpo llega al cuello del modelo, más alta que la cápsula de 1,8 m ({top:F2} m)");
                    c.Check(head.position.y - e1.transform.position.y > 1.9f, "La cabeza está a la altura del modelo");
                }

                Vector3 gun = basePos + new Vector3(0f, 1.4f, -4f);

                // Disparo a la cabeza: mata de un tiro.
                Shoot(gun, HitZones.HeadPoint(e1.transform), Faction.Player, WeaponDefinition.Smg);
                c.Check(e1.IsDead, "Simulación: un disparo a la cabeza mata al enemigo de un tiro");

                // Disparo al cuerpo: quita vida.
                var e2 = Spawn<Enemy>(enemyPrefab, new Vector3(3f, 0f, 0f), 180f);
                Physics.SyncTransforms();
                Shoot(gun + new Vector3(3f, 0f, 0f), HitZones.ChestPoint(e2.transform), Faction.Player, WeaponDefinition.Smg);
                c.Check(!e2.IsDead && Mathf.Approximately(e2.Health, e2.MaxHealth - WeaponDefinition.Smg.Damage),
                    $"Simulación: un disparo al cuerpo quita vida sin matar ({e2.Health})");

                // Cuello / pecho alto (por encima de la cápsula de 1,8 m): también cuenta.
                float neck = HitZones.HeadY(e2.transform) - 0.42f;
                float before = e2.Health;
                Shoot(gun + new Vector3(3f, 0f, 0f), new Vector3(e2.transform.position.x, neck, e2.transform.position.z), Faction.Player, WeaponDefinition.Smg);
                c.Check(e2.Health < before && !e2.IsDead, $"Simulación: el pecho alto ({neck:F2} m) también se puede alcanzar");

                // Cuchillo lanzado a la cabeza: mata. Bate lanzado a la cabeza: no.
                var knifePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.WeaponPrefabPath(WeaponType.Knife));
                var batPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.WeaponPrefabPath(WeaponType.Bat));
                var e3 = Spawn<Enemy>(enemyPrefab, new Vector3(6f, 0f, 0f), 180f);
                var e4 = Spawn<Enemy>(enemyPrefab, new Vector3(9f, 0f, 0f), 180f);
                Physics.SyncTransforms();
                var knife = Object.Instantiate(knifePrefab).GetComponent<WeaponItem>();
                created.Add(knife.gameObject);
                Vector3 from3 = gun + new Vector3(6f, 0f, 0f);
                knife.Throw(from3, HitZones.HeadPoint(e3.transform) - from3, Faction.Player, keepPitch: true);
                for (int i = 0; i < 100 && knife.CurrentMode == WeaponItem.Mode.Thrown; i++) knife.StepThrow(0.02f);
                c.Check(e3.IsDead, "Simulación: un cuchillo lanzado a la cabeza mata");
                var bat = Object.Instantiate(batPrefab).GetComponent<WeaponItem>();
                created.Add(bat.gameObject);
                Vector3 from4 = gun + new Vector3(9f, 0f, 0f);
                bat.Throw(from4, HitZones.HeadPoint(e4.transform) - from4, Faction.Player, keepPitch: true);
                for (int i = 0; i < 100 && bat.CurrentMode == WeaponItem.Mode.Thrown; i++) bat.StepThrow(0.02f);
                c.Check(!e4.IsDead && e4.Health < e4.MaxHealth, "Simulación: un bate lanzado a la cabeza quita vida pero no mata");

                // --- El jugador ---
                var player = Spawn<PlayerLife>(playerPrefab, new Vector3(0f, 0f, 20f), 0f);
                Physics.SyncTransforms();
                c.Check(Mathf.Approximately(player.CurrentHealth, 100f) && Mathf.Approximately(player.MaxHealth, 100f), "El jugador tiene 100 de vida");
                Vector3 enemyGun = basePos + new Vector3(0f, 1.4f, 24f);
                Shoot(enemyGun, HitZones.ChestPoint(player.transform), Faction.Enemy, WeaponDefinition.Smg);
                c.Check(!player.IsDead && Mathf.Approximately(player.CurrentHealth, 100f - WeaponDefinition.Smg.Damage),
                    $"Simulación: un disparo enemigo al cuerpo quita vida ({player.CurrentHealth})");
                c.Check(!player.TakeHit(WeaponDefinition.Bat.Damage, true, WeaponDefinition.Bat.HeadKills) && !player.IsDead,
                    "Un batazo en la cabeza no mata al jugador");
                Shoot(enemyGun, HitZones.HeadPoint(player.transform), Faction.Enemy, WeaponDefinition.Smg);
                c.Check(player.IsDead, "Simulación: un disparo enemigo a la cabeza mata al jugador de un tiro");

                var p2 = Spawn<PlayerLife>(playerPrefab, new Vector3(5f, 0f, 20f), 0f);
                c.Check(p2.TakeHit(WeaponDefinition.Knife.Damage, true, WeaponDefinition.Knife.HeadKills) && p2.IsDead,
                    "Un cuchillazo en la cabeza mata al jugador");
                var p3 = Spawn<PlayerLife>(playerPrefab, new Vector3(10f, 0f, 20f), 0f);
                for (int i = 0; i < 2; i++) p3.TakeHit(WeaponDefinition.Bat.Damage, false, false);
                c.Check(!p3.IsDead, "Dos batazos no matan al jugador");
                p3.TakeHit(WeaponDefinition.Bat.Damage, false, false);
                c.Check(p3.IsDead, "Al llegar a 0 de vida el jugador muere");

                // --- Puntería de los enemigos ---
                var weak = Spawn<EnemyAI>(enemyPrefab, new Vector3(0f, 0f, 40f), 0f);
                var strong = Spawn<EnemyAI>(enemyPrefab, new Vector3(3f, 0f, 40f), 0f);
                weak.Skill = 0.15f;
                strong.Skill = 1f;
                c.Check(weak.HeadChance < 0.02f && strong.HeadChance > 0.4f, "Solo los enemigos buenos apuntan a menudo a la cabeza");
                c.Check(weak.AimError > strong.AimError * 3f, "Los enemigos malos fallan más");
                var rolled = new List<float>();
                for (int i = 0; i < 40; i++)
                {
                    var e = Spawn<EnemyAI>(enemyPrefab, new Vector3(i * 2f, 0f, 60f), 0f);
                    rolled.Add(e.Skill);
                }
                rolled.Sort();
                c.Check(rolled[rolled.Count / 2] < 0.5f && rolled[rolled.Count - 1] > rolled[0] + 0.2f,
                    $"Puntería variada: la mayoría son mediocres y pocos son buenos (mediana {rolled[rolled.Count / 2]:F2})");
            }
            finally
            {
                foreach (GameObject go in created)
                    if (go != null) Object.DestroyImmediate(go);
                foreach (Projectile p in Object.FindObjectsByType<Projectile>(FindObjectsInactive.Include))
                    Object.DestroyImmediate(p.gameObject);
            }
        }

        private static void VerifyCheckpointSave(Checker c)
        {
            // No pisa la partida guardada de quien ejecuta la verificación.
            string backup = SaveSystem.HasSave ? File.ReadAllText(SaveSystem.FilePath) : null;
            try
            {
                SaveSystem.SaveCheckpoint("Level01");
                c.Check(SaveSystem.HasSave && File.ReadAllText(SaveSystem.FilePath).Contains("\"Level01\""),
                    "Al pasar de nivel se guarda automáticamente el nivel siguiente");
                SaveSystem.SaveCheckpoint("Menu");
                c.Check(!SaveSystem.HasSave, "Al terminar el último nivel se borra la partida guardada");
            }
            finally
            {
                if (backup != null) File.WriteAllText(SaveSystem.FilePath, backup);
                else SaveSystem.Delete();
            }
        }
    }
}
