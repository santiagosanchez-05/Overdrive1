using System.Collections.Generic;
using System.Text.RegularExpressions;
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
    /// En primera persona, al pisar una escalera (rampa StairsN) se ve también el piso al que lleva.
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
        private readonly List<Collider> stairs = new List<Collider>();
        private static readonly Regex StairsName = new Regex(@"^Stairs\d+$");

        public Transform Player => player;
        public GameObject[] UpperFloors => upperFloors;
        public BriefcasePickup Briefcase => briefcase;

        /// <summary>Un objeto se ve si está en el piso del jugador o en uno inferior.</summary>
        public static bool IsVisible(float objectY, float playerY) => Floors.IndexOf(objectY) <= Floors.IndexOf(playerY);

        private void Start()
        {
            foreach (Collider c in FindObjectsByType<Collider>(FindObjectsSortMode.None))
                if (StairsName.IsMatch(c.gameObject.name)) stairs.Add(c);
        }

        /// <summary>El jugador está sobre (o dentro de) una rampa de escalera.</summary>
        private bool OnStairs(Vector3 position)
        {
            foreach (Collider c in stairs)
            {
                if (c == null) continue;
                Bounds b = c.bounds;
                b.Expand(new Vector3(0.3f, 2f, 0.3f));
                if (b.Contains(position)) return true;
            }
            return false;
        }

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
            int playerFloor = Floors.IndexOf(playerY);
            // Solo en primera persona: desde la escalera ya se ve el piso de arriba. En cenital taparía al jugador.
            if (ViewMode.FirstPerson && OnStairs(player.position))
            {
                playerFloor++;
                playerY = Floors.BaseY(playerFloor);
            }

            for (int i = 0; i < upperFloors.Length; i++)
                if (upperFloors[i] != null) SetVisible(upperFloors[i].transform, i + 1 <= playerFloor);

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
            bool hasModel = root.GetComponentInChildren<Animator>(true) != null;
            root.GetComponentsInChildren(true, buffer);
            foreach (Renderer r in buffer)
            {
                if (hasModel && (r.gameObject.name == "Body" || r.gameObject.name == "Nose"))
                {
                    r.enabled = false;
                    continue;
                }
                r.enabled = visible;
            }
            // Las luces de un piso oculto (cables LED) tampoco deben teñir el piso de abajo a través de la losa.
            root.GetComponentsInChildren(true, lightBuffer);
            foreach (Light l in lightBuffer) l.enabled = visible;
        }

        private readonly List<Light> lightBuffer = new List<Light>();
    }
}
