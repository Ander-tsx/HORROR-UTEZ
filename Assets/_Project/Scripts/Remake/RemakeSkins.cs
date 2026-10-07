using UnityEngine;
using HorrorUtez.Rendering;

namespace HorrorUtez.Remake
{
    public static class RemakeSkins
    {
        public static readonly string[] Names={"Ander","Erick","Cesar","Juan","Sebas"};
        public static readonly string[] Descriptions={"Lentes / uniforme terracota","Cara original / complexión delgada","Complexión ancha / barba / uniforme verde","Rostro estrecho / uniforme azul","Complexión media / uniforme mostaza"};
        private static readonly Color[] Shirts={new Color(.62f,.23f,.13f),new Color(.8f,.8f,.73f),new Color(.2f,.37f,.27f),new Color(.24f,.35f,.62f),new Color(.62f,.48f,.18f)};
        private static readonly Color[] Skin={new Color(.66f,.45f,.34f),new Color(.72f,.51f,.4f),new Color(.58f,.36f,.25f),new Color(.77f,.57f,.45f),new Color(.68f,.47f,.34f)};
        public static int Clamp(int id)=>Mathf.Clamp(id,0,Names.Length-1);
        public static Color SkinColor(int id)=>Skin[Clamp(id)];
        public static Color ShirtColor(int id)=>Shirts[Clamp(id)];
        public static void Tint(Renderer renderer,Color color) {
            var p=new MaterialPropertyBlock();renderer.GetPropertyBlock(p);
            p.SetColor(PsxShaderProperties.BaseColor,color);p.SetColor("_BaseColor",color);p.SetColor("_Color",color);renderer.SetPropertyBlock(p);
        }
        public static void Apply(GameObject avatar,int id) {
            id=Clamp(id);
            foreach(var renderer in avatar.GetComponentsInChildren<SkinnedMeshRenderer>()) {
                for(int i=0;i<renderer.sharedMesh.blendShapeCount;i++) {
                    string key=renderer.sharedMesh.GetBlendShapeName(i);
                    renderer.SetBlendShapeWeight(i,key==Names[id]||key.EndsWith("."+Names[id])?100:0);
                }
                var materials=renderer.materials;
                foreach(var material in materials) {
                    string name=material.name;
                    if(name.Contains("Head")) {
                        var texture=Resources.Load<Texture2D>("Skins/Skin_"+Names[id]);
                        if(texture!=null){texture.filterMode=FilterMode.Point;texture.wrapMode=TextureWrapMode.Repeat;material.SetTexture(PsxShaderProperties.MainTex,texture);}
                        SetTint(material,Color.white);
                    }
                    else if(name.Contains("Skin"))SetTint(material,Skin[id]);
                    else if(name.Contains("ShirtWhite"))SetTint(material,Shirts[id]);
                    else if(name.Contains("ShirtGrey"))SetTint(material,Color.Lerp(Shirts[id],Color.white,.25f));
                    else if(name.Contains("ShirtNavy"))SetTint(material,Shirts[id]*.5f);
                }
            }
        }
        private static void SetTint(Material m,Color color) {
            if(m.HasProperty(PsxShaderProperties.BaseColor))m.SetColor(PsxShaderProperties.BaseColor,color);
            if(m.HasProperty("_BaseColor"))m.SetColor("_BaseColor",color);
            if(m.HasProperty("_Color"))m.SetColor("_Color",color);
        }
    }
}
