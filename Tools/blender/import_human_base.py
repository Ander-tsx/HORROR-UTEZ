"""Retarget abdoubouam's CC0 basemesh to the existing student avatar and enemy IK.

Run Blender with the existing PlayerCharacter.blend open. The authored/photo head and
animation skeleton are retained. Downloaded rig scripts are never enabled.
"""
import bpy, bmesh, os
from collections import defaultdict
from mathutils import Vector

base='Assets/_Project/Art/Remake/Source~/AbdoubouamHuman/man_rigged.blend'
arm=bpy.data.objects['PlayerRig'];head=bpy.data.objects['PlayerHead'];old=bpy.data.objects['PlayerBody']
with bpy.data.libraries.load(os.path.abspath(base),link=False) as (src,dst):
    dst.objects=['Basemesh_FullbodyMale','rig']
body,rig=dst.objects
bpy.context.scene.collection.objects.link(body);bpy.context.scene.collection.objects.link(rig)
rig.data.pose_position='REST'
source_bones=rig.data.bones
def mapping(name):
    suffix='Left' if '.L' in name else 'Right'
    if 'upper_arm' in name:return suffix+'UpperArm'
    if 'forearm' in name:return suffix+'LowerArm'
    if 'shoulder' in name:return suffix+'Shoulder'
    if any(n in name for n in ('hand','finger','thumb','palm')):return suffix+'Hand'
    if 'thigh' in name:return suffix+'UpperLeg'
    if 'shin' in name:return suffix+'LowerLeg'
    if 'toe' in name:return suffix+'Toes'
    if 'foot' in name:return suffix+'Foot'
    return {'DEF-hips':'Hips','DEF-spine':'Spine','DEF-ribs':'UpperChest','DEF-neck':'Neck','DEF-head':'Head'}.get(name)
groups={}
for bone in source_bones:
    target=mapping(bone.name) if bone.name.startswith('DEF-') else None
    if target:groups.setdefault(target,[]).append(bone)
transforms={}
for name,parts in groups.items():
    # Split twist bones cover one continuous limb. Use its complete segment.
    root=min(parts,key=lambda b:len(b.parent_recursive))
    end=max(parts,key=lambda b:(b.tail_local-root.head_local).length)
    a=root.head_local;b=end.tail_local;t=arm.data.bones[name]
    rotate=(b-a).rotation_difference(t.tail_local-t.head_local)
    scale=(t.tail_local-t.head_local).length/max((b-a).length,.001)
    if name in ('Hips','Spine','UpperChest','Neck','Head'):scale=.82
    transforms[name]=(a,t.head_local,rotate,scale)
positions=[];skinweights=[];original=[]
for v in body.data.vertices:
    p=body.matrix_world @ v.co;original.append(p.copy());w=defaultdict(float)
    for g in v.groups:
        name=mapping(body.vertex_groups[g.group].name)
        if name in transforms:w[name]+=g.weight
    if not w:w['Head' if p.z>1.65 else 'Hips']=1
    total=sum(w.values());w={name:value/total for name,value in w.items()}
    q=Vector()
    for name,value in w.items():
        a,target,rotate,scale=transforms[name]
        q+=(target+rotate@((p-a)*scale))*value
    positions.append(q);skinweights.append(w)

materials=['Player_Skin','Player_ShirtWhite','Player_ShirtGrey','Player_ShirtNavy','Player_Pants','Player_Shoes']
def outfit(p):
    if p.z>1.42 and abs(p.x)<.15:return 0
    if p.z<.105:return 5
    if p.z<1.03:return 4
    if p.z<1.42 and abs(p.x)<.21:return 1 if p.z>1.21 else 2 if p.z>1.14 else 3
    if 1.31<p.z<1.48 and abs(p.x)<.32:return 1
    return 0
faces=[list(f.vertices) for f in body.data.polygons if all(original[i].z<1.76 for i in f.vertices)]
mesh=bpy.data.meshes.new('CC0 student body / editable anatomical topology');mesh.from_pydata(positions,[],faces);mesh.update()
for name in materials:mesh.materials.append(bpy.data.materials.get(name) or bpy.data.materials.new(name))
obj=bpy.data.objects.new('ImportedStudentBody',mesh);bpy.context.scene.collection.objects.link(obj)
for f in mesh.polygons:
    c=sum((positions[i] for i in f.vertices),Vector())/len(f.vertices)
    f.material_index=outfit(c);f.use_smooth=True
    # Cloth covers anatomy, with consistent thickness and less sculpted muscle definition.
