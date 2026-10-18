# Prompt para la siguiente sesión (copiar y pegar)

Proyecto MARMOREAL (Unity 6.3 + HDRP). Lee primero `CLAUDE.md`, el skill `.claude/skills/marmoreal-dev/SKILL.md`,
`Docs/GDD.md` y **`Docs/ANALISIS_PROGRESION.md`** (diagnóstico y propuesta completos).

## Problema a resolver
El juego se siente repetitivo al Pulir: tras cada Pulido, en ~1 minuto compro todo salvo Masa, Pureza y Cincel (el resto
tiene tope y cuesta poco frente al multiplicador permanente de Brillo Eterno/Obra Maestra); los ayudantes (Acólitos,
Discípulas) se desbloquean al instante; el mapa y las reglas son siempre iguales, así que Pulir = "empezar de 0 con
todo". Quiero que cada vida/Pulido se sienta distinta y que el Taller no se agote en 1 minuto, sin volver el juego
imposible ni perder lo divertido que es ahora.

## Trabajo de esta sesión (SESIÓN 1 — Economía y ritmo; no hagas biomas todavía)
1. **Simulador primero**: en `Tools/balance_sim.py` modela y calibra antes de tocar el juego. Objetivo: Taller completo
   en ~8-12 min tras cada Pulido, creciendo ~30 % cada 3 pulidos; primer Pulido ~45 min; sin atascos largos.
2. **Coste del Taller escalado por poder permanente**: `Cost = baseCost × growth^nivel × (GlobalMult × FameMult)^k`, con `k`
   y activación editables en `EconomyConfig`/`Economy.asset` (migración aditiva con `EnsureDefaults`, sin borrar partidas).
3. **Topes dependientes del Pulido** (Cantera, Acólitos, Discípulas, Inercia, etc. suben un poco por Pulido) y más mejoras
   "vivas" ilimitadas (Vínculo, Resonancia, Doble Embestida…) para que siempre haya en qué gastar.
4. **Ayudantes por hito**, no por compra instantánea (p. ej. Galería X en esta vida / N volcánicos rotos).
5. **Feedback**: al Pulir, panel "Qué se abre ahora" (topes/mejoras nuevas) y resumen de la vida anterior.
6. Actualiza `Docs/GDD.md` (tabla de balance §5), el skill y el simulador. Ejecuta `python Tools/balance_sim.py 20` y
   pega la tabla antes/después.

## Reglas
- Todo editable en escena/assets (`CLAUDE.md`); herramientas de editor **aditivas e idempotentes**.
- Flujo de verificación del skill: compilar con `Unity_RunCommand`, **esperar** al `.dll`, no editar `.cs` en Play,
  probar con partida de prueba y **restaurar** `Library/marmoreal_user_save_backup.json` tras Stop. Comprueba
  `Unity_ManageEditor GetProjectRoot` por si hay otro proyecto Unity abierto.
- No rompas la partida del usuario (`marmoreal.save.v1`); haz backup antes de probar.
- Pregúntame solo lo imprescindible; propón valores y calibra con el simulador.

## Después (sesiones siguientes, ver el análisis §5)
2. Salas/biomas por Pulido (`BiomeDef`, 3 salas: Carrara, Cantera Volcánica, Gruta de Cristal).
3. Mutadores al pulir + jefe cada 10 galerías + eventos + contratos.
4. Reliquias / segunda capa de prestigio.
