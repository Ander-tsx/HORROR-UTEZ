using UnityEngine;

namespace HorrorUtez.Remake
{
    // IDs 13..17 remain credentials for network/revive compatibility. New valuables start at 18.
    public static class RemakeLootCatalog
    {
        public static readonly string[] Names = { "Cámara de investigación", "Trofeo UTEZ", "Maletín de reactivos", "Motor de prácticas", "Servidor de respaldo", "Centrífuga", "Caja de libros antiguos", "Brazo de robótica" };
        public static readonly int[] Values = { 650, 480, 900, 1250, 1450, 980, 390, 1150 };
        public static readonly float[] Masses = { 8, 11, 16, 31, 38, 25, 21, 29 };
        public static readonly Vector3[] Sizes = { new Vector3(.35f,.3f,.43f), new Vector3(.33f,.65f,.3f), new Vector3(.6f,.35f,.4f), new Vector3(.62f,.45f,.48f), new Vector3(.42f,.73f,.62f), new Vector3(.62f,.42f,.56f), new Vector3(.55f,.38f,.45f), new Vector3(.42f,.82f,.46f) };
        public static readonly string[] Traits = { "Óptica frágil", "Metal resistente", "Cristal muy frágil", "Muy pesado / cooperativo", "Muy pesado / cooperativo", "Equipo delicado", "Resistente / voluminoso", "Pesado / cooperativo" };

        public static void Model(Transform root, int variant, Material metal, Material detail, Material signal)
        {
            Vector3 size=Sizes[variant];
            void Part(string name,Vector3 p,Vector3 s,Material mat,PrimitiveType shape=PrimitiveType.Cube) {
                var go=GameObject.CreatePrimitive(shape); Object.Destroy(go.GetComponent<Collider>());
                go.name=name;go.transform.SetParent(root,false);go.transform.localPosition=p;go.transform.localScale=s;
                go.GetComponent<Renderer>().sharedMaterial=mat;go.layer=10;
            }
            if(variant==1) {
                Part("Peana",new Vector3(0,-.27f,0),new Vector3(.3f,.1f,.28f),detail);
                Part("Columna",new Vector3(0,-.08f,0),new Vector3(.07f,.3f,.07f),metal);
                Part("Copa",new Vector3(0,.17f,0),new Vector3(.25f,.28f,.25f),signal,PrimitiveType.Sphere);
                for(int sign=-1;sign<=1;sign+=2)Part("Asa",new Vector3(sign*.135f,.12f,0),new Vector3(.07f,.2f,.08f),metal);
            } else if(variant==7) {
                Part("Base robótica",new Vector3(0,-.34f,0),new Vector3(.4f,.12f,.42f),metal);
                Part("Articulación",new Vector3(0,-.2f,0),new Vector3(.2f,.2f,.2f),signal,PrimitiveType.Sphere);
                Part("Brazo",new Vector3(.04f,.04f,0),new Vector3(.1f,.43f,.12f),metal);
                Part("Codo",new Vector3(.04f,.25f,0),new Vector3(.2f,.15f,.2f),detail,PrimitiveType.Sphere);
                Part("Pinza",new Vector3(-.07f,.32f,0),new Vector3(.3f,.1f,.1f),metal);
            } else {
                Part("Carcasa",Vector3.zero,size*.85f,variant==6?detail:metal);
                if(variant==0) {
                    Part("Objetivo",new Vector3(0,0,size.z*.43f),new Vector3(.17f,.17f,.2f),detail,PrimitiveType.Sphere);
                    Part("Lente",new Vector3(0,0,size.z*.5f),new Vector3(.1f,.1f,.02f),signal);
                } else if(variant==2) {
                    for(int i=0;i<4;i++)Part("Frasco",new Vector3((i-1.5f)*.12f,.15f,0),new Vector3(.08f,.18f,.08f),signal,PrimitiveType.Cylinder);
                    Part("Asa",new Vector3(0,.21f,0),new Vector3(.22f,.025f,.035f),detail);
                } else {
                    for(int i=0;i<6;i++)Part("Ventilación / lomo",new Vector3(0,(i-2.5f)*size.y*.1f,-size.z*.43f),new Vector3(size.x*.7f,.012f,.014f),detail);
                    Part("Placa de inventario",new Vector3(size.x*.25f,size.y*.2f,-size.z*.44f),new Vector3(.09f,.05f,.015f),signal);
                    if(variant==3)Part("Eje",new Vector3(size.x*.5f,0,0),new Vector3(.2f,.1f,.1f),detail);
                    if(variant==5)Part("Tapa",new Vector3(0,size.y*.43f,0),new Vector3(.43f,.03f,.4f),detail,PrimitiveType.Sphere);
                }
                for(int sign=-1;sign<=1;sign+=2)Part("Pie",new Vector3(sign*size.x*.3f,-size.y*.46f,0),new Vector3(.07f,.04f,size.z*.65f),detail);
            }
        }
    }
}