for v in mesh.vertices:
    p=v.co
    if outfit(p)!=0:
        p.y=max(p.y,-.095) if .88<p.z<1.08 else p.y
        p.y+=.006 if p.y>=0 else -.009
uv=mesh.uv_layers.new(name='UVMap')
for f in mesh.polygons:
    for li in f.loop_indices:
        p=mesh.vertices[mesh.loops[li].vertex_index].co;uv.data[li].uv=(p.x*4+p.y*4,p.z*4)
for name in arm.data.bones.keys():obj.vertex_groups.new(name=name)
for i,w in enumerate(skinweights):
    for name,value in w.items():obj.vertex_groups[name].add([i],value,'REPLACE')
obj.parent=arm;obj.modifiers.new('Armature','ARMATURE').object=arm
# Preserve the detailed original backpack, shoes and belt as additional authored pieces.
bm=bmesh.new();bm.from_mesh(old.data)
remove=[f for f in bm.faces if not (f.calc_center_median().y>.12 and 1.02<f.calc_center_median().z<1.37 or f.calc_center_median().z<.115 or .865<f.calc_center_median().z<.89)]
bmesh.ops.delete(bm,geom=remove,context='FACES');bm.to_mesh(old.data);bm.free()
bpy.ops.object.select_all(action='DESELECT');old.select_set(True);obj.select_set(True);bpy.context.view_layer.objects.active=obj
bpy.ops.object.join();obj.name='PlayerBody'
# Sculpt variants on the same topology and rig. Erick is the slender base identity.
obj.shape_key_add(name='Basis')
for name,width,depth in [('Ander',.97,.96),('Erick',.86,.88),('Cesar',1.08,1.04),('Juan',.92,.94),('Sebas',1.02,1.00)]:
    key=obj.shape_key_add(name=name)
    for v,k in zip(obj.data.vertices,key.data):
        p=v.co.copy();p.x*=width;p.y*=depth;k.co=p
# Repair the portrait projection on the face, retaining the back/hair UV seam.
for poly in head.data.polygons:
    poly.use_smooth=True
    for li in poly.loop_indices:
        p=head.data.vertices[head.data.loops[li].vertex_index].co
        frontal=max(0,min(1,(-p.y-.015)/.035))
        uv=head.data.uv_layers.active.data[li].uv
        uv.x=uv.x*(1-frontal)+(.5-p.x*2.8)*frontal
        uv.y=uv.y*(1-frontal)+max(.02,min(.98,(p.z-1.50)*4.6))*frontal
head.shape_key_add(name='Basis')
for name,width,chin in [('Ander',1.02,1),('Erick',.95,.98),('Cesar',1.08,1.06),('Juan',.94,1.03),('Sebas',1.00,.97)]:
    key=head.shape_key_add(name=name)
    for v,k in zip(head.data.vertices,key.data):
        p=v.co.copy();p.x*=width
        if p.z<1.58:p.z=1.58+(p.z-1.58)*chin
        k.co=p
for o in list(bpy.data.objects):
    if o not in (arm,obj,head):bpy.data.objects.remove(o,do_unlink=True)
out='Assets/_Project/Art/Remake/Source~/StudentFromHumanBase.blend'
bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(out))
bpy.ops.object.select_all(action='DESELECT')
for o in (arm,obj,head):o.select_set(True)
bpy.ops.export_scene.fbx(filepath=os.path.abspath('Assets/_Project/Art/Characters/Player/PlayerCharacter.fbx'),use_selection=True,
    object_types={'ARMATURE','MESH'},add_leaf_bones=False,bake_anim=False,use_armature_deform_only=True,
    axis_forward='-Z',axis_up='Y',apply_scale_options='FBX_SCALE_ALL',bake_space_transform=True,use_mesh_modifiers=False,mesh_smooth_type='FACE',path_mode='STRIP')
print('HUMAN_BASE_STUDENT',len(obj.data.vertices),'vertices',len(obj.vertex_groups),'bone groups')
