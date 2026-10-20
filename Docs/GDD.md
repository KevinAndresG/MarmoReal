# MARMOREAL — Documento de Diseño (GDD)

> *"Dentro de cada bloque duerme una estatua. Libérala."*

## 1. Concepto

**Género:** Incremental / idle con control físico en 3D (HDRP).
**Fantasía:** eres una **canica de mármol pulido** que rueda por un santuario de mármol en forma de
cuenco. Del suelo brotan **bloques de mármol en bruto**; al chocar contra ellos arrancas trozos y,
cuando cae el último, **se revela la estatua** que dormía dentro, que brilla y estalla en polvo.

**Mezcla de conceptos:**
- *Destroy the Cube* (incremental de destrucción) → bloques que se tallan trozo a trozo.
- *Marble Madness / pinball* → la **velocidad es el daño** (energía cinética ∝ v²), las laderas del
  cuenco dan inercia y la mecánica *Rebote Vivo* encadena golpes.
- Miguel Ángel ("la escultura ya está en la piedra") → cada bloque esconde una estatua: recompensa
  visual garantizada en cada ciclo.
- Idle clásico → acólitos que tallan solos, progreso offline, prestigio con árbol de habilidades.

**Gancho de 10 segundos:** pantalla de título sobre el santuario en órbita → pulsas una tecla →
ruedas, chocas, los trozos saltan, el polvo luminoso vuela hacia ti con notas pentatónicas
ascendentes, la estatua se revela con un acorde. Primera mejora comprable en ~15 s.

## 2. Controles

| Acción | Teclado/Ratón | Mando |
|---|---|---|
| Rodar | WASD / Flechas | Stick izq. |
| Saltar (suelo o encima de bloques) | Espacio | A |
| Golpe en Picado (2.º salto en el aire: cae en vertical; daño normal según la velocidad de caída) | Espacio en el aire | A en el aire |
| Embestida (dash, ×2 daño; varias cargas con *Doble Embestida*) | Shift | B / RB |
| Onda de Resonancia | F / Clic izq. | Y |
| Girar cámara | Clic der. + arrastrar / Q-E | Stick der. |
| Zoom | Rueda | — |
| Bloquear cámara (tercera persona: cursor oculto, el ratón gira siempre) | V | — |
| Colección | C | — |
| Poder n (Supernova, Ingravidez, Meteoros, Coloso, Vórtice, Midas, Rayos) | 1-7 / clic en la barra | Cruceta (4 primeros) |
| Comprar mejora n del Taller | Ctrl + 1-9 | — |
| Árbol del Escultor | T / Tab | — |
| Altar de los Dioses | G | — |
| Mecenazgos | L | — |
| Pulir (prestigio) | P | — |
| Silenciar | M | — |
| Piloto automático (debug) | F9 (solo Editor/Development) | — |

## 3. Bucles de juego

### Bucle núcleo (segundos)
Rodar → ganar velocidad (laderas, embestida) → **impacto** (daño = Masa × (v/9)² × combo × crítico)
→ los trozos más cercanos al impacto se desprenden → **polvo** sale disparado y se queda en el suelo
hasta que pasas cerca: solo lo atrae el radio del **Imán** (y, durante unos segundos, la onda F o la
Supernova dentro de su radio; el Vórtice lo absorbe todo). Nunca vuela solo por tiempo → al quitar el último trozo la **estatua** se revela (1.3 s de brillo) y estalla en una gran
lluvia de polvo.

### Bucle de sesión (minutos)
Polvo → **Taller** (9 mejoras) → bloques más rápidos → **Galería** sube cada N estatuas (8, 10, 12…
máx. 30) → el mármol se endurece (vida ×1.8) pero el polvo vale más (×1.5) y el bloque cambia de
color (Carrara → Rosa → Verde → Nero → Calacatta → Siena…).

### Bucle meta (horas)
Muro suave (la vida crece más que la recompensa) → **Pulir la canica** (prestigio, desde la
Galería IX) → **Esencia** → **Árbol del Escultor** (multiplicadores + **mecánicas nuevas**) →
la canica renace en otro mármol (Carrara → Rosa Portogallo → Verde Alpi → Nero Marquina →
Calacatta Oro) y se llega más lejos.

## 4. Sistemas

### 4.1 Bloques (Monolith)
- Prefabs `Monolith_Small` (2×3×2 = 12 trozos) y `Monolith_Large` (3×4×3 hueco = 34 trozos).
- Vida total repartida por trozo; el daño acumulado arranca trozos (el más cercano al impacto).
- Probabilidad de bloque grande: 0 % en la Galería I, +4 %/galería (máx. 45 %). Vida y recompensa ×2.6.
- **Bloque dorado:** 3 % (+1.5 %/nivel de *Fiebre Dorada*), vida ×3, recompensa ×12 × *Veta de Oro*,
  estatua de oro. Aviso con toast y campanillas.
- Estatuas: Busto, Ánfora, Obelisco, Esfera Armilar, Columna Salomónica (versión de mármol y de oro).

- **Asentamiento:** al romper un trozo, los de encima en la misma columna caen un nivel con
  gravedad (golpe sordo al aterrizar). Nada queda flotando; la estatua del interior sostiene la
  tapa del bloque grande hasta que se rompe lo de alrededor.
- **Límite de la arena:** pared invisible suave en el borde de la terraza; si aun así la canica
  cae al vacío, reaparece en el centro (teletransporte del Rigidbody, no solo del Transform).

### 4.2 Combo y críticos
- Cada impacto suma combo (+3 % daño, máx. 50 golpes = ×2.5) con ventana de 2.5 s.
- Crítico: probabilidad = *Fortuna*; multiplicador ×3 (+1 por nivel de *Ojo Clínico*).
- Hit-stop (micro-pausa) y temblor de cámara proporcionales al golpe.

