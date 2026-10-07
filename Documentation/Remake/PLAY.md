# Probar en Windows

Usa `C:\Users\andre\Software\Projects\HORROR-UTEZ`, rama `remake`.
La otra copia `C:\Users\andre\Software\HORROR-UTEZ` contiene la versión antigua.
Unity Hub no cambia la rama: comprueba `git branch --show-current` en la carpeta correcta.

Abre el proyecto con Unity 6000.6.3f1, espera la importación, abre
`Assets/_Project/Scenes/remake.unity` y pulsa Play. El contenido nuevo aparece en ejecución.
Elige solo, alojar o unirte con la IP del anfitrión. Capacidad prevista: cinco estudiantes.
TCP 27777 debe ser accesible; no hay lobby Steam ni conexión automática por internet.

| Acción | Control |
|---|---|
| Caminar / mirar | WASD / ratón |
| Correr / saltar / agacharse | Shift (gasta energía) / Espacio / Ctrl o C |
| Barrida | Agacharse justo después de correr |
| Rodar como muñeco (tumble) | Q; levantarse con Espacio, Q o moviéndose tras quedar quieto |
| Agarrar | Mantener clic izquierdo o G donde quieras sujetar; soltar para dejar el objeto |
| Distancia del objeto | Rueda del ratón |
| Girar objeto sujetado | Mantener R y mover el ratón |
| Interactuar | E |
| Linterna | F |
| Voz | Activar micrófono en interfaz y mantener V |
| Pausa / cursor | Esc |
| Observar a un compañero (caído) | Clic o Espacio |

La pausa no detiene el mundo compartido. Acomoda objetos en el camión: deben quedar
completamente dentro, quietos y sin ser sujetados para contar. Paga la cuota y entra
al camión para extraerte. Los supervivientes que viajan conservan mejoras; los demás
reviven al día siguiente sin ellas. El anfitrión avanza la jornada desde resultados.

Objetos pesados (UPS 36 kg) requieren dos estudiantes. Los golpes fuertes restan valor
(“-$X”) y el equipo frágil se rompe por debajo del 15 % de su valor.
Un golpe del velador te derriba. Consulta [TESTING.md](TESTING.md) para el
estado real de validación: las pruebas automáticas pasan, la sensación en juego necesita tu revisión.

## Ampliación del 2026-10-05

El botón **MAPA** del menú permite elegir Campus UTEZ, Mansión, Museo o Ártico antes
de explorar solo u hospedar. Los invitados reciben el mapa elegido por el anfitrión.
Usa el mismo ejecutable actualizado en ambas máquinas: el protocolo pasó a versión 2.

Los tres mapas adicionales son diseños compactos construidos con nueve módulos
originales autorizados de R.E.P.O. (tres por mapa), adaptados a nuestro juego. Conservan
el camión como única extracción; no ejecutan el generador ni los eventos originales.

El carrito se retiró. CDS tiene oficinas en ambas plantas y el auditorio tiene
butacas, escenario y zona técnica. El exterior incorpora mobiliario, equipo y más
vegetación; la iluminación ambiental y lunar del remake se redujo a la mitad.

Los enemigos necesitan visión sin obstáculos, tardan más en identificarte agachado,
pierden el rastro antes y hacen menos daño. Sus golpes comprueban paredes antes y
después de prepararse. Al perderte buscan tu última posición visible. Correr, golpear
equipo y hablar siguen produciendo pistas sonoras; las paredes reducen su alcance.

Consulta los resultados y las limitaciones actuales en [TESTING.md](TESTING.md).

## Novedades (segunda pasada)

- Compuaulas de CECADEC (planta baja) amuebladas: casi todas las PCs están rotas y no valen nada. El equipo
  valioso escondido está en el centro de cómputo CC9, Aula 1 y 2, el Lab de Procesos, el laboratorio SE, la
  bodega, el cuarto eléctrico y la sala del fondo.
