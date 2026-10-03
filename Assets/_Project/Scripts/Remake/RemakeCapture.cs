using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HorrorUtez.Remake
{
    public sealed class RemakeCapture : MonoBehaviour
    {
        public void Setup(RemakeGame game)=>StartCoroutine(Capture(game));
        private IEnumerator Capture(RemakeGame game)
        {
            yield return new WaitForSeconds(3);
            string directory=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
            Save(game.CaptureView,Path.Combine(directory,game.Started?"remake-gameplay.png":"remake-menu.png"));
            if(!game.Started)Application.Quit();
        }
        // Render requests work even when the executable's window is hidden during QA.
        public static void Save(Camera camera,string path)
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)return;
            var canvas=Object.FindAnyObjectByType<Canvas>();
            RenderMode oldMode=canvas!=null?canvas.renderMode:RenderMode.ScreenSpaceOverlay;
            Camera oldCamera=canvas!=null?canvas.worldCamera:null;
            if(canvas!=null){canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=.5f;}
            Canvas.ForceUpdateCanvases();
            var target=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32);target.Create();
            var request=new UniversalRenderPipeline.SingleCameraRequest{destination=target};
            RenderPipeline.SubmitRenderRequest(camera,request);
            RenderTexture previous=RenderTexture.active;RenderTexture.active=target;
            var texture=new Texture2D(1600,900,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1600,900),0,0);texture.Apply();
            File.WriteAllBytes(path,texture.EncodeToPNG());RenderTexture.active=previous;
            Object.Destroy(texture);target.Release();Object.Destroy(target);
            if(canvas!=null){canvas.renderMode=oldMode;canvas.worldCamera=oldCamera;}
            Debug.Log("[RemakeCapture] "+path);
        }
    }
}
