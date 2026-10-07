# Piezas nuevas desde Blender

Desde 2026-10-06 **el mapa se arma y se edita en Unity** (prefabs de zona + ProBuilder).
Blender es opcional y sirve solo para piezas pequeñas: muebles, objetos valiosos, utilería
y personajes. No hay scripts de generación: se modela a mano y se exporta un FBX.

Unity nunca importa `.blend` directamente (eso obligaría a todo el equipo a instalar
Blender). Todo modelo que entra al proyecto es `.fbx`.

## Exportar

1. Modela en metros (1 unidad de Blender = 1 m). Origen de la pieza en su base, centrado.
2. Nombra los materiales igual que un material de `Assets/_Project/Art/Remake/Materials`
   (por ejemplo `Metal_Brushed`, `Plastic_Beige`, `Wood_Desk`). Al importar se asigna ese
   material PSX automáticamente. Para una textura nueva, ponla en `Art/Remake/Textures`
   y usa el menú **HORROR-UTEZ → Crear materiales PSX para texturas nuevas**.
3. *File → Export → FBX*: **Selected Objects**, *Apply Scalings: FBX All*, ejes por
   defecto (*Forward: -Z*, *Up: Y*), sin animación (salvo personajes). Son los mismos
   ajustes que usaba Unity al convertir `.blend`. Revisa en Unity que la pieza quede
   derecha y a escala junto a un mueble existente.
4. Guarda el FBX en:
   - `Assets/_Project/Art/Remake/Resources/Lab/` → muebles de laboratorio/oficina.
   - `Assets/_Project/Art/Remake/` → botín, vehículos y piezas generales.
   - `Assets/_Project/Art/Environment/Kit/` → piezas de arquitectura (muros, ventanas).
5. Guarda el `.blend` editable en una carpeta `Source~` junto al FBX (por ejemplo
   `Art/Remake/Source~/`). Unity ignora las carpetas que terminan en `~`.

Las carpetas `Art/Remake/`, `Art/Environment/Kit/` y `Art/Environment/Landmarks/` tienen
reglas de importación automáticas: escala 1, ejes convertidos y sin cámaras ni luces.

## Colocar en el mapa

Arrastra el FBX a la zona correspondiente (en Prefab Mode). Agrega un Box Collider y el
layer **RemakeScenery** si estorba el paso, y márcalo como Static si no se mueve.

## Personajes

El estudiante (`Art/Characters/Player/PlayerCharacter.fbx`) se usa a través del prefab
`Prefabs/Player/PlayerCharacter.prefab`. Tras reexportar el FBX con otro orden de huesos o
de submallas, usa **HORROR-UTEZ → Sincronizar prefab del estudiante con su FBX**. El
original editable está en `Art/Characters/Player/Source~/PlayerCharacter.blend` y los
cuerpos base descargados en `Art/Remake/Source~/` (licencias en `DOWNLOADED_MODELS.md`).

## Historia

Hasta 2026-10-06 los edificios, las texturas, el audio y gran parte del mapa se generaban
con scripts de Python para Blender (`Tools/blender`, `Tools/audio`) y con código C# en
tiempo de ejecución. Todo eso se horneó en assets de Unity y se retiró del repositorio.
Los scripts siguen disponibles en el tag `pre-unity-migration` por si alguien necesita
consultarlos.
