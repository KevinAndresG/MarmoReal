---
name: marmoreal-dev
description: Guía de trabajo para el juego MARMOREAL (Unity 6.3 + HDRP, incremental de canica escultora). Úsala antes de tocar scripts, escena, prefabs, UI, balance o al probar el juego vía Unity MCP — contiene el mapa del código, el flujo compilar→probar que funciona, trampas conocidas del editor y recetas (añadir mejora/nodo del árbol, retocar balance, capturar pantalla).
---

# MARMOREAL — guía rápida de desarrollo

Lee primero `CLAUDE.md` (regla: **todo se edita en la escena/prefabs existentes; nunca re-ejecutar
el builder para "rehacer"**). Diseño completo en `Docs/GDD.md`.

## Mapa del código (`Assets/Marmoreal/Scripts`, namespace `Marmoreal`)
| Archivo | Rol |
|---|---|
| `Core/GameManager.cs` | Estado, economía, combo, resonancia, galerías, prestigio, guardado (PlayerPrefs `marmoreal.save.v1`), offline, Ícor (`AddIcor`), buffs (`StartFrenzy`/`StartFury`, `colossusMult`), `PowerDamage(potencia)`, Mecenazgos (`CheckAchievements` cada 1 s, `StatValue`). Eventos: `StatsChanged`, `GalleryUp`, `Prestiged`, `Toast`, `OfflineEarned`, `AchievementUnlocked`. |
| `Core/EconomyConfig.cs` | ScriptableObject `Config/Economy.asset`: TODAS las cifras + listas `upgrades` (Taller = `Currency.Dust`; Árbol = `Currency.Essence`; Altar = `Currency.Icor`, ambos con `treePos`/`requires`/`branch`), `powers` (`PowerDef`) y `achievements` (`AchievementDef`). `FillDefaults()` solo al crear el asset; `EnsureDefaults()` añade por id lo que falte (lo llama `GameManager.Awake` en memoria y el paso de editor en disco). **Los valores del asset mandan**: cambiar un default en código no cambia el asset. |
| `Gameplay/PowerSystem.cs` | Poderes 1-7 (Supernova, Ingravidez, Meteoros+Gran Meteoro, Coloso, Vórtice errante+implosión, Midas, Cadena de Rayos): recargas, pools de prefabs `Bomb`/`Meteor`/`Vortex`/`Lightning`, luz "Destello" (`FlashAt`). `TryCast(i)`, `ResetCooldowns`, `FreeMeteorShower`, `FreeGolden`. |
| `Gameplay/AllySquad.cs` + `AllyMarble.cs` | Discípulas (canicas aliadas del Taller `gemelas`/`vinculo`) y Legión temporal de las Musas (`SummonLegion`). Prefab `Ally`. |
| `Editor/ContentV2Builder.cs` | Menú *Marmoreal/Añadir Discípulas, Rayo y Volcánicos (aditivo)*: materiales, prefabs Ally/Lightning/JumpRune, disco del Vórtice y AllySquad en escena. Seguro de repetir. |
| `Gameplay/MusaSpawner.cs` | Musas: orbe (prefab `Musa`) cada 60-120 s × Favor; al tocarlo, 1 Ícor + bendición aleatoria. |
| `World/Spinner.cs` | Giro/flotación/pulso decorativo (anillos de vórtice, musa, bomba). |
| `Core/GameState.cs`, `Core/Num.cs` | Datos guardados; formateo (`Num.F`, `Num.Roman`). |
| `Gameplay/PlayerMarble.cs` | Canica (Rigidbody). Input System. Daño por impacto, Salto (Espacio) + Golpe en Picado (2.º salto en el aire, daño normal por velocidad; aterrizar no rebota), Embestida (Shift), pared invisible del borde, `Respawn()`, mecánicas `rebote`/`sismo`. Piloto automático debug `autopilot` (F9). |
| `Gameplay/Monolith.cs` | Bloque de trozos (hijos de `Block`); arranca el trozo más cercano al golpe; los de encima caen (asentamiento por columnas); revela y rompe la estatua. |
| `Gameplay/MonolithSpawner.cs` | Pool de bloques en los pedestales (hijos de "Pedestales"), tiers de material por galería, dorados. |
| `Gameplay/AcolyteSwarm.cs`, `Gameplay/Shockwave.cs`, `Gameplay/CameraRig.cs` | Idle, onda (eco/perpetua/sismo), cámara orbital con shake/FOV kick. |
| `FX/ShardSystem.cs` | Polvo y escombros con `Graphics.RenderMeshInstanced` (sin GameObjects). Mecánica `metralla`. El polvo solo se recoge en el radio del Imán (`ForceCollect(s, radio)` para onda/Supernova, `AttractTo` para el Vórtice); nunca vuela solo por tiempo. |
| `FX/AudioFX.cs` | Audio procedural (o clips asignados en `overrides`). |
| `UI/Hud.cs` + `UI/Hud.uxml` + `UI/Hud.uss` | UI Toolkit. Layout editable en UI Builder; el script solo rellena. Un solo overlay de árbol para las dos monedas (`ToggleTree(Currency)`: T Escultor, G Altar; clase `.altar` cambia la paleta), barra de poderes generada desde `config.powers`, `Banner(texto)`, etiqueta de buffs, marcador de Musa, overlay de Mecenazgos (L). Taller = Ctrl+1-9. |
| `World/ArenaShape.cs` | Perfil analítico del cuenco (lo usan FX y cámara). Si cambias el suelo, ajusta aquí. |
| `World/MeshFactory.cs` | Generadores de meshes (solo los usa el builder). |
| `Editor/MarmorealBuilder.cs` | Construcción inicial aditiva/idempotente (no pisa assets existentes; no toca la escena si existe). |
| `Editor/PowersContentBuilder.cs` | Menú *Marmoreal/Añadir poderes, Altar y Musas (aditivo)*: materiales, prefabs Bomb/Meteor/Vortex/Musa, objetos PowerSystem y MusaSpawner bajo `--- SISTEMAS ---` (solo si faltan) y `EnsureDefaults()` en Economy.asset. Seguro de repetir. |
| `Editor/MarbleTextureGenerator.cs` | Texturas de mármol procedurales tileables. |

