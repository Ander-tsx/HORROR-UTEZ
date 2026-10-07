"""Convert SparrowHawk's CC0 hand into a portable Unity skinned mesh.

Run with Blender -b <downloaded blend> --python this_script. No source scripts executed.
The JSON preserves mesh topology, UVs and interpolated author skin weights; Unity builds
the original deform rig directly, avoiding old Blender FBX axis/constraint ambiguity.
"""
import bpy, json, os
from mathutils import Vector

rig=bpy.data.objects['Hand_Left']
obj=bpy.data.objects['Hand.L']
rig.data.pose_position='REST'
bpy.context.view_layer.objects.active=obj
obj.hide_set(False);obj.select_set(True)
multi=next(m for m in obj.modifiers if m.type=='MULTIRES')
multi.levels=1
bpy.ops.object.modifier_apply(modifier=multi.name)
bones=[b for b in rig.data.bones if b.use_deform]
index={b.name:i for i,b in enumerate(bones)}
def convert(p):return [float(p.x+.055)*.24,float(p.z+.03)*.24,float(-p.y-.69)*.24]
transform=rig.matrix_world.inverted() @ obj.matrix_world
mesh=obj.data;mesh.calc_loop_triangles()
vertices=[];normals=[];uvs=[];weights=[];triangles=[]
# Split UV seams without changing the weighted shape.
lookup={}
for tri in mesh.loop_triangles:
    for li in tri.loops:
        loop=mesh.loops[li];v=mesh.vertices[loop.vertex_index]
        uv=mesh.uv_layers.active.data[li].uv[:] if mesh.uv_layers.active else (0,0)
        key=(v.index,tuple(uv))
        if key not in lookup:
            lookup[key]=len(vertices)
            vertices.append(convert(transform @ v.co))
            n=(transform.to_3x3().inverted().transposed() @ v.normal).normalized()
            normals.append([n.x,n.z,-n.y]);uvs.append(list(uv))
            vw=sorted([(index[obj.vertex_groups[g.group].name],g.weight) for g in v.groups if obj.vertex_groups[g.group].name in index],key=lambda w:-w[1])[:4]
            if not vw:vw=[(index['Bone.016'],1)]
            total=sum(w for _,w in vw)
            weights.append({'indices':[i for i,_ in vw]+[0]*(4-len(vw)),'values':[w/total for _,w in vw]+[0]*(4-len(vw))})
        triangles.append(lookup[key])
data={'author':'SparrowHawk','source':'https://blendswap.com/blend/22269','license':'CC0',
      'vertices':[{'v':v} for v in vertices],'normals':[{'v':v} for v in normals],'uv':[{'v':v} for v in uvs],'triangles':triangles,'weights':weights,
      'bones':[{'name':b.name,'parent':index.get(b.parent.name,-1) if b.parent else -1,'head':convert(b.head_local),'tail':convert(b.tail_local)} for b in bones]}
out='Assets/_Project/Art/Remake/Resources/AnatomicalHand.json'
os.makedirs(os.path.dirname(out),exist_ok=True)
with open(out,'w',encoding='utf-8') as f:json.dump(data,f,separators=(',',':'))
print('HAND_EXPORT',len(vertices),'vertices',len(triangles)//3,'triangles',len(bones),'bones')
