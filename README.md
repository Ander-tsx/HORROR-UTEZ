# HORROR-UTEZ · Turno nocturno

Juego cooperativo de terror en primera persona ambientado en la UTEZ (Universidad
Tecnológica Emiliano Zapata). De 1 a 5 estudiantes recuperan equipo del campus de noche,
lo cargan en un camión de mudanza y escapan mientras los vigilantes y El Rector los cazan.

- **Motor:** Unity 6 (6000.6.3f1) · C# · URP
- **Estética:** low-poly / PSX (vertex jitter, dithering, niebla, filtro CRT)
- **Plataformas:** Windows (prioridad) y Android

> **Todo el juego vive en Unity.** El mapa, los cuartos, los muebles, las luces y los
> puntos de juego se editan directamente en el editor. No hace falta Blender ni correr
> scripts para abrir, jugar o modificar el mapa.

---

## 1. Primeros pasos

### Requisitos

| Herramienta | Versión | Notas |
|---|---|---|
| Unity Hub + **Unity 6000.6.3f1** | exacta | Módulo *Windows Build Support (IL2CPP)*; *Android Build Support* solo si vas a compilar para Android |
| Git | cualquiera reciente | — |
| Blender | **no es necesario** | Solo si vas a modelar piezas nuevas (ver [sección 6](#6-modelar-piezas-nuevas-en-blender)) |

### Clonar y abrir

```bash
git clone https://github.com/Ander-tsx/HORROR-UTEZ.git
```

1. Abre Unity Hub → **Add** → **Add project from disk** → elige la carpeta `HORROR-UTEZ`
   que se acaba de clonar (la que contiene `Assets/`, `Packages/` y `ProjectSettings/`).
2. Si Hub pide instalar la versión 6000.6.3f1, acéptalo.
3. La primera vez tarda varios minutos en importar. Es normal.

### Jugar

Abre `Assets/_Project/Scenes/remake.unity` (o el menú **HORROR-UTEZ → Abrir escena
jugable**) y pulsa **Play**. Aparece el menú principal: *Explorar solo* para probar.

Controles: WASD moverse · Shift correr · Espacio saltar · Ctrl agacharse/barrida · Q rodar ·
clic/G mantener para agarrar · rueda distancia · R girar objeto · E usar · F linterna.

---

## 2. Estructura del proyecto

```
Assets/_Project/
  Scenes/
    remake.unity          ← EL JUEGO. Carga las zonas del campus (prefabs) y el sistema de juego
    remake_shop.unity     ← La tienda entre turnos (se carga encima de remake.unity)
    Legacy/utez.unity     ← Versión antigua de un jugador. No se usa; no editar
  Prefabs/
    Map/                  ← EL MAPA, dividido en zonas (ver sección 3)
    Props/  Landmarks/  Player/
  Art/
    Environment/          ← Edificios y piezas del kit (FBX) + texturas
    Remake/               ← Muebles, botín, camión, personajes, materiales, audio
      Resources/Lab/      ← Muebles de laboratorio listos para colocar (FBX)
      Materials/          ← Materiales PSX (uno por textura)
      Source~/            ← Originales .blend de personajes y piezas (Unity los ignora)
  Scripts/
    Remake/               ← Código del juego (red, jugadores, enemigos, botín, HUD…)
    Remake/Map/           ← Componentes "marcador" que el mapa usa para el gameplay
    Rendering/  World/  Core/  Player/  UI/
Documentation/Remake/     ← Arquitectura, pruebas, cómo jugar, importar modelos
Tools/unity/BuildRemake.ps1  ← Compilar desde la terminal (opcional)
```

---

## 3. Cómo está armado el mapa

`remake.unity` es casi vacía: solo referencia **prefabs de zona**. Cada zona es un archivo
independiente, así que dos personas pueden trabajar en zonas distintas sin pisarse.

| Prefab (`Assets/_Project/Prefabs/Map/`) | Contiene | Ejemplos de tareas |
|---|---|---|
| `Zona_CECADEC` | Edificio CECADEC (fachada, interior, planta alta), compuaulas amuebladas, puerta de vidrio automática este, fluorescentes | Rediseñar un aula, agregar un laboratorio, decorar pasillos |
| `Zona_CDS` | Edificio CDS con oficinas en ambas plantas | Amueblar oficinas, abrir la escalera |
| `Zona_Auditorio` | Auditorio, butacas, escenario | Iluminación del escenario, utilería |
| `Zona_Techado` | Techado/explanada | Detalles del techado |
| `Terreno` | Suelo, plancha de concreto, banquetas, límites | Caminos, jardineras, nuevas áreas |
| `Bosque` | Árboles, pasto, rocas, sotobosque | Densidad del bosque, claros |
| `Props_Campus` | Postes de luz, bancas, extintores, letrero de inventario, equipo suelto | Ambientación exterior |
| `Gameplay_Marcadores` | Puntos de botín, cuartos, patrullas, ruta de El Rector | Balance y diseño de juego (sección 4) |

**Para editar una zona:** doble clic en el prefab (o selecciónalo en la jerarquía de
`remake.unity` → **Open** en el Inspector). Se abre en *Prefab Mode*: edita, guarda
(Ctrl+S) y sal con la flecha `<` de la jerarquía. Si editas una zona directamente en la
escena, usa **Overrides → Apply All** para guardar los cambios en el prefab.

### Construir geometría con ProBuilder

ProBuilder ya viene instalado: **Tools → ProBuilder → ProBuilder Window**.

- **Muros, cuartos, escaleras y rampas nuevos:** crea formas con *New Shape*, ajusta caras y
  vértices, y aplica materiales de `Art/Remake/Materials` o `Art/Materials/Kit`
  arrastrándolos a las caras. Mantén la escala real: 1 unidad = 1 metro (los techos de CECADEC están a 2.7 m; cada planta mide 4 m).
- **Modificar una malla que ya existe** (por ejemplo, un muro de CECADEC): selecciónala y usa
  **ProBuilderize**. Hazlo solo con la pieza que necesites, no con edificios enteros: la
  malla pasa a guardarse dentro del prefab y lo hace más pesado.
- Agrega siempre un **collider** (Mesh o Box) a la geometría que se puede pisar o chocar.

### Colocar muebles y props

Arrastra modelos desde `Art/Remake/Resources/Lab/` (escritorios, PCs, sillas, racks…) o
`Prefabs/Props/`, y ponlos dentro de la zona correspondiente.

- Muebles que estorban: agrega un **Box Collider** y ponlos en el layer **RemakeScenery**.
  La navegación de los enemigos los trata como obstáculos y nunca como piso.
- Muebles que no se mueven: márcalos como **Static** (casilla arriba a la derecha del
  Inspector) para que se agrupen al dibujar.
- El botín que los jugadores agarran **no** se coloca a mano: aparece en los puntos de
  botín (sección 4).

---

## 4. Marcadores de gameplay

El código lee estos componentes de la escena al iniciar la partida. Son GameObjects vacíos
con un componente; en la vista *Scene* se dibujan como íconos de colores. Para mover un
punto, muévelo; para agregar uno, duplica uno existente (Ctrl+D).

| Componente | Dónde | Qué hace | Reglas |
|---|---|---|---|
| `RemakeLootSpot` (amarillo) | `Gameplay_Marcadores/Botín · día 1` | Donde aparece cada objeto valioso el primer día | La **Key** debe conservarse (la usa la red). Hay 13; el objeto cae al piso bajo el punto |
| `RemakeRoom` (verde) | `Gameplay_Marcadores/Cuartos` | Centro de un cuarto: candidato para el botín de días siguientes, luces del turno y aparición de enemigos | Ponlo en piso caminable, al centro. Agregar cuartos nuevos los incluye automáticamente |
| `RemakePatrolPoint` (morado/azul) | `Patrulla interior` / `Patrulla exterior` | Destinos de patrulla de los enemigos | Interior: solo planta baja. Exterior: los **4 primeros** (orden de la jerarquía) son la plaza |
| `RemakeGiantWaypoint` (rojo) | `Ruta de El Rector (en orden)` | El recorrido del gigante por el campus | Sigue el **orden de la jerarquía**; las paradas inalcanzables se descartan solas y se reportan en la consola |
| `RemakeDoorway` (naranja) | Dentro de las zonas | Pasos que la navegación debe considerar abiertos (un hueco en un muro, un panel corrido) | Ajusta **Size** para cubrir el hueco |
| `RemakeFlickerLight` | Fluorescentes de las zonas | Luz que parpadea, se corta y baja cuando El Rector se acerca | Agrégalo a cualquier *Point Light*; **Hum** activa el zumbido |
| `RemakeSlidingDoor` | Entrada este de CECADEC | Puerta de vidrio automática | Las hojas se deslizan sobre el eje Z local |
| `RemakeAnchor` | `Gameplay_Marcadores/Anclas` | Puntos de referencia para el código y las pruebas (`corridor`) | No borrar |

Las puertas con bisagra usan el componente `HingedDoor` (ya en las zonas) y también abren
paso en la navegación.

---

## 5. Lo que sigue en código

Las entidades que se sincronizan por red se crean por código al iniciar, a partir de
modelos y prefabs: estudiantes, enemigos, objetos valiosos, credenciales y el camión.
Detalles en [ARCHITECTURE.md](Documentation/Remake/ARCHITECTURE.md).

| Si vas a trabajar en… | Archivos principales |
|---|---|
| Movimiento, cámara, brazos | `RemakeStudent.cs`, `RemakeCameraRig.cs`, `RemakeHandRig.cs` |
| Enemigos | `RemakeEnemy.cs`, `RemakeBody.cs`, `RemakeBestiary.cs` |
| Botín, física de agarre, daño | `RemakeLoot.cs`, `RemakeLootCatalog.cs` |
| Reglas, días, cuota, camión, navegación | `RemakeGame.cs`, `RemakeDayCycle.cs` |
| Tienda y mejoras | `RemakeShop.cs` + escena `remake_shop.unity` |
| HUD y menú | `RemakeHud.cs` |
| Red y voz | `RemakeWire.cs`, `RemakeVoice.cs` |
| Audio | `RemakeAudio.cs` |

---

## 6. Modelar piezas nuevas en Blender

Blender sirve para piezas pequeñas (un mueble, un objeto valioso, un personaje); el mapa se
arma en Unity. Exporta **FBX**, en metros y con nombres de material iguales a los de
`Art/Remake/Materials` para que se asignen solos. Guía completa:
[BLENDER.md](Documentation/Remake/BLENDER.md).

---

## 7. Trabajo en equipo

- **Rama principal: `main`.** Cada tarea en su propia rama (`mapa/cecadec-aula3`,
  `gameplay/nuevo-enemigo`) y Pull Request hacia `main`.
- **Una persona por zona a la vez.** Los prefabs de Unity no se fusionan bien. Antes de
  editar una zona, avisa al equipo o asígnatela en el tablero de tareas.
- **Evita editar `remake.unity`** salvo para agregar o quitar una zona entera. Lo demás vive
  en los prefabs.
- **Siempre sube los `.meta`** junto con cada archivo nuevo. Nunca subas `Library/`,
  `Temp/`, `Logs/` ni `Builds/` (ya están en `.gitignore`).
- **Merge de escenas/prefabs (recomendado):** configura UnityYAMLMerge una vez en tu
  máquina:

  ```bash
  git config merge.unityyamlmerge.name "Unity SmartMerge"
  git config merge.unityyamlmerge.driver "'C:/Program Files/Unity/Hub/Editor/6000.6.3f1/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p %O %B %A %A"
  git config merge.unityyamlmerge.recursive binary
  ```

  `.gitattributes` ya asigna ese driver a `.unity`, `.prefab`, `.asset` y `.mat`.
- Antes de abrir un PR: dale **Play** y juega una partida corta con *Explorar solo*.
  Verifica que no aparezcan errores rojos en la consola.

---

## 8. Compilar y probar

- **Desde el editor:** menú **HORROR-UTEZ → Build → Windows**. El ejecutable queda en
  `Builds/Remake/Windows/HORROR-UTEZ.exe`.
- **Desde la terminal:** `./Tools/unity/BuildRemake.ps1` (usa `-Isolated` si tienes el
  editor abierto).
- **Prueba automática** (recorre una partida completa y termina con código 0 si todo pasa):

  ```powershell
  ./Builds/Remake/Windows/HORROR-UTEZ.exe -remakeSolo -remakeSmoke -logFile smoke.log
  ```

  Busca `[RemakeSmoke] ALL COMPLETE` en el log. Las pruebas en red (host/cliente) y el tour
  con capturas están en [TESTING.md](Documentation/Remake/TESTING.md).

Otros menús útiles: **HORROR-UTEZ → Reparar configuración del proyecto** (layers, URP y
escenas del build) y **Crear materiales PSX para texturas nuevas**.

---

## Más documentación

- [Cómo jugar y hospedar](Documentation/Remake/PLAY.md)
- [Arquitectura del código](Documentation/Remake/ARCHITECTURE.md)
- [Pruebas y resultados](Documentation/Remake/TESTING.md)
- [Importar modelos desde Blender](Documentation/Remake/BLENDER.md)
- Para agentes de IA (Claude Code / Codex): [AGENTS.md](AGENTS.md) y [HANDOFF.md](Documentation/Remake/HANDOFF.md)