Escena: `Assets/Marmoreal/Scenes/Marmoreal.unity` (`--- ILUMINACIÓN ---`, `--- SANTUARIO ---`, `--- SISTEMAS ---`, jugador, cámara).

## Flujo que funciona con Unity MCP (¡importante!)
1. Editar `.cs` con Write/Edit (más barato que las tools de script del MCP).
2. Compilar: `Unity_RunCommand` con `AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport); CompilationPipeline.RequestScriptCompilation();`
   El editor sin foco **no** compila solo.
3. Esperar a que termine **antes** de dar Play (si no, la recarga de dominio ocurre a mitad de partida):
   `until [ Library/ScriptAssemblies/Assembly-CSharp.dll -nt <último .cs editado> ]; do sleep 2; done`
4. Si no compila: `EditorUtility.scriptCompilationFailed` y `grep "error CS" "$LOCALAPPDATA/Unity/Editor/Editor.log" | tail`.
   (`Unity_GetConsoleLogs` suele venir vacío o lleno de ruido del paquete AI.)
5. Play: `Unity_ManageEditor Play`, luego un RunCommand con
   `Application.runInBackground = true; FindFirstObjectByType<Marmoreal.Hud>().SkipTitle(); FindFirstObjectByType<Marmoreal.PlayerMarble>().autopilot = true;`
6. Estado en vivo: la HUD escribe cada segundo `Temp/hud_state.txt` (solo Editor) con galería, polvo, daño, vida, spawner…
7. Captura: `ScreenCapture.CaptureScreenshot(Path.GetFullPath("Temp/shot.png"))` en RunCommand y luego `Read` del PNG.
   (`Unity_Camera_Capture` con HDRP falla o muestra la Scene View.)
