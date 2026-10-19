using System.IO;
using UnityEngine;

namespace Overdrive.EditorTools
{
    internal static class NeonVisualCapture
    {
        public static void SaveUi(Canvas canvas, string name, int width, int height)
        {
            var camera=Camera.main;
            if(camera==null || canvas==null) return;
            var mode=canvas.renderMode; var canvasCamera=canvas.worldCamera; float distance=canvas.planeDistance;
            var target=camera.targetTexture; var active=RenderTexture.active; int mask=camera.cullingMask;
            var image=new Texture2D(width,height,TextureFormat.RGB24,false);
            var render=new RenderTexture(width,height,24);
            try
            {
                canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=camera.nearClipPlane+1f;
                camera.targetTexture=render; camera.cullingMask=~0;
                Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active=render;
                image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply();
                File.WriteAllBytes("Temp/NeonShots/"+name+".png",image.EncodeToPNG());
            }
            finally
            {
                canvas.renderMode=mode; canvas.worldCamera=canvasCamera; canvas.planeDistance=distance;
                camera.targetTexture=target; camera.cullingMask=mask; RenderTexture.active=active;
                Object.DestroyImmediate(image); Object.DestroyImmediate(render); Canvas.ForceUpdateCanvases();
            }
        }
    }
}
