# HORROR-UTEZ

Juego de terror en primera persona ambientado en la UTEZ (Universidad Tecnológica Emiliano Zapata).

- **Motor:** Unity 6 LTS · C# · URP
- **Estética:** PS1 / PSX (lowpoly, vertex jitter, affine warping, dithering, fog, CRT) con mecánicas modernas.
- **Plataformas:** PC (Windows) y Android.

## Estado

En la rama `remake` hay un prototipo cooperativo en desarrollo. Abre
`Assets/_Project/Scenes/remake.unity` en Unity 6000.6.3f1 y pulsa Play.
Los últimos cambios todavía requieren compilación y validación en Windows.

Para continuar con Codex o Claude Code, lee [AGENTS.md](AGENTS.md),
[CLAUDE.md](CLAUDE.md) y [el relevo completo](Documentation/Remake/HANDOFF.md).

## Estructura

```
Assets/
  _Project/        Código y assets propios
  ThirdParty/      Dependencias vendorizadas (URP-PSX, etc.)
Packages/          Manifiesto de paquetes Unity
ProjectSettings/   Config del proyecto (generado por el editor)
```
