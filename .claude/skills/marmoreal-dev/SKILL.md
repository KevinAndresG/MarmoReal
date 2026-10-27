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
| `Core/BiomeDef.cs` + `Gameplay/BiomeManager.cs` | Salas por Pulido. `BiomeDef` (Config/Biomes): materiales por tier/polvo, `swaps` de materiales de `--- SANTUARIO ---`, Sol, `VolumeProfile`, regla (`BiomeRule` + `ruleA/B/C`, `volatileBonus`), `campaignStage`, `hpMult`/`dustMult`. `BiomeManager` aplica en Play el bioma de `GameState.biomeId` (sin caché: revierte con `swaps` inversos y `biomes[0]`), activa el hijo de `--- SALA ---`, re-renderiza la sonda de reflexión (OnDemand), banner tras cerrar el modal, reglas lava (`PlayerMarble.Launch`, `gm.BreakCombo`) y cristal (`PowerSystem.ReflectBolt`, `FreeShrapnel` en `ShardSystem`). Selección: `NextFor`/`CycleChoice` (botón `modal-alt` del modal de Pulir); `DoPrestige` fija `S.biomeId`. `DebugState` (heat/lava/reflejos) sale en `hud_state.txt`. |
| `Editor/BiomeBuilder.cs` | Menú *Marmoreal/Añadir salas: Volcánica y Cristal (aditivo)*: materiales en `Materials/Biomes`, `VP_Volcanica`/`VP_Cristal`, `Biome_*.asset`, `--- SALA ---` con decoración y `BiomeManager`. Seguro de repetir. |
| `Gameplay/LifeDirector.cs` | Eventos temporales (`lifeEvents`: `StartEvent(id)`, `ActiveEvent`, `GoldenBonus` que lee el spawner) y progreso de contratos por sondeo de `GameState` (`Progress(c)`, `PanelText()` para la HUD). Objeto en `--- SISTEMAS ---`. |
| Mutadores / Coloso / contratos | `GameManager`: `Mutator` (aplicado en `RecalcStats` y `MonolithHP`; `noDash` en `PlayerMarble.TryDash`, `disabledPower` en `PowerSystem.TryCast`, `volatileBonus` en el spawner), `RollMutatorOffer`/`ChooseMutator` (cartas en `Hud.ShowMutatorCards`, contenedor `modal-cards`), `MutatorBonus` al Pulir; `BossDue` (cierra la galería), `OnBossDefeated` + evento `BossDefeated(trial)`; `RollContracts`/`CompleteContract`. Coloso: `MonolithSpawner.SpawnBoss(trial)` (prefab `Monolith_Coloso`, `bossPoint`), `Monolith.MakeBoss`/`BossPhase`. Estado en `GameState` (`mutatorId`, `mutatorOffer`, `bossDoneGallery`, `lifeBosses`, `lifeGolden`, `contracts`, `contractsDone`). |
| Campaña (S4) | `GameManager`: `TrialBiome`/`TrialDue`/`TrialHP`, `CompleteTrial(stage)` (pieza, Ícor, sala siguiente; eventos `TrialPassed`, `CampaignWon`), `CheckColors`/`SelectColor`/`CurrentColor`, `EnterInfinite`, `UpdateRecords`, `Rank`. Datos: `EconomyConfig.campaign` (7 `CampaignPieceDef`), `marbleColors` (`MarbleColorDef`), `campaignPiecesRequired`, `trialHPMult`, `trialIcor`, `infiniteHpPerLife`, `rankNames/rankGalleries`; `BiomeDef.trialGallery`. `GameState`: `pieces`, `colors`, `marbleColor`, `campaignWon`, `victoryShown`, `infinite*`, `legacyStage` (migración v1→v2 en `Load`). `BiomeManager.Unlocked` = etapa 0 / pieza anterior / legacy; `NextFor` = Prueba pendiente o rotación. `World/Monument.cs` (`--- MONUMENTO ---`). HUD en `UI/HudCampaign.cs` (partial de `Hud`): barra `monument`, overlay `coleccion` [C], victoria. `PlayerMarble.ApplyLook` (material/estela) + `Shockwave.SetTint`; `Respawn` evita al jefe. Menú *Marmoreal/Añadir campaña: Monumento y Colección (aditivo)* (`Editor/CampaignBuilder.cs`). |
| Salas 4-7 (S6) | `BiomeRule.IceFloor` (`PlayerMarble.SetSurface`), `IronForge` (`hpMult`/`dustMult` + `World/Magnet.cs` en los hijos de la sala), `GardenGrowth` (`BiomeManager.TickGarden` → `Monolith.Overgrow`), `GoldenPantheon` (+bloques grandes y Prueba "El Coloso de Mármol" ×`ruleB` en `MonolithSpawner.SpawnBoss`). Menú *Marmoreal/Añadir salas 4-7…* (`BiomeBuilder.AddBiomes47`, aditivo; añade al final de `biomes/rooms` y llama `CampaignBuilder.AddCampaign`). Cinemática de victoria: `HudCampaign.VictoryCinematic` (desactiva `CameraRig`, clase `.cine` en la capa `hud`). Toasts en cola durante `Banner` (`bannerOn`). |
| Reliquias (S5, S8) | Pulir es el único reinicio: `DoPrestige()` entrega 1 Reliquia cada `consecrateMinPrestiges` (5) Pulidos (`GrantRelic`, `PrestigesToRelic`, `GameState.relicPrestige`; suma `consecrations`, no reinicia nada). `RelicOn(id)`, `ToggleRelic`, `BuyRelicSlot`, `UnlockRelic`/`RefundRelic` (S9: `GameState.relicTokens` = Puntos de Reliquia; elegir y devolver a mano), `BuyColor`, `AddEternity`, `RollBossMod`). Datos: `EconomyConfig.relics` (`RelicDef`), `bossMods` (`BossModDef`), `consecrate*`, `relicSlot*`, `eternityPer*`. Efectos en `PowerSystem` (`GM.RelicOn("id")`); mods de jefe en `Monolith.Mod` + `MonolithSpawner.SpawnBoss`. UI `UI/HudRelics.cs` (overlay `reliquias`, tecla R). Estado en `GameState` (`consecrations`, `relics`, `relicsEquipped`, `eternity`…). |
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
- Si `Unity_RunCommand` falla con "No logs available" (visto en la Sesión 1), usa `Unity_ManageEditor` (Play/Stop/GetState) y
  `Unity_ManageMenuItem` (parámetros `Action`/`MenuPath` con mayúscula) para ejecutar los menús aditivos (migran `Economy.asset`).
  En la Sesión 1b volvió a funcionar sin tocar nada tras comprobar `Unity_ManageEditor GetProjectRoot` (fallo transitorio
  del puente: editor ocupado/otro editor). Si persiste: dar foco al editor de Unity o reabrir el proyecto y repetir.
