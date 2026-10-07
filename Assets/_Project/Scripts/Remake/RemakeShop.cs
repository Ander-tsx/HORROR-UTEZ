using UnityEngine;

namespace HorrorUtez.Remake
{
    // Authored as a separate additive scene. Session, voice and students remain in the campus scene.
    public sealed class RemakeShop : MonoBehaviour
    {
        public static readonly Vector3 Origin = new Vector3(200,0,0);
        public static Vector3 Spawn(int id) => Origin + new Vector3(-2+(id%3)*1.2f,.08f,-4+(id%2));
        public static Vector3 Product(int kind) => Origin + new Vector3(-5+kind*2,1.05f,1);
        public static readonly Vector3 Exit = Origin + new Vector3(0,0,-5.7f);
        public static bool NearProduct(Vector3 p,int kind) => Vector3.Distance(p,Product(kind))<2.3f;
        public static bool NearExit(Vector3 p) => Vector3.Distance(p,Exit)<2.5f;
        public static int AimedProduct(Camera view)
        {
            if(Physics.Raycast(view.transform.position,view.transform.forward,out var hit,2.6f)) {
                var product=hit.collider.GetComponent<RemakeShopProduct>();if(product!=null)return product.Kind;
            }
            return -1;
        }
        private void Awake()
        {
            transform.position=Origin;
            var game=FindAnyObjectByType<RemakeGame>();
            Material metal=game!=null?game.PropMaterial:null, signal=game!=null?game.SignalMaterial:null;
            GameObject Part(string name,Vector3 at,Vector3 size,Material material) {
                var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(transform,false);
                go.transform.localPosition=at;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;return go;
            }
            Part("Suelo cooperativa",new Vector3(0,-.15f,0),new Vector3(15,.3f,14),metal);
            Part("Pared trasera",new Vector3(0,2.2f,6.8f),new Vector3(15,4.4f,.3f),metal);
            for(int sign=-1;sign<=1;sign+=2)Part("Pared lateral",new Vector3(sign*7.4f,2.2f,0),new Vector3(.3f,4.4f,14),metal);
            Part("Techo",new Vector3(0,4.5f,0),new Vector3(15,.2f,14),metal);
            Part("Mostrador",new Vector3(0,.45f,1),new Vector3(13,.9f,1.1f),metal);
            Text("COOPERATIVA UTEZ / ENTRE TURNOS",new Vector3(0,3.2f,6.5f),.07f);
            Text("E: comprar mirando un producto\nHost: E en la salida para volver al campus",new Vector3(0,2.2f,4),.033f);
            for(int i=0;i<6;i++) {
                Vector3 p=Product(i)-Origin;
                var item=Part(RemakeGame.UpgradeNames[i],p,new Vector3(.42f,.32f,.42f),signal);
                item.AddComponent<RemakeShopProduct>().Kind=i;
                item.GetComponent<Renderer>().enabled=false;
                void Icon(string name,Vector3 offset,Vector3 size,Material mat,PrimitiveType shape=PrimitiveType.Cube) {
                    var icon=GameObject.CreatePrimitive(shape);Destroy(icon.GetComponent<Collider>());icon.name=name;
                    icon.transform.SetParent(item.transform,false);icon.transform.localPosition=Vector3.zero;
                    icon.transform.SetParent(transform,true);icon.transform.localPosition=p+offset;icon.transform.localScale=size;icon.GetComponent<Renderer>().sharedMaterial=mat;
                }
                if(i==0) {
                    Icon("Mancuerna",Vector3.zero,new Vector3(.35f,.055f,.055f),metal);
                    for(int sign=-1;sign<=1;sign+=2)Icon("Disco",new Vector3(sign*.14f,0,0),new Vector3(.075f,.22f,.22f),metal,PrimitiveType.Sphere);
                } else if(i==1) {
                    for(int sign=-1;sign<=1;sign+=2) {Icon("Batería",new Vector3(sign*.08f,0,0),new Vector3(.1f,.14f,.1f),metal,PrimitiveType.Cylinder);Icon("Contacto",new Vector3(sign*.08f,.15f,0),new Vector3(.045f,.025f,.045f),signal);}
                } else if(i==2) {
                    Icon("Extensor",Vector3.zero,new Vector3(.36f,.075f,.08f),metal);
                    for(int sign=-1;sign<=1;sign+=2)Icon("Muñequera",new Vector3(sign*.14f,0,0),new Vector3(.06f,.13f,.14f),signal);
                } else if(i==3) {
                    for(int sign=-1;sign<=1;sign+=2) {Icon("Tenis",new Vector3(sign*.09f,-.04f,0),new Vector3(.12f,.12f,.32f),metal);Icon("Suela",new Vector3(sign*.09f,-.11f,0),new Vector3(.14f,.035f,.34f),signal);}
                } else if(i==4) {
                    Icon("Botiquín",Vector3.zero,new Vector3(.3f,.25f,.16f),metal);
                    Icon("Cruz horizontal",new Vector3(0,0,-.085f),new Vector3(.17f,.04f,.012f),signal);
                    Icon("Cruz vertical",new Vector3(0,0,-.085f),new Vector3(.04f,.17f,.012f),signal);
                } else {
                    for(int sign=-1;sign<=1;sign+=2)for(int ring=0;ring<5;ring++)Icon("Resorte",new Vector3(sign*.08f,-.1f+ring*.045f,0),new Vector3(.1f,.008f,.1f),metal,PrimitiveType.Cylinder);
                }
                Part("Base exposición",p+Vector3.down*.22f,new Vector3(.65f,.1f,.65f),metal);
                Text(RemakeGame.UpgradeNames[i]+" / $"+RemakeGame.UpgradeCost(new StudentState(),i)+"\n"+RemakeGame.UpgradeInfo[i],p+new Vector3(0,.55f,0),.012f);
            }
            Part("Terminal siguiente turno",Exit-Origin+Vector3.up*.9f,new Vector3(.45f,1.8f,.25f),signal);
            Text("VOLVER A UTEZ / E",Exit-Origin+new Vector3(0,2.1f,0),.04f);
            for(int i=-1;i<=1;i++) {
                var lamp=new GameObject("Fluorescente cooperativa");lamp.transform.SetParent(transform,false);lamp.transform.localPosition=new Vector3(i*4,3.7f,0);
                var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.range=11;light.intensity=8;light.color=new Color(.75f,.88f,1);
                Part("Luminaria",new Vector3(i*4,4.2f,0),new Vector3(2,.08f,.18f),signal);
            }
            Part("Banco",new Vector3(-5,.4f,-3),new Vector3(2,.8f,.65f),metal);
            Part("Estantería almacén",new Vector3(5,1.3f,5.7f),new Vector3(2,2.6f,.65f),metal);
            for(int i=0;i<4;i++)Part("Caja de suministros",new Vector3(-4+i*1.2f,.5f,5.5f),new Vector3(.8f,1,.8f),metal);
            for(int i=0;i<7;i++)Part("Línea de espera",new Vector3(-6+i*2,.015f,-1),new Vector3(.8f,.012f,.055f),signal);
        }
        private void Text(string value,Vector3 at,float scale) {
            var go=new GameObject(value);go.transform.SetParent(transform,false);go.transform.localPosition=at;go.transform.localRotation=Quaternion.Euler(0,180,0);
            // TextMesh faces -Z by default, toward students approaching from the entrance.
            go.transform.localRotation=Quaternion.identity;
            var text=go.AddComponent<TextMesh>();text.text=value;text.fontSize=48;text.characterSize=scale;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;
            text.color=new Color(.8f,1,.85f);
        }
    }
    public sealed class RemakeShopProduct : MonoBehaviour { public int Kind; }
}
