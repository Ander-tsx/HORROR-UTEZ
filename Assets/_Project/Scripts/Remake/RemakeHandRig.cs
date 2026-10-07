using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HorrorUtez.Remake
{
    // Original CC0 anatomical mesh, skin weights and deform rig by SparrowHawk.
    public sealed class RemakeHandRig
    {
        [Serializable] private sealed class Coordinates { public float[] v; }
        [Serializable] private sealed class Weight { public int[] indices; public float[] values; }
        [Serializable] private sealed class Bone { public string name; public int parent; public float[] head,tail; }
        [Serializable] private sealed class Model {
            public Coordinates[] vertices,normals,uv; public int[] triangles;
            public Weight[] weights; public Bone[] bones;
        }
        public Transform Root { get; }
        private readonly Transform[] bones;
        private readonly Vector3[] heads,tails;
        private readonly Dictionary<string,int> names=new Dictionary<string,int>();
        private readonly int sign;
        private float grip;
        private readonly float[,] curls=new float[5,3];
        private readonly Mesh deformMesh;
        private readonly SkinnedMeshRenderer skinRenderer;
        private readonly Vector3[] restVertices,restNormals,deformedVertices,deformedNormals;
        private readonly Quaternion[] real,dual;
        private static Weight[] softWeights;
        private bool posed;
        private static Model source;
        private static Material skin;
        private static readonly string[][] fingers={
            new[]{"Bone","Bone.001","Bone.002"},new[]{"Bone.003","Bone.004","Bone.005"},
            new[]{"Bone.006","Bone.007","Bone.008"},new[]{"Bone.009","Bone.010","Bone.011"},
            new[]{"Bone.017","Bone.018","Bone.019"}
        };
        public RemakeHandRig(Transform parent,int side,Material material)
        {
            sign=side==0?-1:1;
            if(source==null) {
                var asset=Resources.Load<TextAsset>("AnatomicalHand");
                if(asset==null)throw new InvalidOperationException("Missing CC0 AnatomicalHand mesh.");
                source=JsonUtility.FromJson<Model>(asset.text);softWeights=SmoothWeights(source);
            }
            if(skin==null) {
                skin=new Material(material){name="Piel / mano anatómica CC0"};
                if(skin.HasProperty("_BaseColor"))skin.SetColor("_BaseColor",new Color(.68f,.49f,.40f));
                if(skin.HasProperty("_Color"))skin.SetColor("_Color",new Color(.68f,.49f,.40f));
            }
            Root=new GameObject("Mano articulada / dedos y contacto").transform;Root.SetParent(parent,false);
            bones=new Transform[source.bones.Length];heads=new Vector3[bones.Length];tails=new Vector3[bones.Length];
            for(int i=0;i<bones.Length;i++) {
                var b=source.bones[i];names[b.name]=i;heads[i]=Point(b.head);tails[i]=Point(b.tail);
                bones[i]=new GameObject(b.name).transform;bones[i].gameObject.layer=8;
                bones[i].SetParent(b.parent>=0?bones[b.parent]:Root,false);
                bones[i].localPosition=heads[i]-(b.parent>=0?heads[b.parent]:Vector3.zero);
            }
            var mesh=new Mesh{name="SparrowHawk anatomical hand / CC0"};
            var vertices=new Vector3[source.vertices.Length];var normals=new Vector3[vertices.Length];var uv=new Vector2[vertices.Length];
            var weights=new BoneWeight[vertices.Length];
            for(int i=0;i<vertices.Length;i++) {
                vertices[i]=Point(source.vertices[i].v);normals[i]=Point(source.normals[i].v);
                uv[i]=new Vector2(source.uv[i].v[0],source.uv[i].v[1]);var w=source.weights[i];
                // The CPU soft skinning below preserves volume. GPU bone zero is a neutral carrier.
                weights[i]=new BoneWeight{boneIndex0=0,weight0=1};
            }
            int[] triangles=(int[])source.triangles.Clone();
            if(sign>0)for(int i=0;i<triangles.Length;i+=3){int a=triangles[i];triangles[i]=triangles[i+2];triangles[i+2]=a;}
            var binds=new Matrix4x4[bones.Length];for(int i=0;i<bones.Length;i++)binds[i]=Matrix4x4.Translate(-heads[i]);
            mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.triangles=triangles;mesh.boneWeights=weights;mesh.bindposes=binds;mesh.RecalculateBounds();
            var go=new GameObject("SparrowHawk / piel continua / 8716 triángulos"){layer=8};go.transform.SetParent(Root,false);
            var renderer=go.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=mesh;renderer.sharedMaterial=skin;
            renderer.bones=bones;renderer.rootBone=bones[0];renderer.quality=SkinQuality.Bone4;
            renderer.updateWhenOffscreen=true;renderer.shadowCastingMode=ShadowCastingMode.Off;
            renderer.localBounds=new Bounds(Vector3.zero,Vector3.one*.8f);
            deformMesh=mesh;mesh.MarkDynamic();skinRenderer=renderer;
            restVertices=vertices;restNormals=normals;deformedVertices=new Vector3[vertices.Length];deformedNormals=new Vector3[vertices.Length];
            real=new Quaternion[bones.Length];dual=new Quaternion[bones.Length];
        }
        public void SetAppearance(Color color)=>RemakeSkins.Tint(skinRenderer,color);
        private Vector3 Point(float[] v)=>new Vector3(v[0]*(sign<0?1:-1),v[1],v[2]);
        public Vector3 Pose(Vector3 idle,Quaternion idleRotation,RemakeLoot item,Vector3 anchor,Vector3 right,Vector3 aim,float effort)
        {
            grip=Mathf.MoveTowards(grip,item!=null?1:0,Time.deltaTime*7);
            Vector3 palm=idle;Quaternion rotation=idleRotation;
            Collider collider=item!=null?item.GetComponent<Collider>():null;
            if(collider!=null) {
                float spread=Mathf.Clamp(item.Size.magnitude*.13f,.065f,.18f);
                Vector3 desired=Vector3.Lerp(anchor,collider.bounds.center,.65f)+right*(sign*spread);
                Vector3 origin=desired+right*(sign*(collider.bounds.extents.magnitude+.35f))-aim*.08f;
                Vector3 direction=(desired-origin).normalized;Vector3 normal;
                if(collider.Raycast(new Ray(origin,direction),out var hit,1.5f)){palm=hit.point;normal=hit.normal;}
                else {palm=collider.ClosestPoint(origin);normal=(origin-palm).normalized;}
                palm+=normal*.018f;
                Vector3 fingersDirection=Vector3.ProjectOnPlane(Vector3.down,normal);
                if(fingersDirection.sqrMagnitude<.05f)fingersDirection=Vector3.ProjectOnPlane(aim,normal);
                rotation=Quaternion.LookRotation(fingersDirection.normalized,normal);
                palm+=right*(Mathf.Sin(Time.time*17+sign)*.0015f*effort);
            }
            float follow=1-Mathf.Exp(-Time.deltaTime*22);
            Root.SetPositionAndRotation(posed?Vector3.Lerp(Root.position,palm,follow):palm,posed?Quaternion.Slerp(Root.rotation,rotation,follow):rotation);posed=true;
            foreach(var bone in bones)bone.localRotation=Quaternion.identity;
            for(int finger=0;finger<5;finger++) {
                for(int joint=0;joint<3;joint++) {
                    int index=names[fingers[finger][joint]];
                    Vector3 segment=tails[index]-heads[index];
                    float curl=(joint==0?12:20)+(finger==4?10:0);
                    if(collider!=null) {
                        float max=(joint==0?36:46)*grip;
                        float accepted=curl;
                        // Stop each joint before its skin enters the object's collision surface.
                        for(float angle=curl;angle<=max;angle+=4) {
                            bones[index].localRotation=Quaternion.AngleAxis(angle,Vector3.right);
                            Vector3 tip=bones[index].TransformPoint(segment);
                            if(Vector3.Distance(tip,collider.ClosestPoint(tip))<.007f)break;
                            accepted=angle;
                        }
                        curl=accepted;
                    }
                    curls[finger,joint]=Mathf.Lerp(curls[finger,joint],curl,1-Mathf.Exp(-Time.deltaTime*12));
                    bones[index].localRotation=Quaternion.AngleAxis(curls[finger,joint],Vector3.right);
                }
            }
            DeformSoftSkin(effort);
            // The arm joins the imported forearm rather than running through the palm.
            return Root.TransformPoint(new Vector3(0,0,-.13f));
        }

        private static Weight[] SmoothWeights(Model model) {
            var adjacent=new HashSet<int>[model.vertices.Length];var values=new float[model.vertices.Length,model.bones.Length];
            for(int i=0;i<adjacent.Length;i++){adjacent[i]=new HashSet<int>();for(int k=0;k<4;k++)values[i,model.weights[i].indices[k]]+=model.weights[i].values[k];}
            for(int i=0;i<model.triangles.Length;i+=3)for(int k=0;k<3;k++){
                int a=model.triangles[i+k],b=model.triangles[i+(k+1)%3];adjacent[a].Add(b);adjacent[b].Add(a);
            }
            // Spread influence along the authored mesh rather than making each phalanx a rigid block.
            for(int pass=0;pass<5;pass++) {
                var next=(float[,])values.Clone();
                for(int i=0;i<adjacent.Length;i++)if(adjacent[i].Count>0)for(int b=0;b<model.bones.Length;b++){
                    float sum=0;foreach(int n in adjacent[i])sum+=values[n,b];next[i,b]=Mathf.Lerp(values[i,b],sum/adjacent[i].Count,.32f);
                }
                values=next;
            }
            var result=new Weight[adjacent.Length];
            for(int i=0;i<result.Length;i++) {
                var indices=new int[4];var weights=new float[4];
                for(int b=0;b<model.bones.Length;b++)for(int k=0;k<4;k++)if(values[i,b]>weights[k]){
                    for(int j=3;j>k;j--){weights[j]=weights[j-1];indices[j]=indices[j-1];}weights[k]=values[i,b];indices[k]=b;break;
                }
                float sum=weights[0]+weights[1]+weights[2]+weights[3];for(int k=0;k<4;k++)weights[k]/=Mathf.Max(sum,.00001f);
                result[i]=new Weight{indices=indices,values=weights};
            }
            return result;
        }
        private static Quaternion Scale(Quaternion q,float w)=>new Quaternion(q.x*w,q.y*w,q.z*w,q.w*w);
        private static Quaternion Add(Quaternion a,Quaternion b)=>new Quaternion(a.x+b.x,a.y+b.y,a.z+b.z,a.w+b.w);
        private void DeformSoftSkin(float effort) {
            for(int b=0;b<bones.Length;b++) {
                Matrix4x4 skin=Root.worldToLocalMatrix*bones[b].localToWorldMatrix*Matrix4x4.Translate(-heads[b]);
                real[b]=skin.rotation;Vector3 t=skin.GetColumn(3);dual[b]=Scale(new Quaternion(t.x,t.y,t.z,0)*real[b],.5f);
            }
            for(int i=0;i<restVertices.Length;i++) {
                Weight w=softWeights[i];Quaternion reference=real[w.indices[0]],r=new Quaternion(0,0,0,0),d=r;
                for(int k=0;k<4;k++) {
                    int b=w.indices[k];float weight=w.values[k]*(Quaternion.Dot(reference,real[b])<0?-1:1);
                    r=Add(r,Scale(real[b],weight));d=Add(d,Scale(dual[b],weight));
                }
                float length=Mathf.Sqrt(Quaternion.Dot(r,r));r=Scale(r,1/Mathf.Max(length,.00001f));d=Scale(d,1/Mathf.Max(length,.00001f));
                d=Add(d,Scale(r,-Quaternion.Dot(r,d)));Quaternion offset=d*Quaternion.Inverse(r);
                Vector3 p=r*restVertices[i]+new Vector3(offset.x,offset.y,offset.z)*2;
                // Small, reversible fleshy compression with a slow return, retaining overall volume.
                float pressure=grip*Mathf.Clamp01(effort)*.025f*Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.03f,.08f,restVertices[i].z));
                p.y*=1-pressure;p.x*=1+pressure*.5f;
                deformedVertices[i]=p;deformedNormals[i]=r*restNormals[i];
            }
            deformMesh.vertices=deformedVertices;deformMesh.normals=deformedNormals;
        }
    }
}
