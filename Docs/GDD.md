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
| Inercia | 30 | ×1.75 | vel. 11 + 0.75n m/s (máx. 25) | — |
| Imán | 20 | ×1.6 | radio 5 + 0.9n m (máx. 25) | — |
| Pureza | 45 | ×1.75 | polvo × 1.2^n | — |
| Fortuna | 150 | ×1.9 | crítico 5 % + 2.5 %/n (máx. 18) | Galería II |
| Cantera | 80 | ×2.15 | bloques simultáneos 5 + n (máx. 12) | Galería II |
| Acólitos | 150 | ×3.0 | +1 acólito (máx. 12) | Galería III |
| Cincel | 220 | ×1.6 | daño acólito 1.6 × 1.28^n | Galería III |
| Resonancia | 300 | ×1.7 | onda × (1 + 0.35n) (máx. 60) | Galería IV |
| Doble Embestida | 600 | ×6 | cargas de Embestida 1 + n (máx. 3 → 4 cargas) | Galería III |
| Discípulas | 2 500 | ×3.2 | +1 canica aliada (máx. 8) | Galería V |
| Vínculo | 4 000 | ×1.85 | las Discípulas copian 30 % + 5 %/n de tu daño (máx. 14 → 100 %) | 1.ª Discípula |
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
- Reinicia polvo, Taller y galerías; conserva Esencia y Árbol. *Memoria* da galerías iniciales.
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

Para retocar: edita `Economy.asset` en el Inspector y replica los números en `Tools/balance_sim.py`;
ejecuta `python Tools/balance_sim.py 20` y revisa la tabla de hitos.

## 6. Juice / feedback
- Trozos que saltan con física simulada (instanced), polvo emisivo atraído, estatua que asciende y brilla.
- Números de daño flotantes (críticos grandes y dorados), combo con "pop", toasts.
- Hit-stop, temblor por trauma², FOV kick en la embestida y a alta velocidad, estela.
- Audio 100 % procedural: impactos, cincel, notas pentatónicas al recoger, acordes de estatua y
  galería, ondas graves, pad ambiental en bucle, reverb de sala.

## 7. Arte y rendimiento
- HDRP con exposición fija (EV 9.5), cielo degradado cálido, niebla, bloom, SSAO, SSR, ACES.
- Texturas de mármol procedurales tileables (6 variedades + suelo de damero con juntas de latón).
- Material con *clear coat* en superficies pulidas; mármol en bruto rugoso en los bloques.
- Optimización: polvo y escombros con `Graphics.RenderMeshInstanced` (sin GameObjects, sin
  físicas, sin GC por frame), pools de bloques, colisión analítica con el cuenco, SRP Batcher,
  sin sombras en luces de braseros, un solo `Rigidbody` dinámico (la canica).
- Selector de calidad (Performant / Balanced / High Fidelity) en la HUD.

## 8. Estructura del proyecto
Ver `CLAUDE.md` y el skill `.claude/skills/marmoreal-dev`.

## 9. Ideas futuras (backlog)
- Eventos temporales ("Luna de Ónice": todo dorado 30 s); más bendiciones de Musas.
- Tercer árbol / reliquias equipables (slots que modifican los poderes: Supernova en racimo, Vórtice móvil…).
- Más estatuas (manos, caballos, rostros) y bloques con forma (columnas, arcos).
- Segunda sala (desbloqueable con Obra Maestra) con gravedad/inclinación distinta.
- Música adaptativa por galería.
