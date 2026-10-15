using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Overdrive.EditorTools
{
    /// <summary>Helpers idempotentes: buscan por nombre y crean solo lo que falta.</summary>
    internal static class BuilderUtil
    {
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        public static T EnsureComponent<T>(GameObject go) where T : Component
        {
            T existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }

        public static GameObject EnsureChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            if (child != null) return child.gameObject;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        public static GameObject EnsurePrimitiveChild(Transform parent, string name, PrimitiveType type, bool keepCollider)
        {
            Transform child = parent.Find(name);
            GameObject go;
            if (child != null)
            {
                go = child.gameObject;
            }
            else
            {
                go = GameObject.CreatePrimitive(type);
                go.name = name;
                go.transform.SetParent(parent, false);
            }
            if (!keepCollider)
            {
                Collider collider = go.GetComponent<Collider>();
                if (collider != null) Object.DestroyImmediate(collider);
            }
            return go;
        }

        public static GameObject EnsureRoot(Scene scene, string name)
        {
            GameObject found = FindRoot(scene, name);
            if (found != null) return found;
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        public static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }

        public static void SetLocal(GameObject go, Vector3 position, Vector3 scale, Quaternion rotation)
        {
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.transform.localRotation = rotation;
        }

        public static void SetMaterial(GameObject go, Material material)
        {
            go.GetComponent<Renderer>().sharedMaterial = material;
        }

        /// <summary>Asigna un campo privado [SerializeField] de referencia.</summary>
        public static void SetRef(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
                throw new InvalidOperationException($"{target.GetType().Name} no tiene el campo serializado '{field}'.");
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static Object GetRef(Object target, string field)
        {
            var so = new SerializedObject(target);
            SerializedProperty property = so.FindProperty(field);
            return property?.objectReferenceValue;
        }

        /// <summary>Abre (o crea) un prefab, lo configura y lo guarda.</summary>
        public static GameObject EditPrefab(string path, string rootName, Action<GameObject> configure)
        {
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(rootName);
            try
            {
                configure(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else Object.DestroyImmediate(root);
            }
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
    }
}
