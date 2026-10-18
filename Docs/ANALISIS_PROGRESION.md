# MARMOREAL — Análisis de progresión y plan contra la repetición

Estado: diagnóstico + propuesta. Nada de esto está implementado. Cifras citadas = `Economy.asset` / `Tools/balance_sim.py`.

## 1. Diagnóstico (lo que cuenta el jugador, y por qué pasa)

| Síntoma | Causa en el código/economía |
|---|---|
| Tras Pulir, en ~1 min todo está al máximo salvo Masa, Pureza y Cincel | Las mejoras con tope (Inercia 25, Imán 25, Fortuna 18, Cantera 12, Acólitos 12, Resonancia 60, Doble Embestida 3…) cuestan poco **frente al polvo/s** que dan *Brillo Eterno* y *Obra Maestra* (multiplican TODO el polvo y no se pierden). El coste base del Taller **no escala** con ese poder permanente. |
| Pulir se siente "volver a empezar con todo" | El Pulido solo reinicia números: mismo mapa, mismos bloques, mismas mejoras, mismo orden de compra. No hay decisión nueva ni sorpresa en la 2.ª/5.ª/10.ª vida. |
| Los ayudantes (Acólitos/Discípulas) "se desbloquean en nada" | Se compran con polvo y están capados bajo (12 / 8). Con el multiplicador permanente cuestan segundos. Desbloqueo por Galería III-V, que con *Memoria* (galerías iniciales) ya está cumplido al empezar. |
| Solo hay 3 mejoras "vivas" (Masa, Pureza, Cincel) | Son las únicas **sin tope** (exponenciales hasta 500). El resto es una lista de "compra y olvida". |
| Más dinero pero más difícil = "meh" | Regla de diseño actual: vida ×1.8/galería > recompensa ×1.5/galería. Es un muro, pero el muro es **siempre el mismo número** subiendo: no cambia *qué haces*, solo *cuánto tardas*. |
| Todo se ve igual | Un solo mapa, un solo set de materiales por tier (cicla cada 7 galerías), mismos bloques. Lo único que varía: dorados, volcánicos (nuevos). |

Conclusión: el problema **no es la dificultad**, es que **entre vidas no cambian las decisiones ni el contexto**. Hay que (a) hacer que el Taller no se agote en 1 min y (b) que cada Pulido te dé un **mundo/reglas distintos**.

## 2. Principios para el rediseño
1. **Cada vida debe plantear una pregunta distinta** (bioma + modificador + objetivo), no la misma tabla de compra.
2. **El Taller debe seguir siendo una conversación durante toda la vida**: nunca "todo al máximo" antes de los ~10 min.
3. **Lo permanente debe desbloquear contenido, no solo multiplicar**. (Hoy Brillo/Obra solo multiplican.)
4. **Dificultad por rareza/variedad, no solo por HP**: el muro de HP se mantiene pero se suavizan los picos con objetivos y bloques especiales.
5. **Aditivo e idempotente** (regla de `CLAUDE.md`): nada de reconstruir la escena; biomas como objetos/prefabs editables.

## 3. Propuestas (ordenadas por impacto / coste)

### A. Arreglar el "todo al máximo en 1 minuto" (impacto ALTO, coste BAJO — hacerlo primero)
- **Coste del Taller escalado por poder permanente.** `Cost = baseCost × growth^nivel × (GlobalMult × FameMult)^k` con k≈0.6-0.8 (editable en `EconomyConfig`). Así los multiplicadores permanentes siguen dando ritmo, pero la compra tarda siempre un tiempo parecido. Calibrar con el simulador: objetivo "Taller completo ≈ 8-12 min tras cada Pulido" que **crece** con los pulidos.
- **Topes que dependen del Pulido**: Cantera 12→ +2 por pulido, Acólitos/Discípulas +1 por cada 2 pulidos hasta un máximo, etc. Cada vida abre un poco más.
- **Desbloqueo de ayudantes por hito, no por polvo**: Discípulas = "Galería X en esta vida" o "N bloques volcánicos rotos"; Acólitos igual. Evita comprarlos a los 5 s.
- **Más mejoras exponenciales "vivas"** (como Masa/Pureza/Cincel): *Vínculo* (Discípulas), *Resonancia* y *Doble Embestida* pasan a niveles ilimitados con coste que crece fuerte; así siempre hay en qué gastar.

### B. Biomas por Pulido — "cambiar el mapa al pulir" (impacto ALTO, coste MEDIO-ALTO)
Cada Pulido elige (o rota) una **Sala** con reglas propias. Todas reutilizan el cuenco (`ArenaShape`) y cambian materiales, bloques, pedestales y UNA regla:

| Sala | Look | Regla/mecánica propia |
|---|---|---|
| Santuario de Carrara (actual) | mármol blanco/negro | base |
| Cantera Volcánica | roca oscura + lava en las juntas | 25 % bloques volcánicos (encadenan); fuego en el suelo que daña si te quedas quieto |
| Gruta de Cristal | cristal azul, luz interior | los bloques **reflejan** parte del daño en rayos; los trozos de cristal rebotan y sirven de metralla gratis |
| Catedral de Hielo | blanco azulado | suelo resbaladizo (fricción baja → más velocidad, más difícil frenar): premia Inercia y Embestida |
| Forja de Hierro | gris/naranja | bloques de **metal** (más HP, polvo ×3); imanes en el suelo que atraen la canica |
| Jardín de Rosa | rosa/verde | bloques que **brotan** de a poco y se multiplican si no los rompes (presión de tiempo) |
| Panteón Dorado (final) | oro + negro | todo dorado, bloques gigantes tipo jefe |