### 4.3 Resonancia
- +10 % por impacto, +1.2 % por golpe de acólito. Al 100 %: **Onda** que talla todo en su radio
  (9 m + 0.45 m/nivel) con daño = Masa × 6 × *Resonancia* × combo y atrae durante 3 s el polvo que haya dentro de su radio.

### 4.4 Acólitos (idle)
- Esferas luminosas que orbitan la canica; cada 1.1 s se lanzan al bloque más cercano (16 m).
- Base del **progreso offline** (hasta 8 h × eficiencia de *Paciencia*).

### 4.5 Taller (Polvo) — `Assets/Marmoreal/Config/Economy.asset`

| Mejora | Coste base | Crec. | Efecto | Desbloqueo |
|---|---|---|---|---|
| Masa | 12 | ×1.5 | daño 3 × 1.3^n | — |
| Inercia | 30 | ×1.75 | vel. 11 + 0.75n m/s (tope 25, +1 por Pulido, hasta 60) | — |
| Imán | 20 | ×1.6 | radio 5 + 0.9n m (tope 25, +1/Pulido, hasta 60) | — |
| Pureza | 45 | ×1.75 | polvo × 1.2^n | — |
| Fortuna | 150 | ×1.9 | crítico 5 % + 2.5 %/n (tope 18, +1/Pulido, hasta 30) | Galería II |
| Cantera | 80 | ×2.15 | bloques simultáneos 5 + n (tope 12, +1/Pulido, hasta 30) | Galería II |
| Acólitos | 150 | ×3.0 | +1 acólito (tope 12, +1,5/Pulido, hasta 50) | Galería III **y** hito: subir 3 galerías en esta vida |
| Cincel | 220 | ×1.6 | daño acólito 1.6 × 1.28^n | Galería III |
| Resonancia | 300 | ×1.7 | onda × (1 + 0.35n) (**sin tope**) | Galería IV |
| Doble Embestida | 600 | ×6 | cargas de Embestida 1 + n (tope 3, +1 cada 2 Pulidos, hasta 8) | Galería III |
| Discípulas | 2 500 | ×3.2 | +1 canica aliada (tope 8, +1/Pulido, hasta 24) | Galería V **y** hito: romper 12 volcánicos en esta vida |
| Vínculo | 4 000 | ×1.85 | las Discípulas copian 30 % + 5 %/n de tu daño (**sin tope**) | 1.ª Discípula |
| Pólvora Divina | 400 | ×2.2 | daño Supernova × 0.25·1.1^n (máx. 20 → ×1.68) | poder Supernova |
| Gravedad Rota | 900 | ×2.2 | daño Ingravidez × 0.25·1.1^n | poder Ingravidez |
| Núcleo Ígneo | 900 | ×2.2 | daño Meteoros × 0.25·1.1^n | poder Meteoros |
| Gigantismo | 2 500 | ×2.1 | duración Coloso 4 + 0.6n s (máx. 15) | poder Coloso |
| Singularidad | 4 000 | ×2.2 | daño Vórtice × 0.25·1.1^n | poder Vórtice |
| Alquimia | 20 000 | ×8 | bloques de Midas 1 + n (máx. 4) | poder Midas |
| Ira de Zeus | 6 000 | ×2.2 | daño Cadena de Rayos × 0.25·1.1^n | poder Rayos |

**Discípulas** (`Gameplay/AllySquad.cs` + `AllyMarble.cs`, prefab `Prefabs/Ally`): canicas físicas que eligen uno
de los 3 bloques más cercanos, toman carrerilla y lo embisten. Daño = tu daño de impacto × (v/9)² × Vínculo (sin
combo; críticos y Furia sí). No usan poderes. No chocan con la canica ni entre ellas. Dan algo de Resonancia.

**Progresión por Pulido (Sesión 1)** — todo editable en `Economy.asset`:
- **Coste escalado:** `Coste = baseCost × growth^nivel × (Brillo×Obra×Fama)^k × crecimiento^pulidos` (solo Taller, campos
  `workshopCostScale/K/PerPrestige`; por mejora `scaleCost`). Defecto: k = 2, crecimiento 1.14. El poder permanente
  sigue dando ritmo, pero el Taller deja de agotarse en 1 min.
- **Topes que suben:** `maxLevelPerPrestige` (admite fracciones) y `maxLevelLimit` en cada `UpgradeDef`; la HUD muestra el tope actual.
- **Ayudantes por hito:** `unlockStat` (`runGalleries` / `runVolatile`) + `unlockThreshold` + `unlockHint`; cuentan solo la vida actual.
- **Mejoras vivas sin tope:** Masa, Pureza, Cincel, Resonancia y Vínculo.
- **Al Pulir:** modal con resumen de la vida anterior y "qué se abre ahora" (topes nuevos, hitos).
- Migración: `EnsureDefaults()` copia estos campos a mejoras existentes solo si el asset no los trae.

Las 7 últimas (borde naranja) son las **mejoras de poderes**: aparecen al desbloquear el poder en el
Altar y, como todo el Taller, se pierden al Pulir. Así cada vida empieza con poderes flojos
(×0.25) y se vuelven devastadores a medida que inviertes polvo.

### 4.6 Árbol del Escultor (Esencia)

```
                         ☀ Brillo Eterno (raíz, ×1.25 todo)
          ┌──────────────────────┼──────────────────────┐
   ● Fuerza Ancestral      ♛ Veta de Oro          ◌ Acólito Eterno
   ├ ✧ Ojo Clínico          │                      ├ ♫ Coro de Acólitos
   └ ↯ REBOTE VIVO         ✪ Fiebre Dorada         └ ⌛ Paciencia
      ├ ✺ EMBESTIDA SÍSMICA │                         ├ » Impulso
      └ ⁂ METRALLA         ⌂ Memoria del Escultor     └ ≋ ECO RESONANTE
                            │                             └ ∞ RESONANCIA PERPETUA
                            └──────────── ♜ OBRA MAESTRA (cumbre, ×2 todo/nivel) ─┘
```

