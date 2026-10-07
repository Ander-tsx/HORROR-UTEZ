# Recursos autorizados de R.E.P.O.

El usuario confirmó el 2026-10-05 que tiene autorización del titular para reutilizar
assets y mapas de R.E.P.O. en este proyecto personal/escolar.

Origen local: instalación Steam de `REPO/REPO_Data/resources.assets`.
Extracción reproducible: `Tools/repo_export_modules.py`, usando UnityPy 1.25.4.
`Resources/Repo/provenance.json` enumera los módulos exportados.

Se conservan las coordenadas, jerarquías, UV, submallas, colores, texturas y colisiones
estáticas originales. Las variantes de conexión se abren y las hojas de puertas
animadas se omiten, porque sus controladores originales no se ejecutan aquí.

El remake construye tres mapas compactos de tres módulos cada uno: mansión, museo y
laboratorios árticos. Usa su propia navegación, estudiantes, enemigos, botín, red,
progresión y camión. No incluye Photon, DLLs, código C# del juego original, eventos,
peligros interactivos originales ni su generador completo.

Los archivos `mesh_*.json` contienen geometría que el remake reconstruye como Mesh;
`tex_*.png` contienen texturas. Los materiales se adaptan a PSX/Lit sobre Unity URP.
Los cuatro archivos `prop_*.json` referencian mallas independientes para el campus.

El inventario completo y el código decompilado de estudio permanecen en `Library/Tooling`
(ignorado por Git). Los IDs internos identifican esta instalación local; no se garantiza
que coincidan después de una actualización de R.E.P.O.

Exportación actual: 239 mallas y 131 texturas. Los subárboles de volúmenes de salas
y objetos valiosos se omiten: contienen metadatos y cajas invisibles, no paredes.
