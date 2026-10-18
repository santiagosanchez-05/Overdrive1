using System.IO;
using UnityEngine;

namespace Overdrive.EditorTools
{
    internal static class EndingCapture
    {
        public static void Save(string path, int width, int height)
        {
            var canvas = Object.FindAnyObjectByType<Overdrive.Story.EndingScreen>().GetComponent<Canvas>();
            var camera = Camera.main;
            var oldMode = canvas.renderMode;
            var oldCamera = canvas.worldCamera;
            float oldDistance = canvas.planeDistance;
            var oldTarget = camera.targetTexture;
            int oldMask = camera.cullingMask;
            var oldActive = RenderTexture.active;
            var target = new RenderTexture(width, height, 24);
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera; canvas.planeDistance = 1f;
                camera.targetTexture = target; camera.cullingMask = ~0;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget; camera.cullingMask = oldMask;
                canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldDistance;
                RenderTexture.active = oldActive;
                Object.DestroyImmediate(pixels); Object.DestroyImmediate(target);
                Canvas.ForceUpdateCanvases();
            }
        }
    }
}
