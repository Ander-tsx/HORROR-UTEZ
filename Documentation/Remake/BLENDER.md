# Blender, modelos y referencias

Blender instalado por Steam:
`C:\Program Files (x86)\Steam\steamapps\common\Blender\blender.exe` (5.2.2).
Registrar esa instalación con `--register` resolvió que Unity no encontrara Blender.
No se instaló correctamente la copia MSI: quedó una extracción temporal en Library.

El entorno original importa `.blend` directamente mediante `PsxKitImporter`: respetar
su conversión de ejes, escala y nombres de materiales. El personaje usa fuentes Blender
en `Source~`, FBX Humanoid y clips Mixamo separados. No unificar ambos flujos a ciegas.

Los props nuevos usan `Tools/blender/gen_remake_props.py`. Ejecutar desde la raíz:

```powershell
& 'C:\Program Files (x86)\Steam\steamapps\common\Blender\blender.exe' -b --factory-startup --python Tools/blender/gen_remake_props.py
```

Genera fuentes editables en `Assets/_Project/Art/Remake/Source~` y FBX explícitos.
Unity omite las carpetas terminadas en `~`. La mano nueva se carga mediante Resources
desde `Art/Remake/Resources/StudentHand.fbx`. Conservar fuentes, exports y `.meta`.
Regenerar sobrescribe modelos generados: incorporar mejoras en el script o guardar
una variante antes de editar manualmente. Revisar pivotes, escala, materiales y orientación.
Los visuales actuales rotan 180° para alinear el FBX con los colliders del juego.
No se han combinado/optimizado todos los múltiples meshes para móvil.

Referencias locales: `C:\Users\andre\Downloads\UTEZ - Mapping-20261003T182058Z-1-001\UTEZ - Mapping`.
Hay 40 JPG, principalmente CECADEC, pasillos, laboratorios y oficinas. Contact sheet:
`Library/Tooling/map-contact.jpg`. No se importaron fotos al repositorio.
Las medidas existentes están en `UtezDimensions.cs`; conservar la distribución real.
Faltan planos adicionales: usar geometría existente e imaginar detalles compatibles.

La última generación agregó teclados/puertos, piezas de microscopios, ventilación,
detalles de impresora/UPS, tablas/rieles/cabina del camión, cuidador y dedos humanos.
Blender terminó sin errores; aún falta validar importación y apariencia en Unity.

## Kit v2 (2026-10-03, segunda pasada)

Ejecutar desde la raíz, en este orden (`$B` = Blender de Steam):

```powershell
& $B -b --factory-startup --python Tools/blender/gen_remake_textures.py   # 51 texturas -> Art/Remake/Textures
& $B -b --factory-startup --python Tools/blender/gen_remake_lab.py        # 26 piezas -> Art/Remake/Resources/Lab
& $B -b --factory-startup --python Tools/blender/gen_remake_props.py      # botín, camión, velador, gigante
& $B -b Assets/_Project/Art/Characters/Player/Source~/PlayerCharacter.blend --python Tools/blender/gen_player_body_v3.py
& $B -b --factory-startup --python Tools/audio/gen_remake_audio.py        # 37 WAV -> Art/Remake/Resources/Audio
```

- `remake_kit.py`: se modela en coordenadas de Unity. `to_blender` es una reflexión `(x, z, y)` que cancela la
  del importador FBX; las caras se invierten para conservar el lado visible y las UV ajustadas (pantallas,
  pizarrón, pósters) se calculan en espacio Unity. Se verificó en Unity con el centro del velador (+x, lado del
  trapeador) y textos legibles en el tour. Ya no hay giros de 180° en tiempo de ejecución.
- Nombres de material = nombres de textura. RemakeBuild crea un material PSX/Lit por textura.
- Personajes enemigos por segmentos (Pelvis, Spine, Chest, Neck, Head, UpperArm/LowerArm/Hand, UpperLeg/
  LowerLeg/Foot `_L/_R`), cada objeto con origen en su articulación; `RemakeBody` los anima.
- Jugador: las fotos de la cara no están en este equipo (`Tools/character/source` falta), así que
  `gen_player_body_v3.py` abre el `.blend` guardado, conserva cabeza, textura y rig, y sólo reemplaza el cuerpo
  (polo, jeans, tenis, manos con dedos, mochila, reloj) con pesos deterministas por pieza. Usa los mismos 7
  materiales que remapea el `.meta` del FBX. RemakeBuild sincroniza el prefab desempacado con el FBX.
- Fuentes del HUD: VT323 y Creepster (Google Fonts, OFL) en `Resources/Fonts` con sus licencias.
# Variantes del estudiante (2026-10-06)

`import_human_base.py` abre el PlayerCharacter.blend original, conserva el rig/cabeza
y adapta el humano descargado. Genera cinco shape keys en cuerpo y cabeza; exportar
con `use_mesh_modifiers=False` y habilitar blendshapes en PsxCharacterImporter.
La cara conserva el atlas original de Erick con proyección frontal corregida.
`gen_student_skin_faces.py` genera los otros cuatro atlas estilizados y copia el de
Erick en Resources/Skins. El FBX mantiene su GUID y siete materiales existentes.