- Dentro de RunCommand, `CompilationPipeline` choca con `Unity.CompilationPipeline`: escribe
  `UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation()` con el nombre completo.
- Cambiar valores ya existentes de `Economy.asset` desde RunCommand: `AssetDatabase.LoadAssetAtPath<EconomyConfig>`, editar,
  `EditorUtility.SetDirty` + `AssetDatabase.SaveAssets()` (diff mínimo; `EnsureDefaults()` no sobrescribe valores existentes).
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
- La "Sonda de Reflexión" en modo OnEnable conserva la sala anterior (suelo con el tinte de otra sala): `BiomeManager`
  la pasa a OnDemand y llama `RequestRenderNextUpdate()` en cada cambio (desactivar/activar no basta).
- USS: una clase de modificador puede chocar con la de un panel (`.toast.gallery` heredaba `.gallery { position: absolute }`).
  Antes de añadir clases a elementos generados, `grep` la clase en `Hud.uss`. Los .uss se pueden reimportar en Play
  (`AssetDatabase.ImportAsset`); los .uxml no (rehacen el árbol).
- Para esperar y capturar en Play sin latencia del puente: corrutina sobre `hud.StartCoroutine(...)` con
  `WaitForSecondsRealtime` y `ScreenCapture` dentro del mismo RunCommand.
- Esquirlas/emisivos nuevos de sala: ~90 nits (como `M_Shard_*`) y bloom 0,12 / scatter 0,5 en su `VP_*`.
- Esperar en tiempo real entre RunCommands: `PowerShell Start-Sleep` (el `sleep` en primer plano de Bash está bloqueado).

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

