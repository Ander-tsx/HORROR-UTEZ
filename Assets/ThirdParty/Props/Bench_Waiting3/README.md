# Bench_Waiting3

Banco metálico de sala de espera, 3 plazas, asiento y respaldo de chapa perforada.
Es el mueble típico de pasillo de institución pública, así que sirve tanto para el
interior de CDS/CECADEC como para la explanada.

## Origen

Descargado como `j88xthybc4qo-Banketka.zip` (paquete "banketka", 2017). El zip trae
una escena de Blender de render de producto: el banco dentro de un backdrop, con
cámara, dos lámparas y un softbox.

## Qué se hizo al portarlo

El original no es utilizable tal cual. Proceso, reproducible desde el zip:

1. Borrar todo lo que no es el banco: `Backdrop`, `Light Array`, `Softbox_Box`,
   cámara y lámparas.
2. Quitar los modificadores **Subdivision** de `seat` y `axis` (con ellos la malla
   evaluada son **118 400 tris**), y aplicar el **Mirror** de `axis`.
3. Unir las dos mallas en un objeto: **7 432 tris**.
4. Fusionar los materiales `axis` y `SilverGlossy` en uno solo — son la misma
   superficie cromada, y separarlos costaba un draw call de más. Quedan 2 submallas:
   `Bench_Seat` (chapa perforada, texturizada) y `Bench_Metal` (cromo liso).
5. **Decimate Collapse hasta 2 400 tris**, dentro de la banda de "prop mediano"
   (800–2500) de `docs/technical/psx-style-guide.md`.
6. Normalizar: eje largo a X, escala a **1.80 × 0.70 × 0.91 m** reales, pivote en el
   centro de la huella y a ras de suelo, para poder soltarlo en Y=0 sin corregir nada.

Resultado: `Bench_Waiting3.fbx`, 1 malla, 2 materiales, 2 400 tris, escala 1:1 con
`UtezDimensions.WorldScale = 1`.

## Texturas

`Bench_Seat_Perforated.jpg` es la textura original del paquete (`dot_faktr02.jpg`,
1728×1080) — el patrón de perforado del asiento. Se conserva íntegra en disco; la
reducción a 512 px con filtro Point la aplica `PsxPropImporter` al importar, que es
reversible y no toca el archivo fuente.

El material `SilverGlossy` del original no traía textura (era cromo de raytracing),
y la que referenciaba el `.blend` (`metallskameiki.jpg`) no venía en el zip. El cromo
se resuelve con un material PSX de color plano generado por `UtezPropAssets`.
