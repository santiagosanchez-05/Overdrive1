using System.Collections.Generic;
using Overdrive.Combat;
using Overdrive.Core;
using Overdrive.Enemies;
using Overdrive.Level;
using Overdrive.Player;
using Overdrive.Scoring;
using Overdrive.UI;
using UnityEditor;
using UnityEngine;

namespace Overdrive.EditorTools
{
    /// <summary>Verificación del sigilo: detección gradual, pasos, Ctrl y ataque por la espalda.</summary>
    internal static partial class Verifier
    {
        private static void VerifyStealthLogic(Checker c)
        {
            BrainSettings s = BrainSettings.Default;
            DetectionSettings d = DetectionSettings.Default;
            Perception See(Vector3 player) => new Perception
            {
                SelfPosition = Vector3.zero, CanSeePlayer = true, PlayerPosition = player,
                Weapon = WeaponKind.Melee, AttackRange = 1.5f,
            };
            Perception Blind() => new Perception { SelfPosition = Vector3.zero, Weapon = WeaponKind.Melee, AttackRange = 1.5f };

            // --- Detección gradual ---
            var brain = new EnemyBrain(Vector3.zero, null, s, d);
            c.Check(brain.DetectionTime(10f) > brain.DetectionTime(3f), "De lejos tarda más en detectarte que de cerca");
            Vector3 far = new Vector3(10f, 0f, 0f);
            brain.Tick(0.1f, See(far));
            c.Check(brain.State == AIState.Idle && brain.Awareness == Awareness.Unaware && brain.Suspicion > 0f,
                "Un vistazo breve de lejos solo empieza a llenar la sospecha");
            c.Check(brain.HasLookTarget && brain.LookTarget == far, "Mientras te ve, te mira");

            float toSuspicious = d.suspiciousThreshold * brain.DetectionTime(10f);
            brain.Tick(toSuspicious, See(far));
            c.Check(brain.State == AIState.Investigate && brain.Awareness == Awareness.Suspicious,
                "Con la sospecha a medias aparece ? y va a mirar");
            c.Check(brain.HasMoveTarget && brain.MoveTarget == far, "Al sospechar, se acerca a donde te vio");

            brain.Tick(brain.DetectionTime(10f), See(far));
            c.Check(brain.State == AIState.Chase && brain.Awareness == Awareness.Alerted && Mathf.Approximately(brain.Suspicion, 1f),
                "Con la sospecha llena aparece ! y te persigue");

            // --- La sospecha baja si te escondes ---
            var forget = new EnemyBrain(Vector3.zero, null, s, d);
            forget.Tick(0.2f, See(far));
            float before = forget.Suspicion;
            forget.Tick(0.5f, Blind());
            c.Check(forget.Suspicion < before, "Si te pierde de vista antes de detectarte, la sospecha baja");
            forget.Tick(10f, Blind());
            c.Check(forget.Suspicion == 0f && forget.Awareness == Awareness.Unaware, "Con tiempo se olvida de ti");

            // --- De cerca, al instante; cazadores, al instante ---
            var close = new EnemyBrain(Vector3.zero, null, s, d);
            close.Tick(0.05f, See(new Vector3(1.2f, 0f, 0f)));
            c.Check(close.Awareness == Awareness.Alerted, "Pegado a él te detecta al instante");

            // --- Ver morir a otro enemigo ---
            var witness = new EnemyBrain(Vector3.zero, null, s, d);
            Vector3 body = new Vector3(5f, 0f, 0f);
            witness.WitnessDeath(body);
            c.Check(witness.Awareness == Awareness.Alerted && witness.Hunting && witness.LastKnownPlayerPosition == body,
                "Ver morir a otro enemigo lo alerta (!) y lo deja en guardia");
            witness.Tick(0.1f, Blind());
            c.Check(witness.HasMoveTarget && witness.MoveTarget == body, "Tras ver morir a otro, corre hacia el cadáver");

            var hunter = new EnemyBrain(Vector3.zero, null, s, d);
            hunter.SetHunting(true);
            hunter.Tick(0.05f, See(far));
            c.Check(hunter.Awareness == Awareness.Alerted, "Los refuerzos te detectan en cuanto te ven");

            // --- Ruido ---
            var listener = new EnemyBrain(Vector3.zero, null, s, d);
            listener.HearNoise(new Vector3(4f, 0f, 0f));
            c.Check(listener.Awareness == Awareness.Suspicious && listener.Suspicion >= d.suspiciousThreshold,
                "Un ruido lo pone en sospecha (?)");
            listener.Alert(far);
            c.Check(listener.Awareness == Awareness.Alerted && listener.IsInCombat, "Alert lo pone en combate (!)");

            // --- Indicadores del HUD ---
            c.Check(HudText.AwarenessMark(Awareness.Unaware) == null, "Sin sospecha no hay indicador");
            c.Check(HudText.AwarenessMark(Awareness.Suspicious) == "?", "Sospecha: ?");
            c.Check(HudText.AwarenessMark(Awareness.Alerted) == "!", "Detectado: !");

            // --- Por la espalda ---
            c.Check(StealthLogic.IsBackstab(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, -1f), false), "Detrás y sin detectarte: por la espalda");
            c.Check(!StealthLogic.IsBackstab(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 1f), false), "De frente no es por la espalda");
            c.Check(!StealthLogic.IsBackstab(Vector3.zero, Vector3.forward, new Vector3(1f, 0f, 0f), false), "De lado no es por la espalda");
            c.Check(!StealthLogic.IsBackstab(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, -1f), true), "Si ya te detectó, no hay ataque por la espalda");

            // --- Pasos ---
            var steps = new FootstepCounter(1.4f);
            c.Check(!steps.Advance(1f) && steps.Advance(0.5f), "Cada 1,4 m andados se da un paso");
            c.Check(!steps.Advance(1.2f), "El resto de la distancia se acumula para el siguiente paso");
        }

        /// <summary>Ctrl, ruido de pasos y ataque por la espalda con los prefabs reales.</summary>
        private static void VerifyStealthSimulation(Checker c)
        {
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.PlayerPrefabPath);
            var enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetFactory.EnemyPrefabPath);
            if (playerPrefab == null || enemyPrefab == null) return;

            var created = new List<GameObject>();
            Vector3 basePos = new Vector3(-800f, 0f, 800f);
            GameObject Make(GameObject prefab, Vector3 offset, float yaw)
            {
                GameObject go = Object.Instantiate(prefab, basePos + offset, Quaternion.Euler(0f, yaw, 0f));
                created.Add(go);
                return go;
            }

            int noises = 0;
            float lastRadius = 0f;
            void OnNoise(Vector3 position, float radius)
            {
                noises++;
                lastRadius = radius;
            }
            NoiseSystem.NoiseMade += OnNoise;

            try
            {
                // --- Referencias y velocidad agachado ---
                var player = Make(playerPrefab, Vector3.zero, 0f);
                var controller = player.GetComponent<PlayerController>();
                var footsteps = player.GetComponent<PlayerFootsteps>();
                c.NotNull(footsteps, "Prefab Player: falta PlayerFootsteps");
                if (footsteps == null) return;
                c.Check(footsteps.Controller == controller, "PlayerFootsteps.controller sin asignar");
                c.Check(controller.SneakSpeed < controller.MoveSpeed * 0.8f, "Agachado (Ctrl) se camina más lento");

                // --- Pasos ---
                Vector3 p = basePos;
                footsteps.Step(p, false);
                int emitted = 0;
                for (int i = 0; i < 10; i++)
                {
                    p += new Vector3(0.5f, 0f, 0f);
                    if (footsteps.Step(p, false)) emitted++;
                }
                c.Check(emitted >= 3 && noises == emitted && Mathf.Approximately(lastRadius, footsteps.NoiseRadius),
                    $"Simulación: caminar normal hace ruido de pasos ({emitted} pasos en 5 m)");

                int noisesBefore = noises;
                for (int i = 0; i < 10; i++)
                {
                    p += new Vector3(0.5f, 0f, 0f);
                    footsteps.Step(p, true);
                }
                c.Check(noises == noisesBefore, "Simulación: agachado con Ctrl no hace ruido");
                c.Check(!footsteps.Step(p + new Vector3(10f, 0f, 0f), false), "Un salto grande (remate, reinicio) no cuenta como paso");

                // --- Un enemigo cerca oye los pasos; uno lejos no ---
                var nearAi = Make(enemyPrefab, new Vector3(3f, 0f, 3f), 0f).GetComponent<EnemyAI>();
                var farAi = Make(enemyPrefab, new Vector3(30f, 0f, 0f), 0f).GetComponent<EnemyAI>();
                c.Check(nearAi.HearNoise(basePos, footsteps.NoiseRadius) && nearAi.Awareness == Awareness.Suspicious,
                    "Un enemigo cercano oye los pasos y sospecha");
                c.Check(!farAi.HearNoise(basePos, footsteps.NoiseRadius) && farAi.Awareness == Awareness.Unaware,
                    "Un enemigo lejano no oye los pasos");

                // --- Ataque por la espalda ---
                player.transform.SetPositionAndRotation(basePos + new Vector3(50f, 0f, 0f), Quaternion.identity);
                var melee = player.GetComponent<PlayerMelee>();
                var unaware = Make(enemyPrefab, new Vector3(50f, 0f, 1.2f), 0f).GetComponent<Enemy>();
                c.Check(unaware.GetComponent<EnemyAI>().IsUnawareOf(player.transform.position), "El enemigo está de espaldas y desprevenido");
                c.Check(melee.TryPunch(new[] { unaware }) == unaware && unaware.IsDead,
                    "Simulación: un puñetazo por la espalda a un enemigo desprevenido lo mata");

                melee.Tick(1f);
                var facing = Make(enemyPrefab, new Vector3(50f, 0f, 1.2f), 180f).GetComponent<Enemy>();
                c.Check(melee.TryPunch(new[] { facing }) == facing && facing.Posture == Posture.Down && !facing.IsDead,
                    "De frente, el puñetazo solo derriba");

                melee.Tick(1f);
                var warned = Make(enemyPrefab, new Vector3(50f, 0f, 1.2f), 0f).GetComponent<Enemy>();
                warned.GetComponent<EnemyAI>().Brain.Alert(player.transform.position);
                c.Check(melee.TryPunch(new[] { warned }) == warned && warned.Posture == Posture.Down && !warned.IsDead,
                    "Si ya te detectó, por la espalda solo lo derribas");

                // --- Ver morir a otro enemigo ---
                var victim = Make(enemyPrefab, new Vector3(70f, 0f, 0f), 0f).GetComponent<Enemy>();
                var watcher = Make(enemyPrefab, new Vector3(70f, 0f, -6f), 0f).GetComponent<EnemyAI>();
                var lookingAway = Make(enemyPrefab, new Vector3(70f, 0f, 8f), 0f).GetComponent<EnemyAI>();
                var nextTo = Make(enemyPrefab, new Vector3(72.5f, 0f, 0f), 90f).GetComponent<EnemyAI>();
                Physics.SyncTransforms();
                victim.ReceiveLethalHit(KillCause.Melee);
                c.Check(watcher.WitnessDeath(victim) && watcher.Awareness == Awareness.Alerted,
                    "Simulación: un enemigo que ve morir a otro se alerta");
                c.Check(!lookingAway.WitnessDeath(victim) && lookingAway.Awareness == Awareness.Unaware,
                    "Simulación: uno de espaldas y lejos no se entera");
                c.Check(nextTo.WitnessDeath(victim) && nextTo.Awareness == Awareness.Alerted,
                    "Simulación: uno pegado al que muere se entera aunque no mire");
                c.Check(!victim.GetComponent<EnemyAI>().WitnessDeath(victim), "El que muere no se alerta a sí mismo");

                // --- Bonus de sigilo en la puntuación ---
                var scoreGo = new GameObject("StealthScoreTest");
                created.Add(scoreGo);
                var manager = scoreGo.AddComponent<LevelManager>();
                BuilderUtil.SetRef(manager, "player", player.GetComponent<PlayerLife>());
                var keeper = scoreGo.AddComponent<ScoreKeeper>();
                BuilderUtil.SetRef(keeper, "level", manager);
                var calm = Make(enemyPrefab, new Vector3(90f, 0f, 0f), 0f).GetComponent<Enemy>();
                var spotted = Make(enemyPrefab, new Vector3(95f, 0f, 0f), 0f).GetComponent<Enemy>();
                spotted.GetComponent<EnemyAI>().Brain.Alert(player.transform.position);

                controller.IsSneaking = false;
                c.Check(!keeper.IsStealthKill(calm), "Sin Ctrl no hay bonus de sigilo");
                controller.IsSneaking = true;
                c.Check(keeper.IsStealthKill(calm), "Agachado y sin que te haya detectado: hay bonus de sigilo");
                c.Check(!keeper.IsStealthKill(spotted), "Si el enemigo ya te detectó (!), no hay bonus de sigilo");

                ScoreGain stealthGain = keeper.AddKill(KillCause.Backstab, calm.transform.position, keeper.IsStealthKill(calm));
                c.Check(stealthGain.Stealth && keeper.State.Points == ScoreRules.KillPoints(KillCause.Backstab) + ScoreRules.StealthBonus,
                    $"Simulación: matar en sigilo suma {ScoreRules.StealthBonus} extra (total {keeper.State.Points})");
                c.Check(keeper.Popups.Count == 1 && HudText.PopupLabel(keeper.Popups[0].Cause, keeper.Popups[0].Gain.Stealth) != null,
                    "Simulación: el texto flotante muestra SIGILO");
                controller.IsSneaking = false;
            }
            finally
            {
                NoiseSystem.NoiseMade -= OnNoise;
                foreach (GameObject go in created)
                    if (go != null) Object.DestroyImmediate(go);
            }
        }
    }
}
