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
| Carrito | Agarra el asa y camina: el carrito va delante de ti |
| Interactuar | E |
| Linterna | F |
| Voz | Activar micrófono en interfaz y mantener V |
| Pausa / cursor | Esc |

La pausa no detiene el mundo compartido. Acomoda objetos en el camión: deben quedar
completamente dentro, quietos y sin ser sujetados para contar. Paga la cuota y entra
al camión para extraerte. Los supervivientes que viajan conservan mejoras; los demás
reviven al día siguiente sin ellas. El anfitrión avanza la jornada desde resultados.

Objetos pesados (UPS 36 kg) requieren dos estudiantes. Los golpes fuertes restan valor
(“-$X”) y el equipo frágil se rompe por debajo del 15 % de su valor; dentro del carrito
no se daña. Un golpe del velador te derriba. Consulta [TESTING.md](TESTING.md) para el
estado real de validación: las pruebas automáticas pasan, la sensación en juego necesita tu revisión.
