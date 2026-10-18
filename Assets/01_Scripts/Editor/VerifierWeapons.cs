using System.Collections.Generic;
using System.Linq;
using Overdrive.Combat;
using Overdrive.Core;
using Overdrive.Enemies;
using Overdrive.Level;
using Overdrive.Player;
using Overdrive.Weapons;
using UnityEditor;
using UnityEngine;

namespace Overdrive.EditorTools
{
    /// <summary>Verificación de armas: lógica pura y simulación con física en modo editor.</summary>
    internal static partial class Verifier
    {
        private static void VerifyWeaponLogic(Checker c)
        {
            c.Check(WeaponDefinition.Bat.IsMelee && WeaponDefinition.Knife.IsMelee, "Bate y cuchillo son cuerpo a cuerpo");
            c.Check(!WeaponDefinition.Shotgun.IsMelee && WeaponDefinition.Shotgun.MaxAmmo == 6 && WeaponDefinition.Shotgun.Pellets > 1,
                "La escopeta tiene 6 cartuchos y varios perdigones");
            c.Check(WeaponDefinition.Smg.MaxAmmo == 30 && WeaponDefinition.Smg.Automatic && WeaponDefinition.Smg.Pellets == 1,
                "El subfusil es automático con 30 balas");
            c.Check(WeaponDefinition.Smg.UseInterval < WeaponDefinition.Shotgun.UseInterval, "El subfusil dispara más rápido que la escopeta");
            c.Check(WeaponDefinition.Shotgun.ProjectileRange < WeaponDefinition.Smg.ProjectileRange * 0.5f,
                "Los perdigones de la escopeta llegan mucho menos lejos que las balas del subfusil");
            var enemyForHealth = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.EnemyPrefabPath);
            float enemyHealth = enemyForHealth != null ? enemyForHealth.GetComponent<Enemy>().MaxHealth : 0f;
            c.Check(enemyHealth > 0f && WeaponDefinition.Shotgun.Damage * 2f >= enemyHealth,
                $"Dos perdigones de escopeta bastan para matar a un enemigo normal ({enemyHealth} de vida)");
            c.Check(WeaponDefinition.Shotgun.ProjectileRange > WeaponDefinition.Shotgun.EnemyAttackRange &&
                    WeaponDefinition.Smg.ProjectileRange > WeaponDefinition.Smg.EnemyAttackRange,
                "Las balas llegan más lejos que la distancia desde la que disparan los enemigos");
            c.Check(LevelMap.WeaponFor('b') == WeaponType.Bat && LevelMap.WeaponFor('K') == WeaponType.Knife &&
                    LevelMap.WeaponFor('s') == WeaponType.Shotgun && LevelMap.WeaponFor('M') == WeaponType.Smg,
                "Las letras del mapa corresponden a sus armas");

            var shotgun = new WeaponState(WeaponDefinition.Shotgun);
            int shots = 0;
            for (int i = 0; i < 10; i++)
            {
                if (shotgun.TryUse()) shots++;
                shotgun.Tick(1f);
            }
            c.Check(shots == 6, $"La escopeta dispara 6 veces y se agota (disparó {shots})");
            c.Check(shotgun.IsEmpty && shotgun.Ammo == 0, "La munición se agota");
            c.Check(!shotgun.TryUse(), "Sin munición no dispara (no hay recarga)");

            var smg = new WeaponState(WeaponDefinition.Smg);
            c.Check(smg.TryUse() && !smg.TryUse(), "Hay enfriamiento entre disparos");
            smg.Tick(0.1f);
            c.Check(smg.TryUse() && smg.Ammo == 28, "Cada disparo del subfusil gasta una bala");

            var bat = new WeaponState(WeaponDefinition.Bat);
            int swings = 0;
            for (int i = 0; i < 100; i++)
            {
                if (bat.TryUse()) swings++;
                bat.Tick(1f);
            }
            c.Check(swings == 100 && !bat.IsEmpty, "Las armas cuerpo a cuerpo no gastan munición");

            float[] fan = ShotPattern.PelletAngles(6, 30f, 0f);
            c.Check(fan.Length == 6, "La escopeta genera 6 perdigones");
            c.Near(fan[0], -15f, "El abanico empieza a la izquierda");
            c.Near(fan[5], 15f, "El abanico termina a la derecha");
            float[] single = ShotPattern.PelletAngles(1, 6f, 1f);
            c.Check(single.Length == 1 && Mathf.Abs(single[0]) <= 3f + 0.001f, "El subfusil dispara una bala con poca desviación");
            c.Near(ShotPattern.Rotate(Vector3.forward, 90f), Vector3.right, "Girar 90° la dirección del disparo");

