using System.Collections.Generic;
using System.Linq;
using Overdrive.CameraRig;
using Overdrive.Combat;
using Overdrive.Core;
using Overdrive.Enemies;
using Overdrive.Level;
using Overdrive.Player;
using Overdrive.Scoring;
using Overdrive.UI;
using Overdrive.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overdrive.EditorTools
{
    /// <summary>Verificación del rebobinado: lógica de instantáneas y una partida simulada en la escena real.</summary>
    internal static partial class Verifier
    {
        private static void VerifyRewindLogic(Checker c)
        {
            // --- Búfer circular ---
            int capacity = RewindBuffer<int>.CapacityFor(5f, 0.1f);
            c.Check(capacity == 51, $"5 s grabando cada 0,1 s son 51 instantáneas (son {capacity})");
            var buffer = new RewindBuffer<int>(capacity);
            for (int i = 0; i < 60; i++) buffer.Push(i);
            c.Check(buffer.Count == 51 && buffer.Oldest == 9, "El búfer guarda solo los últimos 5 s y descarta lo más viejo");
            buffer.ResetTo(42);
            c.Check(buffer.Count == 1 && buffer.Oldest == 42, "Tras rebobinar, el búfer vuelve a empezar desde ese punto");

            // --- Combate ---
            var combat = new CombatState();
            combat.KnockDown(3f);
            combat.Tick(1f);
            CombatState.Snapshot down = combat.Capture();
            combat.Tick(5f);
            combat.Kill();
            combat.Restore(down);
            c.Check(combat.IsDown && !combat.IsDead && Mathf.Approximately(combat.DownTimeRemaining, 2f),
                "Rebobinar devuelve a un enemigo muerto a como estaba (derribado, con su tiempo en el suelo)");
            combat.Finish();
            c.Check(combat.IsDead, "Tras rebobinar se le puede volver a matar");

            // --- Cerebro ---
            var brain = new EnemyBrain(Vector3.zero, null, BrainSettings.Default);
            brain.HearNoise(new Vector3(5f, 0f, 0f));
            EnemyBrain.Snapshot calm = brain.Capture();
            brain.Alert(new Vector3(8f, 0f, 0f));
            brain.Restore(calm);
            c.Check(brain.State == AIState.Investigate && !brain.IsInCombat && brain.LastKnownPlayerPosition == new Vector3(5f, 0f, 0f),
                "Rebobinar devuelve la IA a lo que estaba haciendo y olvida lo que vio después");

            // --- Munición y vuelo ---
            var ammo = new WeaponState(WeaponDefinition.Shotgun);
            int full = ammo.Ammo;
            for (int i = 0; i < 6; i++) { ammo.TryUse(); ammo.Tick(1f); }
            ammo.Restore(full, 0f);
            c.Check(ammo.Ammo == 6 && !ammo.IsEmpty, "Rebobinar devuelve la munición gastada");

            var flight = new LinearFlight();
            flight.Launch(Vector3.zero, Vector3.forward, 10f, 20f);
            flight.Advance(4f);
            LinearFlight.Snapshot mid = flight.Capture();
            flight.Advance(16f);
            flight.Restore(mid);
            c.Check(flight.Active && Mathf.Approximately(flight.Travelled, 4f), "Un arma lanzada vuelve a estar a medio vuelo");

            // --- Progreso y HUD ---
            var progress = new LevelProgress();
            progress.Evaluate(10, 0, true, false);
            progress.Restore(LevelState.Playing);
            c.Check(progress.Evaluate(10, 0, false, false) == LevelState.Playing, "Tras rebobinar una muerte se sigue jugando");
            c.Check(HudText.RewindHint(2) == "T: rebobinar 5 s (quedan 2)" && HudText.RewindHint(0) == HudText.NoRewindsLeft,
                "El HUD indica cuántos rebobinados quedan");
        }

        private static void VerifyRewindScene(Checker c)
        {
            Scene scene = SceneManager.GetActiveScene();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.PlayerPrefabPath);
            var rewind = prefab != null ? prefab.GetComponent<PlayerRewind>() : null;
            c.NotNull(rewind, "Prefab Player: falta PlayerRewind");
            if (rewind != null)
            {
                c.Check(rewind.Life == prefab.GetComponent<PlayerLife>() && rewind.Controller == prefab.GetComponent<PlayerController>() &&
                        rewind.Weapons == prefab.GetComponent<PlayerWeapons>() && rewind.Finisher == prefab.GetComponent<PlayerFinisher>() &&
                        rewind.Melee == prefab.GetComponent<PlayerMelee>() && rewind.Footsteps == prefab.GetComponent<PlayerFootsteps>(),
                    "PlayerRewind tiene todas sus referencias");
            }

            GameObject systems = BuilderUtil.FindRoot(scene, SceneFactory.SystemsName);
            var rewinder = systems != null ? systems.GetComponent<TimeRewinder>() : null;
            c.NotNull(rewinder, "Systems: falta TimeRewinder");
            if (rewinder == null) return;
            GameObject player = BuilderUtil.FindRoot(scene, SceneFactory.PlayerName);
            c.Check(player != null && rewinder.Player == player.GetComponent<PlayerLife>(), "TimeRewinder.player sin asignar");
            c.Check(rewinder.Level == systems.GetComponent<LevelManager>(), "TimeRewinder.level sin asignar");
            c.Check(rewinder.CameraRig == BuilderUtil.FindRoot(scene, SceneFactory.CameraRigName)?.GetComponent<TopDownCamera>(),
                "TimeRewinder.cameraRig sin asignar");
            c.Check(rewinder.MaxUses == 3 && Mathf.Approximately(rewinder.Window, 5f), "3 rebobinados de 5 s por nivel");
            c.Check(systems.GetComponent<GameHUD>()?.Rewinder == rewinder, "GameHUD.rewinder sin asignar");
        }

        /// <summary>
        /// Partida simulada en la escena real: se graba, pasan muchas cosas, el jugador muere y se rebobina.
        /// Debe ejecutarse al final: modifica la escena (que no se guarda).
        /// </summary>
        private static void VerifyRewindSimulation(Checker c, LevelLayout layout)
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject systems = BuilderUtil.FindRoot(scene, SceneFactory.SystemsName);
            var rewinder = systems != null ? systems.GetComponent<TimeRewinder>() : null;
            var manager = systems != null ? systems.GetComponent<LevelManager>() : null;
            var score = systems != null ? systems.GetComponent<ScoreKeeper>() : null;
            var blood = BloodSpawner.Instance;
            GameObject playerGo = BuilderUtil.FindRoot(scene, SceneFactory.PlayerName);
            Transform enemiesRoot = BuilderUtil.FindRoot(scene, SceneFactory.EnemiesName)?.transform;
            Transform weaponsRoot = BuilderUtil.FindRoot(scene, SceneFactory.WeaponsName)?.transform;
            Transform objectiveRoot = BuilderUtil.FindRoot(scene, SceneFactory.ObjectiveName)?.transform;
            if (rewinder == null || manager == null || score == null || blood == null || playerGo == null || enemiesRoot == null ||
                weaponsRoot == null || objectiveRoot == null || manager.Briefcase == null || manager.Reinforcements == null)
            {
                c.Check(false, "Faltan objetos de la escena para simular el rebobinado");
                return;
            }

            var player = playerGo.GetComponent<PlayerLife>();
            var weapons = playerGo.GetComponent<PlayerWeapons>();
            BriefcasePickup briefcase = manager.Briefcase;
            ReinforcementWave wave = manager.Reinforcements;
            Enemy victim = enemiesRoot.Find(SceneFactory.EnemyName('m', 11, 5)).GetComponent<Enemy>();
            Enemy knocked = enemiesRoot.Find(SceneFactory.EnemyName('k', 26, 5)).GetComponent<Enemy>();
            WeaponItem bat = weaponsRoot.GetComponentInChildren<WeaponItem>();
            WeaponItem victimWeapon = victim.Weapon;

            // --- Qué se graba ---
            rewinder.Gather();
            List<IRewindable> targets = rewinder.Targets.ToList();
            c.Check(targets.OfType<PlayerRewind>().Count() == 1, "Se graba al jugador");
            c.Check(targets.OfType<EnemyAI>().Count() == 14, $"Se graban los 10 enemigos y los 4 refuerzos ({targets.OfType<EnemyAI>().Count()})");
            c.Check(targets.OfType<WeaponItem>().Count() == 15, $"Se graban las 15 armas, también las de los refuerzos ({targets.OfType<WeaponItem>().Count()})");
            c.Check(targets.Contains(briefcase) && targets.Contains(wave) && targets.Contains(manager) && targets.Contains(blood),
                "Se graban el maletín, los refuerzos, el progreso y la sangre");
            c.Check(targets.Contains(score), "Se graba la puntuación");

            // --- Momento guardado ---
            manager.Evaluate();
            Vector3 playerStart = playerGo.transform.position;
            Vector3 victimStart = victim.transform.position;
            Vector3 batStart = bat.transform.position;
            Vector3 briefcaseStart = briefcase.transform.position;
            int bloodStart = blood.SplatCount;
            int pointsStart = score.State.Points;
            rewinder.Record();

            // --- Pasan cosas ---
            CharacterController cc = playerGo.GetComponent<CharacterController>();
            void MovePlayer(Vector3 position)
            {
                cc.enabled = false;
                playerGo.transform.position = position;
                cc.enabled = true;
            }
            MovePlayer(bat.transform.position);
            c.Check(weapons.TryPickup(new[] { bat }) == bat, "Simulación: el jugador recoge el bate");
            victim.ReceiveLethalHit(KillCause.Melee);
            knocked.ReceiveKnockdown();
            // En modo editor ScoreKeeper no se suscribe a los eventos (no hay OnEnable): se suma a mano.
            score.AddKill(KillCause.Melee, victim.transform.position);
            score.AddKnockdown(KnockdownCause.Punch, knocked.transform.position);
            knocked.GetComponent<EnemyAI>().Brain.Alert(playerGo.transform.position);
            MovePlayer(briefcase.transform.position);
            briefcase.TryCollect(playerGo.transform);
            manager.Evaluate();
            c.Check(briefcase.IsCollected && wave.Triggered && wave.Squad.All(a => a.gameObject.activeSelf),
                "Simulación: coge el maletín y entran los refuerzos");
            weapons.Throw();
            var bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.BulletPrefabPath);
            Object.Instantiate(bulletPrefab).GetComponent<Projectile>()
                .Launch(playerGo.transform.position + Vector3.up, Vector3.right, 40f, Faction.Enemy);
            player.Kill();
            c.Check(manager.Evaluate() == LevelState.Failed && rewinder.CanRewind, "Al morir se puede rebobinar");
            c.Check(blood.SplatCount > bloodStart, "Hay sangre nueva antes de rebobinar");

            // --- Rebobinar ---
            c.Check(rewinder.Rewind(), "Simulación: T rebobina");
            LevelState after = manager.Evaluate();
            c.Check(rewinder.UsesLeft == 2, "Rebobinar gasta un uso");
            c.Check(!player.IsDead && Vector3.Angle(player.Body.up, Vector3.up) < 1f, "El jugador vuelve a estar vivo y de pie");
            c.Near(playerGo.transform.position, playerStart, "El jugador vuelve a donde estaba");
            c.Check(weapons.Current == null && weapons.enabled, "El jugador vuelve a ir sin arma");
            c.Check(!victim.IsDead && victim.Posture == Posture.Standing && victim.BodyCollider.enabled,
                "El enemigo muerto vuelve a estar vivo y de pie");
            c.Check(victim.BodyRenderer.sharedMaterial == AssetFactory.Mat(AssetFactory.MatEnemy), "El enemigo recupera su color de vivo");
            c.Near(victim.transform.position, victimStart, "El enemigo vuelve a su sitio");
            c.Check(victim.Weapon == victimWeapon && victimWeapon.CurrentMode == WeaponItem.Mode.Held &&
                    victimWeapon.transform.parent == victim.Hand, "El enemigo vuelve a tener su arma en la mano");
            c.Check(knocked.Posture == Posture.Standing && !knocked.GetComponent<EnemyAI>().Brain.IsInCombat,
                "El enemigo derribado vuelve a estar de pie y sin haberte visto");
            c.Check(bat.IsOnGround && bat.transform.parent == weaponsRoot, "El bate vuelve al suelo");
            c.Near(bat.transform.position, batStart, "El bate vuelve a su sitio");
            c.Check(!briefcase.IsCollected && briefcase.transform.parent == objectiveRoot, "El maletín vuelve a estar sin recoger");
            c.Near(briefcase.transform.position, briefcaseStart, "El maletín vuelve a su sitio");
            c.Check(!wave.Triggered && wave.Door.activeSelf && wave.Squad.All(a => !a.gameObject.activeSelf),
                "Los refuerzos vuelven tras la puerta cerrada");
            c.Check(Object.FindObjectsByType<Projectile>(FindObjectsInactive.Include).Length == 0, "Las balas en el aire desaparecen");
            c.Check(blood.SplatCount == bloodStart, "La sangre posterior desaparece");
            c.Check(score.State.Points == pointsStart && score.State.Combo == 0 && score.Popups.Count == 0,
                "Los puntos ganados después del rebobinado se pierden");
            c.Check(after == LevelState.Playing && manager.Progress.Remaining == 10 && !manager.Progress.HasObjective,
                "El nivel vuelve a estar en juego, con 10 enemigos y sin el maletín");

            // --- Tres usos y no más ---
            for (int i = 0; i < 2; i++)
            {
                player.Kill();
                manager.Evaluate();
                rewinder.Rewind();
                manager.Evaluate();
            }
            c.Check(rewinder.UsesLeft == 0 && !player.IsDead, "Se puede rebobinar 3 veces por nivel");
            player.Kill();
            manager.Evaluate();
            c.Check(!rewinder.CanRewind && !rewinder.Rewind() && player.IsDead, "Sin usos, ya no se puede rebobinar");
            c.Check(HudText.RewindHint(rewinder.UsesLeft) == HudText.NoRewindsLeft, "El HUD avisa de que no quedan rebobinados");
        }
    }
}
