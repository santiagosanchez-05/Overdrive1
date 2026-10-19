using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Overdrive.Core;
using Overdrive.Enemies;
using Overdrive.Level;
using Overdrive.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Overdrive.EditorTools
{
    /// <summary>Prueba visual real con HUD IMGUI, pausa y tiempos de fotograma del editor.</summary>
    [InitializeOnLoad]
    public static class NeonPolishPlayTest
    {
        private const string Running="Overdrive.NeonPlayTest";
        private static readonly string[] Scenes={"Menu","Level01","Level02","Level03","Level04"};
        private static readonly List<string> Checks=new List<string>();
        private static readonly List<float> Frames=new List<float>();
        private static int sceneIndex, step, frame;
        private static double started, stage;
        private static float clock;
        private static bool background;
        private static NeonSceneLook look;
        private static PauseMenuController pause;

        static NeonPolishPlayTest()
        {
            EditorApplication.playModeStateChanged+=OnState;
            if (SessionState.GetBool(Running,false)) EditorApplication.update+=Tick;
        }

        [MenuItem("TopDown/Acabado neón/Probar HUD, pausa y menús")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("La prueba empieza fuera de Play Mode.");
            for(int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Guarda las escenas antes de probar.");
            SessionState.SetString("Neon.Previous",SceneManager.GetActiveScene().path);
            SessionState.SetBool("Neon.HadSave",File.Exists(SaveSystem.FilePath));
            SessionState.SetBool("Neon.HadView",PlayerPrefs.HasKey("firstPerson"));
            SessionState.SetBool("Neon.View",ViewMode.FirstPerson);
            ViewMode.FirstPerson=false;
            if(File.Exists(SaveSystem.FilePath)) File.Copy(SaveSystem.FilePath,"Temp/Neon-save.backup",true);
            File.Copy("ProjectSettings/TimeManager.asset","Temp/Neon-TimeManager.backup",true);
            EditorSceneManager.OpenScene("Assets/00_Scenes/Menu.unity",OpenSceneMode.Single);
            Directory.CreateDirectory("Temp/NeonShots");
            SessionState.SetBool(Running,true); EditorApplication.isPlaying=true;
        }

        private static void OnState(PlayModeStateChange state)
        {
            if(!SessionState.GetBool(Running,false)) return;
            if(state==PlayModeStateChange.EnteredPlayMode)
            {
                Checks.Clear(); sceneIndex=step=0; started=stage=EditorApplication.timeSinceStartup; frame=Time.frameCount;
                background=Application.runInBackground; Application.runInBackground=true;
                Application.logMessageReceived+=OnLog; EditorApplication.update-=Tick; EditorApplication.update+=Tick;
                EditorApplication.ExecuteMenuItem("Window/General/Game");
            }
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                Application.runInBackground=background; GameInput.Blocked=false;
                ViewMode.FirstPerson=SessionState.GetBool("Neon.View",false);
                if(!SessionState.GetBool("Neon.HadView",false)) PlayerPrefs.DeleteKey("firstPerson");
                Application.logMessageReceived-=OnLog; EditorApplication.update-=Tick; SessionState.SetBool(Running,false);
                if(SessionState.GetBool("Neon.HadSave",false)) File.Copy("Temp/Neon-save.backup",SaveSystem.FilePath,true);
                else if(File.Exists(SaveSystem.FilePath)) File.Delete(SaveSystem.FilePath);
                File.Copy("Temp/Neon-TimeManager.backup","ProjectSettings/TimeManager.asset",true);
                File.WriteAllLines("Temp/NeonPolish.playtest.txt",Checks);
                string previous=SessionState.GetString("Neon.Previous","Assets/00_Scenes/Menu.unity");
                EditorApplication.delayCall+=()=>EditorSceneManager.OpenScene(previous,OpenSceneMode.Single);
            }
        }

        private static void OnLog(string message,string stack,LogType type)
        {
            if(type==LogType.Error || type==LogType.Exception) Checks.Add("FAIL Unity: "+message);
        }

        private static void Tick()
        {
            if(!EditorApplication.isPlaying) return;
            try
            {
                double now=EditorApplication.timeSinceStartup;
                if(now-started>240) throw new Exception("Tiempo de prueba excedido");
                if(Time.frameCount==frame) return;
                frame=Time.frameCount;
                if(now-stage<1.2) return;
                var scene=SceneManager.GetActiveScene();
                if(sceneIndex==0)
                {
                    var menu=Object.FindAnyObjectByType<MainMenuController>();
                    if(step==0) { Check(menu!=null && menu.levelScenes.Length==4,"Selección contiene cuatro niveles"); Capture("Menu"); Next(); }
                    else if(step==1) { menu.levelsButton.GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); Next(); }
                    else if(step==2)
                    {
                        Check(menu.levelsPanel.alpha>.9f,"Selección se abre con el botón real"); Capture("MenuLevels");
                        NeonVisualCapture.SaveUi(menu.GetComponent<Canvas>(),"MenuLevels-4x3",1280,960); Next();
                    }
                    else if(step==3) { menu.backButtons[1].GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); Next(); }
                    else if(step==4) { menu.settingsButton.GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); Next(); }
                    else if(step==5) { Check(menu.settingsPanel.alpha>.9f,"Ajustes se abre con el botón real"); Capture("MenuSettings"); Next(); }
                    else { LoadNext(); }
                    return;
                }
                switch(step)
                {
                    case 0:
                        foreach(var ai in Object.FindObjectsByType<EnemyAI>()) ai.enabled=false;
                        look=Object.FindAnyObjectByType<NeonSceneLook>(); pause=Object.FindAnyObjectByType<PauseMenuController>();
                        Check(look!=null && pause!=null,scene.name+" inicia con acabado y pausa"); Capture(scene.name+"-HUD");
                        if(scene.name=="Level02") { Time.timeScale=0; Frames.Clear(); step=10; stage=now; }
                        else Next();
                        break;
                    case 1:
                        var combat=Object.FindAnyObjectByType<LevelManager>();
                        if(combat!=null)
                        {
                            var life=combat.Player;
                            life.TakeHit(life.CurrentHealth-life.MaxHealth*.2f,false,false);
                            Check(!life.IsDead && Mathf.Abs(life.CurrentHealth-life.MaxHealth*.2f)<.1f,scene.name+" HUD recibe vida real al 20%");
                        }
                        else
                        {
                            var car=Object.FindAnyObjectByType<Overdrive.Driving.ChaseLevel>().Player;
                            car.TakeDamage(car.Health-car.MaxHealth*.2f);
                            Check(!car.Destroyed && Mathf.Abs(car.Health01-.2f)<.01f,"HUD del coche recibe daño real al 20%");
                        }
                        Next(); break;
                    case 2:
                        Capture(scene.name+"-LowHealth"); step=21; stage=now; break;
                    case 21:
                        pause.Pause(); step=3; stage=now; break;
                    case 3:
                        Check(pause.IsOpen && Time.timeScale==0 && GameInput.Blocked,scene.name+" bloquea controles al pausar");
                        clock=pause.panel.GetComponentInChildren<NeonMenuAtmosphere>(true).Clock; Capture(scene.name+"-Pause");
                        if(scene.name=="Level01") NeonVisualCapture.SaveUi(pause.GetComponent<Canvas>(),"Pause-4x3",1280,960);
                        Next(); break;
                    case 4:
                        Check(pause.panel.GetComponentInChildren<NeonMenuAtmosphere>(true).Clock>clock,scene.name+" anima el neón con tiempo detenido");
                        pause.Resume(); Next(); break;
                    case 5:
                        Check(Time.timeScale==1 && !GameInput.Blocked,scene.name+" reanuda correctamente"); LoadNext(); break;
                    case 10:
                    case 11:
                        Frames.Add(Time.unscaledDeltaTime*1000f);
                        if(Frames.Count<90) return;
                        Frames.Sort();
                        Checks.Add("PERF editor Level02 "+(step==10?"acabado":"base")+" · mediana="+Frames[Frames.Count/2].ToString("F2")+" ms, p95="+Frames[(int)(Frames.Count*.95f)].ToString("F2")+" ms · "+Screen.width+"x"+Screen.height);
                        if(step==10) { look.gameObject.SetActive(false); Frames.Clear(); step=11; stage=now; Capture("Level02-base"); }
                        else { look.gameObject.SetActive(true); Time.timeScale=1; step=1; stage=now; }
                        break;
                }
            }
            catch(Exception e) { Checks.Add("FAIL "+e); EditorApplication.isPlaying=false; }
        }

        private static void Check(bool condition,string message)
        {
            if(!condition) throw new InvalidOperationException(message); Checks.Add("PASS "+message);
        }
        private static void Next() { step++; stage=EditorApplication.timeSinceStartup; }
        private static void LoadNext()
        {
            sceneIndex++; if(sceneIndex>=Scenes.Length) { EditorApplication.isPlaying=false; return; }
            ViewMode.FirstPerson=false;
            step=0; stage=EditorApplication.timeSinceStartup; SceneManager.LoadScene(Scenes[sceneIndex]);
        }
        private static void Capture(string name) => ScreenCapture.CaptureScreenshot("Temp/NeonShots/"+name+".png");
    }
}
