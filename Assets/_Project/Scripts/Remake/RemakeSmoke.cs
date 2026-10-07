using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace HorrorUtez.Remake
{
    // Opt-in executable integration check; never runs in a normal play session.
    public sealed class RemakeSmoke : MonoBehaviour
    {
        private RemakeGame game;
        private bool hadError;
        public void Setup(RemakeGame owner){game=owner;Application.logMessageReceived+=OnLog;StartCoroutine(Check());}
        private void OnLog(string text,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception)hadError=true;}
        private void OnDestroy(){Application.logMessageReceived-=OnLog;}
        private void Assert(bool condition,string message)
        {
            if(!condition){Debug.LogError("[RemakeSmoke] FAIL "+message);Application.Quit(3);throw new Exception(message);}
            Debug.Log("[RemakeSmoke] PASS "+message);
        }
        private IEnumerator Check()
        {
            float until=Time.realtimeSinceStartup+25;
            while(!game.Started && Time.realtimeSinceStartup<until)yield return null;
            Assert(game.Started,"session starts");
            yield return new WaitForSeconds(2);
            Assert(game.Loot.Count==26 && !game.Loot.Any(l=>l.Kind==RemakeLoot.CartKind),"university loot and ID cards spawn without a cart");
            Assert(game.Player.transform.position.y<1,"student stands on campus floor");
            Assert(game.Enemies.Count==5 && game.Enemies.Select(e=>e.DisplayName).Distinct().Count()==5,"five named enemy profiles exist");
            Assert(game.Loot.Where(l=>l.BaseValue>0).Select(l=>l.Kind).Distinct().Count()==16,"sixteen valuable types exist");
            var handMeshes=game.Player.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.sharedMesh.name.Contains("SparrowHawk")).ToArray();
            Assert(handMeshes.Length==2 && handMeshes.All(r=>r.bones.Length==22 && r.sharedMesh.vertexCount>4000),"both hands use the downloaded anatomical mesh and deform rig");
            Assert(game.Player.View!=null && game.Player.View.enabled,"first person camera");
            Assert(game.Local.skin==game.SelectedSkin,"selected skin is authoritative for the local student");
            Assert(RemakeSkins.Names.Length==5 && RemakeSkins.Names.All(n=>Resources.Load<Texture2D>("Skins/Skin_"+n)!=null),"five selectable skins have face textures");
            var skinModel=game.StudentModel.GetComponentsInChildren<SkinnedMeshRenderer>();
            Assert(skinModel.Length>=2 && skinModel.All(r=>r.sharedMesh.blendShapeCount>=5),"body and head have five sculpted skin variants");
            if(game.Authority) {
                var access=new System.Collections.Generic.List<Vector3>();string unreachable="";
                foreach(var valuable in game.Loot.Where(l=>l.Value>0 && l.gameObject.activeSelf)) {
                    game.FindPath(game.SpawnPoint(0),valuable.Spawn,access);
                    if(access.Count==0 || Vector3.Distance(access[access.Count-1],valuable.Spawn)>2.8f)unreachable+=valuable.Label+"; ";
                }
                Assert(unreachable=="","all active valuables have reachable pickup locations "+unreachable);
            }
            if(!game.Authority)
            {
                Assert(game.Students.Count>=2,"client receives host and local student");
                game.Player.Teleport(game.Loot[0].transform.position+new Vector3(0,0,-1.3f),45);
                yield return new WaitForSeconds(1);
                game.Hud.GrabHeld=true;game.Request("grab",0);yield return new WaitForSeconds(1);
                Assert(game.Local.held==0,"client grab acknowledged by host");
                game.Hud.GrabHeld=false;yield return new WaitForSeconds(.5f);
                Assert(game.Local.held==-1,"client release acknowledged by host");
                Assert(!hadError,"client has no runtime errors");
                // Walk rather than teleport across the host's pose-speed validation.
                Vector3 boarding=game.TruckPosition+new Vector3(0,.9f,-2.8f);
                float walkUntil=Time.realtimeSinceStartup+6;
                while(Vector3.Distance(game.Player.transform.position,boarding)>.05f && Time.realtimeSinceStartup<walkUntil){game.Player.Teleport(Vector3.MoveTowards(game.Player.transform.position,boarding,3*Time.deltaTime));yield return null;}
                until=Time.realtimeSinceStartup+150;
                while(!game.ShopReady && Time.realtimeSinceStartup<until)yield return null;
                Assert(game.ShopReady && game.Phase==3,"client loads the separate shop scene");
                Assert(game.Local.escaped,"client boarded the truck and retains earnings");
                Vector3 product=RemakeShop.Product(1)+new Vector3(0,-1.05f,-1);
                while(Vector3.Distance(game.Player.transform.position,product)>.05f){game.Player.Teleport(Vector3.MoveTowards(game.Player.transform.position,product,3*Time.deltaTime));yield return null;}
                yield return new WaitForSeconds(.3f);game.Request("upgrade",1);yield return new WaitForSeconds(1);
                Assert(game.Local.stamina==1,"client physically purchases an upgrade with host authority");
                while(game.Day<2 && Time.realtimeSinceStartup<until)yield return null;
                Assert(game.Day==2 && game.Phase==1 && !game.ShopReady,"client returns to campus on the next day");
                Assert(game.Local.stamina==1,"client keeps purchased upgrade");
                Assert(game.Local.skin==game.SelectedSkin,"client skin survives shop and day progression");
                Assert(!hadError,"client full cycle has no runtime errors");
                Debug.Log("[RemakeSmoke] CLIENT COMPLETE");yield return new WaitForSeconds(2);Application.Quit(0);yield break;

            }
            if(game.Online)
            {
                while(game.Students.Count<2 && Time.realtimeSinceStartup<until)yield return null;
                Assert(game.Students.Count>=2,"host accepts second player");
                yield return new WaitForSeconds(5);
            }
            // Test a third, fallen student and the rule that stranded players lose upgrades.
            game.Students[99]=new StudentState{id=99,name="Smoke fallen",position=new Vector3(-3,0,-3),strength=2,credits=90};
            game.Hurt(99,100);Assert(!game.Students[99].alive,"lethal damage drops the student");
            game.Player.Teleport(game.Loot[0].transform.position+new Vector3(0,0,-1.3f),45);
            yield return new WaitForSeconds(.2f);
            Vector3 before=game.Loot[0].transform.position;game.Hud.GrabHeld=true;game.Request("grab",0);
            yield return new WaitForSeconds(.5f);
            Assert(game.Local.held==0,"host grabs salvage");
            var palms=game.Player.GetComponentsInChildren<Transform>().Where(t=>t.name=="Mano articulada / dedos y contacto").ToArray();
            var heldCollider=game.Loot[0].GetComponent<Collider>();
            Assert(palms.Length==2 && palms.All(t=>Vector3.Distance(t.position,heldCollider.ClosestPoint(t.position))<.07f),"both palms contact the object's surface");
            Assert(handMeshes.All(r=>r.sharedMesh.vertices.All(v=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z)&&v.magnitude<.6f)),"soft hand deformation remains finite and within anatomical bounds");
            Assert(Vector3.Distance(before,game.Loot[0].transform.position)>.08f,"grab applies physical force");
            game.Hud.GrabHeld=false;yield return new WaitForSeconds(.15f);
            Assert(game.Local.held==-1,"releasing grip drops cargo automatically");
            game.Hud.GrabHeld=true;game.Request("grab",0);yield return new WaitForSeconds(.3f);
            game.Hud.TogglePause();yield return new WaitForSeconds(.15f);
            Assert(game.Local.held==-1,"opening pause while gripping drops cargo");
            game.Hud.TogglePause();game.Hud.GrabHeld=false;yield return new WaitForSeconds(.15f);
            // Lift capacity: the 36 kg UPS stays on the floor for one student and rises with a second one.
            // Done in the open by the truck so furniture in the electrical room cannot interfere with the physics check.
            var ups=game.Loot[4];
            ups.Body.position=new Vector3(6,.5f,-3.5f);ups.Body.rotation=Quaternion.identity;
            ups.Body.linearVelocity=Vector3.zero;ups.Body.angularVelocity=Vector3.zero;Physics.SyncTransforms();
            yield return new WaitForSeconds(.6f);
            game.Player.Teleport(ups.transform.position+new Vector3(0,0,-1.4f),-10);
            yield return new WaitForSeconds(.3f);
            float floor=ups.transform.position.y;game.Hud.GrabHeld=true;game.Request("grab",4);
            yield return new WaitForSeconds(1.5f);
            Assert(game.Local.held==4 && ups.transform.position.y-floor<.35f,"one student cannot lift heavy UPS");
            StudentState local=game.Local;
            game.Students[98]=new StudentState{id=98,name="Smoke helper",position=local.position,yaw=local.yaw,pitch=local.pitch,
                eye=local.eye,reach=local.reach,held=4,grabLocal=local.grabLocal,hold=local.hold};
            yield return new WaitForSeconds(1.5f);
            Assert(ups.transform.position.y-floor>.4f,"two students lift heavy UPS together");
            game.Students.Remove(98);game.Hud.GrabHeld=false;yield return new WaitForSeconds(.3f);ups.ResetLoot();
            // Tumble: the student becomes a rolling body and gets back up on the floor.
            game.Player.Teleport(game.SpawnPoint(0));yield return new WaitForSeconds(.3f);
            game.Player.Tumble(new Vector3(0,2,4));yield return new WaitForSeconds(1.2f);
            Assert(game.Player.Tumbling && game.Local.tumble,"student tumbles as a physics body");
            game.Player.GetUp();yield return new WaitForSeconds(.5f);
            Assert(!game.Player.Tumbling && game.Player.transform.position.y<1,"tumbling student gets back up");
            // Fragile gear loses value on hard impacts and shatters below 15% of its worth.
            var scope=game.Loot[7];
            scope.Body.position=scope.Spawn+Vector3.up*.6f;scope.Body.linearVelocity=Vector3.down*5;
            yield return new WaitForSeconds(.8f);
            Assert(scope.Value>0 && scope.Value<scope.BaseValue,"hard impact reduces value");
            scope.Body.position=scope.Spawn+Vector3.up*.6f;scope.Body.linearVelocity=Vector3.down*9;
            yield return new WaitForSeconds(.8f);
            Assert(scope.Value==0 && !scope.gameObject.activeSelf,"broken gear shatters");
            scope.ResetLoot();
            Assert(scope.gameObject.activeSelf && scope.Value==scope.BaseValue,"new day restores broken gear");
            // El Rector: its route is navigable end to end, it chases a visible student, and the east glass door works.
            RemakeEnemy giant=null;foreach(RemakeEnemy e in game.Enemies){e.Rest(600);if(e.Kind==EnemyKind.Giant)giant=e;}
            var leg=new System.Collections.Generic.List<Vector3>();int broken=0;
            for(int i=0;i<game.GiantRoute.Count;i++)
            {
                Vector3 a=game.GiantRoute[i],b=game.GiantRoute[(i+1)%game.GiantRoute.Count];
                game.FindPath(a,b,leg);
                if(leg.Count==0||Vector3.Distance(leg[leg.Count-1],b)>2.5f){broken++;Debug.Log("[RemakeSmoke] route leg "+i+" unreachable "+a+" -> "+b);}
            }
            Assert(game.GiantRoute.Count>=12 && broken==0,"giant route is navigable end to end ("+game.GiantRoute.Count+" waypoints)");
            string closed="";
            foreach(var room in game.Markers.RoomCentres)
            {
                game.FindPath(game.Markers.Corridor,room.Value,leg);
                if(leg.Count==0||Vector3.Distance(leg[leg.Count-1],room.Value)>3.5f)closed+=room.Key+" ";
            }
            if(closed!="")
            {
                var nav=game.NavigationMap(null);File.WriteAllBytes(Path.Combine(Application.dataPath,"..","expansion-nav.png"),nav.EncodeToPNG());Destroy(nav);
                var cds=GameObject.Find("UTEZ_Buildings/CDS_Structure");
                if(cds!=null)
                {
                    foreach(float z in new[]{-14f,-12f,-10f,-8f,-5f,0f})
                    {
                        Vector3 p=cds.transform.TransformPoint(new Vector3(-2.0278f,.2f,z));
                        game.FindPath(game.SpawnPoint(0),p,leg);
                        Debug.Log("[RemakeSmoke] CDS threshold "+z+" route "+leg.Count+" endpoint "+(leg.Count>0?leg[leg.Count-1].ToString():"none"));
                    }
                    Vector3 from=cds.transform.TransformPoint(new Vector3(-2.0278f,1,-14));
                    Vector3 to=cds.transform.TransformPoint(new Vector3(-2.0278f,1,-8));
                    foreach(var hit in Physics.SphereCastAll(from,.25f,(to-from).normalized,6,game.WorldMask,QueryTriggerInteraction.Ignore))
                        Debug.Log("[RemakeSmoke] CDS entrance obstacle "+hit.collider.name+" "+hit.point);
                }
                foreach(var room in game.Markers.RoomCentres)
                    if(closed.Contains(room.Key))
                    {
                        Vector3 probeEye=room.Value+Vector3.up;
                        foreach(var hit in Physics.RaycastAll(probeEye,Vector3.back,15,game.WorldMask,QueryTriggerInteraction.Ignore))
                            Debug.Log("[RemakeSmoke] blocked room "+room.Key+" south probe "+hit.collider.name+" "+hit.point);
                    }
            }
            Assert(closed=="","every dressed room is reachable from the corridor "+closed);
            var ulises=game.Enemies.First(e=>e.Kind==EnemyKind.Ulises);
            ulises.gameObject.SetActive(true);ulises.DebugPlace(new Vector3(6,.1f,-3.5f));ulises.transform.forward=Vector3.left;
            var target=new StudentState{id=96,name="Flashlight probe",position=new Vector3(-5,.1f,-3.5f),torch=false};
            game.Students[96]=target;
            Assert(!ulises.DebugDetects(target),"Ulises cannot notice an unlit distant student");
            target.torch=true;bool noticed=false;for(int n=0;n<5;n++)noticed|=ulises.DebugDetects(target);
            Assert(noticed,"Ulises notices a visible flashlight at distance");game.Students.Remove(96);ulises.Rest(600);
            Vector3 open=new Vector3(6,0.1f,-3.5f);
            // A nearby student behind a solid wall must neither trigger chase nor take melee damage.
            game.Player.Teleport(open);
            giant.DebugPlace(open+new Vector3(-2,0,0)); giant.transform.forward=Vector3.right;
            var barrier=new GameObject("Smoke perception wall");
            barrier.transform.position=open+new Vector3(-1,2,0);
            barrier.AddComponent<BoxCollider>().size=new Vector3(.3f,5,10);
            Physics.SyncTransforms(); int healthBefore=game.Local.health;
            giant.Wake(); yield return new WaitForSeconds(1.3f);
            Assert(!giant.Chasing && game.Local.health==healthBefore,"solid walls block close detection and melee attacks");
            giant.Rest(600);Destroy(barrier);yield return null;
            game.Player.Teleport(open);game.Player.Aim(90,0);
            giant.DebugPlace(open+new Vector3(-15,0,1));giant.Wake();game.Player.Aim(-90,0);
            giant.transform.forward = (open-giant.transform.position).normalized;
            float startGap=Vector3.Distance(giant.transform.position,game.Player.transform.position);
            yield return new WaitForSeconds(3f);
            float gap=Vector3.Distance(giant.transform.position,game.Player.transform.position);
            Assert(giant.Chasing && startGap-gap>4,"giant chases a visible student ("+giant.State+", gap "+startGap.ToString("F1")+" -> "+gap.ToString("F1")+")");
            giant.Rest(600);giant.DebugPlace(game.GiantRoute.Count>11?game.GiantRoute[11]:open+new Vector3(40,0,0));
            game.Local.health=100;game.Local.alive=true;yield return new WaitForSeconds(1.5f);
            if(game.Player.Tumbling)game.Player.GetUp();
            Vector3 door=game.Markers.EastDoor;Vector3 outward=Vector3.ProjectOnPlane(door-game.Markers.Corridor,Vector3.up).normalized;
            Vector3 outside=door+outward*3,inside=door-outward*2.5f;
            game.Player.Teleport(outside+Vector3.up*.05f);yield return new WaitForSeconds(2f);
            Vector3 eye=Vector3.up*1.1f;
            Assert(!Physics.Raycast(outside+eye,(inside-outside).normalized,Vector3.Distance(outside,inside),~(1<<9),QueryTriggerInteraction.Ignore),"east glass door opens and is passable");
            game.FindPath(outside,game.Markers.Corridor,leg);
            Assert(leg.Count>0 && Vector3.Distance(leg[leg.Count-1],game.Markers.Corridor)<2.5f,"navigation reaches the corridor through the glass door");
            // R.E.P.O.-style revive: a fallen student's ID card carried into the truck brings them back.
            game.Students[97]=new StudentState{id=97,name="Smoke card",position=game.TruckPosition+new Vector3(-6,0.1f,-6)};
            game.Hurt(97,200);
            RemakeLoot card=null;foreach(RemakeLoot l in game.Loot)if(l.Kind==RemakeLoot.CredentialKind&&l.Owner==97)card=l;
            Assert(card!=null && card.gameObject.activeSelf,"fallen student drops an ID card");
            yield return new WaitForSeconds(.3f);
            card.Body.position=game.TruckPosition+new Vector3(0,1.0f,-1.5f);card.Body.linearVelocity=Vector3.zero;
            yield return new WaitForSeconds(1.2f);
            Assert(game.Students[97].alive && !card.gameObject.activeSelf,"ID card in the truck revives the student");
            game.Students.Remove(97);
            // Full bounds are required: an object straddling the cargo boundary must not count.
            var first=game.Loot[0];first.Body.position=game.TruckPosition+new Vector3(1.3f,1.5f,-1);
            first.Body.linearVelocity=Vector3.zero;Physics.SyncTransforms();
            Assert(!game.CargoContains(first.Bounds),"partial cargo is rejected");
            for(int i=0;i<3;i++)
            {
                var item=game.Loot[i];item.ResetLoot();item.Body.position=game.TruckPosition+new Vector3((i-1)*.75f,.9f+item.Size.y*.5f,-1);
                item.Body.rotation=Quaternion.identity;item.Body.linearVelocity=Vector3.zero;item.Body.angularVelocity=Vector3.zero;
                item.Value=item.BaseValue;
            }
            game.Player.Teleport(game.TruckPosition+new Vector3(0,.9f,-2.8f));
            yield return new WaitForSeconds(2);
            Assert(game.Cargo>=game.Quota,"settled cargo pays quota");
            game.Request("extract");yield return new WaitForSeconds(5.8f);
            Assert(game.Phase==3,"truck completes successful extraction");
            Assert(game.Local.escaped,"surviving student escapes");
            Assert(game.Students[99].strength==0 && game.Students[99].credits==0,"fallen student loses upgrades");
            until=Time.realtimeSinceStartup+20;
            while(!game.ShopReady && Time.realtimeSinceStartup<until)yield return null;
            Assert(game.ShopReady && UnityEngine.SceneManagement.SceneManager.GetSceneByName("remake_shop").isLoaded,"separate physical shop scene loads");
            Assert(Vector3.Distance(game.Player.transform.position,RemakeShop.Spawn(0))<2,"student arrives in the shop");
            game.Request("upgrade",0);Assert(game.Local.strength==0,"shop rejects purchase away from the product");
            game.Player.Teleport(RemakeShop.Product(0)+new Vector3(0,-1.05f,-1));yield return new WaitForSeconds(.2f);
            game.Request("upgrade",0);Assert(game.Local.strength==1,"survivor physically purchases grip upgrade");
            game.Request("upgrade",0);Assert(game.Local.strength==1,"insufficient funds reject another purchase");
            game.Request("next");Assert(game.Phase==3,"next turn requires the shop exit");
            game.Player.Aim(0,0);RemakeCapture.Save(game.Player.View,Path.Combine(Application.dataPath,"..","shop-smoke.png"));
            if(game.Online) {
                until=Time.realtimeSinceStartup+20;
                while(!game.Students.Values.Any(v=>v.id>0 && v.id<5 && v.stamina==1) && Time.realtimeSinceStartup<until)yield return null;
                Assert(game.Students.Values.Any(v=>v.id>0 && v.id<5 && v.stamina==1),"host validates peer purchase in the physical shop");
            }
            var oldSpawns=game.Loot.Where(l=>l.BaseValue>0).Select(l=>l.Spawn).ToArray();
            string oldCondition=game.DayCondition;
            game.Player.Teleport(RemakeShop.Exit);yield return new WaitForSeconds(.2f);
            game.Request("next");yield return new WaitForSeconds(.3f);
            Assert(game.Day==2 && game.Phase==1,"next day starts");
            Assert(game.Students[99].alive && game.Students[99].health==100,"fallen student revives next day");
            Assert(game.Local.strength==1,"survivor keeps upgrade");
            Assert(game.DayCondition!=oldCondition && game.Loot.Where(l=>l.BaseValue>0).Select(l=>l.Spawn).Where((v,i)=>Vector3.Distance(v,oldSpawns[i])>1).Count()>5,"day changes loot placement and campus conditions");
            yield return new WaitForSeconds(.5f);
            Assert(!UnityEngine.SceneManagement.SceneManager.GetSceneByName("remake_shop").isLoaded,"shop unloads when returning to campus");
            game.Students.Remove(99);
            string output=Path.Combine(Application.dataPath,"..","remake-smoke.png");
            RemakeCapture.Save(game.Player.View,output);
            if(!game.Online) {
                game.Hurt(game.LocalId,1000);yield return new WaitForSeconds(.3f);
                Assert(game.Phase==4,"all students fallen ends the expedition");
                game.Request("next");yield return new WaitForSeconds(.3f);
                Assert(game.Day==1 && game.Phase==1 && game.Local.alive && game.Loot[7].Value==game.Loot[7].BaseValue,"retry restores the first-day loot and students");
            }
            Assert(!hadError,"no runtime errors during complete loop");
            Debug.Log("[RemakeSmoke] ALL COMPLETE");yield return new WaitForSeconds(2);Application.Quit(0);
        }

    }
}
