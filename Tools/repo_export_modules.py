"""Export authorized local module geometry, materials and colliders to engine-neutral JSON.
No MonoBehaviours, Photon components or game DLLs are copied.
"""
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'Library/Tooling/asset-python'))
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler

SOURCE = Path('C:/Program Files (x86)/Steam/steamapps/common/REPO/REPO_Data/resources.assets')
OUT = ROOT / 'Assets/_Project/Art/RepoAuthorized/Resources/Repo'
MODULES = {
    'Manor': 'Module - Manor - N - 1 - Cross Corridors',
    'Museum': 'Module - Museum - N - 1 - Cafe',
    'Arctic': 'Module - Arctic - N - 1 - Labs',
    'ManorKitchen': 'Module - Manor - N - 1 - Big kitchen',
    'ManorRooms': 'Module - Manor - N - 1 - Rooms1',
    'MuseumColumn': 'Module - Museum - N - 1 - Double Column',
    'MuseumRoundabout': 'Module - Museum - N - 1 - Roundabout',
    'ArcticWarehouse': 'Module - Arctic - N - 1 - Warehouse',
    'ArcticLounge': 'Module - Arctic - N - 1 - Lounge',
}


def vec(v):
    return {'x': v.x, 'y': v.y, 'z': v.z}


def array_vec(v, keys):
    return dict(zip(keys, v))