Implementación sugerida: un `BiomeDef` (ScriptableObject en `Config/`) con materiales por tier, prefabs de pedestal/columna/decoración, lista de reglas y un `Volume/Light` opcional. Escena: un objeto `--- SALA ---` con hijos por bioma activables (editable a mano). Pulir llama a `BiomeManager.Select(next)`. La selección puede ser: rotación fija, **elegir entre 2-3 al pulir**, o desbloqueo por Esencia.

### C. Modificadores de vida ("Mutadores") (impacto ALTO, coste MEDIO)
Al pulir eliges 1 de 3 cartas **con riesgo/recompensa** (tipo Hades/Vampire Survivors): p. ej. "Bloques ×2 de vida, polvo ×3", "Sin Acólitos, Discípulas ×3", "Crítico siempre pero sin Embestida", "Los poderes tardan la mitad pero sin Supernova", "Lluvia de volcánicos". Dan Esencia extra si superas el objetivo. Cambian la forma de jugar cada vida y rompen la rutina de compra.

### D. Estructura de la vida: objetivos y eventos (impacto MEDIO, coste BAJO-MEDIO)
- **Jefe cada 10 galerías**: un monolito colosal (HP ×50, 3 fases, estatua especial) con recompensa de Ícor. Rompe la monotonía del goteo de bloques iguales.
- **Eventos temporales** (ya en backlog): "Luna de Ónice" (todo dorado 30 s), "Lluvia de Cristal", "Hora Dorada". Se disparan por tiempo/galería.
- **Contratos** (2-3 objetivos opcionales por vida: "rompe 5 volcánicos de una explosión", "combo 80", "usa 4 poderes en 10 s") → Ícor/Esencia.
- **Mejora de variedad de bloques**: tipos nuevos además de volcánico/dorado: *espejo* (refleja daño), *hielo* (resbala), *bomba* (arrastra a vecinos), *hueco* (estatua con más Ícor). Aparecen según galería y sala.

### E. Pulir con más profundidad (impacto MEDIO-ALTO, coste ALTO)
- **Segunda capa de prestigio** ("Consagrar"/"Reliquias"): tras N pulidos, reinicia Esencia a cambio de **Reliquias** que modifican poderes de forma cualitativa (Supernova en racimo, Vórtice que se divide, Rayo que rebota al revés…). Es el backlog "reliquias equipables".
- **Árbol del Escultor con ramas alternativas** excluyentes por vida (elegir "especialización": Fuerza vs Ritmo vs Espíritu) → build distinto cada ciclo.
- **Memoria del Escultor** hoy da galerías iniciales: cambiar a "empiezas con X mejora del Taller al nivel Y" para dar sensación de "arranque con ventaja" sin saltarse el contenido.

### F. Juice y feedback de progreso (impacto MEDIO, coste BAJO)
- Cambio visible por Pulido: canica con material nuevo ya existe; añadir **cambio de iluminación/cielo/música** por sala, banner de entrada a la sala y resumen "Esta vida: X estatuas, mejor golpe, tiempo".
- Panel de "Qué ha cambiado este Pulido" (nuevas mejoras/topes desbloqueados) para que el jugador sienta que Pulir **abre** cosas, no resetea.

## 4. Balance: reglas nuevas propuestas
1. Duración objetivo del Taller por vida: 8-12 min para completarlo, +30 % por cada 3 pulidos (más topes, no solo coste).
2. Mantener "muro suave" pero con **dos muros**: HP de bloque (como hoy) **y** modificador de sala/mutador (se supera cambiando de estrategia, no solo esperando).
3. Ritmo de Pulido objetivo: 1.º a ~45 min; siguientes cada 40-60 min **de juego activo** (hoy los pulidos 5-13 salen a ~100 min, buen ritmo, pero hay que añadir contenido nuevo cada vez).
4. Cada Pulido debe desbloquear ≥1 cosa visible (sala, mejora, tope, evento). Tabla de "premio por pulido" en `Economy.asset`.
5. Toda cifra nueva va a `Economy.asset` + `balance_sim.py` (añadir al simulador: coste escalado por GlobalMult, topes por pulido, mutadores).

## 5. Hoja de ruta por sesiones (propuesta)
1. **Sesión 1 — Economía/ritmo** (A): coste escalado, topes por pulido, ayudantes por hito, mejoras vivas. Simulador primero.
2. **Sesión 2 — Salas/biomas** (B + F): `BiomeDef`, 3 salas jugables (Carrara, Volcánica, Cristal), transición al Pulir, ajuste de luz/materiales.
3. **Sesión 3 — Mutadores + jefes + eventos + contratos** (C + D).
4. **Sesión 4 — Reliquias / segunda capa** (E) y pulido general.
Verificar cada sesión en Play con el flujo del skill `marmoreal-dev` (guardado de prueba con backup).

## 6. Riesgos
- Costes escalados mal calibrados → el jugador se atasca o vuelve al "todo en 1 min". Mitigar con el simulador y un tope editable.
- Biomas = más assets/escena: mantener prefabs editables y builder aditivo.
- Mutadores muy duros → frustración: siempre dar una opción "segura".
- Rendimiento: los bloques/efectos nuevos usan `ShardSystem` instanciado, sin GameObjects extra.

## 7. Ya hecho en la sesión anterior (para no repetir)
Mecánicas del Árbol a 4 niveles, Doble Embestida, Discípulas + Vínculo, Cadena de Rayos [7], Vórtice errante con implosión, Gran Meteoro, 8 bendiciones de Musas, mármol volcánico. **Se quitaron las Runas de Impulso** (estorbaban al rodar).
