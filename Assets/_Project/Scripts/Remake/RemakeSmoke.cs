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
            Assert(game.Loot.Count==18,"university loot, cart and ID cards spawn");
            Assert(game.Player.transform.position.y<1,"student stands on campus floor");
            Assert(game.Player.View!=null && game.Player.View.enabled,"first person camera");
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
                Debug.Log("[RemakeSmoke] CLIENT COMPLETE");yield return new WaitForSeconds(4);Application.Quit(0);yield break;
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
            // Cart: cargo inside is tallied, and holding the handle steers it towards the student.
            var cart=game.Loot[10];var laptop=game.Loot[0];
            laptop.Body.position=cart.transform.TransformPoint(0,.45f,0);laptop.Body.linearVelocity=Vector3.zero;
            yield return new WaitForSeconds(1.2f);
            Assert(cart.CartHaul>=laptop.Value && laptop.Value>0,"cart tallies cargo inside");
            game.Player.Teleport(cart.transform.position+new Vector3(0,0,-2.1f),20);yield return new WaitForSeconds(.3f);
            game.Hud.GrabHeld=true;game.Request("grab",10);yield return new WaitForSeconds(.2f);
            Assert(game.Local.held==10,"student grabs cart handle");
            Vector3 cartStart=cart.transform.position;
            game.Player.Teleport(cartStart+new Vector3(0,0,-4.2f),20);yield return new WaitForSeconds(1.5f);
            Assert(game.Local.held==10 && cartStart.z-cart.transform.position.z>.8f,"cart follows the student");
            game.Hud.GrabHeld=false;yield return new WaitForSeconds(.3f);
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
            foreach(var room in game.Dressing.RoomCentres)
            {
                game.FindPath(game.Dressing.Corridor,room.Value,leg);
                if(leg.Count==0||Vector3.Distance(leg[leg.Count-1],room.Value)>3.5f)closed+=room.Key+" ";
            }
            Assert(closed=="","every dressed room is reachable from the corridor "+closed);
            Vector3 open=new Vector3(6,0.1f,-3.5f);
            game.Player.Teleport(open);game.Player.Aim(90,0);
            giant.DebugPlace(open+new Vector3(-15,0,1));giant.Wake();game.Player.Aim(-90,0);
            float startGap=Vector3.Distance(giant.transform.position,game.Player.transform.position);
            yield return new WaitForSeconds(3f);
            float gap=Vector3.Distance(giant.transform.position,game.Player.transform.position);
            Assert(giant.Chasing && startGap-gap>4,"giant chases a visible student ("+giant.State+", gap "+startGap.ToString("F1")+" -> "+gap.ToString("F1")+")");
            giant.Rest(600);giant.DebugPlace(game.GiantRoute.Count>11?game.GiantRoute[11]:open+new Vector3(40,0,0));
            game.Local.health=100;game.Local.alive=true;yield return new WaitForSeconds(1.5f);
            if(game.Player.Tumbling)game.Player.GetUp();
            Vector3 door=game.Dressing.EastDoor;Vector3 outward=Vector3.ProjectOnPlane(door-game.Dressing.Corridor,Vector3.up).normalized;
            Vector3 outside=door+outward*3,inside=door-outward*2.5f;
            game.Player.Teleport(outside+Vector3.up*.05f);yield return new WaitForSeconds(2f);
            Vector3 eye=Vector3.up*1.1f;
            Assert(!Physics.Raycast(outside+eye,(inside-outside).normalized,Vector3.Distance(outside,inside),~(1<<9),QueryTriggerInteraction.Ignore),"east glass door opens and is passable");
            game.FindPath(outside,game.Dressing.Corridor,leg);
            Assert(leg.Count>0 && Vector3.Distance(leg[leg.Count-1],game.Dressing.Corridor)<2.5f,"navigation reaches the corridor through the glass door");
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
            game.Request("upgrade");Assert(game.Local.strength==1,"survivor can buy grip upgrade");
            game.Request("next");yield return new WaitForSeconds(.3f);
            Assert(game.Day==2 && game.Phase==1,"next day starts");
            Assert(game.Students[99].alive && game.Students[99].health==100,"fallen student revives next day");
            Assert(game.Local.strength==1,"survivor keeps upgrade");
            game.Students.Remove(99);
            string output=Path.Combine(Application.dataPath,"..","remake-smoke.png");
            RemakeCapture.Save(game.Player.View,output);
            Assert(!hadError,"no runtime errors during complete loop");
            Debug.Log("[RemakeSmoke] ALL COMPLETE");yield return new WaitForSeconds(2);Application.Quit(0);
        }
    }
}
