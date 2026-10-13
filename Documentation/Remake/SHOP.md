# El Sumidero — tienda subterránea

Entrega de escenario, 2026-10-12. Abre `Assets/_Project/Scenes/remake_shop.unity` o
**HORROR-UTEZ → Tienda → Abrir escena subterránea**. No requiere Blender, descargas,
generadores ni ejecutar una preparación. La arquitectura, mobiliario, iluminación y
utilería están guardados en la escena y su prefab.

## Qué contiene

Un mercado clandestino bajo el campus: ladrillo húmedo, nervaduras de bóveda, conductos
oxidados, pavimento deteriorado, una pasarela de metal y luz localizada. Las zonas son:

- **Acceso:** pozo con apertura real en el techo, escalera, indicador verde y pulsador
  SUBIR. El aterrizaje de estudiantes está separado de la descarga del botín.
- **Recepción:** conducto ancho, rampa y bandeja para manipular piezas voluminosas.
- **Vendimia:** báscula física con borde iluminado y terminal de venta.
- **Mejoras:** los seis exhibidores existentes conservan sus componentes de compra,
  modelos, precios y posiciones de interacción; se integran al nuevo espacio.
- **Comprador:** mostrador con reja y figura encapuchada decorativa, ojos ámbar y letrero
  Creepster. No es un NPC animado ni tiene IA.
- **Cerrajería:** banco con estuche de ganzúas, cizalla, llave y candado de muestra.
- **Casino “La Última Ficha”:** acceso entre cortinas, techo bajo, terciopelo granate,
  mesa de ruleta, fichas, taburetes, dos tragamonedas y el aviso sobre dinero del equipo.
- **Detalles:** canaletas, bridas, válvula, manómetro, caja eléctrica, banco de espera,
  estantería, suministros y zumbido eléctrico espacial reutilizando `amb_fluoro.wav`.

## Cómo editarlo

La escena tiene dos conjuntos en la raíz:

1. **El Sumidero - exhibidores de mejoras:** núcleo anterior adaptado; contiene suelo,
   parte del cerramiento, el mostrador de mejoras y sus seis productos funcionales.
2. **Tienda_Subterranea:** instancia de `Prefabs/Map/Tienda_Subterranea.prefab`. Sus nueve
   grupos numerados separan arquitectura, instalaciones, acceso, recepción, comprador,
   cerrajería, casino, señalética/luces y utilería.

Edita el prefab en Prefab Mode o los elementos de la escena. Todas las piezas tienen
nombres descriptivos. Las superficies transitables y los cerramientos tienen colliders;
los adornos pequeños no añaden obstáculos innecesarios. La escena conserva su ubicación
de compatibilidad alrededor de X=200; no representa todavía un túnel continuo bajo la
fuente del campus.

Los puntos para la integración futura son componentes **RemakeAnchor**:

| Clave | Función prevista |
|---|---|
| `shop_entry` | Llegada de jugadores |
| `shop_exit` | Pulsador de subida |
| `shop_loot_arrival` | Salida del conducto de objetos |
| `shop_sale` | Báscula de venta |
| `shop_tools` | Zona de herramientas de acceso |
| `shop_casino` | Zona de apuestas |

Son referencias de autoría, no triggers implementados. El colector del campus se
inicializa antes de cargar la tienda; la integración deberá recoger los marcadores de
esta escena cuando esté lista. La lógica anterior de mejoras todavía usa las posiciones
de `RemakeShop.Product()`: mover esos seis exhibidores requiere actualizar esa integración.

Los materiales propios están en `Art/Remake/Materials/Shop_*.mat`. El shader PSX admite
una región repetible del atlas mediante **Atlas region (size XY, offset ZW)**; los
materiales anteriores usan la región completa. `RemakeShopAtlasBinder` enlaza la región
por renderer para evitar las diferencias de renderizado observadas con SRP batches en
Unity 6000.6. Solo afecta superficies del atlas de la tienda. No crea geometría, luces
ni materiales. Tras cambiar la región, usa su menú contextual **Actualizar materiales
del atlas**. El campus mantiene su configuración de batching.

## Alcance de esta entrega y reglas acordadas

**Esta entrega construye el escenario.** El runtime todavía utiliza el ciclo anterior
de camioneta, cuota, cuenta atrás, saldo individual y tienda entre días. El pulsador de
salida está modelado; la interacción anterior cercana a la salida aún inicia otro turno.
Las herramientas, la báscula y el casino son elementos preparados para conectar, no
compras, ventas o apuestas ya operativas. Sus precios de exposición ($120/$360/$650)
son una propuesta de progresión, pendiente de balance con el loot de las zonas nuevas.