Nodos en MAYÚSCULAS = **mecánicas nuevas** (cambian cómo se juega, no solo números). Todas tienen **4 niveles**
(coste ×2.5 por nivel): el nivel 1 desbloquea la mecánica y los siguientes la potencian.
- **Rebote Vivo:** salir despedida tras cada choque (+40/50/60/70 % de la velocidad de impacto) → estilo pinball.
- **Embestida Sísmica:** los choques del dash y del Picado liberan mini-ondas (daño ×3/6/9/12, radio 4-6.25 m).
- **Metralla:** los trozos que saltan dañan otros bloques (×0.4/0.8/1.2/1.6 del daño) → reacciones en cadena.
- **Eco Resonante:** la onda se repite en 2/3/4/5 ecos (×0.5, 0.4, 0.3, 0.25…; radio +30 % por eco).
- **Resonancia Perpetua:** la onda se dispara sola al llenarse y la Resonancia carga ×1/1.25/1.5/1.75.

### 4.7 Prestigio (Pulir)
- Requiere Galería IX (índice 8). Esencia = ⌊0.3 · (galería − 8 + 3)^1.6⌋.
- Único reinicio del juego (S8): polvo, Taller y galerías; conserva Esencia, Árbol, Ícor, Altar y lo demás. *Memoria* da galerías iniciales.
- Cada 5 Pulidos trae una Reliquia (hito, ver §5 "Tres capas").
- El botón muestra la ganancia actual **y la de la siguiente galería** para tentar a seguir.

### 4.8 Ícor (moneda divina, permanente)
Nunca se pierde al Pulir. Fuentes (cifras en `Economy.asset`, sección *Ícor*):
- 2 % por estatua liberada; 30 % de dar 1 por bloque dorado **natural** (los dorados de Midas no dan Ícor).
- 1 por Musa atrapada · 2 + ⌊Esencia ganada / 10⌋ al Pulir · el premio de cada Mecenazgo.

### 4.9 Altar de los Dioses (Ícor) — tecla G
Segundo árbol permanente (misma vista que el del Escultor, en paleta fuego). 26 nodos.
**Cada poder es una columna independiente**: se compra en cualquier orden (sin requisitos entre poderes) y debajo tiene
su propia rama de mejoras. Abajo, dos nodos generales que afectan a todos los poderes y, a la derecha, la rama de las Musas.

```
 ✹ SUPERNOVA (6)  ⇡ INGRAVIDEZ (7)  ☄ METEOROS (7)  ⬤ COLOSO (15)   ◎ VÓRTICE (20)   ♛ MIDAS (40)   ↯ RAYOS (25)
 └ ◉ Onda Exp.    └ ⇣ Caída Cel.    └ ⁘ Enjambre    └ ♚ Titán       └ ◍ Horizonte    └ ♕ Rey Midas  └ ⇝ Conductor
   └ ⧗ Mecha C.     └ ⧗ Levedad       └ ⧗ Cielo Ab.   └ ⧗ Crec. Vel.  └ ⧗ Colapso      └ ⧗ Avaricia   └ ⧗ Tormenta P.
     └ ₪ Codicia
                  ϟ Ira Divina (×1.25 daño de poderes)   ∞ Eternidad (−5 % recarga)   ♪ Favor Musas ─ ❦ Inspiración
```
Los nodos ⧗ reducen −7 %/nivel la recarga de su poder (máx. 6 niveles, mínimo ×0.4). Se leen como `<id>_cd`
(`nova_cd` para la Supernova) en `PowerSystem.CooldownTotal`.

### 4.10 Poderes (teclas 1-7)
El daño de cada poder = **daño de impacto actual × potencia × mejora del Taller × Ira Divina**: escala con tu progreso y no
rompe el muro. Cifras en `Economy.asset → powers`; código en `Gameplay/PowerSystem.cs`.

| Poder | Recarga | Efecto |
|---|---|---|
| ✹ Supernova | 120 s (×Mecha Corta) | Planta una bomba con mecha (1.6 s), explota en 12 m (×Onda Expansiva) con ×45 de daño (× Pólvora Divina), destello, empuja la canica y deja **Frenesí** ×2 (×Codicia) 6 s. |
| ⇡ Ingravidez | 150 s | Todos los bloques cercanos flotan girando y se estrellan contra el suelo (×60 × Caída Celestial). |
| ☄ Meteoros | 90 s | 12 (+Enjambre) meteoritos repartidos (uno por bloque antes de repetir; ×35, área 4.5 m) y un **Gran Meteoro** final (×150, área ~10 m) donde más bloques hay juntos. |
| ⬤ Coloso | 80 s | La canica crece ×2.2 durante 4 s (+Gigantismo, +Titán): ×3 daño de impacto, cámara más lejos. |
| ◎ Vórtice | 120 s | Agujero negro **errante** de 6 s: viaja de bloque en bloque, ×25 daño/s creciente en 9 m (×Horizonte), se traga escombros y polvo en espiral y al final **implosiona** (×60 en 13 m). |
| ♛ Toque de Midas | 140 s | Convierte 1 (+Alquimia, +Rey Midas) bloques en oro macizo (×12 polvo). |
| ↯ Cadena de Rayos | 100 s | 6 rayos del cielo en 2.4 s; cada uno salta a 3 (+Conductor) bloques cercanos (14 m), ×30 y −15 % por salto; el primer golpe es crítico. |

