using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Overdrive.Combat;
using Overdrive.Core;
using Overdrive.Driving;
using Overdrive.Enemies;
using Overdrive.Level;
using Overdrive.Player;
using Overdrive.Story;
using Overdrive.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Overdrive.EditorTools
{
    /// <summary>Recorre las victorias reales y CONTINUAR de cada checkpoint; conserva los datos del usuario.</summary>
    [InitializeOnLoad]
    public static class CampaignSavePlayTest
    {
        private const string Running = "Overdrive.CampaignSaveTest";
        private static readonly string[] Levels = { "Level01", "Level02", "Level03", "Level04" };
        private static readonly List<string> Checks = new List<string>();
        private static int step, levelIndex, frame;
        private static double started, stage;
        private static bool background;
        private static string expected;
        private static Vector3 entranceStart, snapshotPosition;
        private static LevelManager level;

        [Serializable] private sealed class Checkpoint { public string scene; }

        static CampaignSavePlayTest()
        {
            EditorApplication.playModeStateChanged += OnState;
            if (SessionState.GetBool(Running, false)) EditorApplication.update += Tick;
        }

        [MenuItem("TopDown/Pruebas/Campaña, guardado y Continuar")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Detén Play Mode antes de iniciar la prueba.");
            for (int i=0; i<SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Guarda las escenas antes de probar.");
            SessionState.SetString(Running+".Scene",SceneManager.GetActiveScene().path);
            SessionState.SetBool(Running+".Save",File.Exists(SaveSystem.FilePath));
            if (File.Exists(SaveSystem.FilePath)) File.Copy(SaveSystem.FilePath,"Temp/Campaign-save.backup",true);
            File.Copy("ProjectSettings/TimeManager.asset","Temp/Campaign-TimeManager.backup",true);
            SessionState.SetBool(Running+".ViewExists",PlayerPrefs.HasKey("firstPerson"));
            SessionState.SetInt(Running+".View",PlayerPrefs.GetInt("firstPerson"));
            foreach (string name in Levels)
            {
                string key="Overdrive.Record."+name;
                SessionState.SetBool(Running+key,PlayerPrefs.HasKey(key));
                SessionState.SetInt(Running+key+".Value",PlayerPrefs.GetInt(key));
            }
            ViewMode.FirstPerson=false;
            SaveSystem.Delete();
            EditorSceneManager.OpenScene("Assets/00_Scenes/Menu.unity",OpenSceneMode.Single);
            SessionState.SetBool(Running,true);
            EditorApplication.isPlaying=true;
        }

        private static void OnState(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Running,false)) return;
            if (state==PlayModeStateChange.EnteredPlayMode)
            {
                Checks.Clear(); step=levelIndex=0; frame=Time.frameCount;
                started=stage=EditorApplication.timeSinceStartup;
                background=Application.runInBackground; Application.runInBackground=true;
                Application.logMessageReceived+=OnLog;
                EditorApplication.update-=Tick; EditorApplication.update+=Tick;
            }
            if (state!=PlayModeStateChange.EnteredEditMode) return;
            Application.runInBackground=background; GameInput.Blocked=false;
            Application.logMessageReceived-=OnLog; EditorApplication.update-=Tick;
            SessionState.SetBool(Running,false);
            if (SessionState.GetBool(Running+".Save",false)) File.Copy("Temp/Campaign-save.backup",SaveSystem.FilePath,true);
            else if (File.Exists(SaveSystem.FilePath)) File.Delete(SaveSystem.FilePath);
            ViewMode.FirstPerson=SessionState.GetInt(Running+".View",0)==1;
            if (!SessionState.GetBool(Running+".ViewExists",false)) PlayerPrefs.DeleteKey("firstPerson");
            foreach (string name in Levels)
            {
                string key="Overdrive.Record."+name;
                if (SessionState.GetBool(Running+key,false)) PlayerPrefs.SetInt(key,SessionState.GetInt(Running+key+".Value",0));
                else PlayerPrefs.DeleteKey(key);
            }
            PlayerPrefs.Save();
            File.Copy("Temp/Campaign-TimeManager.backup","ProjectSettings/TimeManager.asset",true);
            File.WriteAllLines("Temp/CampaignSave.playtest.txt",Checks);
            string previous=SessionState.GetString(Running+".Scene","Assets/00_Scenes/Menu.unity");
            EditorApplication.delayCall+=()=>EditorSceneManager.OpenScene(previous,OpenSceneMode.Single);
        }

        private static void OnLog(string message,string trace,LogType type)
        {
            if (type==LogType.Error || type==LogType.Exception) Checks.Add("FAIL Unity: "+message);
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying) return;
            try
            {
                double now=EditorApplication.timeSinceStartup;
                if (now-started>180) throw new InvalidOperationException("Tiempo de prueba excedido en paso "+step);
                if (Time.frameCount==frame) return;
                frame=Time.frameCount;
                if (step==10) { WalkPastNina(); return; }
                if (now-stage<1.2) return;
                switch (step)
                {
                    case 0:
                        var menu=Object.FindAnyObjectByType<MainMenuController>();
                        Check(menu!=null && !menu.continueButton.gameObject.activeSelf,"Sin partida no aparece CONTINUAR");
                        Check(!SaveSystem.Continue(),"Sin archivo no se intenta cargar una escena");
                        SaveSystem.SaveCheckpoint("Level04");
                        menu.playButton.GetComponent<Button>().onClick.Invoke(); Go(1); break;
                    case 1:
                        Check(SceneManager.GetActiveScene().name==Levels[levelIndex],"Carga real de "+Levels[levelIndex]);
                        if (levelIndex==3) { FinishChase(); break; }
                        level=Object.FindAnyObjectByType<LevelManager>();
                        FreezeCombat();
                        if (levelIndex==0)
                        {
                            expected="Level01";
                            VerifyCheckpoint();
                            Check(true,"JUGAR reemplaza el checkpoint anterior por una campaña nueva");
                            snapshotPosition=level.Player.transform.position;
                            Object.FindAnyObjectByType<TimeRewinder>().SetUsesLeft(2);
                            Check(SaveSystem.Save(),"Serialización del estado de Level01 en disco");
                            Warp(level.Player.transform,snapshotPosition+Vector3.right);
                            Check(SaveSystem.Continue(),"Carga diferida del guardado de Level01");
                            Go(15);
                        }
                        else if (levelIndex==1)
                        {
                            entranceStart=level.Player.transform.position;
                            var nina=Object.FindAnyObjectByType<NpcDialogue>();
                            Check(nina!=null && Vector3.Distance(entranceStart,nina.transform.position)<3f,"Nina está visible junto al inicio de Level02");
                            Check(nina.Talking,"El diálogo de Nina se activa desde el inicio");
                            Go(10);
                        }
                        else
                        {
                            var elevator=Object.FindAnyObjectByType<ArenaElevator>();
                            foreach (var guard in elevator.Guards) guard.ReceiveLethalHit();
                            Check(elevator.IsOpen,"Los guardias del loft abren el ascensor");
                            Warp(level.Player.transform,elevator.transform.position); Go(16);
                        }
                        break;
                    case 15:
                        level=Object.FindAnyObjectByType<LevelManager>(); FreezeCombat();
                        Check(Vector3.Distance(level.Player.transform.position,snapshotPosition)<.3f,"El guardado restaura la posición real");
                        Check(Object.FindAnyObjectByType<TimeRewinder>().UsesLeft==2,"El guardado restaura los rebobinados");
                        PrepareVictory(); break;
                    case 16:
                        var lift=Object.FindAnyObjectByType<ArenaElevator>();
                        Check(lift.Used && Vector3.Distance(level.Player.transform.position,lift.ArenaStart.position)<.6f,"El ascensor transporta al jugador al ático");
                        PrepareVictory(); break;
                    case 2:
                        Warp(level.Player.transform,level.Exit.transform.position);
                        Check(level.Evaluate()==LevelState.Complete,"La salida completa "+Levels[levelIndex]);
                        expected=level.NextScene; VerifyCheckpoint();
                        SceneManager.LoadScene("Menu"); Go(3); break;
                    case 3:
                        Check(SceneManager.GetActiveScene().name=="Menu","Regreso al menú con checkpoint de "+expected);
                        var continueMenu=Object.FindAnyObjectByType<MainMenuController>();
                        Check(continueMenu.continueButton.gameObject.activeSelf,"CONTINUAR visible para "+expected);
                        continueMenu.continueButton.GetComponent<Button>().onClick.Invoke(); Go(4); break;
                    case 4:
                        Check(SceneManager.GetActiveScene().name==expected,"CONTINUAR carga "+expected+" desde el archivo");
                        Check(Time.timeScale==1f && !GameInput.Blocked,"La carga recupera tiempo y controles");
                        if (expected=="Ending")
                        {
                            var ending=Object.FindAnyObjectByType<EndingScreen>();
                            Check(ending.Timeline.Phase==EndingPhase.Epilogue,"El checkpoint final abre el epílogo");
                            ending.Timeline.SkipEpilogue();
                            Check(ending.Timeline.Phase==EndingPhase.Credits,"El epílogo conduce a los créditos");
                            ending.Timeline.Tick(300f);
                            Check(ending.Timeline.Phase==EndingPhase.Thanks,"Los créditos llegan al agradecimiento");
                            ending.ReturnToMenu(); Go(5);
                        }
                        else
                        {
                            levelIndex++;
                            if (levelIndex<3)
                            {
                                var fresh=Object.FindAnyObjectByType<LevelManager>();
                                Check(!fresh.Player.IsDead && fresh.Player.CurrentHealth==fresh.Player.MaxHealth,"El checkpoint inicia al jugador con vida completa");
                                Check(fresh.Briefcase==null || !fresh.Briefcase.IsCollected,"El siguiente nivel no hereda el maletín anterior");
                            }
                            Go(1);
                        }
                        break;
                    case 20:
                        var chase=Object.FindAnyObjectByType<ChaseLevel>();
                        Check(chase.ContinueAfterWin(),"La victoria de Level04 abre el final"); Go(21); break;
                    case 21:
                        Check(SceneManager.GetActiveScene().name=="Ending","Level04 conecta con Ending");
                        SceneManager.LoadScene("Menu"); Go(3); break;
                    case 5:
                        Check(SceneManager.GetActiveScene().name=="Menu" && !SaveSystem.HasSave,"Finalizar la campaña limpia su checkpoint");
                        Check(!Object.FindAnyObjectByType<MainMenuController>().continueButton.gameObject.activeSelf,"CONTINUAR se oculta al terminar la campaña");
                        EditorApplication.isPlaying=false; break;
                }
            }
            catch (Exception e) { Checks.Add("FAIL "+e); EditorApplication.isPlaying=false; }
        }

        private static void WalkPastNina()
        {
            var controller=level.Player.GetComponent<CharacterController>();
            // Stop before the security gate at z=26.4, which correctly stays closed
            // until the briefcase is collected. This still walks past Nina's capsule.
            float target=entranceStart.z+1.8f;
            if (level.Player.transform.position.z<target-.12f)
            {
                controller.Move(Vector3.forward*Mathf.Min(.08f,target-level.Player.transform.position.z));
                if (EditorApplication.timeSinceStartup-stage>8) throw new InvalidOperationException("Nina bloquea el paso por el corredor.");
                return;
            }
            Check(Mathf.Abs(level.Player.transform.position.x-entranceStart.x)<.15f,"El CharacterController atraviesa el pasillo junto a Nina sin atascarse");
            Warp(level.Player.transform,entranceStart); PrepareVictory();
        }

        private static void FreezeCombat()
        {
            GameInput.Blocked=true;
            foreach (var ai in Object.FindObjectsByType<EnemyAI>(FindObjectsInactive.Include)) ai.enabled=false;
            foreach (var boss in Object.FindObjectsByType<BossEnemy>()) boss.enabled=false;
        }

        private static void PrepareVictory()
        {
            if (level.Briefcase!=null && !level.Briefcase.IsCollected)
            {
                Warp(level.Player.transform,level.Briefcase.transform.position);
                Check(level.Briefcase.TryCollect(level.Player.transform),"Recogida del objetivo de "+Levels[levelIndex]);
            }
            level.Evaluate(); FreezeCombat();
            foreach (var boss in Object.FindObjectsByType<BossEnemy>())
            {
                var enemy=boss.GetComponent<Enemy>();
                for (int i=0;i<boss.MaxHealth+2 && !enemy.IsDead;i++) { boss.ClockOffset+=1f; enemy.ReceiveLethalHit(KillCause.Shot); }
                Check(enemy.IsDead && boss.Raging && boss.Desperate,"El Jefe cae pasando por sus fases y refuerzos");
            }
            foreach (var enemy in Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include)) if (!enemy.IsDead) enemy.ReceiveLethalHit();
            Check(level.Evaluate()==LevelState.ExitOpen,"Enemigos y refuerzos abren la salida de "+Levels[levelIndex]);
            Go(2);
        }

        private static void FinishChase()
        {
            var chase=Object.FindAnyObjectByType<ChaseLevel>();
            Check(chase!=null && !chase.Player.Destroyed && chase.Player.Health01>.99f,"El checkpoint de Level04 restaura el coche completo");
            chase.Player.Rail.Along=chase.Road.Length-20f; chase.Player.Rail.Speed=0;
            chase.Player.Rail.Step(.0001f); chase.Tick(.1f);
            Check(chase.State==ChaseState.Won,"Llegar al refugio gana Level04");
            expected=chase.NextScene; VerifyCheckpoint(); Go(20);
        }

        private static void VerifyCheckpoint()
        {
            Check(SaveSystem.HasSave && JsonUtility.FromJson<Checkpoint>(File.ReadAllText(SaveSystem.FilePath)).scene==expected,"Checkpoint guardado apunta a "+expected);
        }

        private static void Warp(Transform target,Vector3 position)
        {
            var controller=target.GetComponent<CharacterController>(); bool enabled=controller!=null && controller.enabled;
            if (controller!=null) controller.enabled=false; target.position=position;
            if (controller!=null) controller.enabled=enabled;
            Physics.SyncTransforms();
        }
        private static void Go(int value) { step=value; stage=EditorApplication.timeSinceStartup; }
        private static void Check(bool condition,string detail)
        {
            if (!condition) throw new InvalidOperationException(detail); Checks.Add("PASS "+detail);
        }
    }
}
