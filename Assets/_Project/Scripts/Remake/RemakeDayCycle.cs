using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HorrorUtez.Remake
{
    public sealed partial class RemakeGame
    {
        public int SessionSeed { get; private set; }
        public bool ShopReady { get; private set; }
        public string DayCondition { get; private set; }
        public int LayoutDay { get; private set; }
        private int observedPhase;
        private bool shopLoading;
        private Transform truckRoot;
        private readonly List<int> originalValues=new List<int>();
        private readonly List<Vector3> originalSpawns=new List<Vector3>();
        private readonly List<GameObject> dayLights=new List<GameObject>();
        private float originalFog;
        private Color originalAmbient;
        public bool CanMove => Phase==1 || Phase==2 || (Phase==3 && ShopReady);
        public void ShopInteract()
        {
            int kind=RemakeShop.AimedProduct(Player.View);
            if(kind>=0)Request("upgrade",kind);
            else if(RemakeShop.NearExit(Player.transform.position))Request("next");
        }
        private void BuildExtraLoot()
        {
            for(int i=0;i<RemakeLootCatalog.Names.Length;i++) {
                var go=new GameObject(RemakeLootCatalog.Names[i]){layer=10};go.transform.position=new Vector3(3+i*.7f,1,-10);
                RemakeLootCatalog.Model(go.transform,i,PropMaterial,SleeveMaterial,SignalMaterial);
                go.AddComponent<BoxCollider>().size=RemakeLootCatalog.Sizes[i];
                var loot=go.AddComponent<RemakeLoot>();loot.Setup(this,Loot.Count,10+i,RemakeLootCatalog.Names[i],RemakeLootCatalog.Values[i],RemakeLootCatalog.Masses[i],RemakeLootCatalog.Sizes[i]);
                loot.SetAuthority(false);Loot.Add(loot);
            }
            foreach(var item in Loot){originalValues.Add(item.BaseValue);originalSpawns.Add(item.Spawn);}
            originalFog=RenderSettings.fogDensity;originalAmbient=RenderSettings.ambientLight;
        }
        private void ApplyDayLayout()
        {
            if(LayoutDay==Day)return;
            LayoutDay=Day;
            var random=new System.Random(unchecked(SessionSeed+Day*7919));
            string[] conditions={"Niebla espesa", "Corte parcial de luz", "Turno de vigilancia", "Noche sin luna"};
            DayCondition=conditions[(Day-1)%conditions.Length];
            foreach(var weather in FindObjectsByType<HorrorUtez.World.WeatherSystem>(FindObjectsSortMode.None)) {
                weather.NightLightScale=(Day-1)%4==3?.22f:.5f;weather.FogScale=(Day-1)%4==0?1.35f:1;
            }
            RenderSettings.fogDensity=originalFog*((Day-1)%4==0?1.35f:1);
            foreach(var go in dayLights)if(go!=null)Destroy(go);dayLights.Clear();
            var candidates=new List<Vector3>();
            if(Map==0) {
                if(Dressing!=null)candidates.AddRange(Dressing.RoomCentres.Values);
            }else candidates.AddRange(GiantRoute);
            // Host and clients use the same ordered static candidates and seed; no random live physics probes.
            candidates=candidates.Where(p=>Vector3.Distance(p,TruckPosition)>7).OrderBy(p=>p.x).ThenBy(p=>p.z).ToList();
            for(int i=candidates.Count-1;i>0;i--){int j=random.Next(i+1);var v=candidates[i];candidates[i]=candidates[j];candidates[j]=v;}
            int slot=0;
            foreach(var item in Loot) {
                if(item.Kind==RemakeLoot.CredentialKind)continue;
                item.BaseValue=item.Id<3?originalValues[item.Id]:Mathf.RoundToInt(originalValues[item.Id]*(.85f+(float)random.NextDouble()*.3f));
                if(candidates.Count>0 && (item.Id>=18 || Day>1)) {
                    Vector3 p=candidates[slot%candidates.Count];int stack=slot/candidates.Count;slot++;
                    item.Spawn=p+Vector3.up*(item.Size.y*.5f+.08f+stack*.9f);
                } else item.Spawn=originalSpawns[item.Id];
                item.SpawnRotation=Quaternion.Euler(0,random.Next(0,4)*90,0);
                item.ResetLoot();
                // Every day has a different subset of the eight special valuables.
                if(item.Id>=18 && ((item.Id-18+Day)%4==0)){item.Value=0;item.gameObject.SetActive(false);}
            }
            if(Map==0) {
                foreach(var room in Dressing.RoomCentres) {
                    var lamp=new GameObject("Luz del turno / "+room.Key);lamp.transform.position=room.Value+Vector3.up*2.2f;
                    var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.range=8;light.intensity=(Day-1)%4==1?0:3;light.color=new Color(.28f,.42f,.55f);dayLights.Add(lamp);
                }
                foreach(var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if(light.name.Contains("Fluorescente") && !light.name.Contains("cooperativa"))light.enabled=(Day-1)%4!=1 || Mathf.Abs(light.transform.position.x)%3<1;
            }
            foreach(var enemy in Enemies) {
                bool enabled=enemy.Kind==EnemyKind.Giant || enemy.Kind==EnemyKind.Caretaker || (Day-1)%4==2 || (Day+SessionSeed+(int)enemy.Kind)%3!=0;
                enemy.gameObject.SetActive(enabled);
                if(candidates.Count>0)enemy.DaySpawn(candidates[random.Next(candidates.Count)]);
                else enemy.ResetEnemy();
                enemy.Rest(20+random.Next(5,30));
            }
            Physics.SyncTransforms();
        }
        private void TickCycle()
        {
            if(Phase!=observedPhase) {
                observedPhase=Phase;
                if(Phase==3 && !shopLoading)StartCoroutine(TravelToShop());
                if(Phase==1) {
                    ShopReady=false;
                    if(SceneManager.GetSceneByName("remake_shop").isLoaded)SceneManager.UnloadSceneAsync("remake_shop");
                    if(truckRoot!=null)truckRoot.position=TruckPosition;
                    RenderSettings.ambientLight=originalAmbient;RenderSettings.fogDensity=originalFog*((Day-1)%4==0?1.35f:1);
                    foreach(var weather in FindObjectsByType<HorrorUtez.World.WeatherSystem>(FindObjectsSortMode.None))weather.FogScale=(Day-1)%4==0?1.35f:1;
                }
            }
        }
        private IEnumerator TravelToShop()
        {
            shopLoading=true;ShopReady=false;
            Audio?.Play("truck_horn",TruckPosition,1);
            Audio?.Play("truck_engine",TruckPosition,1);
            // Cargo and boarded students travel with the truck; campus colliders remain where authored.
            var aboard=Students.Values.Where(s=>s.escaped).Select(s=>s.id).ToArray();
            var cargo=Loot.Where(l=>l.gameObject.activeSelf && CargoContains(l.Bounds)).ToArray();
            foreach(var item in cargo)item.SetAuthority(false);
            Vector3 start=TruckPosition, previous=Vector3.zero;
            float elapsed=0;
            while(elapsed<3 && Phase==3) {
                elapsed+=Time.deltaTime;Vector3 offset=Vector3.forward*(elapsed*elapsed*2);
                Vector3 delta=offset-previous;previous=offset;
                if(truckRoot!=null)truckRoot.position=start+offset;
                foreach(var item in cargo){item.transform.position+=delta;item.Body.position=item.transform.position;}
                if(Local!=null && Local.escaped)Player.Carry(delta);
                if(Authority)foreach(int id in aboard)if(Students.TryGetValue(id,out var s))s.position+=delta;
                yield return null;
            }
            if(Phase!=3){shopLoading=false;yield break;}
            var load=SceneManager.LoadSceneAsync("remake_shop",LoadSceneMode.Additive);yield return load;
            RenderSettings.fogDensity=.003f;RenderSettings.ambientLight=new Color(.15f,.18f,.22f);
            foreach(var weather in FindObjectsByType<HorrorUtez.World.WeatherSystem>(FindObjectsSortMode.None))weather.FogScale=.12f;
            if(Authority)foreach(var s in Students.Values){s.alive=true;s.health=s.MaxHealth;s.position=RemakeShop.Spawn(s.id);}
            if(Local!=null){Local.alive=true;Player.Teleport(RemakeShop.Spawn(LocalId));}
            ShopReady=true;shopLoading=false;Hud.ShowGame();
            Tell("Cooperativa: E sobre un producto para comprar. El anfitrión inicia el siguiente turno en la salida.");
        }
    }
}