**Progresión por Pulido:** coste del Taller = `CostOf` (`GameManager.WorkshopCostFactor`, params `workshopCost*` en
`EconomyConfig`); topes = `GameManager.MaxLevelOf(d)` (`maxLevelPerPrestige`/`maxLevelLimit`); hitos de ayudantes =
`unlockStat/unlockThreshold` (`GameManager.RunStat`, `OnVolatileBroken` lo llama `Monolith.Erupt`); resumen al Pulir =
`GameManager.PrestigeReport` (modal en `Hud.OnPrestiged`). Estado de vida en `GameState` (`lifeStartGallery`, `runVolatile`, `lastLife*`).
Simulador: `CAPS`, `MILESTONE`, env `SIMK` (k), `SIMPG` (crecimiento/pulido), tabla "Vida | Taller completo".
Calibración actual (S1b): k 1,8 · crecimiento 1,6/Pulido · Acólitos +3,25/Pulido (límite 60) → Taller completo ~12 min
en las vidas 1-3 y +25 % de media cada 3 pulidos (tabla en GDD §5). Acólitos es el cuello de botella del Taller completo:
su tope por Pulido es la palanca principal de esa curva; k mueve sobre todo la vida 1 (k ≥ 2,2 la atasca).
Para barridos, importa el módulo y cambia `bs.SCALE_K`, `bs.CFG["costPrestigeGrowth"]`, `bs.CAPS` antes de `Sim().run_sim(...)`.
Usa siempre `Hud`/`gm.IsMaxed`/`gm.CostOf` (no `d.maxLevel`/`d.Cost` directos).

**Añadir bioma/sala:** crea `Biome_X.asset` (Create ▸ Marmoreal ▸ Bioma o copia uno): `id` único, `campaignStage`,
6 `chunkMaterials` (mismo nº que los tiers base) y 7 `shardMaterials` (el último, dorado), `swaps` desde los
materiales base de `Materials/` (M_Floor, M_Column, M_OuterGround, M_Gold, M_BrassInlay…) a los nuevos, Sol y
`VolumeProfile` (copia de `VP_Santuario`). Crea un hijo en `--- SALA ---` con su decoración y añade ambos a
`BiomeManager.biomes`/`rooms` (mismo índice). Regla nueva = valor en `BiomeRule` + su lógica en `BiomeManager`
(o un gancho como `OnPlayerHit`/`FreeShrapnel`). Un material nunca debe ser `from` y `to` a la vez.
Probar: forzar Pulidos en Play (`gm.S.galleryLevel = prestigeMinGallery + 2; bm.CycleChoice(p+1); gm.DoPrestige()`).

**Añadir mutador:** `MutatorDef` en `Economy.asset` → `mutators` (y en `FillDefaults()` para migrar). Si solo usa los
campos existentes (multiplicadores, `alwaysCrit`, `noDash`, `disabledPower`, `volatileBonus`) no hay código; un efecto nuevo =
campo nuevo + leerlo donde aplique (stats en `GameManager.RecalcStats`). Actualiza la media `MUT` del simulador.

**Añadir evento temporal:** `LifeEventDef` en `lifeEvents` (`goldenChance`, `frenzyMult`, `meteorEvery/Count`, `duration`,
`minGallery`, `weight`). Efecto nuevo = campo + lógica en `LifeDirector.StartEvent`/`UpdateEvents`. Probar con
`FindFirstObjectByType<LifeDirector>().StartEvent("id")` en Play.

**Añadir contrato:** `ContractDef` en `contracts` con un `ContractKind` existente (`target`, `window`, `rewardIcor`,
`rewardEssencePct`). Tipo nuevo = valor en `ContractKind` + `case` en `LifeDirector.Progress` (sondear `GameState`, sin eventos).

**Probar el Coloso:** en Play, `gm.S.galleryLevel = 9 (o 19…); gm.S.bossDoneGallery = -1;` y alejar la canica del centro
(`bossClearRadius`); para ver las fases, `boss.TakeDamage(boss.HP*0.4f, …)`. Con la partida del usuario (daño enorme) cae al
instante: pon a 0 los niveles del Taller y `RecalcStats()`.

