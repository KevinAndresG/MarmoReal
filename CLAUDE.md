# CLAUDE.md — Proyecto MARMOREAL (Unity 6.3 + HDRP 17)

## REGLA PRINCIPAL: todo vive en la escena y en assets editables

- Todo lo que se construya (arena, luces, volumen, cámara, jugador, UI, spawners, decoración)
  debe quedar **guardado en la escena** (`Assets/Marmoreal/Scenes/Marmoreal.unity`) como
  GameObjects reales, o como **prefabs / materiales / meshes / texturas en `Assets/Marmoreal/`**,
  para que el usuario pueda editarlo a mano en el Editor.
- **Nada de generar el mundo en runtime** desde código (salvo FX efímeros: shards, debris,
  números flotantes). Los monolitos se instancian desde **prefabs editables**.
- Las meshes procedurales se generan **una vez** y se guardan como `.asset` en
  `Assets/Marmoreal/Meshes/`; las texturas como `.png` en `Assets/Marmoreal/Textures/`.
- Un comando de menú/editor puede usarse para la **construcción inicial** (rapidez y precisión),
  pero **nunca debe volver a ejecutarse** sobre una escena existente si eso borra o
  sobrescribe cambios del usuario.
- Las **modificaciones posteriores** se hacen **directamente sobre la escena/prefabs existentes**
  (vía Unity MCP `Unity_RunCommand` editando objetos concretos, o editando assets),
  sin reconstruir desde cero. Antes de tocar algo, leer su estado actual.
- Cualquier herramienta de editor nueva debe ser **aditiva e idempotente**: crear solo lo que
  falta, nunca borrar lo existente.

## Stack
- Unity 6000.3.15f1, HDRP 17.3, Input System 1.19 (solo el nuevo: usar `Keyboard.current`, etc.).
- APIs de Unity 6: `Rigidbody.linearVelocity`, `linearDamping`, `FindFirstObjectByType`.
- UI: UI Toolkit (UIDocument + PanelSettings en `Assets/Marmoreal/UI/`).
- Scripts en `Assets/Marmoreal/Scripts/` (namespace `Marmoreal`), editor en `Scripts/Editor/`.

## Documentos y herramientas
- Diseño, economía y ritmo objetivo: `Docs/GDD.md`.
- Simulador de balance: `python Tools/balance_sim.py 20` (cifras iguales a `Assets/Marmoreal/Config/Economy.asset`).
- Skill del proyecto (mapa del código, flujo de pruebas, trampas, recetas): `.claude/skills/marmoreal-dev/SKILL.md`.

## Flujo de verificación (detalles en el skill)
1. Editar scripts con las herramientas de archivos.
2. Compilar con `Unity_RunCommand` (`AssetDatabase.Refresh` + `CompilationPipeline.RequestScriptCompilation`) y
   **esperar** a que `Library/ScriptAssemblies/Assembly-CSharp.dll` sea más nuevo que el `.cs` antes de dar Play.
3. Errores: `grep "error CS" "$LOCALAPPDATA/Unity/Editor/Editor.log"`.
4. Play → `SkipTitle()` + `autopilot = true` → leer `Temp/hud_state.txt` → `ScreenCapture.CaptureScreenshot` a `Temp/` y `Read`.
5. Stop y guardar la escena. **No borrar la partida del usuario** (`marmoreal.save.v1`) sin preguntar; el piloto
   automático compra mejoras y lanza poderes, así que úsalo con una partida de prueba (respaldo en
   `Library/marmoreal_user_save_backup.json`, restaurar tras Stop) o avisa.
6. Contenido nuevo de economía: añadirlo a `FillDefaults()` (se migra por id con `EnsureDefaults()`); los valores
   ya existentes en `Economy.asset` se cambian editando el asset, no el código.

## Código robusto a recargas de dominio
Singletons `X.I` que se re-buscan solos y suscripciones a eventos con campos `[NonSerialized]` re-suscritas en
`Update`. Los sistemas nuevos deben seguir ese patrón. Para teletransportar la canica usar `PlayerMarble.Respawn()`
(mueve el Rigidbody, no solo el Transform).
