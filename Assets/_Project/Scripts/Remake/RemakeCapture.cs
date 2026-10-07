using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HorrorUtez.Remake
{
    public sealed class RemakeCapture : MonoBehaviour
    {
        public void Setup(RemakeGame game, bool tour = false) => StartCoroutine(tour ? Tour(game) : Capture(game));
        // -remakeWatchGiant: the student waits safely inside the truck while the giant patrols at 3x speed;
        // logs its state, waypoint and position so the route can be checked from the log.
        public void Watch(RemakeGame game) => StartCoroutine(WatchGiant(game));
        private IEnumerator WatchGiant(RemakeGame game)
        {
            while(!game.Started)yield return null;
            yield return new WaitForSeconds(1);
            RemakeEnemy giant=null;foreach(RemakeEnemy e in game.Enemies){if(e.Kind==EnemyKind.Giant)giant=e;else e.Rest(9999);}
            var leg=new System.Collections.Generic.List<Vector3>();
            for(int i=0;i<game.GiantRoute.Count;i++)
            {
                Vector3 a=game.GiantRoute[i],b=game.GiantRoute[(i+1)%game.GiantRoute.Count];
                game.FindPath(a,b,leg);float len=0;Vector3 at=a;foreach(Vector3 q in leg){len+=Vector3.Distance(at,q);at=q;}
                Debug.Log("[Watch] leg "+i+" "+a.ToString("F0")+" -> "+b.ToString("F0")+" path "+len.ToString("F0")+" m, straight "+Vector3.Distance(a,b).ToString("F0")+" m");
            }
            foreach(HorrorUtez.World.HingedDoor d in Object.FindObjectsByType<HorrorUtez.World.HingedDoor>(FindObjectsInactive.Include))
                Debug.Log("[Watch] door "+d.name+" at "+d.transform.position.ToString("F1")+" open "+d.IsOpen);
            foreach(var room in game.Dressing.RoomCentres)
            {
                game.FindPath(game.Dressing.Corridor,room.Value,leg);float len=0;Vector3 at=game.Dressing.Corridor;foreach(Vector3 q in leg){len+=Vector3.Distance(at,q);at=q;}
                bool ok=leg.Count>0&&Vector3.Distance(leg[leg.Count-1],room.Value)<3.5f;
                Debug.Log("[Watch] room "+room.Key+" reachable "+ok+" path "+len.ToString("F0")+" m straight "+Vector3.Distance(game.Dressing.Corridor,room.Value).ToString("F0"));
            }
            game.Player.Teleport(game.TruckPosition+new Vector3(0,.9f,-1.5f));
            giant.Wake();Time.timeScale=3;
            var visited=new System.Collections.Generic.HashSet<string>();
            var trail=new System.Collections.Generic.List<Vector3>();
            for(float t=0;t<180;t+=3)
            {
                yield return new WaitForSecondsRealtime(1);
                Vector3 p=giant.transform.position;trail.Add(p);visited.Add(giant.State.Substring(giant.State.LastIndexOf(' ')+1));
                Debug.Log("[Watch] t="+(t).ToString("F0")+" "+giant.State+" pos "+p.ToString("F1")+" crawl "+giant.Crawl.ToString("F2")+" | student "+game.Player.transform.position.ToString("F1")+" phase "+game.Phase);
            }
            Time.timeScale=1;
            Debug.Log("[Watch] waypoints visited: "+string.Join(",",visited));
            string dir=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
            File.WriteAllBytes(Path.Combine(dir,"nav-map.png"),game.NavigationMap(trail).EncodeToPNG());
            File.WriteAllBytes(Path.Combine(dir,"nav-cecadec.png"),game.NavigationMap(trail,new Vector2(-16,-58),new Vector2(18,-6),10).EncodeToPNG());
            Application.Quit(0);
        }
        private IEnumerator Capture(RemakeGame game)
        {
            yield return new WaitForSeconds(3);
            string directory=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
            Save(game.CaptureView,Path.Combine(directory,game.Started?"remake-gameplay.png":"remake-menu.png"));
            if(!game.Started)Application.Quit();
        }
        // Diagnostic tour (-remakeSolo -remakeTour): fixed shots of the dressed labs, the enemies, the truck
        // and a stand-in remote student, so visuals can be reviewed from a build without the editor.
        private IEnumerator Tour(RemakeGame game)
        {
            float until=Time.realtimeSinceStartup+20;
            while(!game.Started && Time.realtimeSinceStartup<until)yield return null;
            yield return new WaitForSeconds(2);
            string directory=Path.GetFullPath(Path.Combine(Application.dataPath,".."));
            game.Hud.ShowManual(true);yield return null;
            Save(game.Player.View,Path.Combine(directory,"tour-manual.png"));game.Hud.ShowManual(false);
            foreach(var enemy in game.Enemies)enemy.Rest(600);
            Vector3 skinStage=game.TruckPosition+new Vector3(-4,.05f,-7);
            game.Students[77]=new StudentState{id=77,name="Erick",position=skinStage,yaw=0,alive=true,torch=false,aim=Vector3.forward};
            for(int skin=0;skin<5;skin++) {
                game.Students[77].skin=skin;game.Students[77].name=RemakeSkins.Names[skin];
                game.Player.Teleport(skinStage+new Vector3(.35f,0,1.8f));game.Player.Aim(190,0);
                yield return new WaitForSeconds(.65f);
                if(skin==1) {
                    var avatar=game.Avatar(77);
                    foreach(var r in avatar.GetComponentsInChildren<SkinnedMeshRenderer>())if(r.name=="PlayerHead") {
                        var baked=new Mesh();r.BakeMesh(baked);Vector3 face=Vector3.zero;int count=0;
                        var verts=baked.vertices;var uv=baked.uv;
                        for(int v=0;v<verts.Length;v++)if(uv[v].x>.4f&&uv[v].x<.6f&&uv[v].y>.3f&&uv[v].y<.55f){face+=r.transform.TransformPoint(verts[v]);count++;}
                        if(count>0)Debug.Log("[SkinQA] portrait surface relative to avatar "+avatar.InverseTransformPoint(face/count)+" head "+avatar.InverseTransformPoint(r.bounds.center)+" shapes "+r.sharedMesh.blendShapeCount);
                        Destroy(baked);
                    }
                }
                Save(game.Player.View,Path.Combine(directory,"tour-skin-"+RemakeSkins.Names[skin]+".png"));
                if(skin==1) {
                    game.Player.Teleport(skinStage+new Vector3(0,0,-1.1f));game.Player.Aim(0,0);
                    yield return new WaitForSeconds(.4f);Save(game.Player.View,Path.Combine(directory,"tour-skin-Erick-opposite.png"));
                    game.Player.Teleport(skinStage+new Vector3(0,0,1.1f));game.Player.Aim(180,0);
                    yield return new WaitForSeconds(.4f);Save(game.Player.View,Path.Combine(directory,"tour-skin-Erick-front.png"));
                }
            }
            game.Students[77].skin=1;game.Students[77].eye=.82f;
            yield return new WaitForSeconds(.7f);Save(game.Player.View,Path.Combine(directory,"tour-skin-crouch.png"));
            game.Students.Remove(77);
            Vector3 handStage=game.TruckPosition+new Vector3(-6,.15f,-8);
            foreach(int index in new[]{0,18,19,25}) {
                var item=game.Loot[index];item.ResetLoot();item.Body.position=handStage+new Vector3(0,.65f,1.05f);item.Body.rotation=Quaternion.identity;
                item.Body.linearVelocity=Vector3.zero;item.Body.angularVelocity=Vector3.zero;
                game.Player.Teleport(handStage);game.Player.Aim(0,12);yield return new WaitForSeconds(.2f);
                game.Hud.GrabHeld=true;game.Request("grab",index);yield return new WaitForSeconds(.7f);
                Save(game.Player.View,Path.Combine(directory,"tour-grip-"+index+".png"));
                game.Hud.GrabHeld=false;game.Request("drop");yield return new WaitForSeconds(.1f);item.ResetLoot();
            }
            foreach(var enemy in game.Enemies) {
                enemy.gameObject.SetActive(true);enemy.DebugPlace(handStage+new Vector3(0,0,5));enemy.transform.forward=Vector3.back;
                game.Player.Teleport(handStage);game.Player.Aim(0,-5);yield return new WaitForSeconds(.4f);
                Save(game.Player.View,Path.Combine(directory,"tour-profile-"+enemy.DisplayName.ToLower()+".png"));
                enemy.ResetEnemy();enemy.Rest(600);
            }
            game.Students[77]=new StudentState{id=77,name="Compañero",position=game.TruckPosition+new Vector3(-4,0.05f,-7),yaw=200,alive=true,torch=true,aim=Vector3.forward};
            var shots=new System.Collections.Generic.List<(string name,Vector3 at,Vector3 look)>();
            foreach(string building in new[]{"CDS", "Auditorium"})
            {
                var structure=GameObject.Find("UTEZ_Buildings/"+building+"_Structure");
                if(structure!=null) shots.Add((building.ToLower()+"_interior",structure.transform.TransformPoint(new Vector3(0,1.38f,4)),structure.transform.TransformPoint(new Vector3(3,1,-5))));
            }
            shots.Add(("forest",new Vector3(32,1.38f,-8),new Vector3(48,3,-16)));
            Transform interior=null;
            foreach(Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))if(t.name=="CECADEC_Interior"){interior=t;break;}
            if(interior!=null)
            {
                Vector3 W(float x,float z,float y=1.38f)=>interior.TransformPoint(new Vector3(x,y,z));
                shots.Add(("cc9",W(3.2f,-15.0f),W(8.5f,-21.5f,.8f)));
                shots.Add(("cc9_board",W(4.5f,-16.5f),W(6.5f,-22.2f,1.5f)));
                shots.Add(("aula1",W(3.2f,-1.2f),W(8.5f,-7.5f,.8f)));
                shots.Add(("corridor",W(0,-1.5f),W(0,-20,1.2f)));
                shots.Add(("process_lab",W(3.0f,-33.0f),W(7.5f,-28.8f,.9f)));
                shots.Add(("back_room",W(0,-38.8f),W(-6,-43.5f,.8f)));
            }
            if(game.Dressing!=null && game.Dressing.Ready)
            {
                Vector3 door=game.Dressing.EastDoor, outward=Vector3.ProjectOnPlane(door-game.Dressing.Corridor,Vector3.up).normalized;
                Vector3 side=Vector3.Cross(Vector3.up,outward);
                shots.Add(("east_door_out",door+outward*7+side*2.5f+Vector3.up*1.38f,door+Vector3.up*1.3f));
                shots.Add(("east_door_in",door-outward*4.5f-side*1.5f+Vector3.up*1.38f,door+Vector3.up*1.2f));
            }
            Vector3 friend=game.Students[77].position;
            shots.Add(("player_model",friend+new Vector3(.9f,1.38f,-2.6f),friend+Vector3.up*.8f));
            shots.Add(("truck",game.TruckPosition+new Vector3(-5,1.38f,-9),game.TruckPosition+new Vector3(0,1.4f,0)));
            foreach(RemakeEnemy enemy in game.Enemies)
            {
                Vector3 p=enemy.transform.position, f=enemy.transform.forward;
                float d=enemy.Kind==EnemyKind.Giant?5.5f:3f;
                shots.Add((enemy.Kind==EnemyKind.Giant?"giant":"caretaker_"+enemy.name.Substring(enemy.name.Length-1),
                    p+f*d+Vector3.up*1.38f,p+Vector3.up*(enemy.Kind==EnemyKind.Giant?2.2f:1.3f)));
            }
            foreach((string name,Vector3 at,Vector3 look) in shots)
            {
                game.Local.alive=true; game.Local.health=100;
                game.Player.Teleport(at-Vector3.up*1.38f);
                Vector3 d=look-at;
                game.Player.Aim(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,-Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg);
                yield return new WaitForSeconds(1.2f);
                Save(game.Player.View,Path.Combine(directory,"tour-"+name+".png"));
            }
            // The giant awake: striding at the student outdoors, then folded into a crawl inside CECADEC.
            RemakeEnemy giant=null; foreach(RemakeEnemy e in game.Enemies) if(e.Kind==EnemyKind.Giant) giant=e;
            if(giant!=null)
            {
                Vector3 plaza=game.TruckPosition+new Vector3(-4,0,-9);
                giant.DebugPlace(plaza+new Vector3(12,0,0)); giant.Wake();
                game.Local.alive=true; game.Local.health=100; game.Player.Teleport(plaza); game.Player.Aim(90,-8);
                yield return new WaitForSeconds(1.6f); Save(game.Player.View,Path.Combine(directory,"tour-giant_walk.png"));
                yield return new WaitForSeconds(1.2f); Save(game.Player.View,Path.Combine(directory,"tour-giant_close.png"));
                if(interior!=null)
                {
                    Vector3 inside=interior.TransformPoint(new Vector3(0,0,-16));
                    giant.DebugPlace(inside); giant.Wake();
                    game.Local.alive=true; game.Local.health=100;
                    Vector3 eye=interior.TransformPoint(new Vector3(0,1.38f,-4.5f));
                    game.Player.Teleport(eye-Vector3.up*1.38f);
                    Vector3 d=inside+Vector3.up*1.2f-eye;
                    game.Player.Aim(Mathf.Atan2(d.x,d.z)*Mathf.Rad2Deg,-Mathf.Atan2(d.y,new Vector2(d.x,d.z).magnitude)*Mathf.Rad2Deg);
                    yield return new WaitForSeconds(1.8f); Save(game.Player.View,Path.Combine(directory,"tour-giant_corridor.png"));
                    yield return new WaitForSeconds(1.0f); Save(game.Player.View,Path.Combine(directory,"tour-giant_corridor2.png"));
                }
            }
            Debug.Log("[RemakeCapture] Tour complete: "+shots.Count+" shots");
            Application.Quit(0);
        }
        // Render requests work even when the executable's window is hidden during QA.
        public static void Save(Camera camera,string path)
        {
            if(SystemInfo.graphicsDeviceType==GraphicsDeviceType.Null)return;
            var hud=Object.FindAnyObjectByType<RemakeHud>();
            var canvas=hud!=null?hud.GetComponentInChildren<Canvas>():Object.FindAnyObjectByType<Canvas>();
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
