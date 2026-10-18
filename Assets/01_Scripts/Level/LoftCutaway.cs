using System.Collections.Generic;
using Overdrive.Core;
using UnityEngine;

namespace Overdrive.Level
{
    /// <summary>
    /// Vista cenital en un modelo con techo (el loft del nivel 3): oculta las mallas que empiezan por encima de la cabeza
    /// del jugador (techo, piso de arriba), así la cámara ve dónde está. En primera persona se ve todo.
    /// Solo apaga el dibujo: los colliders siguen igual.
    /// </summary>
    public class LoftCutaway : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private Transform model;
        [Tooltip("Metros sobre los pies del jugador a partir de los cuales una malla se oculta.")]
        [SerializeField] private float headroom = 1.6f;
        [SerializeField] private float refreshInterval = 0.1f;

        private readonly List<Renderer> renderers = new List<Renderer>();
        private float timer;

        private void Start()
        {
            if (model != null) model.GetComponentsInChildren(true, renderers);
        }

        private void LateUpdate()
        {
            timer -= Time.deltaTime;
            if (timer > 0f || player == null) return;
            timer = refreshInterval;
            bool showAll = ViewMode.FirstPerson;
            float cut = player.position.y + headroom;
            foreach (Renderer r in renderers)
                if (r != null) r.forceRenderingOff = !showAll && r.bounds.min.y > cut;
        }

        private void OnDisable()
        {
            foreach (Renderer r in renderers)
                if (r != null) r.forceRenderingOff = false;
        }
    }
}
