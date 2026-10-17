using System.Collections.Generic;
using Overdrive.Combat;
using Overdrive.Core;
using Overdrive.Enemies;
using Overdrive.Weapons;
using UnityEngine;

namespace Overdrive.Level
{
    /// <summary>
    /// Con cámara cenital, los pisos de arriba taparían al jugador: se ocultan los pisos superiores
    /// al del jugador, y también los enemigos, armas y sangre que estén en ellos.
    /// </summary>
    public class FloorVisibility : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [Tooltip("Raíz de cada piso, empezando por el piso 1 (la planta baja siempre se ve).")]
        [SerializeField] private GameObject[] upperFloors = new GameObject[0];
        [SerializeField] private BriefcasePickup briefcase;
        [SerializeField] private float refreshInterval = 0.1f;

        private readonly List<Renderer> buffer = new List<Renderer>();
        private float timer;

        public Transform Player => player;
        public GameObject[] UpperFloors => upperFloors;
        public BriefcasePickup Briefcase => briefcase;

        /// <summary>Un objeto se ve si está en el piso del jugador o en uno inferior.</summary>
        public static bool IsVisible(float objectY, float playerY) => Floors.IndexOf(objectY) <= Floors.IndexOf(playerY);

        private void LateUpdate()
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = refreshInterval;
            Refresh();
        }

        /// <summary>Aplica la visibilidad ya. Público para simularlo en el editor.</summary>
        public void Refresh()
        {
            if (player == null) return;
            float playerY = player.position.y;

            for (int i = 0; i < upperFloors.Length; i++)
                if (upperFloors[i] != null) SetVisible(upperFloors[i].transform, i + 1 <= Floors.IndexOf(playerY));

            foreach (Enemy enemy in Enemy.All) SetVisible(enemy.transform, IsVisible(enemy.transform.position.y, playerY));
            foreach (WeaponItem item in WeaponItem.All)
                if (item.transform.parent == null) SetVisible(item.transform, IsVisible(item.transform.position.y, playerY));
            // Recogido, va a la espalda del jugador y siempre se ve.
            if (briefcase != null && !briefcase.IsCollected)
                SetVisible(briefcase.transform, IsVisible(briefcase.transform.position.y, playerY));

            BloodSpawner blood = BloodSpawner.Instance;
            if (blood != null)
                foreach (Transform splat in blood.transform)
                    SetVisible(splat, IsVisible(splat.position.y, playerY));
        }

        private void SetVisible(Transform root, bool visible)
        {
            root.GetComponentsInChildren(true, buffer);
            foreach (Renderer r in buffer) r.enabled = visible;
            // Las luces de un piso oculto (cables LED) tampoco deben teñir el piso de abajo a través de la losa.
            root.GetComponentsInChildren(true, lightBuffer);
            foreach (Light l in lightBuffer) l.enabled = visible;
        }

        private readonly List<Light> lightBuffer = new List<Light>();
    }
}
