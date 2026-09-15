# FireExtinguisher

Extintor ABC de 6 kg. Prop de pasillo: va colgado o de pie junto a puertas,
escaleras y salidas.

## Origen

Descargado como `10285_Fire_Extinguisher_v3_L3...zip`. Export de 3ds Max de 2011
(Wavefront OBJ + MTL + un difuso de 1024×1024), Z-up y a escala arbitraria.

## Qué se hizo al portarlo

1. Importar sin conversión de ejes (el OBJ ya es Z-up) y dejar que el exportador FBX
   haga la única conversión a Y-up de Unity.
2. **Decimate Dissolve** con límite de 3° primero: el cuerpo es un torneado con
   tramos planos largos, y colapsarlos gastaría presupuesto en geometría que no
   aporta silueta. 29 056 → 15 792 tris.
3. **Decimate Collapse hasta 700 tris**, dentro de la banda de "prop pequeño"
   (200–800) de `docs/technical/psx-style-guide.md`.
4. Normalizar a **0.23 × 0.15 × 0.52 m** reales, pivote centrado y a ras de suelo.

Resultado: `FireExtinguisher.fbx`, 1 malla, 1 material, 700 tris, escala 1:1.

## Texturas

`FireExtinguisher_Diffuse.jpg` es el difuso original del paquete, sin retocar.
`PsxPropImporter` lo baja a 512 px con filtro Point al importarlo.
