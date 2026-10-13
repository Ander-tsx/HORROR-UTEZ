# Entrada UTEZ y fondo del menú

La entrada está dentro de `Assets/_Project/Scenes/remake.unity`, separada del campus jugable,
en **(120, 0, -80)**. Es un conjunto 3D editable, construido a partir de las fotos que el usuario
dejó en Downloads. Conserva el lenguaje low-poly/PSX del juego.

## Revisarla en Unity

1. Abre **HORROR-UTEZ → Entrada UTEZ → Abrir y enfocar entrada**.
2. En la jerarquía selecciona **Entrada_UTEZ**. Sus ocho grupos separan pórtico, caseta,
   explanada, rejas, iluminación, vegetación, detalles y encuadre.
3. Pulsa Play para ver el menú mirando hacia esta entrada. Los controles están a la izquierda.
4. Para reubicarla, mueve o gira **el objeto raíz completo**. `MenuView` y `MenuFocus` se mueven
   con él; el encuadre sigue esas referencias sin coordenadas fijas de la entrada en el código.
5. Para retocar el encuadre, mueve esos dos hijos en el grupo 08. `RemakeEntrance` permite
   ajustar campo de visión y un movimiento lateral muy tenue de la cámara.

La posición es provisional, como pidió el usuario. No existe todavía un camino que conecte esta
explanada con los edificios ni se amplió la cuadrícula de navegación de enemigos hasta ella.
Las rejas y barreras son decorado estático; no tienen un sistema de apertura nuevo.

## Modelo y materiales

- Cuatro columnas elípticas de estuco rojo con zócalos crema, dintel central con el nombre completo
  de la universidad, vigas laterales y seis aletas longitudinales rojas.
- Caseta con marquesina oval, remates, emblema UTez, ventanería, marcos, puerta posterior,
  mostrador, monitor y silla. Cámaras de vigilancia y señalización complementan el acceso.
- Seis paneles de reja reutilizables (`Entrada_Reja.prefab`), cada uno con 23 barrotes tubulares
  y dos travesaños; un collider sencillo por panel evita crear físicas para cada barrote.
- Calzada, banquetas, cruces peatonales, guarniciones y jardines centrales; ocho instancias
  de `Entrada_Palmera.prefab`, arbustos, flores, papeleras, bolardos, postes y cableado.
- Siete luces locales sin sombras en tiempo real. La iluminación del campus sigue usando
  el sistema de clima existente. El asfalto se divide en seis superficies para que URP pueda
  seleccionar las luces cercanas de cada zona.

Toda la geometría y las luces viven en prefabs. No se añadió un generador de mapa ni una
dependencia de Blender. `RemakeEntrance` solamente enlaza el atlas y encuadra la cámara.
`RemakeEntranceReview` inspecciona y captura los assets existentes; no guarda geometría nueva.

`Entrance_Materials.png` es un atlas original de cuatro cuadrantes: estuco rojo, concreto crema,
asfalto y acero pintado rojo. Sus cuatro materiales repiten únicamente su región mediante
`_AtlasRect`; el componente raíz enlaza esa región por renderer, igual que el atlas de la tienda.
`Entrance_Yellow` colorea guarniciones y `Entrance_Palm` recorta la textura original de fronda
con alfa (doble cara). El tronco, vidrio, metal, vegetación y luminarias reutilizan materiales
existentes del proyecto. No se modificaron sus fuentes.

## Referencias y procedencia

Fotos proporcionadas por el usuario en `C:\Users\andre\Downloads`, revisadas visualmente:

- `UTEZ-15-ANIVERSARIO1.jpg`: referencia principal de estuco, vigas, caseta, rejas y vegetación.
- `contenido-noticiasmorelostv-7.jpg` y `images (1).jpg`: vistas aéreas de distribución.
- `images.jpg`, `contenido-noticiasmorelostv-32.jpg` y `utez-carrusel.jpeg`: frente y proporciones.

Las fotografías no se copiaron al repositorio ni se usaron como un fondo plano del menú.
Las medidas son aproximaciones visuales; no es un levantamiento arquitectónico certificado.
Los letreros y el emblema son texto editable en Unity, no una descarga del logotipo oficial.

Se usó ImageGen para dos texturas originales. El atlas tomó la foto principal como referencia
de color y acabado; la fronda se generó con fondo transparente. Los PNG originales de salida
se conservan en `.codex/generated_images/01a132ae-bbf8-7a13-86e3-0d8fd439dd7a/` del usuario.
Las copias utilizables están en `Assets/_Project/Art/Remake/Textures/Entrance_*.png`.
La herramienta produjo el atlas a 1254 × 1254 y la fronda a 1024 × 1536; Unity conserva
sus mipmaps y aplica los ajustes de importación. La fronda tiene alfa real, comprobado
en el PNG; el material recorta también su halo semitransparente.

Prompt del atlas:

> Create an original production environment MATERIAL ATLAS for a Unity game reconstructing the UTEZ entrance shown in the reference photograph. Use the photo ONLY as an architectural material color/finish reference; do not reproduce the scene, people, lettering or logos. Square 2048 by 2048, exact four equal quadrants in a 2 by 2 grid, no gutters or labels. Top left: warm terracotta red rough pebbled Mexican stucco, fine grain, believable aging and tiny darker rain stains, match the reference red pillar finish. Top right: warm pale cream/beige architectural concrete plaster, restrained mottling and faint vertical water weathering, match the reference lintel. Bottom left: dark grey outdoor asphalt with aggregate, hairline cracks and subtle patches, no lines or markings. Bottom right: oxidized dark red painted steel gate surface, subtle scratches and tiny rust near seams, distinguish its metal finish from the rough stucco. Flat orthographic diffuse albedo, neutral consistent lighting with no baked shadows. High-quality realistic hand-painted materials compatible with a modern low-poly PSX horror game. Every tile is uniformly filled and visually tileable, no objects, perspective, vignette, words or watermark.

Prompt de la fronda:

> Production game foliage texture with TRUE TRANSPARENT background. A single Mexican ornamental palm frond isolated, flat orthographic top-down view, oriented vertically: thin stem emerges at bottom center and the pointed tip reaches top center. Long slender curved central rib with many narrow tapered leaflets branching left and right, natural slight asymmetry, clean separate leaflet silhouettes with transparent gaps. Dark olive green mature palm, subdued yellow-green rib, subtle natural wear, high quality realistic hand-painted albedo suitable for modern low-poly PSX architecture. Entire frond fits with modest padding, no pot, no trunk, no other leaves, no ground, no baked shadows, no text, no watermark.

## Capturas y validación

Las capturas finales se guardan en [Entrance](Entrance/). Las vistas de fachada, caseta y conjunto
usan iluminación temporal de inspección en una copia aislada; no cambian la luz guardada en la escena.
El encuadre nocturno usa posprocesamiento; la captura del menú procede del ejecutable de Windows.
Consulta [TESTING.md](TESTING.md) para resultados, fallos intermedios y limitaciones.

Este trabajo termina el escenario de entrada y su uso en el menú. El nuevo flujo de alcantarilla,
venta, economía compartida y casino sigue pendiente, como detalla [SHOP.md](SHOP.md).