### 4.11 Musas (bendiciones aleatorias)
Desde la Galería II aparece un orbe de luz (con haz y marcador en pantalla) cada 60-120 s
(× *Favor de las Musas*); dura 18 s. Al tocarlo: 1 Ícor y una bendición al azar
(duración × *Inspiración*), explicada en un aviso al recogerla:
- **Frenesí:** polvo ×5 durante 20 s.
- **Furia Divina:** 12 s de golpes siempre críticos y Embestida sin recarga.
- **Recarga Divina:** reinicia los poderes (solo sale si tienes alguno).
- **Lluvia Dorada:** la mitad de los bloques (mín. 3) se vuelven oro.
- **Tormenta:** 18 meteoritos y Gran Meteoro.
- **Onda Infinita:** 8 s de Resonancia que se rellena sola (F sin parar).
- **Legión:** 4 Discípulas fantasma durante 20 s (copian ≥50 % de tu daño).
- **Cornucopia:** polvo instantáneo = 90 s de tu polvo/s (mínimo 30 bloques).

### 4.11b Arena: interacciones
- **Mármol volcánico** (desde la Galería III, 6 %): bloque de vetas incandescentes que al romperse del todo
  **estalla** (×12 del daño en 7 m) y puede encadenar otros volcánicos. Material `M_Chunk_Volcanic`, cifras en
  `Economy.asset` (sección *Bloques volcánicos*).

### 4.11c Salas (biomas por Pulido)
Cada vida transcurre en una **Sala**: mismo cuenco, otro look (materiales del santuario, bloques, polvo, Sol,
cielo/niebla) y **una regla**. Datos en `Assets/Marmoreal/Config/Biomes/Biome_*.asset` (`BiomeDef`), aplicados en
Play por `BiomeManager` (`--- SISTEMAS ---`); decoración/luces propias bajo `--- SALA ---/<sala>` (editable a mano).
| Sala | Etapa | Look | Regla (`rule`, `ruleA/B/C`) |
|---|---|---|---|
| Santuario de Carrara | 0 | mármol blanco/negro, atardecer | ninguna |
| Cantera Volcánica | 1 | basalto, juntas y grietas de lava, cielo rojo | +25 % bloques volcánicos (desde la Galería I); **lava**: si vas a < 2,5 m/s 1,6 s seguidos, el suelo te lanza por los aires y pierdes el combo (chispas de aviso) |
| Gruta de Cristal | 2 | cristal azul con luz interior, bosque de cristales | cada golpe tuyo tiene 35 % de **reflejar un rayo** (50 % del daño) al bloque más cercano; los trozos son **metralla gratis** (×0,4 del daño de impacto, o tu nivel de Metralla si es mayor) |
| Catedral de Hielo | 3 | hielo blanco azulado, carámbanos, luz fría | **suelo resbaladizo** (`IceFloor`): ×0,55 aceleración (cuesta frenar y girar), ×1,3 velocidad máxima, rozamiento de la canica 0,01 (`PlayerMarble.SetSurface`) |
| Forja de Hierro | 4 | hierro gris, ascuas naranjas, pilares | **bloques de metal**: `hpMult` 2, `dustMult` 3; 4 **imanes** en el suelo (componente `Magnet`, radio 9 m, 14 m/s²) atraen la canica |
| Jardín de Rosa | 5 | rosa/verde, rosales | **los bloques crecen** (`GardenGrowth`): cada 8 s sin romperlos, ×1,35 HP y polvo, hasta 4 veces |
| Panteón Dorado | 6 | oro y negro, obeliscos dorados, polvo dorado | +25 % bloques grandes; la Prueba es **El Coloso de Mármol** (vida ×1,5 sobre la Prueba normal) |
- **Selección (S4)**: la 1.ª sala está abierta; cada **Prueba superada** abre la siguiente (`BiomeManager.Unlocked`:
  etapa 0, o pieza de la etapa anterior, o `GameState.legacyStage` = pulidos que tenía una partida anterior a la S4).
  Por defecto el Pulido lleva a la sala con la Prueba pendiente más temprana; con todo superado o en el Modo Infinito,
  rotación mezclada entre las desbloqueadas. **CAMBIAR SALA** alterna entre las desbloqueadas.
- Guardado: `GameState.biomeId` (partidas antiguas → `carrara`). Banner con nombre y regla al entrar; el resumen
  de "Canica pulida" incluye la sala.
- Economía: `hpMult`/`dustMult` por sala (1,0 salvo la Forja: ×2 HP, ×3 polvo) — el simulador no los modela.
- Salas 4-7 (S6): menú *Marmoreal/Añadir salas 4-7: Hielo, Forja, Jardín y Panteón (aditivo)* (`BiomeBuilder.AddBiomes47`):
  crea solo lo que falta (materiales `Materials/Biomes/*_Hielo|_Hierro|_Rosa|_Oro`, `VP_*`, `Biome_*`, hijos de `--- SALA ---`)
  y los añade al final de `BiomeManager.biomes/rooms`.

### 4.11d Mutadores, Colosos, eventos y contratos (S3)
Datos en `Economy.asset` (listas `mutators`, `lifeEvents`, `contracts` y cabeceras *Jefes*, *Eventos*, *Contratos*);
`EnsureDefaults()` los migra por id. Lógica: `GameManager` (mutador, Coloso, contratos), `MonolithSpawner.SpawnBoss`,
`Gameplay/LifeDirector.cs` (eventos y progreso de contratos, objeto en `--- SISTEMAS ---`).
- **Mutadores** (`MutatorDef`): al Pulir, el modal "Canica pulida" ofrece 3 cartas (la segura *Camino Sereno* + 2 al
  azar); por defecto queda la segura. Dura toda la vida (`GameState.mutatorId`). Campos: `hpMult`, `dustMult`,
  `magnetMult`, `acolyteMult`, `allyMult`, `powerCooldownMult`, `volatileBonus`, `alwaysCrit`, `noDash`, `disabledPower`.
  Si en la vida subes `goalGalleries` galerías, al Pulir das `essenceBonus` de Esencia extra.
  | Carta | Efecto | Objetivo → bono |
  |---|---|---|
  | Camino Sereno | nada (segura) | — |
  | Mármol Pétreo | bloques ×2 vida, polvo ×3 | 12 gal → +15 % |
  | Voto de Soledad | sin Acólitos, Discípulas ×3 | 11 → +25 % |
  | Furia Ciega | todo crítico, sin Embestida | 12 → +20 % |
  | Plegaria Impaciente | recarga de poderes ×0,5, Supernova sellada | 12 → +15 % |
  | Lluvia de Volcánicos | +35 % volcánicos, vida ×1,3 | 11 → +25 % |
  | Avaricia | polvo ×2, Imán ×0,4 | 12 → +20 % |
  | Mármol Quebradizo | vida ×0,5, polvo ×0,4 | 14 → +30 % |