def write(name, data):
    (OUT / (name + '.json')).write_text(json.dumps(data, separators=(',', ':')), encoding='utf-8')


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    env = UnityPy.load(str(SOURCE))
    meshes, materials, textures = set(), set(), set()

    def mesh(ptr):
        if not ptr.path_id:
            return ''
        key = 'mesh_' + str(ptr.path_id)
        if key in meshes:
            return key
        data = ptr.read()
        handler = MeshHandler(data)
        handler.process()
        write(key, {'vertices': [array_vec(v[:3], 'xyz') for v in handler.m_Vertices],
                    'uv': [array_vec(v[:2], 'xy') for v in (handler.m_UV0 or [])],
                    'submeshes': [{'triangles': [i for tri in sub for i in tri]} for sub in handler.get_triangles()]})
        meshes.add(key)
        return key

    def material(ptr):
        if not ptr.path_id:
            return {'name': 'fallback', 'texture': '', 'color': {'r': .45, 'g': .45, 'b': .45, 'a': 1}}
        data = ptr.read()
        tex = ''
        props = data.m_SavedProperties
        colors = dict(props.m_Colors)
        color = colors.get('_Color', colors.get('_BaseColor'))
        envs = dict(props.m_TexEnvs)
        slot = envs.get('_MainTex', envs.get('_BaseMap'))
        if slot and slot.m_Texture.path_id:
            tex = 'tex_' + str(slot.m_Texture.path_id)
            if tex not in textures:
                slot.m_Texture.read().image.save(OUT / (tex + '.png'))
                textures.add(tex)
        materials.add(data.m_Name)
        return {'name': data.m_Name, 'texture': tex,
                'color': {k: getattr(color, k) if color else 1 for k in 'rgba'}}

    def walk(transform, nodes, parent=-1, parent_active=True, parent_door=False, parent_volume=False):
        go = transform.m_GameObject.read()
        active = parent_active and go.m_IsActive
        # Modules' connector variants are switched by the original generator. Expose their open variant.
        if go.m_Name == 'Connected': active = parent_active
        if go.m_Name in ('Disconnected', 'Not Connected', 'NotConnected'): active = False
        label = go.m_Name.lower()
        door_leaf = parent_door or ('door' in label and not any(s in label for s in ('frame', 'way', 'handle', 'sign', 'module')))
        # Room/valuable volumes are metadata probes on dedicated layers in the original,
        # including non-trigger child boxes. They must never become solid walls here.
        volume = parent_volume or 'volume' in label
        components = [pair.component.read() for pair in go.m_Component
                      if pair.component.type.name in ('MeshRenderer', 'MeshFilter', 'BoxCollider', 'MeshCollider', 'SphereCollider', 'CapsuleCollider')]
        pos = vec(transform.m_LocalPosition)
        rot = transform.m_LocalRotation
        node = {'name': go.m_Name, 'parent': parent, 'position': pos,
                'rotation': {k: getattr(rot, k) for k in 'xyzw'}, 'scale': vec(transform.m_LocalScale),
                'mesh': '', 'materials': [], 'colliders': []}
        # Root prefab placement is replaced by the expedition placement in Horror UTEZ.
        if parent == -1:
            node['position'] = {'x': 0, 'y': 0, 'z': 0}
        renderer = next((c for c in components if c.__class__.__name__ == 'MeshRenderer' and c.m_Enabled), None)
        mf = next((c for c in components if c.__class__.__name__ == 'MeshFilter'), None)
        if renderer and mf and active and not door_leaf and not volume:
            node['mesh'] = mesh(mf.m_Mesh)
            node['materials'] = [material(p) for p in renderer.m_Materials]
        for c in components:
            kind = c.__class__.__name__
            if kind not in ('BoxCollider', 'MeshCollider', 'SphereCollider', 'CapsuleCollider'):
                continue
            if c.m_IsTrigger or not c.m_Enabled or not active or volume:
                continue
            # Original animated door leaves have no driver here: keep their frames, omit locked leaves.
            if door_leaf:
                continue
            col = {'kind': kind}
            if kind == 'MeshCollider':
                col['mesh'] = mesh(c.m_Mesh)
            else:
                col['center'] = vec(c.m_Center)
                if kind == 'BoxCollider': col['size'] = vec(c.m_Size)
                else: col['radius'] = c.m_Radius
                if kind == 'CapsuleCollider': col.update(height=c.m_Height, direction=c.m_Direction)
            node['colliders'].append(col)
        index = len(nodes)
        nodes.append(node)
        for child in transform.m_Children:
            walk(child.read(), nodes, index, active, door_leaf, volume)

    found = {}
    for obj in env.objects:
        if obj.type.name == 'GameObject':
            go = obj.read()
            if go.m_Name in MODULES.values():
                found[go.m_Name] = go
    for key, name in MODULES.items():
        go = found[name]
        transform = next(pair.component.read() for pair in go.m_Component if pair.component.type.name == 'Transform')
        nodes = []
        walk(transform, nodes)
        write(key, {'source': name, 'nodes': nodes})
        print(key, len(nodes), 'nodes', flush=True)
    # A few original props are also exposed independently for campus dressing.
    for obj in env.objects:
        if obj.type.name == 'Mesh':
            data = obj.read()
            if data.m_Name in ('Computer Horizontal', 'Trash Bin 1', 'Bush mesh', 'Valuable Arctic Server Rack'):
                write('prop_' + data.m_Name.replace(' ', '_'), {'mesh': mesh(obj), 'source': data.m_Name})
    # Remove only obsolete files emitted by previous runs of this exporter, never other project assets.
    assert OUT.resolve().is_relative_to((ROOT / 'Assets/_Project/Art/RepoAuthorized').resolve())
    for pattern, retained in (('mesh_*.json', meshes), ('tex_*.png', textures)):
        for old in OUT.glob(pattern):
            if old.stem not in retained:
                old.unlink()
                meta = old.with_name(old.name + '.meta')
                if meta.exists(): meta.unlink()
    write('provenance', {'source': str(SOURCE), 'authorization': 'User confirmed permission on 2026-10-05',
                          'modules': MODULES, 'meshes': len(meshes), 'textures': len(textures),
                          'limitations': 'Static geometry only; no original game scripts, navigation, events or procedural generator.'})
    print('Export complete', len(meshes), 'meshes', len(textures), 'textures', flush=True)


if __name__ == '__main__':
    main()