8. Antes de terminar: `Stop`. NO borres la partida del usuario sin preguntar (el piloto compra mejoras con ella).
   Para probar con una partida de prueba: copia `PlayerPrefs.GetString("marmoreal.save.v1")` a
   `Library/marmoreal_user_save_backup.json`, pon un `GameState` de prueba (p. ej. con Ícor), juega, y **tras Stop**
   (el juego guarda al salir) restaura el JSON. El piloto automático también lanza poderes listos y persigue Musas.
9. Si la HUD bloquea (título/modal offline), cierra el modal con
   `using (var e = NavigationSubmitEvent.GetPooled()) { e.target = btn; btn.SendEvent(e); }` sobre `modal-ok`.

## Trampas conocidas
- **No edites `.cs` con el juego en Play**: cada `Unity_RunCommand` refresca el AssetDatabase, recompila y hace una
  recarga de dominio a mitad de partida (corrutinas cortadas, clips de audio nulos). Para → edita → compila → Play.
- El puente Unity MCP habla con **el último editor abierto**: si hay otro proyecto abierto, comprueba
  `Unity_ManageEditor GetProjectRoot` antes de ejecutar nada.
- Desde `Unity_RunCommand` los **estáticos** (`GameManager.I`, etc.) pueden verse `null`: usa
  `Object.FindFirstObjectByType<T>()` y miembros de instancia (`gm.S`, `gm.DebugState`, `hud.DebugState`).
- Las recargas de dominio en Play borran estáticos, listas `readonly` y suscripciones a eventos.
  Por eso los singletons se re-buscan solos y las suscripciones usan campos `[NonSerialized]`.
  Mantén ese patrón en código nuevo.
- `Mesh`/`SceneView` chocan con namespaces dentro de RunCommand: usa `UnityEngine.Mesh`, `UnityEditor.SceneView`.
- `System.Diagnostics`/`System.Reflection.BindingFlags` no permitidos en RunCommand.
- VolumeProfile creado por script: los overrides deben añadirse con `AssetDatabase.AddObjectToAsset` (ver `BuildVolumeProfile`).
- El perfil por defecto de HDRP trae motion blur: el perfil del santuario lo pone a 0.

## Recetas
**Añadir mejora del Taller:** añade `UpgradeDef` en `Economy.asset` (Inspector o RunCommand editando
`config.upgrades`), y lee su valor en `GameManager.RecalcStats()` con `Value("id")`. La HUD la lista sola.
Replica la cifra en `Tools/balance_sim.py` (`DUST`).

**Añadir nodo del árbol:** `UpgradeDef` con `currency = Essence`, `treePos` (x columnas −3..3, y filas 0..4.3),
`requires` y `branch` (0 oro, 1 masa, 2 espíritu, 3 cumbre). Mecánica nueva = `maxLevel 1` y comprobar
`GameManager.I.Has("id")` donde aplique. Replica en `TREE` del simulador.

**Añadir nodo del Altar / poder:** `UpgradeDef` con `currency = Icor` (mismo esquema que el árbol). Un poder nuevo =
nodo de nivel 1 con el mismo id + `PowerDef` en `powers` + un `case` en `PowerSystem.Cast` + su mejora de Taller
`pw_<id>` (Dust, `requires = {id}`; se lee con `GameManager.PowerUpgrade(id, fallback)` / `PowerDamage(id, potencia)`). Replica en `ALTAR`/`POWERS`
del simulador. Añádelo también a `FillDefaults()` para que `EnsureDefaults()` lo migre a partidas/asset antiguos.

**Retocar balance:** `python Tools/balance_sim.py 20` → tabla de hitos (objetivos en `Docs/GDD.md §5`).
Prueba real: piloto automático compra lo más barato; compara `Temp/hud_state.txt` con el simulador.

**Cambiar aspecto:** materiales en `Assets/Marmoreal/Materials` (HDRP/Lit; tras cambiar propiedades por
script llama `HDMaterial.ValidateMaterial`). Iluminación: objetos de `--- ILUMINACIÓN ---` y
`Config/VP_Santuario.asset` (exposición fija EV 9.5: luces en lux/lúmenes físicos).