- **Coloso** (jefe): en cada galería múltiplo de `bossEveryGalleries` (X, XX…) brota en el centro (`Pedestal del
  Coloso`, hijo del spawner) el prefab `Prefabs/Monolith_Coloso` (Monolith_Large ×2, editable). Vida = `bossHPMult` (50)
  bloques pequeños; polvo = `bossDustMult` (40) bloques; estatua especial (`bossStatuePrefab`, obelisco dorado).
  3 fases visibles (100-67-33 %): las grietas brillan naranja y luego rojo (`phaseGlow2/3`, `phaseGlowIntensity`),
  estallido de escombros, rótulo y temblor. **Cierra la galería**: las estatuas se quedan en N−1 hasta que cae; al caer
  da `bossIcor + bossIcorPerTier × (nº de Coloso − 1)` Ícor y abre la galería. No levita ni se vuelve oro.
  Gancho S4: `MonolithSpawner.SpawnBoss(trial: true)` y el evento `GameManager.BossDefeated(bool trial)`.
- **Eventos temporales** (`LifeEventDef`): cada `eventIntervalMin-Max` s (150-260) desde la galería `eventUnlockGallery`,
  uno al azar (por `weight` y `minGallery`): **Luna de Ónice** (30 s, todo dorado, convierte los bloques en pie),
  **Lluvia de Cristal** (20 s, 4 meteoros gratis cada 4 s), **Hora Dorada** (45 s, Frenesí ×2,5). Rótulo + toast al
  empezar; contador en la línea de buffs. Prueba: `LifeDirector.StartEvent("luna")`.
- **Contratos** (`ContractDef`, `ContractKind`): `contractsPerLife` (3) al azar por vida desde el Pulido
  `contractsUnlockPrestige` (1); lista con progreso en el panel izquierdo de la HUD (label `contracts` de `Hud.uxml`).
  Cadena de 4 volcánicos (Ícor 3), combo 80 (Ícor 2), 4 poderes en 10 s (Ícor 3; solo si tienes 4 poderes),
  3 estatuas doradas (+10 % de la Esencia de Pulir), derrotar un Coloso (Ícor 4), 80 estatuas (+15 %).
  El resumen al Pulir muestra el mutador (cumplido/no) y los contratos cumplidos.

### 4.11e Campaña: El Gran Monumento, Colección y Modo Infinito (S4)
- **Meta**: 7 piezas (Base, Columnas, Torso, Brazos, Rostro, Corona, Alma) en `Economy.asset → campaign`
  (índice = `campaignStage` de la sala). Se ganan con `campaignPiecesRequired` = **7** piezas (S6). Migración: quien ya
  ganó con 3 conserva `campaignWon` (victoria y Modo Infinito) y puede seguir sacando las piezas 4-7.
- **Prueba de sala**: al llegar a `BiomeDef.trialGallery` (nº visible: Carrara **45**, Volcánica **65**, Cristal **90**, Hielo **105**, Forja **112**, Jardín **118**, Panteón **124**)
  brota el jefe (`SpawnBoss(trial: true)`, vida = Coloso × `trialHPMult` 2) y cierra la galería. Al caer: pieza,
  +`trialIcor` 15 Ícor, color de canica y la sala siguiente se abre (`GameManager.CompleteTrial`).
- **HUD**: barra de 7 piezas bajo la galería (Hud.uxml `monument`, `piece-0..6`; clases `got`/`trial`/`future`) y
  pista "Prueba de X: Galería N". **Monumento físico** `--- MONUMENTO ---` (componente `Monument`, un hijo por pieza
  que se activa al conseguirla), detrás de la columnata.
- **Colección [C]** (`marbleColors`): Carrara (inicio) · Rosa (1.er Pulido) · Verde (Prueba 1) · Rojo (Prueba 2) ·
  Violeta (Prueba 3) · Celeste (Prueba 4) · Hierro Forjado (Prueba 5) · Flor de Jardín (Prueba 6) · Nero (25 pulidos) · Dorado (campaña) · Cósmica (galería 500 en el infinito).
  Cada una: material `M_Player_*`, color de estela y de la onda. Solo cosmético. Al desbloquear se estrena sola.
- **Victoria**: cinemática corta (`HudCampaign.VictoryCinematic`: 8 s orbitando `--- MONUMENTO ---` con la HUD oculta,
  cualquier tecla la salta) y luego modal con tiempo de campaña, estatuas, pulidos, mejor golpe y galería máxima; desbloquea el Modo
  Infinito (entrar ahora o desde Colección).
- **Modo Infinito** (`GameState.infinite`): salas mezcladas, sin carta de mutador segura, vida de los bloques
  ×(1 + `infiniteHpPerLife` 0,25 × pulidos en el infinito). Récords (galería máx., tiempo, mejor golpe, polvo/s) y
  rango por galería: Aprendiz 0 → Maestro 150 → Leyenda 300 → Deidad 500 (`rankNames`/`rankGalleries`).
- **Respawn**: si hay un Coloso/jefe en pie y el punto de reaparición cae dentro de su radio, la canica aparece fuera.

