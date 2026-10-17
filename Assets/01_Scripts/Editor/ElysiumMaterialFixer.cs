using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Reconstruye los materiales URP del Elysium (huge-nightclub) a partir de sus texturas
// y prepara la iluminacion de Level02 para que se vea como discoteca.
public static class ElysiumMaterialFixer
{
    const string Root = "Assets/00_Scenes/huge-nightclub";
    const string MaterialsDir = Root + "/source/elysium/source/Elysium/Materials";
    const string GeneratedDir = Root + "/textures/Generated";
    static readonly string[] TextureDirs = { Root + "/textures", Root + "/source/elysium/textures" };

    // Materiales que usan transparencia real (vidrio, bebidas, letrero, holograma).
    static readonly string[] TransparentPrefixes = { "Drink_Textures", "ElysiumSign_Texture", "Holo_Texture" };

    [MenuItem("TopDown/Elysium/Arreglar materiales y luces (Level02)")]
    public static void FixAll()
    {
        FixMaterials();
        if (EditorSceneManager.GetActiveScene().name == "Level02")
        { SetupLighting(); AddClubLights(); }
        else
            Debug.LogWarning("[Elysium] Abre Level02 para aplicar tambien la iluminacion.");
    }

    [MenuItem("TopDown/Elysium/Solo materiales")]
    public static void FixMaterials()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        Directory.CreateDirectory(GeneratedDir);
        int count = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { MaterialsDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            string prefix = mat.name.Replace("M_Elysium_", "");
            if (prefix.EndsWith("1")) prefix = prefix.Substring(0, prefix.Length - 1);

            Texture2D baseCol = FindTex(prefix, "BaseColor");
            Texture2D normal = FindTex(prefix, "Normal");
            Texture2D metal = FindTex(prefix, "Metallic");
            Texture2D rough = FindTex(prefix, "Roughness");
            Texture2D ao = FindTex(prefix, "ambient_occlusion");
            Texture2D emis = FindTex(prefix, "Emissive");
            Texture2D alpha = FindTex(prefix, "Transparency");

            if (baseCol == null && emis == null)
            {
                Debug.LogWarning($"[Elysium] Sin texturas para {mat.name}, se deja como esta.");
                continue;
            }

            mat.shader = lit;
            mat.SetColor("_BaseColor", Color.white);

            if (baseCol != null)
            {
                Texture2D albedo = alpha != null
                    ? Combine(baseCol, alpha, $"{prefix}_BaseColorAlpha", true, false)
                    : baseCol;
                mat.SetTexture("_BaseMap", albedo);
                mat.SetTexture("_MainTex", albedo);
            }

            if (normal != null)
            {
                MakeNormalMap(normal);
                mat.SetTexture("_BumpMap", normal);
                mat.SetFloat("_BumpScale", 1f);
                mat.EnableKeyword("_NORMALMAP");
            }

            if (metal != null || rough != null)
            {
                Texture2D ms = Combine(metal, rough, $"{prefix}_MetallicSmoothness", false, true);
                mat.SetTexture("_MetallicGlossMap", ms);
                mat.SetFloat("_SmoothnessTextureChannel", 0);
                mat.SetFloat("_Smoothness", 0.8f); // escala de la smoothness del mapa
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }

            if (ao != null)
            {
                mat.SetTexture("_OcclusionMap", ao);
                mat.SetFloat("_OcclusionStrength", 1f);
                mat.EnableKeyword("_OCCLUSIONMAP");
            }

            if (emis != null)
            {
                mat.SetTexture("_EmissionMap", emis);
                mat.SetColor("_EmissionColor", Color.white * 2.5f); // HDR para que el bloom brille
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                mat.SetColor("_EmissionColor", Color.black);
                mat.DisableKeyword("_EMISSION");
            }

            bool transparent = System.Array.IndexOf(TransparentPrefixes, prefix) >= 0;
            if (transparent) SetTransparent(mat);
            else if (alpha != null) SetCutout(mat, 0.5f);
            else SetOpaque(mat);

            EditorUtility.SetDirty(mat);
            count++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Elysium] {count} materiales reconstruidos.");
    }

    static Texture2D FindTex(string prefix, string suffix)
    {
        foreach (string dir in TextureDirs)
        {
            foreach (string ext in new[] { ".jpeg", ".jpg", ".png" })
            {
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{prefix}_{suffix}{ext}");
                if (t != null) return t;
            }
        }
        return null;
    }

    static void MakeNormalMap(Texture2D tex)
    {
        var imp = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex));
        if (imp.textureType == TextureImporterType.NormalMap) return;
        imp.textureType = TextureImporterType.NormalMap;
        imp.SaveAndReimport();
    }

    // Lee una textura via GPU (no necesita Read/Write habilitado).
    static Color[] ReadPixels(Texture2D src, int w, int h, bool linear)
    {
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32,
            linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.sRGB);
        Graphics.Blit(src, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tmp = new Texture2D(w, h, TextureFormat.RGBA32, false, linear);
        tmp.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tmp.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        Color[] px = tmp.GetPixels();
        Object.DestroyImmediate(tmp);
        return px;
    }

    // rgbSource -> RGB, alphaSource.r -> A. Si metallicMode: R=metal, A=1-roughness.
    static Texture2D Combine(Texture2D rgbSource, Texture2D alphaSource, string name, bool srgb, bool metallicMode)
    {
        Texture2D refTex = rgbSource != null ? rgbSource : alphaSource;
        int w = refTex.width, h = refTex.height;
        bool linear = !srgb;
        Color[] a = rgbSource != null ? ReadPixels(rgbSource, w, h, linear) : null;
        Color[] b = alphaSource != null ? ReadPixels(alphaSource, w, h, true) : null;

        var outPx = new Color[w * h];
        for (int i = 0; i < outPx.Length; i++)
        {
            if (metallicMode)
            {
                float m = a != null ? a[i].r : 0f;
                float s = b != null ? 1f - b[i].r : 0.3f;
                outPx[i] = new Color(m, m, m, s);
            }
            else
            {
                Color c = a[i];
                c.a = b != null ? b[i].r : 1f;
                outPx[i] = c;
            }
        }

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, linear);
        tex.SetPixels(outPx);
        tex.Apply();
        string path = $"{GeneratedDir}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.sRGBTexture = srgb;
        imp.alphaSource = TextureImporterAlphaSource.FromInput;
        imp.alphaIsTransparency = !metallicMode;
        imp.maxTextureSize = 4096;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static void SetOpaque(Material m)
    {
        m.SetFloat("_Surface", 0);
        m.SetFloat("_AlphaClip", 0);
        m.SetFloat("_SrcBlend", (float)BlendMode.One);
        m.SetFloat("_DstBlend", (float)BlendMode.Zero);
        m.SetFloat("_ZWrite", 1);
        m.SetFloat("_Cull", (float)CullMode.Back);
        m.DisableKeyword("_ALPHATEST_ON");
        m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Opaque");
        m.renderQueue = (int)RenderQueue.Geometry;
    }

    static void SetCutout(Material m, float cutoff)
    {
        SetOpaque(m);
        m.SetFloat("_AlphaClip", 1);
        m.SetFloat("_Cutoff", cutoff);
        m.SetFloat("_Cull", (float)CullMode.Off);
        m.EnableKeyword("_ALPHATEST_ON");
        m.SetOverrideTag("RenderType", "TransparentCutout");
        m.renderQueue = (int)RenderQueue.AlphaTest;
    }

    static void SetTransparent(Material m)
    {
        m.SetFloat("_Surface", 1);
        m.SetFloat("_Blend", 0);
        m.SetFloat("_AlphaClip", 0);
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0);
        m.SetFloat("_Cull", (float)CullMode.Off);
        m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.SetOverrideTag("RenderType", "Transparent");
        m.renderQueue = (int)RenderQueue.Transparent;
    }

    [MenuItem("TopDown/Elysium/Colocar luces de antro")]
    public static void AddClubLights()
    {
        var elysium = GameObject.Find("Elysium");
        if (elysium == null) { Debug.LogError("[Elysium] No hay objeto 'Elysium' en la escena."); return; }

        var old = GameObject.Find("Elysium_ClubLights");
        if (old != null) Object.DestroyImmediate(old);

        Bounds b = new Bounds(elysium.transform.position, Vector3.zero);
        foreach (var r in elysium.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);

        var root = new GameObject("Elysium_ClubLights");
        var lights = new System.Collections.Generic.List<Light>();
        Color[] colors = { new Color(0.1f, 1f, 0.45f), new Color(1f, 0.55f, 0.15f), new Color(1f, 0.1f, 0.8f), new Color(0.1f, 0.6f, 1f) };
        float range = Mathf.Max(b.size.x, b.size.z) * 0.3f;

        // Rejilla de point lights en 3 alturas (piso, medio, techo).
        int grid = 4, idx = 0;
        float[] heights = { 0.15f, 0.5f, 0.85f };
        foreach (float hy in heights)
            for (int x = 0; x < grid; x++)
                for (int z = 0; z < grid; z++)
                {
                    var p = new Vector3(
                        Mathf.Lerp(b.min.x, b.max.x, (x + 0.5f) / grid),
                        Mathf.Lerp(b.min.y, b.max.y, hy),
                        Mathf.Lerp(b.min.z, b.max.z, (z + 0.5f) / grid));
                    var l = NewLight(root.transform, $"Point_{idx}", LightType.Point, p, colors[idx % colors.Length]);
                    l.range = range;
                    l.intensity = 6f;
                    lights.Add(l);
                    idx++;
                }

        // Focos giratorios desde el techo sobre el centro (pista de baile).
        for (int i = 0; i < 6; i++)
        {
            float ang = i * Mathf.PI * 2f / 6f;
            var p = b.center + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * b.extents.x * 0.25f;
            p.y = b.max.y - b.size.y * 0.05f;
            var l = NewLight(root.transform, $"Spot_{i}", LightType.Spot, p, colors[i % colors.Length]);
            l.range = b.size.y * 1.5f;
            l.spotAngle = 35f;
            l.intensity = 40f;
            l.transform.rotation = Quaternion.Euler(75f, i * 60f, 0f);
            lights.Add(l);
        }

        root.AddComponent<Overdrive.Level.ClubLights>().Setup(lights.ToArray());
        EditorUtility.SetDirty(root);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[Elysium] {lights.Count} luces de antro colocadas. Guarda la escena (Ctrl+S).");
    }

    static Light NewLight(Transform parent, string name, LightType type, Vector3 pos, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = type;
        l.color = color;
        l.shadows = LightShadows.None;
        return l;
    }

    static void SetupLighting()
    {
        // Ambiente oscuro sin cielo: evita que el metal refleje un cielo blanco.
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.55f, 0.42f, 0.30f);
        RenderSettings.ambientEquatorColor = new Color(0.25f, 0.40f, 0.32f);
        RenderSettings.ambientGroundColor = new Color(0.12f, 0.15f, 0.15f);
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.customReflectionTexture = null;
        RenderSettings.reflectionIntensity = 0.5f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(0.04f, 0.05f, 0.05f);
        RenderSettings.fogDensity = 0.002f;

        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type != LightType.Directional) continue;
            light.color = new Color(1f, 0.72f, 0.42f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
        }

        foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
        }

        // Volumen global con bloom para los emisivos (neones, pantallas, letrero).
        var go = GameObject.Find("Elysium_GlobalVolume") ?? new GameObject("Elysium_GlobalVolume");
        var vol = go.GetComponent<Volume>() ?? go.AddComponent<Volume>();
        vol.isGlobal = true;
        string profilePath = "Assets/00_Scenes/Level02_VolumeProfile.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
        }
        if (!profile.TryGet(out Bloom bloom)) bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(1.2f);
        bloom.threshold.Override(0.9f);
        bloom.scatter.Override(0.7f);
        if (!profile.TryGet(out Tonemapping tone)) tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.ACES);
        if (!profile.TryGet(out ColorAdjustments color)) color = profile.Add<ColorAdjustments>(true);
        color.saturation.Override(15f);
        color.postExposure.Override(0.8f);
        EditorUtility.SetDirty(profile);
        vol.sharedProfile = profile;

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[Elysium] Iluminacion y post-proceso de Level02 configurados. Guarda la escena (Ctrl+S).");
    }
}
