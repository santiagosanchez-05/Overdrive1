using System;
using System.Collections.Generic;
using Overdrive.Core;
using Overdrive.Story;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Overdrive.Driving
{
    public enum ChaseState
    {
        Driving,
        Won,
        Lost,
    }

    /// <summary>Una oleada de dealers: aparece al pasar por un punto de la carretera.</summary>
    [Serializable]
    public struct ChaseWave
    {
        [Tooltip("Metros desde la salida a los que aparece.")]
        public float startDistance;
        public int dealers;
        public int armored;
    }

    /// <summary>Progreso de la persecución (lógica pura, comprobable en el editor).</summary>
    public static class ChaseLogic
    {
        /// <summary>Oleada que toca según lo recorrido (-1 si ninguna ha empezado).</summary>
        public static int WaveAt(IReadOnlyList<ChaseWave> waves, float along)
        {
            int index = -1;
            for (int i = 0; i < waves.Count; i++)
                if (along >= waves[i].startDistance) index = i;
            return index;
        }

        public static int Total(ChaseWave w) => w.dealers + w.armored;

        /// <summary>
        /// Carril y sitio de la camioneta número n de una oleada: alterna izquierda y derecha, y se reparten por delante
        /// y por detrás del jugador para que siempre haya alguna a tiro.
        /// </summary>
        public static (int side, float slot) Placement(int n)
        {
            int side = n % 2 == 0 ? -1 : 1;
            float[] slots = { -4f, 3f, 9f, -10f, 15f, -16f };
            return (side, slots[(n / 2 + n) % slots.Length]);
        }

        /// <summary>Se gana al llegar al final; las camionetas que queden no lo impiden (siempre se puede avanzar).</summary>
        public static bool Finished(float along, float roadLength, float finishDistance) => along >= roadLength - finishDistance;
    }

    /// <summary>
    /// Nivel 4: persecución por la autopista. Las camionetas de los dealers aparecen por oleadas al avanzar, se ponen
    /// en los carriles laterales y embisten; se acaba con ellas a tiros (ratón para apuntar, clic izquierdo para
    /// disparar). Nina habla durante todo el camino. Se gana al llegar al piso franco; si el coche queda destrozado,
    /// R reinicia.
    /// </summary>
    public class ChaseLevel : MonoBehaviour
    {
        [SerializeField] private RoadPath road;
        [SerializeField] private Car player;
        [SerializeField] private RadioDialogue dialogue;
        [SerializeField] private Car dealerPrefab;
        [SerializeField] private Car armoredPrefab;
        [SerializeField] private ChaseWave[] waves = new ChaseWave[0];
        [SerializeField] private float finishDistance = 50f;
        [Tooltip("Las que se quedan muy atrás desaparecen (no cuentan como bajas).")]
        [SerializeField] private float despawnBehind = 140f;
        [SerializeField] private string nextScene = "Menu";

        private readonly List<Car> alive = new List<Car>();
        private readonly List<Car> spawned = new List<Car>();
        private readonly Dictionary<Car, int> waveOf = new Dictionary<Car, int>();
        private readonly Queue<(int wave, bool armored)> pending = new Queue<(int, bool)>();
        private readonly HashSet<int> cleared = new HashSet<int>();
        private int waveStarted = -1;
        private int kills;
        private bool warnedLowHealth;
        private float endTime;
        private float spawnTimer;
        private bool ending;

        public ChaseState State { get; private set; }
        public RoadPath Road => road;
        public Car Player => player;
        public ChaseWave[] Waves => waves;
        public int WaveStarted => waveStarted;
        public int Kills => kills;
        public int Alive => alive.Count;
        public IReadOnlyList<Car> Spawned => spawned;
        public string NextScene => nextScene;
        public float FinishDistance => finishDistance;
        public float Along => player == null || road == null ? 0f : player.Rail != null ? player.Rail.Along : road.Project(player.transform.position, out _);
        public float Progress01 => road != null && road.Length > 0f ? Mathf.Clamp01(Along / (road.Length - finishDistance)) : 0f;
        public RadioDialogue Dialogue => dialogue;
        public bool IsCleared(int wave) => cleared.Contains(wave);

        private void Start()
        {
            if (player != null)
            {
                player.ResetCar();
                if (player.Rail != null)
                {
                    player.Rail.Road = road;
                    player.Rail.SnapToRoad();
                }
                player.Damaged += OnPlayerDamaged;
                player.Exploded += OnPlayerWrecked;
            }
            if (dialogue != null) dialogue.Say(ChaseLines.Intro);
        }

        private void Update()
        {
            if (State == ChaseState.Won)
            {
                endTime += Time.unscaledDeltaTime;
                if (endTime > 1f && !GameInput.Blocked && (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return)))
                    SceneManager.LoadScene(string.IsNullOrEmpty(nextScene) ? "Menu" : nextScene);
                return;
            }
            if (State == ChaseState.Lost)
            {
                if (GameInput.RestartPressed) SceneManager.LoadScene(SceneManager.GetActiveScene().name);
                return;
            }
            Tick(Time.deltaTime);
        }

        /// <summary>Avanza la persecución. Público para simularla en el editor.</summary>
        public void Tick(float dt)
        {
            if (State != ChaseState.Driving || player == null || road == null) return;
            float along = Along;

            int due = ChaseLogic.WaveAt(waves, along);
            while (waveStarted < due) StartWave(waveStarted + 1);

            // Las camionetas de la oleada llegan escalonadas.
            spawnTimer -= dt;
            if (pending.Count > 0 && spawnTimer <= 0f)
            {
                var next = pending.Dequeue();
                Spawn(next.wave, next.armored);
                spawnTimer = next.armored ? 2.5f : 1.4f;
            }

            // Las que se quedan muy atrás desaparecen.
            foreach (Car c in alive.ToArray())
            {
                if (c == null || c.Destroyed) continue;
                float carAlong = c.Rail != null ? c.Rail.Along : road.Project(c.transform.position, out _);
                if (carAlong < along - despawnBehind)
                {
                    alive.Remove(c);
                    if (Application.isPlaying) Destroy(c.gameObject);
                }
            }
            alive.RemoveAll(c => c == null || c.Destroyed);
            for (int w = 0; w <= waveStarted; w++)
                if (!cleared.Contains(w) && !HasPending(w) && !HasAlive(w)) ClearWave(w);

            if (ChaseLogic.Finished(along, road.Length, finishDistance)) Win();
        }

        private bool HasPending(int wave)
        {
            foreach (var p in pending) if (p.wave == wave) return true;
            return false;
        }

        private bool HasAlive(int wave)
        {
            foreach (Car c in alive) if (c != null && waveOf.TryGetValue(c, out int w) && w == wave) return true;
            return false;
        }

        private void StartWave(int index)
        {
            waveStarted = index;
            ChaseWave w = waves[index];
            for (int i = 0; i < w.dealers; i++) pending.Enqueue((index, false));
            for (int i = 0; i < w.armored; i++) pending.Enqueue((index, true));
            spawnTimer = 0f;
            if (dialogue != null && index < ChaseLines.WaveStart.Length) dialogue.Say(ChaseLines.WaveStart[index]);
        }

        private void ClearWave(int index)
        {
            cleared.Add(index);
            if (dialogue != null && index < ChaseLines.WaveClear.Length) dialogue.Say(ChaseLines.WaveClear[index]);
        }

        /// <summary>Saca una camioneta por detrás, en su carril lateral, a la velocidad del jugador.</summary>
        public Car Spawn(int wave, bool armored)
        {
            Car prefab = armored && armoredPrefab != null ? armoredPrefab : dealerPrefab;
            if (prefab == null) return null;
            int n = spawned.Count;
            var (side, slot) = ChaseLogic.Placement(n);
            float along = Along;
            // Aparece fuera de la vista, por detrás, en su carril, y llega deprisa hasta ponerse a la par.
            float at = Mathf.Max(along - 55f - (n % 3) * 8f, 2f);
            float lane = DrivingLogic.LaneOffset(side, road.Width);
            Vector3 pos = road.PointAt(at) + road.RightAt(at) * lane;
            Vector3 heading = road.TangentAt(at);
            Car car = Instantiate(prefab, pos + Vector3.up * player.transform.position.y - Vector3.up * pos.y, Quaternion.LookRotation(heading, Vector3.up));
            car.name = $"Dealer_{wave + 1}_{n + 1}{(armored ? "_Blindada" : "")}";
            car.ResetCar();
            if (car.Rail != null)
            {
                car.Rail.Road = road;
                car.Rail.Along = at;
                car.Rail.Lateral = lane;
                car.Rail.Speed = Mathf.Max(player.ForwardSpeed, 20f) + 15f;
            }
            var ai = car.GetComponent<EnemyCarAI>();
            if (ai != null) ai.Setup(car, player, road, side, slot);
            car.Exploded += OnDealerWrecked;
            alive.Add(car);
            spawned.Add(car);
            waveOf[car] = wave;
            return car;
        }

        private void OnDealerWrecked(Car car)
        {
            if (!ending) kills++; // las que estallan al llegar a la meta no cuentan como bajas
            alive.Remove(car);
            if (Application.isPlaying) Destroy(car.gameObject, 6f); // la chatarra se queda un rato en la carretera
        }

        private void OnPlayerDamaged(Car car, float damage)
        {
            if (warnedLowHealth || car.Health01 > 0.3f || car.Destroyed) return;
            warnedLowHealth = true;
            if (dialogue != null) dialogue.Interrupt(ChaseLines.LowHealth);
        }

        private void OnPlayerWrecked(Car car)
        {
            State = ChaseState.Lost;
            Lock();
            if (dialogue != null) dialogue.Interrupt(ChaseLines.Wrecked);
        }

        private void Win()
        {
            State = ChaseState.Won;
            ending = true;
            Lock();
            foreach (Car c in alive.ToArray()) if (c != null && !c.Destroyed) c.Explode();
            if (dialogue != null) dialogue.Interrupt(ChaseLines.Finish);
        }

        private void Lock()
        {
            var input = player.GetComponent<PlayerCarInput>();
            if (input != null) input.Locked = true;
            var gun = player.GetComponent<CarGun>();
            if (gun != null) gun.enabled = false;
        }
    }
}