### 4.12 Mecenazgos (logros)
28 hitos (estatuas, dorados, galería, pulidos, combo, poderes, Musas, polvo total). Cada uno da Ícor
y **Fama** (+2 % de polvo permanente, aditivo). Se comprueban cada segundo y son retroactivos.
Panel con progreso en la tecla L.

## 5. Balance y ritmo

Calibrado con `Tools/balance_sim.py` (jugador que compra lo más barato y pule al atascarse):

| Tiempo | Hito esperado |
|---|---|
| 15 s | Primera mejora |
| ~1 min | Galería II, desbloqueos nuevos |
| 20 min | ~Galería XVI |
| ~40 min | Muro suave → primer Pulido (~25 Esencia) |
| 1.5 h | 2.º Pulido, árbol con ~25 niveles |
| 3-4 h | Todas las mecánicas del árbol desbloqueadas |
| ~2 min | Primera **Supernova** (los primeros Mecenazgos dan casi todo el Ícor) |
| ~30-45 min | Ingravidez y Meteoros |
| ~1.5-3 h | Coloso, Vórtice y Toque de Midas |
| 20 h | Galería ~190, 13-14 pulidos, *Obra Maestra* subiendo, Altar ~90 niveles |
| decenas de horas | Altar completo (≈17 000 Ícor sin contar Ira Divina, que no tiene límite) |

**Sesión 1 (economía y ritmo) — antes / después** (`python Tools/balance_sim.py 20`):

| Métrica | Antes | Después |
|---|---|---|
| 1.er Pulido | 53,8 min · Gal. 35 | 52,1 min · Gal. 29 |
| 5.º Pulido | 234,8 min · Gal. 108 | 206,0 min · Gal. 68 |
| 10.º Pulido | 768,0 min · Gal. 186 | 683,6 min · Gal. 117 |
| Galería a las 20 h | 219 (13 pulidos) | 127 (15 pulidos) |
| Taller completo, vida 1 / 2 / 5 / 10 / 14 | ~27 / 7 / 1,8 / 1,4 / 1,3 min | ~52 / 12 / 8 / 6 / 7 min |
| Duración de la vida (pulidos 7-14) | ~100-118 min | ~77-119 min |

"Taller completo" = todas las mejoras con tope al máximo. Objetivo 8-12 min: se cumple en las vidas 2-4 (12→8 min);
después baja a ~5-7 min y **no crece** el 30 % cada 3 pulidos (pendiente: más topes/mejoras "vivas" o k mayor;
con k=2,4 la vida 1 se atasca y nunca completa el Taller). Los topes ilimitados de Resonancia/Vínculo cuentan fuera de esa medida.

**Sesión 1b (curva del Taller completo) — antes / después.** El cuello de botella del Taller completo es
**Acólitos** (coste ×3 por nivel) en todas las vidas desde la 2.ª, así que su tope por Pulido es el motor de la curva;
el crecimiento de coste por Pulido lo acompaña. Además se arregló un artefacto del simulador (la fracción de bloques
se perdía en cada corte de la tabla y retrasaba el 1.er Pulido ~4 min); las cifras "antes" ya usan el simulador corregido.

| Parámetro | Antes | Después |
|---|---|---|
| `workshopCostK` (k) | 2,0 | **1,8** |
| `workshopCostPerPrestige` | 1,14 | **1,6** |
| Acólitos: tope +/Pulido · límite | +1,5 · 50 | **+3,25 · 60** |
| Resto de topes, Resonancia/Vínculo | — | sin cambios (vivas, sin tope) |

| Métrica (`python Tools/balance_sim.py 20`) | Antes | Después |
|---|---|---|
| 1.er Pulido | 52,2 min · Gal. 29 | 50,2 min · Gal. 29 |
| 5.º Pulido | 206 min · Gal. 68 | 225 min · Gal. 66 |
| 10.º Pulido | 684 min · Gal. 117 | 641 min · Gal. 102 |
| Pulidos en 20 h · galería máx. | 15 · 136 | 16 · 113 |
| Taller completo, vidas 1-15 (min) | 12 10 8 8 8 7 7 5 6 6 6 7 6 7 | 13 11 11 12 13 14 20 14 13 16 15 18 24 34 30 |
| Media por bloques de 3 vidas | 10,0 · 7,5 · 6,3 · 6,3 | **11,8 · 13,1 · 15,4 · 16,3 · 29,0** |
| Crecimiento cada 3 pulidos | −14 % de media | **+25 % de media** (+11, +18, +6, +78 %) |
| Atascos (galerías de > 5 min en 20 h) | 42 | 46 (tope de atasco: 10 min, igual) |

Ruido conocido: el Taller completo salta cuando entra un poder nuevo con su mejora `pw_*` (vidas 5-7: Rayo/Vórtice)
y cae cuando llega el Toque de Midas (vida 8, Pulido de +400 Esencia). Son eventos de contenido, no de la curva;
si molestan, la palanca es el coste base de esas `pw_*`, no k. Con k ≥ 2,2 la vida 1 vuelve a no completar el Taller;
con Acólitos ≥ +3,5/Pulido aparecen vidas sin Taller completo (vida 7).

Con Poderes, Musas y Fama el primer Pulido llega a los ~45 min en la Galería ~30 (antes ~40 min,
Galería 22): el muro sigue ahí, pero llegas un poco más lejos y se gana más Esencia.

**Reglas de diseño del balance:**
1. La recompensa por bloque **no** depende de su vida (evita el bucle explosivo daño→polvo→daño).
2. Vida ×1.8/galería > recompensa ×1.5/galería → muro suave natural que invita a pulir.
3. Mejoras multiplicativas con coste creciente más rápido que su efecto (Masa ×1.3 por ×1.5 de coste).
4. Esencia polinómica en la galería → el prestigio acelera pero nunca se desboca.
5. Los poderes tienen mucho daño pero recargas largas: el impacto económico es (bloques rotos / recarga),
   un +10-25 % de ritmo. El simulador los modela así.