- **El Rector**: un gigante que despierta tras un rato, camina por fuera y entra a CECADEC agachándose o
  gateando. Las luces parpadean cuando se acerca. Lánzale equipo pesado para aturdirlo. Su golpe te lanza.
- Si caes, tu **credencial** queda brillando donde caíste: que un compañero la lleve al camión para revivirte.
  Mientras tanto puedes observar a tus compañeros.
- **Tienda** al terminar el día (sólo quien escapó): fuerza, energía, alcance, velocidad, salud y salto extra.
- Los estudiantes miden 1.5 m: el campus y el Rector se sienten más grandes.

## Partida real de dos jugadores (Windows + Mac o dos PCs)

Ejecutables: `Builds/Remake/Release/HORROR-UTEZ-Windows.zip` y `HORROR-UTEZ-Mac.zip`
(se generan con `Tools/unity/BuildRemake.ps1 -Isolated -Mac`; la carpeta Builds no va a Git).

1. Ambas máquinas en la misma red (mismo Wi-Fi/router).
2. Anfitrión: abre el juego, escribe tu nombre y pulsa **HOSPEDAR · 5**. La primera vez Windows pregunta por el
   firewall: permite **redes privadas**. La barra de estado muestra `ANFITRIÓN · <IP>:27777`.
   No tengas al mismo tiempo el editor de Unity en Play hospedando: ocupa el mismo puerto.
3. Invitado: escribe la IP del anfitrión en el campo de IP y pulsa **UNIRME**.
4. Mac (app sin firma de Apple): descomprime, clic derecho en `HORROR-UTEZ.app` → **Abrir** → Abrir. Si macOS
   dice que está dañada: en Terminal `xattr -cr ~/Downloads/HORROR-UTEZ.app` y vuelve a abrirla. Si el Mac hospeda,
   acepta "permitir conexiones entrantes".
5. Por internet: el anfitrión debe redirigir TCP 27777 en su router, o ambos usar una VPN de red local
   (Tailscale, ZeroTier, Radmin VPN) y unirse con la IP de la VPN.

Para pruebas automáticas en la misma PC usa otro puerto: `-remakePort 27877` en host y cliente.

## Cambios del 5 de octubre: campus, tienda y personajes

- El menú incluye **MANUAL** con Hugo, Carsi, Ulises, Cristian y Derick: consulta sus
  señales y formas de evitarlos. Ulises responde a la linterna; Cristian busca equipo
  valioso; Derick corre en ráfagas y descansa entre ataques.
- Hay ocho tipos de botín adicionales, entre 8 y 38 kg. Mantén el botón para agarrar;
  soltarlo deja caer el objeto. El equipo pesado requiere ayuda o mejoras de fuerza.
- Al extraer, el camión viaja a **remake_shop**, una escena independiente. Acércate a un
  producto, apúntalo y pulsa **E** para comprar. El HUD muestra el precio actual.
  El anfitrión se acerca a la salida y pulsa **E** para comenzar el siguiente día.
- Cada día cambia la distribución/selección del botín, parte de sus valores, enemigos
  activos y condiciones del campus. Quienes quedaron atrás reviven sin mejoras.
- Las manos ahora usan una malla anatómica gratuita con esqueleto, y el cuerpo visible
  en cooperativo usa una base humana editable con uniforme. El agarre aún necesita
  evaluación humana en distintos objetos; las pruebas automáticas no validan sensación.
- El ejecutable Windows actual está en `Builds/Remake/Windows/HORROR-UTEZ.exe`.
  Los ZIP/Mac anteriores no incluyen necesariamente estos cambios; Android queda pendiente.
# Skins (2026-10-06)

En el menú, pulsa ESTUDIANTE para recorrer Ander, Erick, Cesar, Juan y Sebas antes
de iniciar o unirte. La elección se recuerda y los compañeros la ven en línea.
Erick mantiene la cara original y tiene complexión más delgada. Las otras caras son
estilizadas; no se han recibido fotografías para reproducir a esas personas.
Esta compilación usa protocolo 4: host y clientes deben usar la misma versión.
