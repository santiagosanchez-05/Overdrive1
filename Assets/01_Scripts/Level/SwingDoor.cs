using System.Collections.Generic;
using Overdrive.Enemies;
using Overdrive.Player;
using UnityEngine;

namespace Overdrive.Level
{
    /// <summary>Apertura de una puerta batiente (lógica pura, comprobable en el editor).</summary>
    public static class SwingDoorLogic
    {
        /// <summary>
        /// ¿Está el punto (en el espacio local de la puerta: X a lo ancho, Z a través) en la zona que la abre?
        /// </summary>
        public static bool InZone(Vector3 local, float width, float reach) =>
            Mathf.Abs(local.x) <= width * 0.5f + 0.3f && Mathf.Abs(local.z) <= reach && local.y > -1f && local.y < 2.5f;

        /// <summary>Hacia dónde se abre: siempre alejándose de quien llega (+1 hacia +Z si viene por -Z).</summary>
        public static float SwingSide(Vector3 local) => local.z <= 0f ? 1f : -1f;

        /// <summary>Avanza la apertura (0 cerrada, 1 abierta) hacia su objetivo con velocidades distintas al abrir y al cerrar.</summary>
        public static float Step(float openness, bool wantOpen, float dt, float openTime, float closeTime)
        {
            float speed = wantOpen ? 1f / Mathf.Max(openTime, 0.01f) : 1f / Mathf.Max(closeTime, 0.01f);
            return Mathf.MoveTowards(openness, wantOpen ? 1f : 0f, speed * dt);
        }

        /// <summary>Giro de cada hoja: la izquierda (bisagra en -X) y la derecha (en +X) giran en sentidos opuestos.</summary>
        public static float LeafAngle(bool leftLeaf, float openness, float side, float maxAngle) =>
            (leftLeaf ? -1f : 1f) * side * maxAngle * Mathf.SmoothStep(0f, 1f, openness);
    }

    /// <summary>
    /// Puerta batiente de dos hojas: se abre al acercarse el jugador o un enemigo vivo (siempre hacia el lado contrario
    /// del que llega) y se cierra sola al quedar libre. Cerrada, sus hojas paran las balas y tapan la vista: sirve de
    /// cobertura. No forma parte del NavMesh, así que los enemigos la atraviesan abriéndola.
    /// </summary>
    [DisallowMultipleComponent]
    public class SwingDoor : MonoBehaviour
    {
        [SerializeField] private Transform leftHinge;
        [SerializeField] private Transform rightHinge;
        [Tooltip("Ancho del hueco que cierra (m).")]
        [SerializeField] private float width = 3f;
        [Tooltip("Distancia a cada lado de la puerta a la que se abre (m).")]
        [SerializeField] private float reach = 1.4f;
        [Tooltip("Giro al abrirse: algo menos de 90° para que la hoja quede pegada al lado del hueco sin meterse en la pared.")]
        [SerializeField] private float maxAngle = 88f;
        [SerializeField] private float openTime = 0.18f;
        [SerializeField] private float closeTime = 0.45f;
        [Tooltip("Tiempo que sigue abierta tras quedar libre (s).")]
        [SerializeField] private float holdTime = 0.8f;

        private readonly List<Vector3> visitors = new List<Vector3>();
        private Transform player;
        private float openness;
        private float side = 1f;
        private float freeFor;

        public Transform LeftHinge => leftHinge;
        public Transform RightHinge => rightHinge;
        public float Width => width;
        public float Reach => reach;
        public float Openness => openness;
        public float Side => side;
        public bool IsClosed => openness <= 0.001f;

        private void Update()
        {
            visitors.Clear();
            if (player == null)
            {
                var go = GameObject.FindGameObjectWithTag("Player");
                player = go != null ? go.transform : null;
            }
            if (player != null)
            {
                var life = player.GetComponent<PlayerLife>();
                if (life == null || !life.IsDead) visitors.Add(player.position);
            }
            foreach (Enemy enemy in Enemy.All)
                if (enemy != null && !enemy.IsDead && enemy.Posture == Overdrive.Combat.Posture.Standing)
                    visitors.Add(enemy.transform.position);
            Tick(Time.deltaTime, visitors);
        }

        /// <summary>Avanza la puerta con las posiciones de quienes pueden abrirla. Público para simularla en el editor.</summary>
        public void Tick(float dt, IReadOnlyList<Vector3> positions)
        {
            bool someone = false;
            foreach (Vector3 p in positions)
            {
                Vector3 local = transform.InverseTransformPoint(p);
                if (!SwingDoorLogic.InZone(local, width, reach)) continue;
                // Elige el lado al empezar a abrirse; ya abierta no da la vuelta de golpe.
                if (!someone && IsClosed) side = SwingDoorLogic.SwingSide(local);
                someone = true;
            }
            freeFor = someone ? 0f : freeFor + dt;
            bool wantOpen = someone || freeFor < holdTime && !IsClosed;
            openness = SwingDoorLogic.Step(openness, wantOpen, dt, openTime, closeTime);
            Apply();
        }

        private void Apply()
        {
            if (leftHinge != null) leftHinge.localRotation = Quaternion.Euler(0f, SwingDoorLogic.LeafAngle(true, openness, side, maxAngle), 0f);
            if (rightHinge != null) rightHinge.localRotation = Quaternion.Euler(0f, SwingDoorLogic.LeafAngle(false, openness, side, maxAngle), 0f);
        }
    }
}
