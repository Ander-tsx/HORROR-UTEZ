"""Inventory and export locally authorized REPO assets; never imports game scripts.

Run with Blender's Python, with UnityPy installed under Library/Tooling/asset-python.
Source files stay untouched. Inventory is written to ignored Library/Tooling/RepoAssets.
"""
import argparse
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / 'Library/Tooling/asset-python'))
import UnityPy


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--source', type=Path, default=Path('C:/Program Files (x86)/Steam/steamapps/common/REPO/REPO_Data'))
    parser.add_argument('--export', nargs='*', default=[])
    args = parser.parse_args()
    report = ROOT / 'Library/Tooling/RepoAssets'
    report.mkdir(parents=True, exist_ok=True)
    inventory = []
    export = ROOT / 'Assets/_Project/Art/RepoAuthorized/Resources/Repo'
    for path in sorted(args.source.rglob('*')):
        if not path.is_file() or not (path.suffix in ('.assets', '.bundle') or path.name.startswith('level') and not path.suffix):
            continue
        print('Reading', path.name, flush=True)
        env = UnityPy.load(str(path))
        for obj in env.objects:
            if obj.type.name not in ('Mesh', 'Texture2D', 'GameObject'):
                continue
            data = obj.read()
            name = data.m_Name
            inventory.append({'file': str(path.relative_to(args.source)), 'type': obj.type.name, 'name': name, 'id': obj.path_id})
            if name not in args.export or obj.type.name == 'GameObject':
                continue
            export.mkdir(parents=True, exist_ok=True)
            safe = ''.join(c if c.isalnum() or c in '_-' else '_' for c in name)
            if obj.type.name == 'Mesh':
                (export / (safe + '.obj')).write_text(data.export(), encoding='utf-8')
            else:
                data.image.save(export / (safe + '.png'))
            print('Exported', name, flush=True)
    (report / 'inventory.json').write_text(json.dumps(inventory, indent=2), encoding='utf-8')
    print('Inventory:', len(inventory), flush=True)


if __name__ == '__main__':
    main()