6. Siempre hay algo cerca: galerías cada ~1 min al principio, mejoras cada pocos segundos,
   desbloqueos escalonados (galerías II-IV) y bloques dorados aleatorios.

**S3 (simulador):** el Coloso se modela como 50/1,48 bloques medios extra al cerrar la galería X, XX… (+40 bloques
de polvo, Ícor 3 + 1 por Coloso anterior) y el mutador medio como polvo ×1,34 / vida ×1,11 desde la vida 2 y +21 % de
Esencia si la vida sube 12 galerías (`SIMMUT=0` lo apaga). Resultado (20 h): el Taller completo apenas cambia
(vidas 1-15: 12 11 12 11 13 12 11 12 12 13 14 15 17 20 19 min), 1.er Pulido ~50 min, 16 pulidos (antes 15).
Los Colosos suman ~+55 % de Ícor en 20 h (con +2 Ícor por tier era +85 %: por eso `bossIcorPerTier` = 1).

**S4 (campaña):** la Prueba se modela como 2 × Coloso al llegar a su galería (`TRIALS`, env `SIMTRIALS`); la
campaña es lineal. Con 45/65/90: Prueba 1 a **1,8 h**, Prueba 2 a **3,3 h** (+1,5 h), Prueba 3 a **5,1 h** (+1,8 h)
→ objetivo 1,5-3 h por sala cumplido; campaña de 3 salas ≈ 5 h.

**S6 (7 salas):** pasado ~G110 el ritmo se frena mucho (la vida tarda horas en subir unas pocas galerías), así que
las Pruebas 4-7 van en pasos cortos: 105/112/118/124 → Prueba 4 a **7,5 h** (+2,3 h), 5 a **10,2 h** (+2,7 h),
6 a **15,0 h** (+4,9 h), 7 a **18,5 h** (+3,5 h). Campaña ≈ 18,5 h (objetivo 12-20 h); la sala 6 se pasa del objetivo
de 3 h porque el simulador avanza a saltos por Pulido (118 cae justo tras un muro). No modela ×2 HP/×3 polvo de la Forja
ni el crecimiento del Jardín. Probadas: 120 (sala 4 = 10 h), 110/125 (5 h + 8,7 h), 117/123, 116/121, 115/120.

Para retocar: edita `Economy.asset` en el Inspector y replica los números en `Tools/balance_sim.py`;
ejecuta `python Tools/balance_sim.py 20` y revisa la tabla de hitos.

### Tres capas de progreso y un solo reinicio (S8)
| Capa | Moneda / árbol | Duración |
|---|---|---|
| Vida | **Polvo** → **Taller** | Se reinicia en cada **Pulido** (junto con las Galerías) |
| Escultor | **Esencia** → **Árbol del Escultor** [T] | Permanente: se acumula Pulido tras Pulido |
| Divinidades | **Ícor** → **Altar** [G] | Permanente |

**Pulir es el único reinicio.** Conserva Esencia, Árbol, Ícor, Altar, Reliquias, campaña, colección y récords.
"Consagrar" ya no es un botón: es un **hito de Reliquia** que llega solo con el Pulido.

