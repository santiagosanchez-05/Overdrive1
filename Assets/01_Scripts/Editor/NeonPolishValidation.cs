using System;
using System.IO;
using System.Linq;
using System.Text;
using Overdrive.Level;
using Overdrive.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Overdrive.EditorTools
{
    public static partial class NeonPolishBuilder
    {
        [MenuItem("TopDown/Acabado neón/Actualizar HUD, tipografía y menús")]
        public static void RefreshInterface()
        {
            VisitScenes(scene => { NeonMenuStyler.Apply(scene); if (scene.name != "Menu") ApplyHudAndTypography(scene); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); });
            AssetDatabase.SaveAssets();
        }

        [MenuItem("TopDown/Acabado neón/Verificar escenas y tipografía")]
        public static void VerifyAll()
        {
            Verify();
            var report = new StringBuilder();
            VisitScenes(scene =>
            {
                void Check(bool condition, string detail) { if (!condition) throw new InvalidOperationException(scene.name + ": " + detail); }
                Check(Find<NeonMenuButton>(scene).All(b => Mathf.Max(b.neon.r,b.neon.g,b.neon.b)-Mathf.Min(b.neon.r,b.neon.g,b.neon.b)>.2f), "botones monocromáticos");
                if (scene.name == "Menu") return;
                var look = Find<NeonSceneLook>(scene).Single();
                Check(look.Bindings.Length > 0, "materiales ausentes");
                foreach (var binding in look.Bindings)
                {
                    Check(binding.renderer != null && binding.original.Length == binding.styled.Length, "binding inválido");
                    Check(binding.renderer.sharedMaterials.SequenceEqual(binding.styled), "asignación de materiales no persistida");
                    for (int i=0; i<binding.original.Length; i++)
                    {
                        var before=binding.original[i]; var after=binding.styled[i];
                        if (before==null || before==after) continue;
                        Check(before.shader==after.shader, "cambio inesperado de shader");
                        foreach (string property in new[] { "_BaseMap", "_BumpMap", "_EmissionMap" })
                            if (before.HasProperty(property)) Check(before.GetTexture(property)==after.GetTexture(property), "se perdió " + property);
                    }
                }
                var probes=look.GetComponentsInChildren<ReflectionProbe>();
                Check(probes.Length>0 && probes.All(p=>p.mode==ReflectionProbeMode.Custom && p.customBakedTexture!=null), "reflejos sin hornear");
                Check(look.GetComponent<Volume>()?.sharedProfile!=null, "perfil visual ausente");
                var horrendo=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(DataPath+"/WorldHorrendo.asset");
                Check(Find<TextMeshPro>(scene).All(t=>t.font==horrendo), "texto del mundo sin Horrendo");
                Check(Find<TextMesh>(scene).All(t=>t.font==AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/horrendo.ttf")), "rótulo clásico sin Horrendo");
                foreach (var component in Find<GameHUD>(scene).Cast<Component>().Concat(Find<Overdrive.Driving.ChaseHUD>(scene)))
                    Check(new SerializedObject(component).FindProperty("hudFont").objectReferenceValue==AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/LiberationSans.ttf"), "HUD sin Liberation Sans legible");
                report.AppendLine("PASS " + scene.name + " · materiales="+look.Bindings.Length+", probes="+probes.Length+", rótulos Horrendo="+Find<TextMeshPro>(scene).Length);
            });
            var shader=Shader.Find("Overdrive/UI/Neon Atmosphere");
            if (shader==null || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Error en shader de interfaz");
            File.WriteAllText("Temp/NeonPolish.validation.txt", report.ToString());
        }

        public static void PlayTest() => NeonPolishPlayTest.Run();
        public static void MoveNina() => StoryFactory.UpdateNinaEntrance();
        public static void CampaignTest() => CampaignSavePlayTest.Run();

        public static void FinalizeForPlay()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("La prueba todavía está ejecutándose.");
            VerifyAll(); VerifyEntrance();
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene("Assets/00_Scenes/Menu.unity",OpenSceneMode.Single);
            File.WriteAllText("Temp/NeonPolish.ready.txt","PASS Menú listo para probar la campaña completa.");
        }

        public static void VerifyEntrance()
        {
            var scene=SceneManager.GetSceneByPath(StoryFactory.Level02Path);
            bool opened=!scene.isLoaded;
            if (opened) scene=EditorSceneManager.OpenScene(StoryFactory.Level02Path,OpenSceneMode.Additive);
            try
            {
                var player=Find<Overdrive.Player.PlayerController>(scene).Single();
                var nina=Find<Overdrive.Story.NpcDialogue>(scene).Single();
                float distance=Vector3.Distance(player.transform.position,nina.transform.position);
                if(distance>3f || distance<1f) throw new InvalidOperationException("Nina debe quedar junto al inicio, a 1–3 m; actual="+distance.ToString("F1"));
                Physics.SyncTransforms();
                if(!StoryFactory.IsOpenSpot(nina.transform.position,player.transform,StoryFactory.EntranceClearance)) throw new InvalidOperationException("Nina invade un obstáculo del pasillo.");
                File.WriteAllText("Temp/NinaEntrance.validation.txt","PASS Nina a "+distance.ToString("F2")+" m, diálogo accesible y cápsula despejada.");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene,true); }
        }

        public static void RepairProbeBindings()
        {
            VisitScenes(scene =>
            {
                var look=Find<NeonSceneLook>(scene).SingleOrDefault(); if(look==null) return;
                var probes=look.GetComponentsInChildren<ReflectionProbe>();
                foreach(var probe in probes)
                {
                    string index=probe.name.Substring("Baked Neon ".Length);
                    probe.mode=ReflectionProbeMode.Custom;
                    probe.customBakedTexture=AssetDatabase.LoadAssetAtPath<Texture>(DataPath+"/Probes/"+scene.name+"_"+index+".exr");
                }
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            });
        }

        private static void VisitScenes(Action<Scene> action)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Detén Play Mode antes de modificar o verificar las escenas guardadas.");
            foreach (string name in Paths)
            {
                string path="Assets/00_Scenes/"+name+".unity";
                var scene=SceneManager.GetSceneByPath(path); bool opened=!scene.isLoaded;
                if (opened) scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                try { action(scene); } finally { if (opened) EditorSceneManager.CloseScene(scene,true); }
            }
        }
    }
}