**Probar la campaña:** en Play (partida de prueba), sala con Prueba pendiente y `gm.S.galleryLevel = trialGallery - 1`,
alejar la canica del centro → brota el jefe "PRUEBA DE LA SALA"; rematarlo con `b.TakeDamage((float)(b.HPLeft*0.5+1), …)` en bucle.
Piezas directas: `gm.CompleteTrial(stage)`; con `campaignPiecesRequired` piezas salta la victoria (3 s después).
Infinito: botón del modal o `gm.EnterInfinite()`; luego `gm.DoPrestige()` rota sala y ofrece 3 mutadores sin carta segura.
Añadir una sala de campaña: `BiomeDef` con `campaignStage` = índice de su pieza y `trialGallery`; ajusta `SIMTRIALS`.
Cinemática de victoria: con 7 piezas sale a los 3 s; probar con `gm.CompleteTrial(i)` para i=0..6 (partida de prueba). Forzar sala: `gm.S.biomeId = "hielo"` en Play (BiomeManager la aplica al frame siguiente; `hud_state` muestra `brotes`/`drag`).

**Añadir reliquia:** `RelicDef` en `relics` (y en `FillDefaults()`; para volcarla al asset ejecuta el menú aditivo *Añadir campaña…*, que llama `EnsureDefaults`), y en `PowerSystem` un `if (GM.RelicOn("id"))` en el poder que modifica. Probar en Play: `gm.S.relics.Add(id); gm.S.relicsEquipped.Add(id)` y `ps.Cast(gm.config.GetPower(poder), true)`; Hito de Reliquia: `gm.S.relicPrestige = gm.S.prestigeCount - 4; gm.S.galleryLevel = prestigeMinGallery + 2; gm.DoPrestige()`. El ×coste del Taller por Pulido usa `PrestigesSinceConsecrate` (congelado en la última Consagración antigua, S5-S7). Simulador S9: `SIMHP`, `SIMIRA`, `SIMTRIALS`, `SIMLEGACY`/`SIMREFUND`. Migración única `GameManager.MigrateConsecrateRefund` (flag `consecrateRefundDone`, aviso en `PendingNotice`). Simulador: `SIMHITO=5 SIMHBONUS=0.25` (los viejos `SIMCONS`/`SIMCRESET`/`SIMKEEP` modelan la Consagración con reinicio, ya no usada). Modales irreversibles: `OpenModal(..., clickOnly: true)` (Intro no confirma). Simular teclas reales en Play: `InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(Key.X))`, esperar 2 frames y soltar con `new KeyboardState()` (no llamar `InputSystem.Update()`). Cinemática de victoria: oculta con `forceRenderingOff` lo que cruce la línea cámara→monumento (bounds < 12 m) y cierra paneles abiertos. Modificador de jefe: `BossModDef` en `bossMods`. Simulador: `SIMCONS`.

**Retocar balance:** `python Tools/balance_sim.py 20` → tabla de hitos (objetivos en `Docs/GDD.md §5`).
Prueba real: piloto automático compra lo más barato; compara `Temp/hud_state.txt` con el simulador.

**Cambiar aspecto:** materiales en `Assets/Marmoreal/Materials` (HDRP/Lit; tras cambiar propiedades por
script llama `HDMaterial.ValidateMaterial`). Iluminación: objetos de `--- ILUMINACIÓN ---` y
`Config/VP_Santuario.asset` (exposición fija EV 9.5: luces en lux/lúmenes físicos).
Salas: sus `VP_*` deben igualar a `VP_Santuario` en niebla (150 m), luminancia del cielo y `colorFilter` (casi blanco); Sol 2600 lux
casi blanco en `Biome_*`. Si una sala se ve oscura, mira primero el `_BaseColor` de `M_Floor_*`/`M_Column_*`/`M_OuterGround_*` (S8: los
oscuros eran la causa principal). Comparar: forzar `gm.S.biomeId`, `pm.Respawn()`, `rig.yaw=0; rig.pitch=28; rig.distance=22` y capturar.

**S10 (UI/fluidez):** `workshopCostScale = 0` en `Economy.asset` (el Taller ya no se encarece al Pulir); `IsUnlocked` ya no oculta
mejoras del Taller por galería/hito; números arábigos (`Num.Roman` devuelve `n.ToString()`). Mejoras `pw_*` se muestran en el
ALTAR [G] (`Hud.IsPowerUpgrade`/`InTree`/`TreePos`), no en el Taller. `GameManager.MutatorBlocks(d)` oculta lo anulado por el mutador.
Menú de pausa (Esc / `menu-btn`, `Time.timeScale = 0`), Taller plegable (B / `shop-toggle`), objetivo en `objective` (`Hud.ObjectiveText`).
