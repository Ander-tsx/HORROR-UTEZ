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
