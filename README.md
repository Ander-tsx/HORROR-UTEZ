# HORROR-UTEZ · Turno nocturno

Juego cooperativo de terror en primera persona ambientado en la UTEZ (Universidad
Tecnológica Emiliano Zapata). De 1 a 5 estudiantes entran de noche al campus para
recuperar equipo valioso (laptops, proyectores, microscopios…), lo cargan en un camión de
mudanza y escapan antes de que los vigilantes y **El Rector**, un gigante que recorre el
campus, los atrapen.

- **Motor:** Unity 6 (versión exacta **6000.6.3f1**) · lenguaje C# · URP
- **Estética:** low-poly / PSX, como los juegos de PlayStation 1 (pixelado, tambaleo de
  vértices, niebla, filtro de televisor viejo)
- **Plataformas:** Windows (prioridad) y Android

> **Esta guía es para empezar desde cero.** No necesitas saber de videojuegos, Unity,
> modelado 3D ni Git. Léela en orden la primera vez; después úsala como consulta con el
> índice. Si algo no queda claro, pregunta al equipo y ayúdanos a mejorar este documento.

---

## Índice

1. [El juego en 2 minutos](#1-el-juego-en-2-minutos)
2. [Conceptos básicos (glosario para principiantes)](#2-conceptos-básicos-glosario-para-principiantes)
3. [Instalar tu entorno paso a paso](#3-instalar-tu-entorno-paso-a-paso)
4. [Descargar y abrir el proyecto](#4-descargar-y-abrir-el-proyecto)
5. [Conocer el editor de Unity](#5-conocer-el-editor-de-unity)
6. [Jugar el juego](#6-jugar-el-juego)
7. [Estructura del proyecto](#7-estructura-del-proyecto)
8. [Cómo está armado el mapa](#8-cómo-está-armado-el-mapa)
9. [Tutorial: tu primera modificación de principio a fin](#9-tutorial-tu-primera-modificación-de-principio-a-fin)
10. [Modelar directamente en Unity con ProBuilder](#10-modelar-directamente-en-unity-con-probuilder)
11. [Terreno: suelos, caminos, jardines y colinas](#11-terreno-suelos-caminos-jardines-y-colinas)
12. [Muebles, props y vegetación](#12-muebles-props-y-vegetación)
13. [Luces y ambiente](#13-luces-y-ambiente)
14. [Materiales y texturas](#14-materiales-y-texturas)
15. [Modelar en Blender y llevarlo a Unity](#15-modelar-en-blender-y-llevarlo-a-unity)
16. [Marcadores de gameplay](#16-marcadores-de-gameplay)
17. [Reglas para que los enemigos puedan moverse por tu mapa](#17-reglas-para-que-los-enemigos-puedan-moverse-por-tu-mapa)
18. [Programar: dónde está cada cosa](#18-programar-dónde-está-cada-cosa)
19. [Git y trabajo en equipo](#19-git-y-trabajo-en-equipo)
20. [Compilar y probar](#20-compilar-y-probar)
21. [Problemas frecuentes](#21-problemas-frecuentes)
22. [Más documentación](#22-más-documentación)

---

## 1. El juego en 2 minutos

Una partida ("turno") funciona así:

1. Los estudiantes aparecen junto al **camión** en la plaza de la UTEZ.
2. Hay una **cuota** de dinero (por ejemplo $1600) y un tiempo límite de 8 minutos.
3. Los estudiantes recorren los edificios (CECADEC, CDS, auditorio) buscando **equipo
   valioso**. Lo agarran con unos brazos elásticos y lo llevan al camión. Si lo golpean,
   pierde valor; si se golpea mucho, se rompe.
4. Los **enemigos** (vigilantes y El Rector) patrullan, escuchan ruidos y persiguen a quien
   vean. Si te atrapan, caes y dejas tu credencial; un compañero puede llevarla al camión
   para revivirte.
5. Con la cuota pagada, los estudiantes suben al camión y se van. Pasan a la **tienda**,
   compran mejoras y empieza el día siguiente, con una cuota más alta.

Todo eso ya funciona. El trabajo que sigue es **hacer el mapa más grande, más detallado y
más aterrador**, y mejorar la jugabilidad.

---

## 2. Conceptos básicos (glosario para principiantes)

No hace falta memorizarlo; vuelve aquí cuando aparezca una palabra que no conozcas.

### Videojuegos y Unity

| Término | Qué es |
|---|---|
| **Motor de videojuegos** | Programa que ya resuelve lo difícil (dibujar 3D, física, sonido, controles) para que tú armes el juego. Usamos **Unity**. |
| **Unity Hub** | Programa que instala versiones de Unity y abre proyectos. |
| **Proyecto** | La carpeta del juego. En Unity todo lo tuyo vive en `Assets/`. |
| **Escena** (`.unity`) | Un "nivel" o espacio del juego. Nuestro juego vive en `remake.unity`. |
| **GameObject** | Cualquier cosa dentro de una escena: un muro, una luz, una silla, un punto invisible. Por sí solo no hace nada; lo que hace depende de sus componentes. |
| **Componente** | Pieza que le da una función a un GameObject: *Mesh Renderer* lo dibuja, *Collider* lo hace sólido, *Light* ilumina, un script le da comportamiento. |
| **Transform** | El componente que todo GameObject tiene: **posición**, **rotación** y **escala**. |
| **Jerarquía** | Los GameObjects se ordenan como carpetas: un objeto "hijo" se mueve junto con su "padre". |
| **Prefab** (`.prefab`) | Un GameObject (con sus hijos) guardado como archivo reutilizable. Si editas el prefab, cambian todas sus copias. Nuestras zonas del mapa son prefabs. |
| **Asset** | Cualquier archivo del proyecto: modelos, texturas, sonidos, scripts, escenas, prefabs. |
| **Malla / mesh** | La forma 3D de un objeto: puntos (**vértices**) unidos por líneas (**aristas**) que forman **caras** (polígonos). |
| **Low-poly** | Modelos con pocas caras. Es nuestro estilo y además corre bien en celulares. |
| **Material** | Define cómo se ve una superficie: color, textura, brillo. Se aplica a las caras de una malla. |
| **Textura** | Imagen (`.png`) que se "pega" sobre una malla mediante un material. |
| **UV** | Las instrucciones de cómo se estira una textura sobre las caras. Una UV mal hecha estira o deforma la textura. |
| **Shader** | Programa que dibuja un material. Usamos `HorrorUtez/PSX/Lit` para el estilo PSX. |
| **Collider** | Forma invisible que hace sólido un objeto (para que no lo atravieses). Sin collider, caes a través del piso. |
| **Rigidbody** | Componente que hace que un objeto obedezca la física (caer, rodar, empujarse). |
| **Layer (capa)** | Etiqueta que agrupa objetos para que la física o el código los traten distinto. |
| **Script** (`.cs`) | Código en C# que da comportamiento: mover al jugador, hacer que un enemigo persiga. |
| **Play Mode** | Cuando presionas ▶ Play en Unity, el juego corre dentro del editor. **Lo que cambies durante Play se pierde al detenerlo.** |
| **Build** | El juego compilado como ejecutable (`.exe`) para jugarlo sin Unity. |
| **FBX** (`.fbx`) | Formato estándar de modelos 3D. Lo entienden Unity y Blender. Todos nuestros modelos están en FBX. |
| **`.meta`** | Archivo que Unity crea junto a cada asset con su identificador único. **Siempre se sube a Git junto con el asset.** |

### Modelado 3D

| Término | Qué es |
|---|---|
| **Blender** | Programa gratuito de modelado 3D. En este proyecto es opcional: sirve para piezas pequeñas. |
| **ProBuilder** | Herramienta de modelado **dentro de Unity**. Con ella se construyen muros, cuartos, escaleras y suelos. Ya viene instalada. |
| **Extruir** | Jalar una cara para "sacarla" y crear volumen (por ejemplo, levantar un muro desde el piso). |
| **Normal** | Hacia dónde "mira" una cara. Las caras se ven solo desde el frente; si una cara se ve invisible, su normal está al revés. |
| **Pivote / origen** | El punto desde donde se mueve y rota un objeto. Para muebles conviene en la base. |

### Git

| Término | Qué es |
|---|---|
| **Git** | Sistema que guarda el historial de cambios del proyecto y permite trabajar en equipo. |
| **GitHub** | Sitio web donde vive nuestro repositorio: <https://github.com/Ander-tsx/HORROR-UTEZ> |
| **Repositorio (repo)** | El proyecto con todo su historial. |
| **Clonar** | Descargar el repositorio a tu computadora la primera vez. |
| **Commit** | Una "foto" de tus cambios con un mensaje que los describe. |
| **Push** | Subir tus commits a GitHub. |
| **Pull** | Bajar los cambios que otros subieron. |
| **Rama (branch)** | Línea de trabajo paralela. Cada tarea se hace en su propia rama para no estorbar a los demás. |
| **Pull Request (PR)** | Solicitud en GitHub para unir tu rama a `main`. Otro compañero la revisa antes. |
| **Conflicto** | Cuando dos personas cambiaron lo mismo y Git no sabe con cuál quedarse. |

---

## 3. Instalar tu entorno paso a paso

### 3.1 Qué instalar según tu rol

| Programa | Para quién | Obligatorio |
|---|---|---|
| Git (o GitHub Desktop) | Todos | Sí |
| Unity Hub + Unity **6000.6.3f1** | Todos | Sí |
| Visual Studio Community 2022 (o Rider / VS Code) | Quien programe | Para programar |
| Blender 4.2 o más reciente | Quien modele piezas | Opcional |

### 3.2 Cuenta de GitHub y acceso

1. Crea una cuenta gratuita en <https://github.com>.
2. Pásale tu usuario al dueño del repositorio para que te agregue como colaborador.
3. Acepta la invitación que te llega por correo.

### 3.3 Git

Tienes dos opciones. Si nunca has usado una terminal, empieza con GitHub Desktop.

- **GitHub Desktop (más fácil, con botones):** descárgalo de <https://desktop.github.com>,
  instálalo e inicia sesión con tu cuenta de GitHub.
- **Git en terminal:** descarga Git para Windows de <https://git-scm.com/download/win> e
  instálalo con las opciones por defecto. Después, en una terminal (Git Bash o PowerShell),
  configura tu nombre y correo, que aparecerán en tus commits:

  ```bash
  git config --global user.name "Tu Nombre"
  git config --global user.email "tu-correo@ejemplo.com"
  ```

### 3.4 Unity

1. Descarga **Unity Hub** de <https://unity.com/download> e instálalo.
2. Abre Unity Hub e inicia sesión. Crea una cuenta de Unity gratuita si no tienes;
   la licencia *Personal* es suficiente.
3. Instala la versión exacta **6000.6.3f1**. Otra versión puede romper el proyecto o
   modificar archivos de todos.
   - En Unity Hub: **Installs → Install Editor**. Si 6000.6.3f1 no aparece en la lista, búscala en
     la pestaña **Archive** o en <https://unity.com/releases/editor/archive>; el enlace
     abre Unity Hub con esa versión.
   - Al elegir módulos, marca:
     - **Microsoft Visual Studio Community** (si vas a programar y no lo tienes).
     - **Windows Build Support (IL2CPP)**.
     - **Android Build Support** (solo si vas a compilar para celular).
     - Opcional: **Documentation**.
4. Espera a que termine; son varios GB.

### 3.5 Editor de código (solo si vas a programar)

1. Instala Visual Studio Community 2022 con la carga de trabajo **"Desarrollo de juegos
   con Unity"**. Si lo instalaste desde Unity Hub, ya está.
2. En Unity: **Edit → Preferences → External Tools → External Script Editor** → elige
   Visual Studio, Rider o VS Code.
3. Doble clic en cualquier script `.cs` dentro de Unity lo abre en tu editor.

### 3.6 Blender (opcional)

Descárgalo de <https://www.blender.org/download/> (4.2 LTS o más reciente) e instálalo.
Solo lo necesitas si vas a modelar piezas fuera de Unity (sección 15).

---

## 4. Descargar y abrir el proyecto

### 4.1 Clonar (solo la primera vez)

**Con GitHub Desktop:** *File → Clone repository* → pestaña *GitHub.com* → elige
`Ander-tsx/HORROR-UTEZ` → elige una carpeta local, por ejemplo `C:\Proyectos` → *Clone*.

**Con terminal:**

```bash
cd C:/Proyectos
git clone https://github.com/Ander-tsx/HORROR-UTEZ.git
```

Se crea la carpeta `HORROR-UTEZ`. Esa carpeta **es** el proyecto de Unity: contiene
`Assets/`, `Packages/` y `ProjectSettings/`. Usa rutas sin acentos ni espacios raros y
cortas; Windows a veces falla con rutas muy largas.

### 4.2 Abrir en Unity

1. Unity Hub → **Projects → Add → Add project from disk** → elige la carpeta `HORROR-UTEZ`.
2. Verifica que diga la versión **6000.6.3f1** y ábrelo.
3. **La primera apertura tarda bastante** (de 5 a 20 minutos): Unity importa todos los
   modelos y texturas y descarga los paquetes (URP, ProBuilder, Input System). Es normal.
   Las siguientes aperturas son rápidas.
4. Si Unity pregunta por **Safe Mode**, hay errores de código: elige *Ignore* y avisa al
   equipo (ver [Problemas frecuentes](#21-problemas-frecuentes)).

### 4.3 Mantenerte al día

Antes de empezar a trabajar cada día:

- **GitHub Desktop:** botón **Fetch origin** y después **Pull origin**.
- **Terminal:** `git pull`.

Después de hacer pull **no hay que correr ningún script ni menú**. Abre Unity, abre la
escena y presiona Play: ya tienes la versión más reciente. Si Unity estaba abierto durante
el pull, espera a que termine de reimportar (barra de progreso abajo a la derecha).

---

## 5. Conocer el editor de Unity

### 5.1 Las ventanas principales

```
┌──────────────┬────────────────────────────────┬──────────────┐
│  Hierarchy   │   Scene / Game                 │  Inspector   │
│ (lo que hay  │  (la vista 3D / el juego)      │ (propiedades │
│  en la escena)│                               │  de lo       │
│              │                                │  seleccionado)│
├──────────────┴────────────────────────────────┴──────────────┤
│  Project (archivos del proyecto)   │   Console (mensajes)    │
└────────────────────────────────────┴─────────────────────────┘
```

- **Hierarchy:** lista de todos los GameObjects de la escena abierta, con sus padres e hijos.
- **Scene:** la vista 3D donde construyes. Aquí mueves y colocas cosas.
- **Game:** lo que ve el jugador. Se activa al presionar Play.
- **Inspector:** muestra y edita los componentes del objeto seleccionado.
- **Project:** el explorador de archivos del proyecto (`Assets/…`).
- **Console:** mensajes, advertencias (amarillo) y errores (rojo). Si algo falla, mira aquí
  primero. Si no la ves: **Window → General → Console**.

Si desacomodas las ventanas: **Window → Layouts → Default**.

### 5.2 Moverte en la vista Scene

| Acción | Cómo |
|---|---|
| Volar como en un juego | Mantén **clic derecho** y usa **W A S D** (Q/E bajar/subir). Shift para ir más rápido |
| Orbitar alrededor | **Alt + clic izquierdo** y arrastra |
| Desplazar la vista | **Clic central** (rueda) y arrastra |
| Acercar / alejar | **Rueda del mouse** |
| Enfocar un objeto | Selecciónalo y presiona **F** (o doble clic en la Hierarchy) |
| Vista desde arriba | Clic en el eje **Y** del gizmo de la esquina superior derecha |

### 5.3 Herramientas para mover objetos

| Tecla | Herramienta |
|---|---|
| **W** | Mover (flechas de colores: rojo = X, verde = Y arriba, azul = Z) |
| **E** | Rotar |
| **R** | Escalar |
| **T** | Rect (para UI y planos) |
| **Mantener Ctrl** al arrastrar | Mover en pasos fijos (por defecto 0.25 m) |
| **Mantener V** al arrastrar | Pegar un vértice del objeto a un vértice de otro. Sirve para poner muebles sobre el piso o pegar muros exactos |
| **Ctrl + D** | Duplicar |
| **Supr** | Borrar |
| **Ctrl + Z / Ctrl + Y** | Deshacer / rehacer |
| **Ctrl + S** | Guardar la escena o el prefab abierto |

### 5.4 Reglas de oro del editor

1. **No edites con el juego en Play.** Todo cambio hecho en Play Mode se pierde al detenerlo.
   El botón ▶ se pinta de azul mientras corre.
2. **Guarda seguido** (Ctrl+S). Un asterisco `*` junto al nombre de la escena o prefab
   indica cambios sin guardar.
3. **Unidades reales:** 1 unidad de Unity = **1 metro**.
4. **Activa los Gizmos** (botón *Gizmos* arriba de la vista Scene) para ver los marcadores
   de juego y las luces.

---

## 6. Jugar el juego

1. En la ventana Project abre `Assets/_Project/Scenes/remake.unity` (doble clic), o usa el
   menú superior **HORROR-UTEZ → Abrir escena jugable**.
2. Presiona **▶ Play**. Aparece el menú principal.
3. **Explorar solo** para jugar sin red. **Hospedar** para que otros se unan, y **Unirme**
   con la IP del anfitrión (puerto TCP 27777, en la misma red). Más detalles en
   [PLAY.md](Documentation/Remake/PLAY.md).
4. Presiona ▶ otra vez para detener.

**Controles:** WASD moverse · Shift correr · Espacio saltar · Ctrl agacharse/barrida ·
Q rodar · clic izquierdo o G **mantener** para agarrar (soltar para dejar) · rueda del mouse
distancia del objeto · R girar el objeto · E usar · F linterna · V hablar (si activaste el
micrófono).

---

## 7. Estructura del proyecto

```
HORROR-UTEZ/
├── Assets/_Project/              ← TODO lo nuestro
│   ├── Scenes/
│   │   ├── remake.unity          ← EL JUEGO: carga las zonas del mapa y el sistema de juego
│   │   ├── remake_shop.unity     ← La tienda entre días (se carga encima del campus)
│   │   └── Legacy/utez.unity     ← Versión antigua de un jugador. No se usa; no editar
│   ├── Prefabs/
│   │   ├── Map/                  ← EL MAPA dividido en zonas (sección 8)
│   │   ├── Landmarks/            ← Edificios completos (CECADEC) usados dentro de las zonas
│   │   ├── Props/                ← Props listos para colocar (bancas, extintores…)
│   │   └── Player/               ← El modelo del estudiante
│   ├── Art/
│   │   ├── Environment/
│   │   │   ├── Kit/              ← Piezas de arquitectura (muro 4 m, ventana, columna…) en FBX
│   │   │   └── Landmarks/        ← Modelos grandes de CECADEC en FBX
│   │   ├── Materials/Kit/        ← Materiales de arquitectura y terreno (Kit_*)
│   │   ├── Textures/             ← Texturas de arquitectura y terreno
│   │   ├── Characters/           ← Personaje del estudiante (FBX + original .blend en Source~)
│   │   └── Remake/
│   │       ├── Resources/Lab/    ← Muebles de laboratorio/oficina (FBX) listos para colocar
│   │       ├── Resources/Audio/  ← Música y sonidos
│   │       ├── Materials/        ← Materiales PSX de muebles, botín y personajes
│   │       ├── Textures/         ← Sus texturas
│   │       ├── Baked/            ← Mallas generadas al migrar el mapa (no tocar)
│   │       ├── *.fbx             ← Botín, camión, enemigos
│   │       └── Source~/          ← Originales .blend (Unity ignora carpetas que terminan en ~)
│   ├── Scripts/
│   │   ├── Remake/               ← Código del juego (red, jugadores, enemigos, botín, HUD…)
│   │   ├── Remake/Map/           ← Componentes "marcador" del mapa (sección 16)
│   │   └── Core/ Rendering/ World/ Player/ UI/   ← Sistemas base (estilo PSX, clima, puertas…)
│   ├── Shaders/                  ← Shaders PSX
│   └── Settings/                 ← Configuración de URP (render)
├── Packages/                     ← Lista de paquetes de Unity (no tocar a mano)
├── ProjectSettings/              ← Configuración del proyecto (no tocar a mano)
├── Documentation/Remake/         ← Documentación técnica
└── Tools/unity/BuildRemake.ps1   ← Compilar desde la terminal (opcional)
```

Carpetas que **no** están en Git porque Unity las genera solo: `Library/`, `Temp/`, `Logs/`,
`UserSettings/`, `Builds/`.

---

## 8. Cómo está armado el mapa

### 8.1 Escena + zonas

`remake.unity` casi no contiene nada propio: carga varios **prefabs de zona**. Cada zona es un
archivo independiente, así que dos personas pueden trabajar en zonas distintas al mismo
tiempo sin pisarse.

| Prefab (`Assets/_Project/Prefabs/Map/`) | Qué contiene | Ejemplos de tareas |
|---|---|---|
| `Zona_CECADEC` | Edificio CECADEC (fachada, interior, planta alta), compuaulas amuebladas ("Compuaulas · ambientación"), puerta de vidrio automática este, fluorescentes | Rediseñar un aula, agregar un laboratorio, decorar pasillos, más sustos |
| `Zona_CDS` | Edificio CDS con oficinas en ambas plantas | Amueblar oficinas, abrir la escalera |
| `Zona_Auditorio` | Auditorio con butacas y escenario | Iluminación del escenario, utilería |
| `Zona_Techado` | Techado / explanada | Detalles del techado |
| `Terreno` | Suelo de tierra, plancha de concreto, banquetas, jardineras, límites | Caminos, áreas nuevas, desniveles |
| `Bosque` | Árboles, palmeras, pasto, rocas y sotobosque alrededor del campus | Densidad, claros, senderos |
| `Props_Campus` | Postes de luz, bancas, extintores, letrero de inventario, equipo suelto | Ambientación exterior |
| `Gameplay_Marcadores` | Puntos de botín, cuartos, patrullas y ruta de El Rector | Diseño y balance del juego (sección 16) |

Fuera de las zonas, en la escena, quedan: la luz del sol/luna (*Directional Light*), el
*Global Volume* (efectos de cámara), *Weather* (clima, lluvia, rayos), *UI* y
**"REMAKE · Turno nocturno"**, el objeto con el script `RemakeGame` que maneja la partida.

### 8.2 Lo que NO está en la escena

Al presionar Play, el código crea: los estudiantes, los enemigos, los objetos valiosos, las
credenciales, el camión, el menú y el HUD. Por eso no los verás en la Hierarchy mientras
editas. Su posición inicial se controla con los marcadores (sección 16).

### 8.3 Prefab Mode: cómo editar una zona

1. En la ventana Project ve a `Assets/_Project/Prefabs/Map/` y haz **doble clic** en la zona
   (por ejemplo `Zona_CECADEC`). También puedes seleccionarla en la Hierarchy de `remake.unity`
   y presionar **Open** en el Inspector, o usar la flechita `>` junto a su nombre.
2. La vista cambia a **Prefab Mode**: solo ves esa zona, con un fondo distinto y una barra
   azul arriba.
3. Edita lo que necesites.
4. Guarda con **Ctrl+S** (o deja activado *Auto Save* en la barra superior de la vista Scene).
5. Sal con la flecha **`<`** arriba de la Hierarchy.

Si editas la zona directamente en la escena (sin entrar a Prefab Mode), los cambios quedan como
*overrides*: el nombre se pone en negritas y aparece una barra azul a la izquierda.
Para guardarlos en el prefab, selecciona la raíz de la zona → **Overrides** (arriba del
Inspector) → **Apply All**. Si no lo haces, el cambio queda en `remake.unity` y puede chocar con
el trabajo de otros.

> **Regla del equipo:** edita las zonas en Prefab Mode y deja `remake.unity` sin tocar,
> salvo para agregar o quitar una zona completa.

### 8.4 Crear una zona nueva

1. En `remake.unity`, clic derecho en la Hierarchy → **Create Empty**. Nómbralo, por ejemplo,
   `Zona_Biblioteca`, y ponlo como hijo de `UTEZ_Buildings` si es un edificio.
2. Construye dentro de él (ProBuilder, muebles, luces).
3. Arrástralo desde la Hierarchy a `Assets/_Project/Prefabs/Map/`. Eso lo convierte en prefab.
4. Guarda la escena (este es uno de los pocos casos en que sí se modifica `remake.unity`;
   avisa al equipo).

---

## 9. Tutorial: tu primera modificación de principio a fin

Objetivo: agregar un escritorio con una computadora en un aula de CECADEC y subir el cambio.
Así practicas todo el flujo.

**1. Bajar lo último y crear tu rama**

- GitHub Desktop: *Fetch / Pull* → menú *Branch → New branch* → nombre
  `mapa/mi-primer-escritorio` → *Create branch*.
- Terminal:
  ```bash
  git checkout main
  git pull
  git checkout -b mapa/mi-primer-escritorio
  ```

**2. Abrir la zona**

Abre Unity → Project → `Assets/_Project/Prefabs/Map/Zona_CECADEC` (doble clic).

**3. Ubicarte**

En la Hierarchy despliega `Compuaulas · ambientación`; ahí están los muebles de las aulas.
Selecciona cualquier escritorio y presiona **F** para enfocarlo. Vuela con clic derecho + WASD
hasta un espacio libre dentro de un aula.

**4. Colocar el escritorio**

1. En Project ve a `Assets/_Project/Art/Remake/Resources/Lab/`.
2. Arrastra `Lab_Desk` a la vista Scene, dentro del aula.
3. Arrástralo en la Hierarchy para que quede como hijo de `Compuaulas · ambientación`.
4. Ajústalo con **W** (mover) y **E** (rotar). Mantén **V** y arrastra desde una esquina de la
   base para pegarlo exactamente al piso.
5. Arrastra `Lab_CRT` (un monitor) encima del escritorio, y `Lab_Chair` delante.

**5. Hacerlo sólido y correcto**

Con el escritorio seleccionado, en el Inspector:

1. **Add Component → Box Collider**. Ajusta *Size/Center* si no cubre el mueble. El botón
   *Edit Collider* permite arrastrar sus caras.
2. Arriba a la derecha, **Layer → RemakeScenery**. Cuando pregunte, elige "Yes, change
   children".
3. Marca la casilla **Static** (arriba a la derecha) → "Yes, change children".

Repite el collider con la silla. El monitor sobre la mesa puede quedarse sin collider.

**6. Guardar y probar**

1. **Ctrl+S** para guardar el prefab. Sal con `<`.
2. Abre `remake.unity`, presiona **Play**, elige *Explorar solo* y camina hasta el aula
   (el indicador de arriba muestra la distancia al camión). Comprueba que chocas con el
   escritorio.
3. Detén Play. Revisa que la Console no tenga errores rojos nuevos.

**7. Subir tu cambio**

- GitHub Desktop: verás los archivos cambiados (`Zona_CECADEC.prefab`). Escribe un resumen
  ("Escritorio extra en Aula 1") → **Commit to mapa/mi-primer-escritorio** → **Push origin** →
  **Create Pull Request** (se abre GitHub).
- Terminal:
  ```bash
  git status                      # revisa qué cambió
  git add Assets/_Project/Prefabs/Map/Zona_CECADEC.prefab
  git commit -m "Escritorio extra en Aula 1"
  git push -u origin mapa/mi-primer-escritorio
  ```
  Después abre GitHub; aparece el botón **Compare & pull request**.

**8. Pull Request**

Describe qué hiciste (puedes pegar una captura), pide revisión a un compañero y, cuando lo
aprueben, presiona **Merge**. ¡Listo! Vuelve a `main` y haz pull antes de tu siguiente tarea.

---

## 10. Modelar directamente en Unity con ProBuilder

ProBuilder permite construir y editar geometría dentro de Unity, sin exportar nada. Es la
herramienta principal para **arquitectura**: muros, cuartos, pasillos, escaleras, rampas,
plataformas y suelos. Para objetos detallados u orgánicos (personajes, máquinas, muebles
complejos) es mejor Blender (sección 15).

### 10.1 Cómo se usa ProBuilder (versión 6)

ProBuilder 6 no tiene una ventana propia: todo se hace desde la vista Scene y el menú
**Tools → ProBuilder**.

- **Entrar al modo de edición:** selecciona un objeto de ProBuilder. En la barra de
  herramientas de la vista Scene (columna de botones a la izquierda: mover, rotar…), el botón
  **Tool Context** cambia entre *GameObject* y **ProBuilder**. Al elegir ProBuilder aparecen los
  **modos de selección**:
  - **Vértice:** mueves puntos.
  - **Arista:** mueves líneas.
  - **Cara:** mueves polígonos. Es el modo más útil para construir.
  Para volver a mover objetos completos, regresa el Tool Context a *GameObject*.
- **Acciones:** con algo seleccionado, haz **clic derecho en la vista Scene**: el menú contextual
  muestra las acciones de ProBuilder disponibles (Extrude, Subdivide, Flip Normals…). También
  están en **Tools → ProBuilder → Geometry / Object / Selection**.
- Si no ves las barras, actívalas con el menú ⋮ de la vista Scene → *Overlay Menu*, o presiona
  la tecla **`** (acento grave) con el mouse sobre la vista Scene.

### 10.2 Crear formas

**Tools → ProBuilder → Editors → Create Shape** → elige la forma (o **Ctrl+Shift+K** para un
cubo). También hay un botón *Create Shape* en la barra de herramientas de la vista Scene.
Después dibújala arrastrando en la vista Scene:

| Forma | Úsala para |
|---|---|
| **Cube** | Muros, columnas, mesas simples, plataformas |
| **Plane** | Pisos, techos, terreno plano |
| **Stairs** | Escaleras (ajusta escalones, altura y ancho) |
| **Door** | Muro con hueco de puerta incluido |
| **Arch** | Arcos |
| **Cylinder** | Columnas redondas, botes, tubos |
| **Prism / Pipe / Cone** | Techos a dos aguas, tuberías, conos |

Mientras la forma recién creada siga seleccionada, el panel de la herramienta permite cambiar
sus medidas y opciones (escalones, segmentos…). Para editarla más tarde:
**Tools → ProBuilder → Edit → Edit Shape**. También puedes usar la herramienta Escalar.

**PolyShape** (*Tools → ProBuilder → Editors → Create PolyShape*) permite dibujar la planta de
un cuarto con clics, punto por punto, y luego darle altura. Es ideal para pisos con forma
irregular. Para modificarla después: *Tools → ProBuilder → Edit → Edit PolyShape*.

### 10.3 Acciones básicas

Todas aparecen en el **clic derecho en la vista Scene** (en modo ProBuilder) o en
**Tools → ProBuilder → Geometry / Object**.

| Acción | Para qué | Cómo / atajo |
|---|---|---|
| **Extrude** | Sacar volumen de una cara o arista (levantar muros desde un piso, alargar un pasillo) | En modo Cara o Arista, mantén **Shift** mientras arrastras con la herramienta Mover, o **Ctrl+E** |
| **Smart Subdivide** | Partir caras en más caras para poder recortar | **Alt+S** |
| **Insert Edge Loop** | Agregar un corte alrededor de una pieza | Selecciona una arista → **Alt+U** |
| **Smart Connect** | Unir vértices o aristas seleccionados con nuevas aristas | **Alt+E** |
| **Delete Faces** | Borrar caras (por ejemplo, para abrir un hueco) | Selecciona caras → **Retroceso** (Backspace) |
| **Bridge Edges** | Crear una cara entre dos aristas | Menú contextual |
| **Flip Face Normals** | Voltear caras que se ven invisibles o al revés | **Alt+N** |
| **Merge Objects** | Unir varias piezas de ProBuilder en una | Selecciona objetos → menú *Object* |
| **Center Pivot** | Poner el pivote en el centro | Menú *Object* |
| **Set Collider** | Convertir la pieza en un collider invisible (muros invisibles, límites) | Menú *Object* |

### 10.4 Ejemplo: construir un cuarto de 6 × 8 m con puerta

Construiremos el cuarto con piezas separadas (piso, muros y techo), que es lo más fácil de
entender y de corregir.

1. **Contenedor:** dentro de la zona, clic derecho en la Hierarchy → *Create Empty* y nómbralo
   `Cuarto_Nuevo`. Todas las piezas irán dentro de él.
2. **Piso:** *Create Shape → Plane* de 6 × 8 m. Colócalo a la altura del piso existente:
   mantén **V** y arrastra desde una esquina hasta un vértice del piso del edificio.
3. **Tres muros lisos:** *Create Shape → Cube*. Con la herramienta Escalar o en el panel de la
   forma, dale **6 m × 2.7 m × 0.15 m** (largo, alto, grosor). Colócalo sobre un borde del piso.
   Duplícalo (Ctrl+D) para el muro de enfrente y haz otros dos de **8 m × 2.7 m × 0.15 m** para los
   lados, girándolos 90° con **E** (mantén Ctrl para girar en pasos exactos). Pega las esquinas
   con **V**.
4. **Muro con puerta:** borra uno de los muros de 6 m y constrúyelo con tres bloques (*Cube*),
   dejando un hueco de 0.9 m de ancho × 2.1 m de alto:
   - Dos tramos laterales de **2.55 m × 2.7 m × 0.15 m**, uno a cada lado del hueco
     (2.55 + 0.9 + 2.55 = 6 m).
   - Un dintel de **0.9 m × 0.6 m × 0.15 m** encima del hueco, entre 2.1 m y 2.7 m de altura.
   Si quieres puerta física, copia una puerta existente de CECADEC (objetos con el componente
   `HingedDoor`) y colócala en el hueco. La forma *Create Shape → Door* también hace un muro
   con hueco, pero sus parámetros (*Door Height* = grosor del dintel, *Leg Width* = ancho de
   cada lado) son menos intuitivos.
5. **Techo:** otro Plane de 6 × 8 m a 2.7 m de altura. Si no se ve desde dentro, selecciónalo y
   voltea sus normales: *Tools → ProBuilder → Object → Flip Object Normals*.
6. **Materiales:** arrastra `Kit_Floor` o `Kit_Tile_Floor` al piso, `Kit_Wall_White` a los muros y
   `Kit_Ceiling` al techo (10.5).
7. **Pruébalo:** revisa que cada pieza tenga collider (10.7), guarda el prefab y entra en Play
   para recorrerlo. Si es un cuarto jugable, agrégale sus marcadores (sección 16).

### 10.5 Materiales en ProBuilder

- Arrastra un material desde Project a una cara en la vista Scene: se aplica a esa cara.
- Para pintar varias caras, selecciónalas en modo Cara y arrastra el material sobre la
  selección, o usa **Tools → ProBuilder → Editors → Open Material Editor** (guarda materiales
  frecuentes en ranuras y los aplica con un clic).
- Usa materiales del proyecto, nunca los grises por defecto:
  - Arquitectura: `Assets/_Project/Art/Materials/Kit/` (`Kit_Wall_White`, `Kit_Wall_Red`,
    `Kit_Floor`, `Kit_Tile_Floor`, `Kit_Ceiling`, `Kit_Concrete`, `Kit_Glass`…).
  - Detalles: `Assets/_Project/Art/Remake/Materials/` (`Concrete_Dirty`, `Rust`, `Blood_Dry`…).

### 10.6 UVs: que la textura no se estire

ProBuilder calcula las UV automáticamente (*Auto UV*). Si una textura se ve estirada:

1. Selecciona las caras → **Tools → ProBuilder → Editors → Open UV Editor**.
2. En modo *Auto*, ajusta **Tiling** (repeticiones) y **Offset**.
3. Para muros largos, usa un tiling proporcional al tamaño, por ejemplo 1 repetición por metro.

### 10.7 Colliders

Las formas de ProBuilder traen un **Mesh Collider** por defecto: ya son sólidas. Revisa en el
Inspector que lo tengan. En objetos decorativos que no se deben pisar (cables, papeles),
puedes quitarlo.

### 10.8 Editar un modelo que ya existe (ProBuilderize)

Para cambiar la forma de una malla importada (por ejemplo, abrir un hueco en un muro de
CECADEC):

1. Selecciónala → **Tools → ProBuilder → Object → Pro Builderize** (o clic derecho en el título del componente *Mesh Filter* en el Inspector → *ProBuilderize*).
2. Ahora puedes editar sus vértices, aristas y caras.

⚠️ Al hacerlo, la malla deja de venir del FBX y se guarda dentro del prefab: el archivo crece
y ese objeto ya no se actualiza si alguien cambia el FBX. **Hazlo solo con la pieza concreta**
que necesites, nunca con edificios completos. Muchas veces es más fácil desactivar el muro
original (casilla junto al nombre en el Inspector) y construir uno nuevo con ProBuilder.

### 10.9 Medidas de referencia

| Elemento | Medida |
|---|---|
| Estudiante | 1.5 m de alto (ojos a 1.38 m) |
| El Rector | 3.55 m de pie; 1.45 m cuando gatea en interiores |
| Puerta estándar | 0.9 m de ancho × 2.1 m de alto (mínimo para que pasen los enemigos: 0.9 m) |
| Pasillo cómodo | 1.8 m o más |
| Techo de aula en CECADEC | 2.7 m |
| Altura de una planta | 4 m |
| Escalón máximo que la IA puede subir | 0.35 m |
| Escritorio / mesa | 0.75 m de alto |

---

## 11. Terreno: suelos, caminos, jardines y colinas

### 11.1 Cómo es el terreno actual

El terreno del campus (`Prefabs/Map/Terreno`) está hecho de **mallas planas y piezas del kit**,
no del sistema *Terrain* de Unity:

- `Dirt_Ground`: suelo de tierra grande que rodea todo.
- `Concrete_Slab`: la plancha de concreto del campus.
- Grupos por edificio (`CECADEC_Group`, `CDS_Group`…): banquetas, jardineras, bordillos y
  explanadas hechos con piezas del kit (`Kit_Pad`, `Kit_Kerb`…).
- `Boundary`: límites del área de juego.

Así todo usa materiales PSX y se ve coherente con el resto.

### 11.2 Opción A (recomendada): suelos con ProBuilder y piezas del kit

Para caminos, plazas, estacionamientos, jardineras y desniveles suaves:

1. *Tools → ProBuilder → Editors → Create Shape → Plane* (o *Create PolyShape* para formas irregulares) del tamaño del área.
2. Súbela 1-2 cm sobre el suelo existente para que no parpadee (*z-fighting*: dos caras en la
   misma altura se "pelean").
3. Material: `Kit_Concrete`, `Kit_Pad`, `Kit_Aggregate`, `Kit_Grass`, `Kit_Soil`, `Kit_Stone`…
4. Bordes: duplica piezas `Kit_Kerb` y `Kit_Kerb_Rounded` desde las jardineras existentes
   (Ctrl+D) y alinéalas con **V**.
5. **Montículos o desniveles pequeños:** crea un Plane con varias subdivisiones (*Tools → ProBuilder → Object → Subdivide Object*, varias veces),
   pasa a modo Vértice y sube algunos vértices. Mantén pendientes suaves: si la diferencia de
   altura entre dos puntos a 0.4 m supera 0.35 m, los enemigos no pasan (sección 17).
6. Rampas: forma *Cube* o *Stairs* y rótala, o mueve los vértices superiores de un Cube.

### 11.3 Opción B: el sistema Terrain de Unity (áreas naturales grandes)

Para colinas o zonas boscosas grandes **fuera** del campus pavimentado:

1. **GameObject → 3D Object → Terrain.** Crea un terreno de 1000 × 1000 m por defecto. En el
   Inspector, en la pestaña ⚙ (*Terrain Settings*), ajusta *Terrain Width/Length* al área
   real (por ejemplo, 100 × 100) y la resolución.
2. Herramientas del Inspector del Terrain:
   - **Raise or Lower Terrain:** clic para subir, Shift+clic para bajar.
   - **Set Height:** aplana a una altura fija (útil para que coincida con el campus).
   - **Smooth Height:** suaviza.
   - **Paint Texture:** crea *Terrain Layers* con texturas de `Art/Textures` (pasto, tierra) y
     píntalas.
   - **Paint Trees / Paint Details:** árboles y pasto en masa.
3. Ajusta el *Brush Size* y la *Opacity* para pinceladas suaves.

Ten en cuenta:

- El Terrain usa su propio shader y no el PSX por material. El filtro PSX de pantalla sí se
  aplica, pero la textura se verá más definida que el resto. Pruébalo y coméntalo con el equipo
  antes de usarlo en un área grande.
- Es pesado en celulares: usa resoluciones bajas.
- Trae su propio collider (*Terrain Collider*).
- Los enemigos solo navegan dentro del área de la sección 17 y no suben pendientes fuertes.

### 11.4 Agua, charcos y espejos

En la escena hay ejemplos en `UTEZ_LookTest` (charcos con reflejo: componente
`PsxPlanarReflection` y shader `HorrorUtez/PSX/Puddle`). Duplica uno para agregar charcos.
Úsalos con moderación: cada reflejo cuesta rendimiento.

---

## 12. Muebles, props y vegetación

### 12.1 Catálogo disponible

- **Muebles de laboratorio y oficina** — `Assets/_Project/Art/Remake/Resources/Lab/`:
  `Lab_Desk`, `Lab_TeacherDesk`, `Lab_Chair`, `Lab_Stool`, `Lab_Bench`, `Lab_CRT`, `Lab_LCD`,
  `Lab_Tower`, `Lab_TowerOpen`, `Lab_Keyboard`, `Lab_Mouse`, `Lab_Rack`, `Lab_Shelf`,
  `Lab_Locker`, `Lab_Whiteboard`, `Lab_Poster`, `Lab_ProjectorMount`, `Lab_AC`,
  `Lab_FluoroHanging`, `Lab_CeilingTile`, `Lab_CableMess`, `Lab_Papers`, `Lab_Trash`,
  `Lab_Tape`, `Lab_Blood`, `Lab_ExitSign`.
- **Props** — `Assets/_Project/Prefabs/Props/`: bancas, extintores…
- **Arquitectura modular** — `Assets/_Project/Art/Environment/Kit/`: `Kit_Wall_4m`,
  `Kit_Window`, `Kit_Pillar`, `Kit_Pilaster`, `Kit_Floor_4m`, `Kit_Roof`, `Kit_Pad`, `Kit_Kerb`.
- **Vegetación** — dentro del prefab `Bosque` (árboles `PSX_Tree*`, palmeras, pasto, rocas,
  juncos, flores). Para más, duplica uno existente (Ctrl+D) y muévelo; varía la rotación y la
  escala para que no se vean iguales.

### 12.2 Reglas al colocar

| Regla | Por qué |
|---|---|
| Colócalos **dentro de la zona** a la que pertenecen (Prefab Mode) | Para que queden en el archivo correcto |
| Agrega **Box Collider** a lo que estorba el paso | Si no, los jugadores lo atraviesan |
| Pon el layer **RemakeScenery** a los muebles | La IA los trata como obstáculo y nunca como piso; además los tiene en cuenta al ver y escuchar |
| Marca **Static** lo que nunca se mueve | Unity los agrupa al dibujar: el juego corre más rápido |
| Pégalos al piso con **V** | Nada flotando ni enterrado |
| Rompe la simetría | Un escritorio girado, una silla caída, papeles: da más miedo que todo alineado |
| No uses objetos con Rigidbody como decoración | El botín sincronizado por red lo maneja el código (sección 16) |

### 12.3 Los objetos valiosos

Los objetos que los jugadores recogen (laptop, proyector, microscopio, UPS…) **no se colocan a
mano**: el código los crea en los **puntos de botín** (sección 16). Para cambiar dónde aparecen,
mueve los marcadores.

---

## 13. Luces y ambiente

### 13.1 Tipos de luz

| Tipo | Uso aquí |
|---|---|
| **Point Light** | Fluorescentes, lámparas, focos. La más usada |
| **Spot Light** | Reflectores, luces dirigidas |
| **Directional Light** | Sol/luna. Ya existe una; no agregues otra |

Crear: clic derecho en la Hierarchy (dentro de tu zona) → **Light → Point Light**.

### 13.2 Valores que ya usa el juego

- Fluorescente de aula: *Range* 7.5, *Intensity* entre 1.6 y 3.2, color blanco-azulado,
  **Shadow Type: No Shadows**.
- Luz de emergencia: *Range* 13, *Intensity* 0.8, color verdoso.
- Las **sombras** cuestan mucho rendimiento, sobre todo en celulares: déjalas desactivadas
  salvo en casos muy justificados.
- La oscuridad es parte del juego: no ilumines todo. La linterna del jugador debe seguir
  siendo útil. Hay aulas a oscuras a propósito.

### 13.3 Fluorescentes que fallan

Agrega el componente **`RemakeFlickerLight`** a una Point Light: parpadea, se corta a ratos y
se apaga cuando El Rector se acerca. *Base Intensity* es su brillo normal, *Seed* un número
cualquiera (cámbialo para que no parpadeen igual) y *Hum* activa el zumbido. Si nombras la luz
empezando con **"Fluorescente"**, el sistema de días la incluye en los apagones parciales.

### 13.4 Lo que maneja el código

La niebla, la intensidad nocturna, la lluvia y los rayos los controlan `WeatherSystem`, el
ciclo de días (`RemakeDayCycle`) y `RemakeGame.ApplyNightMood()`. No cambies la niebla ni la
luz ambiental desde *Lighting Settings* sin hablarlo con el equipo: el código las modifica al
iniciar. Los efectos de cámara están en el objeto **Global Volume**.

---

## 14. Materiales y texturas

### 14.1 Usar un material existente

Arrastra el material desde Project a un objeto en la vista Scene (o a una cara con ProBuilder).
Revisa primero `Art/Materials/Kit/` (arquitectura y terreno) y `Art/Remake/Materials/` (objetos).

### 14.2 Crear un material nuevo

1. Consigue o crea la textura `.png`. Para el estilo PSX conviene que sea **pequeña**: de 64 a
   256 px por lado. Si es de internet, verifica que su licencia permita usarla (CC0 es lo más
   seguro) y anota la fuente en el PR.
2. Guárdala en `Assets/_Project/Art/Remake/Textures/` (objetos) o `Assets/_Project/Art/Textures/`
   (arquitectura/terreno).
3. **Opción rápida:** si la pusiste en `Art/Remake/Textures/`, usa el menú
   **HORROR-UTEZ → Crear materiales PSX para texturas nuevas**. Crea en
   `Art/Remake/Materials/` un material con el mismo nombre que la textura.
4. **Opción manual:** clic derecho en la carpeta de materiales → **Create → Material**. En el
   Inspector, cambia el *Shader* a **HorrorUtez → PSX → Lit** y arrastra la textura a su
   ranura principal. Ajusta el color y el brillo a tu gusto.

Si un objeto se ve **rosa/magenta**, su material usa un shader que no existe en URP: cámbialo
a `HorrorUtez/PSX/Lit`.

---

## 15. Modelar en Blender y llevarlo a Unity

Blender sirve para **piezas**: muebles, objetos valiosos, utilería, máquinas, personajes. El
mapa (arquitectura y terreno) se arma en Unity. Unity **no** abre archivos `.blend` en este
proyecto, para que nadie esté obligado a instalar Blender: todo entra como **FBX**.

### 15.1 Lo mínimo de Blender

| Acción | Tecla |
|---|---|
| Orbitar / desplazar / zoom | Clic central / Shift + clic central / rueda |
| Mover / rotar / escalar | **G** / **R** / **S** (después X, Y o Z para fijar el eje; escribe un número para precisión) |
| Modo edición ↔ objeto | **Tab** |
| Vértice / arista / cara (en edición) | **1** / **2** / **3** |
| Extruir | **E** |
| Corte en bucle | **Ctrl + R** |
| Aplicar transformaciones | **Ctrl + A → All Transforms** |
| Ver medidas exactas | **N** (panel lateral) |
| Desplegar UV | En edición, selecciona todo (**A**) → **U → Smart UV Project** |

Para aprender lo básico, busca un tutorial introductorio de Blender low-poly (el clásico de la
"dona" de Blender Guru sirve para entender la interfaz).

### 15.2 Crear una pieza nueva

1. **Escala real:** 1 unidad de Blender = 1 m. Usa el panel **N** para comprobar medidas
   (sección 10.9).
2. **Pocos polígonos:** una silla de 200 a 800 triángulos, una máquina grande hasta ~3000.
   Puedes ver el conteo activando *Statistics* en *Overlays*.
3. **Origen en la base** de la pieza, centrado: así se apoya bien en el piso en Unity.
4. **Aplica transformaciones** (Ctrl+A → All Transforms) antes de exportar, para que la escala
   sea 1 y la rotación 0.
5. **UV:** despliega (U → Smart UV Project) para que las texturas no se deformen.
6. **Materiales con nombre exacto:** nombra cada material igual que uno existente en Unity y se
   asignará solo al importar:
   - Piezas en `Art/Remake/…` → nombres de `Art/Remake/Materials` (`Metal_Brushed`,
     `Plastic_Beige`, `Laminate_Wood`, `Screen_Dead`…).
   - Edificios en `Art/Environment/Landmarks/` → nombres de `Art/Materials/Kit` (`Kit_Wall_Red`,
     `Kit_Floor`…).
   - Si inventas un nombre nuevo, crea antes el material en Unity (sección 14).
7. **Nombres de objetos claros** (`Escritorio`, `Monitor`), sin acentos.

### 15.3 Exportar a FBX

1. Selecciona los objetos de tu pieza.
2. **File → Export → FBX (.fbx)** con estas opciones:
   - *Limit to:* **Selected Objects**
   - *Object Types:* **Mesh** (y **Armature** solo si es un personaje)
   - *Transform:* *Apply Scalings* → **FBX All**; *Forward* → **-Z Forward**; *Up* → **Y Up**
     (son los valores por defecto)
   - *Geometry:* *Apply Modifiers* activado
   - *Animation:* desactivado (salvo personajes)
3. Guarda directamente dentro del proyecto:
   - Muebles → `Assets/_Project/Art/Remake/Resources/Lab/`
   - Botín, vehículos y objetos → `Assets/_Project/Art/Remake/`
   - Piezas de arquitectura → `Assets/_Project/Art/Environment/Kit/`
4. Guarda también tu `.blend` en la carpeta `Source~` junto al FBX (por ejemplo
   `Assets/_Project/Art/Remake/Source~/MiPieza.blend`). Unity ignora las carpetas que terminan
   en `~`, pero el archivo queda en Git para editarlo después.
5. Vuelve a Unity: lo importa solo. Arrástralo a la escena junto a un mueble conocido y comprueba
   la **escala**, que esté **derecho** y que tenga los **materiales** correctos.

Esas carpetas tienen reglas de importación automáticas: escala 1, conversión de ejes de
Blender a Unity y sin cámaras ni luces.

### 15.4 Editar un FBX que ya existe en el proyecto

Sí se puede abrir un FBX del proyecto en Blender, modificarlo y que Unity lo actualice:

1. Si existe un `.blend` en la carpeta `Source~` cercana, **ábrelo**: conserva más información.
   Si no:
   1. En Blender: **File → New → General** y borra el cubo, la cámara y la luz (selecciona
      todo con **A** → **X**). Así evitas que los nombres choquen y Blender les agregue `.001`.
   2. **File → Import → FBX (.fbx)** → elige el archivo dentro de `Assets/…`. Usa las opciones
      por defecto.
2. Edita lo que necesites.
3. Exporta con las opciones de 15.3 **sobrescribiendo el mismo archivo** (mismo nombre, misma
   carpeta).
4. Guarda un `.blend` en `Source~` para la próxima vez.
5. Regresa a Unity: detecta el cambio y reimporta. Todas las escenas y prefabs que usan ese
   modelo se actualizan solos.

**Muy importante:**

- **No cambies los nombres de los objetos ni de los materiales** dentro del FBX. Unity
  identifica cada parte por su nombre: si renombras o borras una parte, los prefabs que la usan
  la pierden y queda un hueco o un objeto vacío.
- **No muevas ni renombres el FBX desde el Explorador de Windows.** Si necesitas moverlo, hazlo
  desde la ventana Project de Unity, que mueve también su `.meta` y conserva las referencias.
- Lo que se hizo en Unity sobre un modelo (colliders agregados, partes desactivadas, materiales
  cambiados en la escena) vive en el prefab, no en el FBX. Al reimportar se conserva.
- Los modelos de CECADEC (`Art/Environment/Landmarks/`) son muy grandes (`CECADEC_North.fbx`
  pesa ~49 MB). Edítalos solo si es indispensable y avisa al equipo: son difíciles de revisar y
  cuidado con el límite de 100 MB por archivo de GitHub.
- **Personajes** (`Art/Characters/Player/PlayerCharacter.fbx`): después de reexportar usa
  **HORROR-UTEZ → Sincronizar prefab del estudiante con su FBX**. Su original está en
  `Art/Characters/Player/Source~/PlayerCharacter.blend`.

Más detalles: [BLENDER.md](Documentation/Remake/BLENDER.md).

---

## 16. Marcadores de gameplay

El código del juego lee estos componentes desde la escena al iniciar la partida. Son
GameObjects vacíos con un componente; en la vista Scene se dibujan como figuras de colores
(activa **Gizmos**). La mayoría están en el prefab `Gameplay_Marcadores`.

| Componente | Dónde está | Qué hace | Reglas |
|---|---|---|---|
| `RemakeLootSpot` (cubo amarillo) | `Gameplay_Marcadores/Botín · día 1` | Donde aparece cada objeto valioso el primer día | Hay 13 y su **Key** no se cambia (la usa la red). Muévelos libremente; el objeto cae al piso bajo el punto |
| `RemakeRoom` (esfera verde) | `Gameplay_Marcadores/Cuartos` | Centro de un cuarto: allí aparecen el botín de los días siguientes, las luces del turno y los enemigos al empezar el día | Ponlo en piso caminable, al centro. Agregar uno lo incluye automáticamente |
| `RemakePatrolPoint` (esfera morada = interior, azul = exterior) | `Patrulla interior` / `Patrulla exterior` | Destinos a los que caminan los enemigos al patrullar | Interiores: solo planta baja. Exteriores: los **4 primeros** (orden de la Hierarchy) son la plaza |
| `RemakeGiantWaypoint` (esfera roja con línea) | `Ruta de El Rector (en orden)` | El recorrido de El Rector por el campus | Sigue el **orden de la Hierarchy** (arrastra para reordenar). Las paradas inalcanzables se descartan solas y se avisan en la Console |
| `RemakeDoorway` (caja naranja) | Dentro de las zonas | Huecos que la navegación debe considerar abiertos (un hueco en un muro, un panel corrido) | Ajusta **Size** para que cubra el hueco de lado a lado |
| `RemakeFlickerLight` | Fluorescentes de las zonas | Luz que falla (sección 13.3) | — |
| `RemakeSlidingDoor` | Entrada este de CECADEC | Puerta de vidrio automática | Las hojas se deslizan sobre el eje Z local del objeto |
| `RemakeAnchor` | `Gameplay_Marcadores/Anclas` | Referencias para el código y las pruebas (`corridor` = pasillo de CECADEC) | No borrar |

Las puertas con bisagra (componente `HingedDoor`, ya presentes en los edificios) también abren
paso automáticamente en la navegación.

### Ejemplo: agregar un cuarto nuevo al juego

1. Construye el cuarto en su zona (secciones 10 y 12).
2. Abre `Gameplay_Marcadores`, selecciona un marcador de `Cuartos` y duplícalo (Ctrl+D).
3. Cámbiale el nombre y el campo *Room Name* (por ejemplo "Biblioteca") y muévelo al centro del
   cuarto nuevo.
4. Para que los enemigos también lo visiten, duplica un `Patrulla interior` y ponlo dentro.
5. Si el cuarto tiene un hueco de entrada sin puerta, agrega un `RemakeDoorway` que lo cubra.
6. Prueba en Play: desde el segundo día puede aparecer botín ahí.

---

## 17. Reglas para que los enemigos puedan moverse por tu mapa

Los enemigos no usan el NavMesh de Unity: al iniciar, el juego mide el mapa con una cuadrícula
de celdas de 0.4 m usando los colliders. Al construir, ten en cuenta:

| Regla | Detalle |
|---|---|
| **Área navegable** | Solo entre X = −38 y 38.8 y Z = −66 y 42.8 (coordenadas del mundo). Fuera de ahí los enemigos no entran |
| **Solo planta baja** | Celdas con piso por debajo de 0.75 m de altura. No suben a la planta alta |
| **Espacio libre** | Cada celda necesita ~1.55 m de alto libre y 0.25 m de radio sin obstáculos |
| **Escalones** | Diferencia máxima de 0.35 m entre celdas vecinas |
| **Puertas** | Las puertas `HingedDoor` y los `RemakeDoorway` abren paso aunque el hueco sea justo |
| **Muebles** | Los que tienen collider son obstáculos. Deja pasillos de al menos 0.9 m entre ellos |
| **Todo debe tener collider** | Un piso sin collider es un hueco para la IA (y para los jugadores) |

Si un enemigo no entra a un cuarto nuevo, revisa estas reglas y la Console: la ruta de El Rector
avisa qué paradas descartó.

---

## 18. Programar: dónde está cada cosa

### 18.1 Antes de tocar código

- Lee [ARCHITECTURE.md](Documentation/Remake/ARCHITECTURE.md): explica cada sistema y la red.
- El juego es **host-authoritative**: el anfitrión decide todo (física del botín, daño, cuota) y
  los demás reciben el estado. Un cambio de reglas casi siempre va del lado del host.
- Si cambias los mensajes de red, sube la versión del protocolo: todos deben usar el mismo build
  para jugar juntos.
- **No generes el mapa por código.** Si necesitas datos del mapa, crea un componente marcador
  (como los de `Scripts/Remake/Map/`) y colócalo en la escena.

### 18.2 Mapa del código

| Si vas a trabajar en… | Archivos |
|---|---|
| Movimiento, cámara, brazos elásticos | `RemakeStudent.cs`, `RemakeCameraRig.cs`, `RemakeHandRig.cs` |
| Enemigos (IA, ataques, animación) | `RemakeEnemy.cs`, `RemakeBody.cs`, `RemakeBestiary.cs` |
| Botín, física de agarre, daño | `RemakeLoot.cs`, `RemakeLootCatalog.cs` |
| Reglas, días, cuota, camión, navegación | `RemakeGame.cs`, `RemakeDayCycle.cs` |
| Marcadores del mapa | `Scripts/Remake/Map/*.cs` |
| Tienda y mejoras | `RemakeShop.cs`, `RemakeShopProduct.cs` + escena `remake_shop.unity` |
| HUD y menú | `RemakeHud.cs` |
| Red y chat de voz | `RemakeWire.cs`, `RemakeVoice.cs` |
| Audio | `RemakeAudio.cs` |
| Aspectos de los estudiantes | `RemakeSkins.cs` |
| Estilo PSX, niebla, clima, puertas | `Scripts/Rendering/`, `Scripts/World/` |
| Prueba automática | `RemakeSmoke.cs` (si agregas una mecánica, agrega su comprobación) |

---

## 19. Git y trabajo en equipo

### 19.1 Flujo diario

1. `git checkout main` y `git pull` (o *Fetch/Pull* en GitHub Desktop).
2. Crea una rama para tu tarea: `git checkout -b tipo/descripcion-corta`.
   - Tipos: `mapa/…`, `gameplay/…`, `arte/…`, `fix/…`, `docs/…`.
3. Trabaja y haz **commits pequeños** con mensajes claros: "Muebles en oficinas CDS planta alta".
4. `git push -u origin tu-rama` y abre un **Pull Request** hacia `main`.
5. Un compañero lo revisa y lo aprueba. Se hace **Merge** y se borra la rama.

### 19.2 Reglas para no pisarnos

- **Una persona por zona a la vez.** Las escenas y los prefabs no se fusionan bien cuando dos
  personas los cambian al mismo tiempo. Antes de editar una zona, avisa en el grupo o asígnate la
  tarea en el tablero de GitHub (*Projects*).
- **No edites `remake.unity`** salvo para agregar o quitar una zona.
- **Sube siempre los `.meta`** de los archivos nuevos. Sin ellos, los demás verán referencias
  rotas.
- **No subas** `Library/`, `Temp/`, `Logs/`, `UserSettings/` ni `Builds/` (ya están en
  `.gitignore`).
- **Archivos grandes:** GitHub avisa con más de 50 MB y rechaza más de 100 MB. Antes de subir un
  modelo o audio grande, coméntalo.
- **No cambies la versión de Unity** ni actualices paquetes sin acordarlo: afecta a todos.
- Revisa `git status` antes de cada commit para no subir cambios accidentales. Unity a veces
  toca archivos que no editaste, como materiales o `ProjectSettings`; si no los cambiaste a
  propósito, déjalos fuera.

### 19.3 Si aparece un conflicto en una escena o prefab

1. No entres en pánico: nada se pierde mientras no hagas commit.
2. Lo más seguro es quedarte con **una** de las dos versiones y rehacer a mano los cambios de la
   otra:
   ```bash
   git checkout --theirs Assets/_Project/Prefabs/Map/Zona_CDS.prefab   # la versión que viene de main (durante un merge)
   # o
   git checkout --ours   Assets/_Project/Prefabs/Map/Zona_CDS.prefab   # tu versión
   git add Assets/_Project/Prefabs/Map/Zona_CDS.prefab
   ```
3. Si no estás seguro, pide ayuda antes de continuar.

### 19.4 Fusión inteligente de Unity (recomendado)

Unity incluye *UnityYAMLMerge*, que entiende escenas y prefabs y resuelve muchos conflictos solo.
Configúralo una vez en tu máquina (ajusta la ruta si instalaste Unity en otro lugar):

```bash
git config --global merge.unityyamlmerge.name "Unity SmartMerge"
git config --global merge.unityyamlmerge.driver "'C:/Program Files/Unity/Hub/Editor/6000.6.3f1/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p %O %B %A %A"
git config --global merge.unityyamlmerge.recursive binary
```

El archivo `.gitattributes` del repositorio ya indica usarlo con `.unity`, `.prefab`, `.asset`
y `.mat`.

### 19.5 Antes de abrir un Pull Request

- [ ] Presionaste Play y jugaste una partida corta con *Explorar solo*.
- [ ] La Console no tiene errores rojos nuevos.
- [ ] Lo nuevo tiene colliders y está dentro de la zona correcta.
- [ ] Incluiste los `.meta` de los archivos nuevos.
- [ ] El PR explica qué cambiaste y, si es visual, trae una captura.

---

## 20. Compilar y probar

### 20.1 Compilar el juego

- **Desde Unity:** menú **HORROR-UTEZ → Build → Windows**. El ejecutable queda en
  `Builds/Remake/Windows/HORROR-UTEZ.exe`. Comparte la carpeta `Windows` completa, no solo el
  `.exe`.
- **Desde la terminal** (PowerShell, en la carpeta del proyecto, con Unity cerrado):
  ```powershell
  ./Tools/unity/BuildRemake.ps1
  ```
  Con Unity abierto, agrega `-Isolated` (compila una copia aparte).

### 20.2 Prueba automática

El juego incluye una prueba que recorre una partida completa (agarrar, dañar, cargar el camión,
morir, revivir, tienda, día siguiente) y verifica que todo funcione:

```powershell
./Builds/Remake/Windows/HORROR-UTEZ.exe -remakeSolo -remakeSmoke -logFile smoke.log
```

Al terminar, busca `[RemakeSmoke] ALL COMPLETE` en `smoke.log`. Si algo falla, aparece
`[RemakeSmoke] FAIL` con la descripción. Las pruebas en red y el tour con capturas están en
[TESTING.md](Documentation/Remake/TESTING.md).

### 20.3 Menús útiles (barra superior → HORROR-UTEZ)

| Menú | Qué hace |
|---|---|
| Abrir escena jugable | Abre `remake.unity` |
| Reparar configuración del proyecto | Restablece layers, URP y las escenas del build. No toca escenas ni prefabs. Úsalo solo si algo de configuración se rompió |
| Crear materiales PSX para texturas nuevas | Crea materiales para texturas nuevas de `Art/Remake/Textures` |
| Sincronizar prefab del estudiante con su FBX | Tras reexportar el personaje desde Blender |
| Build → Windows / macOS / Android | Compila |

Ninguno es necesario para abrir el proyecto o jugar.

---

## 21. Problemas frecuentes

| Problema | Solución |
|---|---|
| Unity abre en **Safe Mode** | Hay errores de código. Revisa la Console y haz pull por si ya se corrigieron. Si el error viene de tu cambio, deshazlo. Si no, avisa al equipo |
| Objetos **rosas/magenta** | El material usa un shader incompatible: cámbialo a `HorrorUtez/PSX/Lit` |
| Hice cambios y **desaparecieron** | Los hiciste en Play Mode, o no guardaste el prefab (Ctrl+S / Apply All) |
| El jugador **atraviesa** algo | Le falta collider |
| El jugador **cae al vacío** | El piso no tiene collider o hay un hueco entre piezas |
| Un enemigo **no entra** a un área | Revisa la sección 17 (planta baja, área navegable, pasillos, `RemakeDoorway`) |
| Una cara se ve **invisible** desde un lado | Normal invertida: *Flip Normals* en ProBuilder o en Blender (*Mesh → Normals → Flip*) |
| Una textura **se ve estirada** | Ajusta las UV (10.6) o despliega UV en Blender |
| Un modelo **aparece acostado o gigante** | Revisa las opciones de exportación FBX (15.3) y que aplicaste transformaciones (Ctrl+A) |
| `git push` dice **rejected** | Alguien subió antes: haz `git pull` y vuelve a hacer push |
| La primera apertura **tarda muchísimo** | Normal: Unity importa todo. Las siguientes aperturas son rápidas |
| Errores raros después de un pull grande | Cierra Unity, borra la carpeta `Library/` del proyecto y ábrelo de nuevo (se regenera; tarda como la primera vez) |
| Unity pide **otra versión** | Instala exactamente 6000.6.3f1 (sección 3.4). No aceptes "upgrade" |
| La escena tiene **marcadores invisibles** | Activa el botón *Gizmos* de la vista Scene |

---

## 22. Más documentación

- [Cómo jugar y hospedar](Documentation/Remake/PLAY.md)
- [Arquitectura del código](Documentation/Remake/ARCHITECTURE.md)
- [Pruebas y resultados](Documentation/Remake/TESTING.md)
- [Modelos desde Blender](Documentation/Remake/BLENDER.md)
- Para agentes de IA (Claude Code / Codex): [AGENTS.md](AGENTS.md) y
  [HANDOFF.md](Documentation/Remake/HANDOFF.md)

Recursos para aprender:

- Unity Learn (gratis, en inglés): <https://learn.unity.com> — empieza por *Unity Essentials*.
- Manual de ProBuilder: <https://docs.unity3d.com/Packages/com.unity.probuilder@6.0/manual/index.html>
- Manual de Blender: <https://docs.blender.org/manual/es/latest/>
- Git en 15 minutos (interactivo): <https://learngitbranching.js.org/?locale=es_AR>