### Hito de Reliquia (antes Consagrar), Reliquias y Fragmentos de Eternidad
- Cada `consecrateMinPrestiges` (**5**) Pulidos, el Pulido entrega **1 Reliquia** al azar (se equipa si hay ranura libre) y suma
  1 Consagración (`GameState.consecrations`, cuenta para los Mecenazgos *Consagrado*/*Relicario*). Sin pérdidas ni confirmaciones.
  El modal de Pulir avisa "Reliquia nueva a los N Pulidos" / "★ Este Pulido trae una RELIQUIA nueva"; el resumen del Pulido abre
  con "★ RELIQUIA NUEVA". El panel RELIQUIAS [R] muestra los Pulidos que faltan. Contador: `GameState.relicPrestige`
  (migración: se inicia en `prestigesAtConsecrate`).
- Cada Consagración/hito: Esencia por Pulido ×(1 + `consecrateEssenceBonus`·n), **0,25** (antes 0,5, ver tabla); cada reliquia poseída: poder +10 %.
- Partidas con Consagraciones antiguas (S5-S7, que reiniciaban el Árbol): `prestigesAtConsecrate` queda congelado y, con
  `consecrateResetsCostGrowth`, el ×coste del Taller por Pulido cuenta solo los Pulidos desde entonces (así no se encarece el
  Taller de quien perdió el Árbol). Las partidas nuevas tienen 0 → crecimiento normal ×1,6/Pulido.
- **Reliquias** (8, `Economy.asset → relics`): Supernova en Racimo, Vórtice Escindido, Rayo Inverso, Meteoros Dorados, Pisada Sísmica (Coloso), Ingravidez Imantada, Corona Avara (Midas), Reloj de Arena. Ranuras: 2 base, +1 por 40 y +1 por 120 Fragmentos. Panel UI Toolkit `reliquias` en Hud.uxml.
- **Fragmentos de Eternidad** (solo Modo Infinito): 1 por galería, 5 por Coloso (×2 con modificador). Se gastan en ranuras y en la canica Eterna (60).
- **Jefes del infinito**: cada Coloso lleva 1 modificador aleatorio (`bossMods`: Blindado, Regenerante, Titánico, Sordo a los Dioses, Acorazado). Las Pruebas de campaña no llevan.
- **Mecenazgos de campaña**: Pieza 1/7…7/7, Obra Maestra (victoria), Consagrado, Relicario.

Simulador `SIMHITO=5 SIMHBONUS=x python Tools/balance_sim.py 20` (hito cada 5 Pulidos, sin reinicio):

| | Antes S8: sin Consagrar | Antes S8: Consagrar cada 10 (S7) | Hito ×0,5 | **Hito ×0,25 (actual)** | Hito ×0,15 |
|---|---|---|---|---|---|
| Prueba 3 | 5,14 h | — | 5,12 h | 5,14 h | 5,14 h |
| Prueba 5 | 10,15 h | — | 7,69 h | 8,50 h | 8,54 h |
| Prueba 7 (campaña) | 18,5 h | 13,6 h | 11,4 h (bajo objetivo) | **13,4 h** | 13,6 h |
| Taller completo, vidas 1-15 | 11-20 min | 12-14 min | 10-16 min | 11-16 min | 11-18 min |

Con ×0,5 la campaña bajaba de 12 h (objetivo 12-20 h), así que se bajó a 0,25.

## 6. Juice / feedback
- Trozos que saltan con física simulada (instanced), polvo emisivo atraído, estatua que asciende y brilla.
- Números de daño flotantes (críticos grandes y dorados), combo con "pop" (abajo, sobre los buffs), toasts.
- **Rótulos y toasts nunca se pisan** (S6): mientras hay rótulo (`Hud.Banner`) los toasts se ocultan y los nuevos esperan en
  cola (`pendingToasts`, se vacía al acabar el rótulo o al cerrar un modal). El toast de galería usa la clase `gallery`, que
  también es el panel de arriba: `.toast.gallery` la anula (`position: relative`) — era el solape antiguo.
- Hit-stop, temblor por trauma², FOV kick en la embestida y a alta velocidad, estela.
- Audio 100 % procedural: impactos, cincel, notas pentatónicas al recoger, acordes de estatua y
  galería, ondas graves, pad ambiental en bucle, reverb de sala.

## 7. Arte y rendimiento
- HDRP con exposición fija (EV 9.5), cielo degradado cálido, niebla, bloom, SSAO, SSR, ACES.
- Salas (S8): misma luz que Carrara (Sol 2600 lux casi blanco, niebla 150 m, cielo con la luminancia del Santuario y solo un leve matiz,
  `colorFilter` casi neutro en los `VP_*`). La identidad va en los materiales (suelo/columnas/exterior en `Materials/Biomes`, tonos claros)
  y en luces de acento de la sala (cristal azul, lava naranja). No tintar la escena entera.
- Texturas de mármol procedurales tileables (6 variedades + suelo de damero con juntas de latón).
- Material con *clear coat* en superficies pulidas; mármol en bruto rugoso en los bloques.
- Optimización: polvo y escombros con `Graphics.RenderMeshInstanced` (sin GameObjects, sin
  físicas, sin GC por frame), pools de bloques, colisión analítica con el cuenco, SRP Batcher,
  sin sombras en luces de braseros, un solo `Rigidbody` dinámico (la canica).
- Selector de calidad (Performant / Balanced / High Fidelity) en la HUD.

## 8. Estructura del proyecto
Ver `CLAUDE.md` y el skill `.claude/skills/marmoreal-dev`.

## 9. Ideas futuras (backlog)
- Más eventos temporales y bendiciones de Musas; tipos de bloque nuevos (espejo, hielo, bomba, hueco).
- Tercer árbol / reliquias equipables (slots que modifican los poderes: Supernova en racimo, Vórtice móvil…).
- Más estatuas (manos, caballos, rostros) y bloques con forma (columnas, arcos).
- Música adaptativa por galería.

### S9 — Rebalanceo, compensación y Reliquias a elegir
**Diagnóstico (partida real, 7 h, 16 Pulidos):** la Consagración antigua (S5/S7) puso la Esencia y el Árbol a 0 (Árbol invertido 462 de 4391 Esencia ganadas
en total; Altar 77 niveles, 323 Ícor sin gastar porque *Ira* es exponencial). Con Brillo×Obra ×9,8 y Fama ×1,6 el factor de coste del Taller es ×1477;
el HP de un bloque pequeño en la Galería LII es 2,3·10¹⁴ (×1,8/galería) frente a un daño de impacto ~3·10² por nivel de Taller: muro. Para la XC (90) hacían falta ×13 más de poder.

| Valor | Antes | Después |
|---|---|---|
| `hpGrowthPerGallery` (Economy.asset) | 1,8 | **1,72** (HP en LII −48 %, en XC −85 %) |
| Pruebas Hielo / Forja / Jardín / Panteón (`BiomeDef.trialGallery`) | 105 / 112 / 118 / 124 | **115 / 135 / 155 / 175** (Carrara 45, Volcánica 65, Cristal 90 sin cambio) |
| Reliquias | 1 al azar cada 5 Pulidos | **1 Punto de Reliquia** cada 5 Pulidos; se gasta a mano (1 punto = 1 reliquia) |
| Respec de reliquias | — | **DEVOLVER** gratis (+1 punto) y re-equipar; equipar/quitar ya era libre |
| Compensación | — | Única: Esencia = ganada − actual − invertida en el Árbol (`consecrateRefundDone`): **+3718** en la partida real |

Simulador (`SIMHP`, `SIMTRIALS`, `SIMLEGACY=<pulido>`, `SIMREFUND=<pulido>`; jugador óptimo, el real va ~2× más lento): sala por sala
1,8/1,5/1,8/2,3/1,1/4,2/0,7 h (antes, 13,4 h totales) → con 1,72 y Pruebas 45/65/90/115/135/160/185: 1,0/0,8/1,7/0,4/1,3/3,3/7,6 h
(≈ 16 h); con 1,75: 1,3/0,9/2,4/1,5/2,3/5,0/1,7 h. Se eligió 1,72 + Pruebas 45…175 (≈ 1-2,5 h por sala para un jugador real).
Jugador de Consagración antigua (SIMLEGACY=6): sin compensación Prueba 7 a 17,0 h; con compensación (SIMREFUND=7) 16,4 h y Prueba 5 −2,4 h.