La dirección aprobada para la siguiente integración sustituye ese ciclo:

- Trasladar a pie el botín hasta la fuente del campus y arrojarlo a una alcantarilla
  semiprofunda; los jugadores saltan para entrar.
- Precargar la tienda aditivamente; entrada y regreso individuales sin reiniciar sesión.
- Subir usando un botón junto a la escalera y aparecer arriba.
- Conservar identidad, valor y deterioro del loot; recibirlo físicamente y venderlo
  explícitamente. El host valida traslados y operaciones económicas.
- Saldo compartido, mejoras individuales y apuestas voluntarias que pueden perder el
  dinero del equipo.
- Sin cuota ni límite de tiempo por ahora. Un reloj interno sigue avanzando incluso
  dentro de la tienda y aumenta la dificultad del exterior.
- Comprar herramientas para abrir cerraduras/pasajes y acceder a botín de mayor valor.
- Tienda segura; todavía hay que definir rescate de muertos, conservación de mejoras y
  cómo termina/reinicia una expedición. No heredar automáticamente las reglas del camión.

La fuente exterior, sus triggers, el traslado de loot, la precarga, el saldo común,
las apuestas, las cerraduras y el escalado de dificultad **no se implementaron** aquí.

## Recursos y procedencia

Dos texturas originales generadas con la herramienta integrada ImageGen, guardadas en
`Assets/_Project/Art/Remake/Textures/`:

- **Shop_MaterialAtlas.png:** cuadrantes de ladrillo, metal con pátina, losas y terciopelo.
- **Shop_Roulette.png:** gráfico cenital de ruleta envejecida. Es una textura decorativa;
  los números dibujados no son la fuente de verdad de un futuro juego de apuestas.

Se reutilizan los materiales, modelos de mejora, suministros y audio del proyecto.
Las fuentes Creepster y VT323 ya presentes tienen sus licencias OFL en `Resources/Fonts`.
No se añadieron modelos externos ni scripts de archivos descargados.

Prompts utilizados (herramienta integrada, sin CLI/API externa):

> Create a production game environment MATERIAL ATLAS, square 2048x2048, exact 2 by 2 equal grid without gutters, four flat orthographic seamless material swatches. Top left: damp aged ochre brickwork in horizontal courses, mortar, subtle moss, water staining. Top right: oxidized dark teal painted industrial steel, scratched edges, rust blooms, understated grime. Bottom left: worn warm grey concrete paving square tiles, hairline cracks, drainage moisture, subtle oil spots. Bottom right: distressed deep burgundy casino velvet, subtle fabric weave and diamond stitch pattern, no symbols. For a Mexican underground clandestine campus shop, modern low poly PSX aesthetic, hand painted painterly texture with restrained detail and strong readable material identity. Uniform diffuse albedo illumination, no directional shadows, no perspective, no scene, no objects, no text, no logos, no borders. Each quadrant fully fills its exact quarter.

> Production game texture, square 1024x1024, a worn clandestine casino roulette wheel viewed EXACTLY from directly above, flat orthographic albedo only. One perfectly centered circular roulette wheel taking up 90 percent of image, antique brass rim, alternating 36 narrow red and charcoal black radial pockets and a green zero pocket, aged ivory small numbers around rim, dark polished walnut concentric inner bowl, small brass central spindle. Restrained modern low-poly PSX hand-painted style, readable large graphic shapes, modest texture wear, diffuse even illumination, NO perspective, NO directional shadows, NO table or environment. Outside the circle uniformly very dark burgundy. No logos, no watermark. This will be mapped to a circular tabletop disk.

La herramienta produjo ambas imágenes a 1254 × 1254; se conservan los originales y Unity
aplica sus ajustes de importación. No se presentan como imágenes de la escena final.

## Revisión

`RemakeShopReview` es una herramienta de editor que audita referencias y circulación,
y permite capturar siete vistas con cámara temporal. No genera ni guarda arquitectura.
`Canonicalize` únicamente normaliza assets ya authored usando el serializador de Unity.
No se usa para crear o reconstruir el mapa.

Las capturas de la escena real están en [Shop/](Shop/). Los resultados de compilación y
pruebas, incluidos problemas intermedios, se registran en [TESTING.md](TESTING.md).
