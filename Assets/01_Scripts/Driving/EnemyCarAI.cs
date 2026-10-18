using UnityEngine;

namespace Overdrive.Driving
{
    /// <summary>
    /// Camioneta de los dealers sobre la carretera: llega muy rápido por detrás hasta ponerse a la par del jugador (cada
    /// una en su sitio, un poco adelantada o atrasada) en su carril lateral, y desde ahí sigue a su ritmo. Cuando lo tiene
    /// al lado da un volantazo y lo embiste: el golpe solo lo desplaza un poco de lado (nunca lo gira ni lo frena) y le
    /// quita vida; luego vuelve a su carril y espera un rato antes de repetir.
    /// </summary>
    [RequireComponent(typeof(Car))]
    public class EnemyCarAI : MonoBehaviour
    {
        [SerializeField] private Car car;
        [SerializeField] private Car target;
        [SerializeField] private RoadPath road;
        [SerializeField] private float catchUp = 20f;
        [SerializeField] private float hitDamage = 12f;
        [SerializeField] private float push = 7f;
        [SerializeField] private float attackTime = 1.6f;
        [SerializeField] private Vector2 cooldownRange = new Vector2(2f, 4.5f);

        private int side = 1;
        private float slot;
        private float cooldown;
        private float attackLeft;
        private float touchCooldown;

        public Car Car => car;
        public Car Target => target;
        public int Side => side;
        public float Slot => slot;
        public bool Attacking => attackLeft > 0f;
        public float HitDamage => hitDamage;
        public float LaneOffset => road != null ? DrivingLogic.LaneOffset(side, road.Width) : 0f;
        public int Hits { get; private set; }

        /// <summary>Carril (-1 izquierdo, +1 derecho) y sitio respecto al jugador (m por delante, negativo = detrás).</summary>
        public void Setup(Car self, Car player, RoadPath path, int laneSide = 1, float slotAlong = 0f)
        {
            car = self;
            target = player;
            road = path;
            side = laneSide == 0 ? 1 : (int)Mathf.Sign(laneSide);
            slot = slotAlong;
            cooldown = Random.Range(cooldownRange.x, cooldownRange.y) * 0.5f;
        }

        /// <summary>Para las pruebas: embestir ya.</summary>
        public void ForceAttack()
        {
            attackLeft = attackTime;
            cooldown = 0f;
        }

        private void FixedUpdate() => Think(Time.fixedDeltaTime);

        /// <summary>Decide velocidad y lado, y resuelve los choques con el jugador. Público para simularlo en el editor.</summary>
        public void Think(float dt)
        {
            RailCar me = car != null ? car.Rail : null;
            RailCar player = target != null ? target.Rail : null;
            if (me == null || player == null || car.Destroyed) return;

            float alongGap = me.Along - player.Along;
            float lateralGap = player.Lateral - me.Lateral;
            cooldown -= dt;
            touchCooldown -= dt;

            if (attackLeft > 0f)
            {
                attackLeft -= dt;
                if (attackLeft <= 0f) cooldown = Random.Range(cooldownRange.x, cooldownRange.y);
            }
            else if (!target.Destroyed && DrivingLogic.ShouldAttack(alongGap, lateralGap, cooldown))
            {
                attackLeft = attackTime;
            }

            // Velocidad: alcanzar su sitio junto al jugador y quedarse a su ritmo.
            float alongError = player.Along + slot - me.Along;
            if (Attacking) alongError = player.Along - me.Along; // al embestir se pone justo a su altura
            me.Speed = RailLogic.ChaserSpeed(player.Speed, alongError, catchUp);

            // De lado: a su carril, o contra el jugador si embiste.
            float wanted = Attacking ? player.Lateral : LaneOffset;
            float diff = wanted - me.Lateral;
            me.LateralInput = Mathf.Clamp(diff / 1.2f, -1f, 1f);

            // Choque con el jugador: lo desplaza un poco de lado (y a sí misma al revés) y, si embestía, le hace daño.
            if (touchCooldown <= 0f && RailLogic.Touching(alongGap, lateralGap, me.Length, player.Length, me.Width, player.Width))
            {
                touchCooldown = 0.5f;
                float away = Mathf.Sign(lateralGap == 0f ? -side : lateralGap);
                player.Nudge(away, push);
                me.Nudge(-away, push);
                if (Attacking && !target.Destroyed)
                {
                    target.TakeDamage(hitDamage);
                    Hits++;
                    Core.GameSounds.Play(Core.Sound.ThrownHit);
                    CameraRig.CameraShake.Shake(0.35f);
                    attackLeft = 0f; // golpe dado: de vuelta a su carril
                    cooldown = Random.Range(cooldownRange.x, cooldownRange.y);
                }
            }
        }
    }
}