            var flight = new LinearFlight();
            flight.Launch(Vector3.zero, new Vector3(0f, 1f, 1f), 10f, 5f);
            c.Near(flight.Direction, Vector3.forward, "El vuelo es horizontal");
            float step = flight.NextStep(0.3f);
            c.Near(step, 3f, "Paso de vuelo = velocidad x tiempo");
            flight.Advance(step);
            c.Near(flight.NextStep(0.3f), 2f, "El vuelo no supera su distancia máxima");
            flight.Advance(2f);
            c.Check(!flight.Active, "El vuelo termina al llegar a su distancia máxima");

            var spots = new List<Vector3> { new Vector3(1f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(5f, 0f, 0f) };
            c.Check(PickupLogic.FindNearest(Vector3.zero, spots, 1.6f) == 1, "Se recoge el arma más cercana");
            c.Check(PickupLogic.FindNearest(new Vector3(20f, 0f, 0f), spots, 1.6f) == -1, "No se recogen armas lejanas");

            c.Check(NoiseSystem.CanHear(new Vector3(10f, 0f, 0f), Vector3.zero, 15f), "Un enemigo dentro del radio oye el disparo");
            c.Check(!NoiseSystem.CanHear(new Vector3(20f, 0f, 0f), Vector3.zero, 15f), "Un enemigo fuera del radio no lo oye");
        }

