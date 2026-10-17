using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Overdrive.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Overdrive.Level
{
    /// <summary>
    /// Guardar y cargar la partida en disco. Reutiliza las instantáneas del rebobinado
    /// (<see cref="IRewindable"/>): cada instantánea se aplana por reflexión a pares nombre/valor
    /// (las referencias a objetos de la escena se guardan como ruta de jerarquía) y, al cargar,
    /// se reconstruye sobre una instantánea nueva del mismo objeto.
    /// Las manchas de sangre y las balas en el aire no se guardan.
    /// </summary>
    public static class SaveSystem
    {
        private const int MaxDepth = 6;

        [Serializable]
        private class Entry
        {
            public string key;
            public List<string> names = new List<string>();
            public List<string> values = new List<string>();
        }

        [Serializable]
        private class SaveData
        {
            public string scene;
            public string savedAt;
            public int rewindUses = -1;
            public List<Entry> entries = new List<Entry>();
        }

        private static SaveData pending;

        // Ruta de cada objeto al cargar la escena. Las armas cambian de padre al recogerlas
        // (suelo -> mano), así que la ruta actual no sirve como identificador: se usa la inicial.
        private static readonly Dictionary<GameObject, string> initialPath = new Dictionary<GameObject, string>();
        private static readonly Dictionary<string, GameObject> byInitialPath = new Dictionary<string, GameObject>();

        public static string FilePath => Path.Combine(Application.persistentDataPath, "savegame.json");

        public static bool HasSave => File.Exists(FilePath);

        /// <summary>Fecha del guardado, o null si no hay.</summary>
        public static string SavedAt => Read()?.savedAt;

        public static void Delete()
        {
            if (HasSave) File.Delete(FilePath);
        }

        /// <summary>Guarda el estado actual del nivel abierto. Devuelve false si no hay nada que guardar.</summary>
        public static bool Save()
        {
            Scene scene = SceneManager.GetActiveScene();
            var data = new SaveData
            {
                scene = scene.name,
                savedAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
            };
            TimeRewinder rewinder = Object.FindAnyObjectByType<TimeRewinder>();
            if (rewinder != null) data.rewindUses = rewinder.UsesLeft;

            foreach (KeyValuePair<string, IRewindable> t in Targets(scene))
            {
                var entry = new Entry { key = t.Key };
                Flatten(t.Value.Capture(), "", entry, 0);
                data.entries.Add(entry);
            }
            if (data.entries.Count == 0) return false;

            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(data));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] No se pudo guardar: {e.Message}");
                return false;
            }
        }

        /// <summary>Carga la escena guardada y, cuando arranca, deja todo como estaba.</summary>
        public static bool Continue()
        {
            SaveData data = Read();
            if (data == null || !Application.CanStreamedLevelBeLoaded(data.scene)) return false;
            pending = data;
            Time.timeScale = 1f;
            GameInput.Blocked = false;
            SceneManager.LoadScene(data.scene);
            return true;
        }

        private static SaveData Read()
        {
            if (!HasSave) return null;
            try { return JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath)); }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] Guardado ilegible: {e.Message}");
                return null;
            }
        }

        // ---------------------------------------------------------------- Carga diferida

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            pending = null;
            initialPath.Clear();
            byInitialPath.Clear();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RegisterInitialPaths(scene);
            if (pending == null || pending.scene != scene.name) return;
            SaveData data = pending;
            pending = null;
            var runner = new GameObject("SaveLoader").AddComponent<Runner>();
            runner.StartCoroutine(runner.ApplyLater(scene, data));
        }

        private class Runner : MonoBehaviour
        {
            public IEnumerator ApplyLater(Scene scene, SaveData data)
            {
                yield return null; // que todos los Start (rebobinado incluido) hayan corrido
                Apply(scene, data);
                Destroy(gameObject);
            }
        }

        private static void Apply(Scene scene, SaveData data)
        {
            var byKey = new Dictionary<string, Entry>();
            foreach (Entry e in data.entries) byKey[e.key] = e;

            foreach (KeyValuePair<string, IRewindable> t in Targets(scene))
            {
                if (!byKey.TryGetValue(t.Key, out Entry entry)) continue;
                object snapshot = t.Value.Capture();
                var values = new Dictionary<string, string>();
                for (int i = 0; i < entry.names.Count; i++) values[entry.names[i]] = entry.values[i];
                snapshot = Rebuild(snapshot, snapshot.GetType(), "", values, scene, 0);
                try { t.Value.Restore(snapshot); }
                catch (Exception e) { Debug.LogWarning($"[SaveSystem] No se pudo restaurar {t.Key}: {e.Message}"); }
            }

            TimeRewinder rewinder = Object.FindAnyObjectByType<TimeRewinder>();
            if (rewinder != null)
            {
                if (data.rewindUses >= 0) rewinder.SetUsesLeft(data.rewindUses);
                rewinder.Gather();
                rewinder.Record();
                if (rewinder.CameraRig != null) rewinder.CameraRig.SnapToTarget();
            }
        }

        // ---------------------------------------------------------------- Objetivos

        /// <summary>Todo lo rebobinable de la escena, con una clave estable (ruta + tipo + índice).</summary>
        private static List<KeyValuePair<string, IRewindable>> Targets(Scene scene)
        {
            var list = new List<KeyValuePair<string, IRewindable>>();
            var used = new HashSet<string>();
            foreach (MonoBehaviour b in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
            {
                if (!(b is IRewindable r) || b.gameObject.scene != scene) continue;
                string baseKey = PathOf(b.transform) + "#" + b.GetType().Name;
                string key = baseKey;
                for (int i = 1; !used.Add(key); i++) key = baseKey + "#" + i;
                list.Add(new KeyValuePair<string, IRewindable>(key, r));
            }
            list.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return list;
        }

        private static void RegisterInitialPaths(Scene scene)
        {
            initialPath.Clear();
            byInitialPath.Clear();
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                {
                    string path = CurrentPath(t);
                    initialPath[t.gameObject] = path;
                    if (!byInitialPath.ContainsKey(path)) byInitialPath[path] = t.gameObject;
                }
        }

        /// <summary>Identificador estable: la ruta que tenía el objeto al cargar la escena.</summary>
        private static string PathOf(Transform t) =>
            initialPath.TryGetValue(t.gameObject, out string path) ? path : CurrentPath(t);

        private static string CurrentPath(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (Transform p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }

        private static GameObject FindByPath(Scene scene, string path)
        {
            if (byInitialPath.TryGetValue(path, out GameObject known) && known != null) return known;
            int slash = path.IndexOf('/');
            string rootName = slash < 0 ? path : path.Substring(0, slash);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != rootName) continue;
                if (slash < 0) return root;
                Transform found = root.transform.Find(path.Substring(slash + 1));
                if (found != null) return found.gameObject;
            }
            return null;
        }

        // ---------------------------------------------------------------- Aplanado por reflexión

        private static bool IsLeaf(Type t) =>
            t.IsPrimitive || t.IsEnum || t == typeof(string) || typeof(Object).IsAssignableFrom(t);

        private static IEnumerable<FieldInfo> Fields(Type t) =>
            t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static void Flatten(object value, string prefix, Entry entry, int depth)
        {
            if (value == null) return;
            Type type = value.GetType();
            if (IsLeaf(type))
            {
                entry.names.Add(prefix);
                entry.values.Add(Encode(value));
                return;
            }
            if (depth > MaxDepth) return;
            foreach (FieldInfo f in Fields(type))
            {
                string name = prefix.Length == 0 ? f.Name : prefix + "." + f.Name;
                object v = f.GetValue(value);
                if (v == null || (v is Object o && o == null))
                {
                    if (typeof(Object).IsAssignableFrom(f.FieldType)) { entry.names.Add(name); entry.values.Add(""); }
                    continue;
                }
                Flatten(v, name, entry, depth + 1);
            }
        }

        private static string Encode(object v)
        {
            switch (v)
            {
                case Component c: return PathOf(c.transform);
                case GameObject g: return PathOf(g.transform);
                case Object _: return "";
                case float f: return f.ToString("R", CultureInfo.InvariantCulture);
                case double d: return d.ToString("R", CultureInfo.InvariantCulture);
                case IFormattable fm: return fm.ToString(null, CultureInfo.InvariantCulture);
                default: return v.ToString();
            }
        }

        private static object Rebuild(object current, Type type, string prefix, Dictionary<string, string> values, Scene scene, int depth)
        {
            if (IsLeaf(type))
                return values.TryGetValue(prefix, out string s) ? Decode(s, type, scene, current) : current;
            if (current == null || depth > MaxDepth) return current;

            object boxed = current; // para structs, se modifica la copia en caja y se devuelve
            foreach (FieldInfo f in Fields(type))
            {
                string name = prefix.Length == 0 ? f.Name : prefix + "." + f.Name;
                object v = f.GetValue(boxed);
                object rebuilt = Rebuild(v, f.FieldType, name, values, scene, depth + 1);
                if (!ReferenceEquals(rebuilt, v) || f.FieldType.IsValueType) f.SetValue(boxed, rebuilt);
            }
            return boxed;
        }

        private static object Decode(string s, Type type, Scene scene, object fallback)
        {
            try
            {
                if (typeof(Object).IsAssignableFrom(type))
                {
                    if (string.IsNullOrEmpty(s)) return null;
                    GameObject go = FindByPath(scene, s);
                    if (go == null) return fallback;
                    if (type == typeof(GameObject)) return go;
                    return go.GetComponent(type);
                }
                if (type.IsEnum) return Enum.Parse(type, s);
                return Convert.ChangeType(s, type, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }
    }
}
