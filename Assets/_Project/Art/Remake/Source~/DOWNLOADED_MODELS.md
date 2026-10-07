# Downloaded anatomical bases (2026-10-05)

Downloaded through the user's authenticated Chrome/BlendSwap session with authorization.

- **Hands Rigged**, SparrowHawk: https://blendswap.com/blend/22269 — CC0, 8.83 MB.
  Original `.blend` and provided license HTML are in `SparrowHawkHands/`.
  No textures supplied. The feminine left hand supplies both sides (right is mirrored).
  `Tools/blender/import_sparrowhawk_hands.py` preserves author geometry, UVs, weights and
  22 deform bones, applies one multires level and exports the runtime JSON.
  Each hand has 4,383 seam-split vertices and 8,716 triangles. No primitive finger meshes.
- **Rigged Man basemesh**, abdoubouam: https://blendswap.com/blend/20895 — listed CC0,
  17.9 MB. Original `.blend` and provided license HTML are in `AbdoubouamHuman/`.
  The author's linked legacy base URL no longer resolves to the referenced human model;
  the download and license supplied by BlendSwap are retained, without claiming a verified
  upstream author identity. Embedded `rig_ui.py` was NOT executed (Blender scripts disabled).
  `Tools/blender/import_human_base.py` maps the original body weights/geometry to the
  existing avatar skeleton, retains the student's authored head, clothes the body, and
  preserves backpack/shoes/belt. Output: `StudentFromHumanBase.blend` and the existing
  `Characters/Player/PlayerCharacter.fbx` (stable GUID/material mappings).

`PreviousProceduralStudent.fbx` is the pre-replacement rollback copy. The original
`Characters/Player/Source~/PlayerCharacter.blend` is preserved. The complete downloaded
human includes its original head/hands and remains editable for skins and future enemy
sculpting. Named enemies still use their current segmented procedural models; they are
not yet rebuilt from this body base. First-person hands use the separate detailed asset.

Regenerate from repository root using the Steam Blender executable, with scripts disabled:

```
blender -b "Assets/_Project/Art/Remake/Source~/SparrowHawkHands/Hands + armature.blend" --python Tools/blender/import_sparrowhawk_hands.py
blender -b Assets/_Project/Art/Characters/Player/Source~/PlayerCharacter.blend --python Tools/blender/import_human_base.py
```