        /// <summary>Recoger, intercambiar, disparar, lanzar y golpear con los prefabs reales y física.</summary>
        private static void VerifyWeaponSimulation(Checker c)
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.PlayerPrefabPath);
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.EnemyPrefabPath);
            if (playerPrefab == null || enemyPrefab == null) return;

            var created = new List<GameObject>();
            Vector3 basePos = new Vector3(-500f, 0f, -500f);
            T Spawn<T>(GameObject prefab, Vector3 offset, float yaw = 0f) where T : Component
            {
                GameObject go = Object.Instantiate(prefab, basePos + offset, Quaternion.Euler(0f, yaw, 0f));
                created.Add(go);
                return go.GetComponent<T>();
            }
            WeaponItem SpawnWeapon(WeaponType type, Vector3 offset) =>
                Spawn<WeaponItem>(AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.WeaponPrefabPath(type)), offset);

            int noises = 0;
            float lastNoiseRadius = 0f;
            void OnNoise(Vector3 position, float radius)
            {
                noises++;
                lastNoiseRadius = radius;
            }
            NoiseSystem.NoiseMade += OnNoise;

            try
            {
                var weapons = Spawn<PlayerWeapons>(playerPrefab, Vector3.zero);
                GameObject player = weapons.gameObject;

                // --- Recoger e intercambiar ---
                WeaponItem batOnGround = SpawnWeapon(WeaponType.Bat, new Vector3(0.6f, 0f, 0f));
                WeaponItem shotgun = SpawnWeapon(WeaponType.Shotgun, new Vector3(0f, 0f, 5f));
                var items = new[] { batOnGround, shotgun };

                c.Check(weapons.TryPickup(items) == batOnGround, "E recoge el arma cercana");
                c.Check(weapons.Current == batOnGround && batOnGround.CurrentMode == WeaponItem.Mode.Held &&
                        batOnGround.transform.parent == weapons.Hand, "El arma recogida queda en la mano");
                c.Check(weapons.TryPickup(items) == null, "Sin armas cerca, E no hace nada");

                shotgun.DropAt(basePos + new Vector3(-0.6f, 0f, 0f), 0f);
                c.Check(weapons.TryPickup(items) == shotgun, "E intercambia por el arma del suelo");
                c.Check(weapons.Current == shotgun && batOnGround.IsOnGround && batOnGround.transform.parent == null,
                    "Al intercambiar, el arma anterior queda en el suelo");

                // --- Disparar la escopeta ---
                var spawned = new List<Projectile>();
                int pellets = shotgun.TryFire(weapons.ShotOrigin, player.transform.forward, Faction.Player, spawned);
                c.Check(pellets == WeaponDefinition.Shotgun.Pellets && spawned.Count == pellets,
                    $"La escopeta genera {WeaponDefinition.Shotgun.Pellets} perdigones (generó {spawned.Count})");
                c.Check(spawned.All(b => Mathf.Approximately(b.Flight.MaxDistance, WeaponDefinition.Shotgun.ProjectileRange)),
                    "Los perdigones desaparecen al alcance de la escopeta");
                c.Check(shotgun.State.Ammo == 5, "Cada disparo gasta un cartucho");
                c.Check(noises == 1 && Mathf.Approximately(lastNoiseRadius, WeaponDefinition.Shotgun.NoiseRadius),
                    "El disparo hace ruido con el radio del arma");
                c.Check(weapons.Attack(true, true, new Enemy[0]) == false, "Hay enfriamiento entre disparos");
                shotgun.State.Tick(1f);
                c.Check(!weapons.Attack(false, true, new Enemy[0]), "La escopeta no es automática: mantener el botón no dispara");
                for (int i = 0; i < 10; i++)
                {
                    shotgun.State.Tick(1f);
                    weapons.Attack(true, true, new Enemy[0]);
                }
                c.Check(shotgun.State.IsEmpty, "Simulación: la munición de la escopeta se agota");
                shotgun.State.Tick(1f);
                c.Check(!weapons.Attack(true, true, new Enemy[0]), "Sin munición no dispara");
                DestroyAllProjectiles();

                // --- Lanzar el arma vacía contra un enemigo armado ---
                var thrownTarget = Spawn<Enemy>(enemyPrefab, new Vector3(0f, 0f, 4f), 180f);
                WeaponItem knife = SpawnWeapon(WeaponType.Knife, new Vector3(0f, 0f, 10f));
                thrownTarget.Equip(knife);
                c.Check(knife.CurrentMode == WeaponItem.Mode.Held && knife.transform.parent == thrownTarget.Hand,
                    "El enemigo lleva su arma en la mano");
                Physics.SyncTransforms();

                c.Check(weapons.Throw() == shotgun && weapons.Current == null && shotgun.CurrentMode == WeaponItem.Mode.Thrown,
                    "Clic derecho lanza el arma");
                for (int i = 0; i < 60 && shotgun.CurrentMode == WeaponItem.Mode.Thrown; i++) shotgun.StepThrow(0.02f);
                c.Check(thrownTarget.Posture == Posture.Down && !thrownTarget.IsDead, "Simulación: un arma lanzada derriba y no mata");
                c.Check(shotgun.IsOnGround && shotgun.transform.position.z < thrownTarget.transform.position.z,
                    "El arma lanzada cae al suelo delante del enemigo");
                c.Check(thrownTarget.Weapon == null && knife.IsOnGround, "Al ser derribado, el enemigo suelta su arma");

                // --- Subfusil: la bala mata, atraviesa a los derribados y no daña a su dueño ---
                var shotTarget = Spawn<Enemy>(enemyPrefab, new Vector3(0f, 0f, 7f), 180f);
                WeaponItem targetBat = SpawnWeapon(WeaponType.Bat, new Vector3(0f, 0f, 20f));
                shotTarget.Equip(targetBat);
                WeaponItem smg = SpawnWeapon(WeaponType.Smg, new Vector3(0f, 0f, 30f));
                weapons.Equip(smg);
                Physics.SyncTransforms();

                Random.InitState(1234);
                spawned.Clear();
                c.Check(smg.TryFire(weapons.ShotOrigin, player.transform.forward, Faction.Player, spawned) == 1, "El subfusil dispara una bala");
                StepProjectiles(spawned);
                c.Check(!player.GetComponent<PlayerLife>().IsDead, "Tus propias balas no te dañan");
                c.Check(thrownTarget.Posture == Posture.Down, "La bala pasa por encima del enemigo derribado");
                c.Check(!shotTarget.IsDead && shotTarget.Health < shotTarget.MaxHealth, "Simulación: una bala en el cuerpo quita vida sin matar");
                for (int i = 0; i < 6 && !shotTarget.IsDead; i++) shotTarget.ReceiveDamage(WeaponDefinition.Smg.Damage, false, true, KillCause.Shot);
                c.Check(shotTarget.IsDead, "Simulación: sin vida, el enemigo muere");
                c.Check(shotTarget.Weapon == null && targetBat.IsOnGround, "Al morir, el enemigo armado deja su arma en el suelo");
                smg.State.Tick(1f);
                int ammoBefore = smg.State.Ammo;
                c.Check(weapons.Attack(false, true, new Enemy[0]) && smg.State.Ammo == ammoBefore - 1,
                    "El subfusil es automático: dispara manteniendo el botón");
                DestroyAllProjectiles();

                // --- Las paredes paran las balas ---
                player.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                var behindWall = Spawn<Enemy>(enemyPrefab, new Vector3(6f, 0f, 0f), -90f);
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                created.Add(wall);
                wall.transform.position = basePos + new Vector3(3f, 1.25f, 0f);
                wall.transform.localScale = new Vector3(0.5f, 2.5f, 4f);
                Physics.SyncTransforms();

                smg.State.Tick(1f);
                spawned.Clear();
                smg.TryFire(weapons.ShotOrigin, player.transform.forward, Faction.Player, spawned);
                StepProjectiles(spawned);
                c.Check(!behindWall.IsDead, "Simulación: las balas no atraviesan paredes");
                c.Check(spawned.TrueForAll(p => p == null), "La bala se destruye al chocar con la pared");

                // --- Cuerpo a cuerpo con arma: el bate quita vida (no mata de un golpe); a un derribado lo remata ---
                weapons.Throw();
                WeaponItem bat = SpawnWeapon(WeaponType.Bat, new Vector3(0f, 0f, 40f));
                weapons.Equip(bat);
                player.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
                var standing = Spawn<Enemy>(enemyPrefab, new Vector3(-1.3f, 0f, 0f), 90f);
                c.Check(weapons.Attack(true, true, new[] { standing }) && !standing.IsDead &&
                        Mathf.Approximately(standing.Health, standing.MaxHealth - WeaponDefinition.Bat.Damage),
                    "Simulación: el bate quita vida pero no mata de un golpe");
                var headTarget = Spawn<Enemy>(enemyPrefab, new Vector3(-1.3f, 0f, 3f), 90f);
                c.Check(!headTarget.ReceiveDamage(WeaponDefinition.Bat.Damage, true, WeaponDefinition.Bat.HeadKills) && !headTarget.IsDead,
                    "El bate en la cabeza tampoco mata de un golpe");
                c.Check(headTarget.ReceiveDamage(WeaponDefinition.Bat.Damage, false, WeaponDefinition.Bat.HeadKills) && headTarget.IsDead,
                    "Dos golpes de bate matan");
                c.Check(standing.ReceiveDamage(WeaponDefinition.Knife.Damage, true, WeaponDefinition.Knife.HeadKills) && standing.IsDead,
                    "El cuchillo en la cabeza mata de un golpe");

                var downed = Spawn<Enemy>(enemyPrefab, new Vector3(-1.2f, 0f, 0.3f), 90f);
                downed.ReceiveKnockdown();
                c.Check(!weapons.Attack(true, true, new[] { downed }), "El bate tiene enfriamiento");
                bat.State.Tick(1f);
                c.Check(weapons.Attack(true, true, new[] { downed }) && downed.IsDead,
                    "El arma cuerpo a cuerpo también mata a un derribado");
                c.Check(bat.State.Ready == false && !bat.State.IsEmpty, "El bate no gasta munición");

                // --- Escopeta a quemarropa: con el enemigo pegado, la bala no nace al otro lado de él ---
                weapons.Throw();
                player.transform.rotation = Quaternion.identity;
                WeaponItem pointBlankGun = SpawnWeapon(WeaponType.Shotgun, new Vector3(0f, 0f, 60f));
                weapons.Equip(pointBlankGun);
                var pointBlank = Spawn<Enemy>(enemyPrefab, new Vector3(0f, 0f, 0.85f), 180f);
                Physics.SyncTransforms();
                Vector3 origin = pointBlankGun.ShotOriginFrom(weapons.ShotOrigin);
                c.Check(Vector3.Distance(origin, weapons.ShotOrigin) < 0.001f,
                    "A quemarropa la bala sale del cuerpo, no de la boca del arma (que queda dentro o detrás del enemigo)");
                DestroyAllProjectiles();
                c.Check(weapons.Attack(true, true, new[] { pointBlank }), "La escopeta dispara a quemarropa");
                StepProjectiles(Object.FindObjectsByType<Projectile>(FindObjectsInactive.Include).ToList());
                c.Check(pointBlank.IsDead, "Simulación: la escopeta mata a un enemigo pegado al jugador");
                pointBlank.gameObject.SetActive(false);
                Physics.SyncTransforms();
                c.Check(Vector3.Distance(pointBlankGun.ShotOriginFrom(weapons.ShotOrigin), weapons.ShotOrigin) > 0.1f,
                    "Sin nadie delante, la bala sale de la boca del arma");
                DestroyAllProjectiles();

                // --- Mano alta (animación o modelo más grande): en vista cenital la bala va a la altura del pecho ---
                if (!ViewMode.FirstPerson)
                {
                    weapons.Hand.position += Vector3.up * 1.4f;
                    var tallTarget = Spawn<Enemy>(enemyPrefab, new Vector3(0f, 0f, 4f), 180f);
                    Physics.SyncTransforms();
                    float height = weapons.FireOrigin().y - player.transform.position.y;
                    c.Check(height > 0.5f && height < 1.5f, $"En vista cenital la bala sale a la altura del pecho ({height:F2} m), aunque la mano esté alta");
                    pointBlankGun.State.Tick(1f);
                    c.Check(weapons.Attack(true, true, new[] { tallTarget }), "Dispara con la mano alta");
                    StepProjectiles(Object.FindObjectsByType<Projectile>(FindObjectsInactive.Include).ToList());
                    c.Check(tallTarget.IsDead || tallTarget.Health < tallTarget.MaxHealth,
                        "Simulación: con la mano alta la bala no pasa por encima del enemigo (le da)");
                    weapons.Hand.position -= Vector3.up * 1.4f;
                    DestroyAllProjectiles();
                }

                // --- Balas enemigas: matan al jugador y no a otros enemigos ---
                var ally = Spawn<Enemy>(enemyPrefab, new Vector3(-3f, 0f, 0f), 90f);
                Physics.SyncTransforms();
                var bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.BulletPrefabPath);
                var enemyBullet = Spawn<Projectile>(bulletPrefab, Vector3.zero);
                enemyBullet.Launch(basePos + new Vector3(-6f, 1f, 0f), Vector3.right, 40f, Faction.Enemy);
                StepProjectiles(new List<Projectile> { enemyBullet });
                c.Check(!ally.IsDead, "Las balas enemigas no matan a otros enemigos");
                c.Check(player.GetComponent<PlayerLife>().IsDead, "Simulación: una bala enemiga mata al jugador de un disparo");
            }
            finally
            {
                NoiseSystem.NoiseMade -= OnNoise;
                DestroyAllProjectiles();
                foreach (GameObject go in created)
                    if (go != null) Object.DestroyImmediate(go);
            }
        }

        /// <summary>Los modelos de las armas miden su longitud de juego (exagerada para que se lean desde la cámara).</summary>
        private static void VerifyWeaponModelSizes(Checker c)
        {
            foreach (WeaponType type in System.Enum.GetValues(typeof(WeaponType)))
            {
                if (!WeaponModelFactory.TryGetWeapon(type, out WeaponModelFactory.Spec spec)) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.WeaponPrefabPath(type));
                if (prefab == null) continue;
                var copy = Object.Instantiate(prefab, new Vector3(-900f, 0f, -900f), Quaternion.identity);
                try
                {
                    Renderer[] renderers = copy.GetComponentsInChildren<Renderer>()
                        .Where(r => r.transform.name == WeaponModelFactory.MeshName || r.transform.GetComponentsInParent<Transform>().Any(t => t.name == WeaponModelFactory.MeshName))
                        .ToArray();
                    if (renderers.Length == 0) continue;
                    Bounds b = renderers[0].bounds;
                    foreach (Renderer r in renderers) b.Encapsulate(r.bounds);
                    float length = Mathf.Max(b.size.x, b.size.y, b.size.z);
                    float expected = WeaponModelFactory.RealLength(type) * WeaponModelFactory.WeaponGameScale;
                    c.Check(Mathf.Abs(length - expected) < expected * 0.15f,
                        $"{type}: el modelo mide {length:F2} m (longitud de juego {expected:F2} m, x{WeaponModelFactory.WeaponGameScale} la real)");
                }
                finally
                {
                    Object.DestroyImmediate(copy);
                }
            }
        }

        private static void StepProjectiles(List<Projectile> projectiles)
        {
            for (int i = 0; i < 200; i++)
            {
                bool anyActive = false;
                foreach (Projectile p in projectiles)
                {
                    if (p == null || !p.Active) continue;
                    p.Step(0.02f);
                    anyActive = true;
                }
                if (!anyActive) return;
            }
        }

        private static void DestroyAllProjectiles()
        {
            foreach (Projectile p in Object.FindObjectsByType<Projectile>(FindObjectsInactive.Include))
                Object.DestroyImmediate(p.gameObject);
        }
    }
}
