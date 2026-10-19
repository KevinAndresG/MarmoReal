"""
Simulador de balance de MARMOREAL.
Modela un jugador "razonable": compra la mejora más barata disponible, pule cuando
la ganancia de Esencia lo merece o cuando lleva demasiado tiempo atascado.

Uso:  python Tools/balance_sim.py [horas]
Los números deben coincidir con EconomyConfig.FillDefaults() / Economy.asset.
"""
import math, sys
sys.stdout.reconfigure(encoding="utf-8")

HOURS = float(sys.argv[1]) if len(sys.argv) > 1 else 10

CFG = dict(
    baseHP=22, hpGrowth=1.8, baseReward=4, rewardGrowth=1.5, statueBonus=1.5,
    largeMult=2.6, largeShare=0.3, goldenChance=0.03, goldenHP=3, goldenReward=12,
    statBase=8, statStep=2, statMax=30,
    critMult=3, shockMult=6, resPerImpact=0.1,
    prestigeMin=8, prestigeBase=0.3, prestigePow=1.6,
    acolyteInterval=1.1, volatileChance=0.06, volatileUnlock=2,
    # Coste del Taller escalado por poder permanente: × (GlobalMult × Fama)^k (EconomyConfig.workshopCostScale*)
    costPrestigeGrowth=float(__import__('os').environ.get('SIMPG',1.6)), costScaleOn=True, costScaleK=1.8,
)
# S3: Coloso cada 10 galerías (Economy.asset boss*) y mutador medio (media de las 7 cartas no seguras)
BOSS = dict(every=10, hpMult=50, dustMult=40, icor=3, icorPerTier=1)
# Campaña (S4): Prueba de cada sala al llegar a su galería (BiomeDef.trialGallery, nº visible). Jefe = Coloso × hpMult.
# La sala por defecto al Pulir es la de la Prueba pendiente, así que la campaña es lineal. env SIMTRIALS="45,65,90,105,112,118,124" (S6: 7 salas).
TRIALS = [int(x) for x in __import__("os").environ.get("SIMTRIALS", "45,65,90,105,112,118,124").split(",")]
TRIAL = dict(hpMult=2.0, icor=15)
MUT = dict(on=__import__("os").environ.get("SIMMUT", "1") == "1",
           dust=(3 + 1 + 1 + 1 + 1 + 2 + 0.4) / 7, hp=(2 + 1 + 1 + 1 + 1.3 + 1 + 0.5) / 7,
           bonus=(0.15 + 0.25 + 0.2 + 0.15 + 0.25 + 0.2 + 0.3) / 7, goal=12)
# Consagrar (S5): SIMCONS = pulidos entre Consagraciones (0 = nunca). Reinicia Esencia y Árbol; Esencia/Pulido x(1+0.5*n); poder +10%/reliquia
CONS = int(__import__("os").environ.get("SIMCONS", "0")); CONS_BONUS = 0.5
# S7: el ×coste por Pulido cuenta solo los Pulidos desde la última Consagración (SIMCRESET=0 = fórmula S5);
# Consagrar conserva SIMKEEP (fracción) de los niveles del Árbol
CONS_RESET = int(__import__("os").environ.get("SIMCRESET", "1")); CONS_KEEP = float(__import__("os").environ.get("SIMKEEP", "0.5"))
# S8: Reliquia como hito (sin reinicio): cada SIMHITO Pulidos +1 Consagración (Esencia ×(1+SIMHBONUS·n)); 0 = off
HITO = int(__import__("os").environ.get("SIMHITO", "0"))
if HITO: CONS_BONUS = float(__import__("os").environ.get("SIMHBONUS", "0.5"))
SCALE_K = float(__import__("os").environ.get("SIMK", CFG["costScaleK"]))
CFG["costScaleOn"] = SCALE_K > 0
SCOPE = __import__("os").environ.get("SIMSCOPE", "all")

# id: (baseCost, growth, max, mode, base, per, unlockGallery)
DUST = {
    "masa":       (12, 1.5, 500, "exp", 3, 1.3, 0),
    "inercia":    (30, 1.75, 25, "lin", 11, 0.75, 0),
    "iman":       (20, 1.6, 25, "lin", 5, 0.9, 0),
    "pureza":     (45, 1.75, 500, "exp", 1, 1.2, 0),
    "fortuna":    (150, 1.9, 18, "lin", 0.05, 0.025, 1),
    "cantera":    (80, 2.15, 12, "lin", 5, 1, 1),
    "acolitos":   (150, 3.0, 12, "lin", 0, 1, 2),
    "cincel":     (220, 1.6, 500, "exp", 1.6, 1.28, 2),
    "resonancia": (300, 1.7, 500, "lin", 1, 0.35, 3),   # viva (sin tope)
    "cargas":     (600, 6, 3, "lin", 1, 1, 2),          # cargas de Embestida
    "gemelas":    (2500, 3.2, 8, "lin", 0, 1, 4),       # Discípulas (canicas aliadas)
    "vinculo":    (4000, 1.85, 500, "lin", 0.3, 0.05, 4),  # % de tu daño que copian (requiere gemelas) · viva
    # Mejoras de poderes: solo aparecen con el poder desbloqueado en el Altar (se pierden al pulir)
    "pw_supernova":  (400, 2.2, 20, "exp", 0.25, 1.1, 0),
    "pw_ingravidez": (900, 2.2, 20, "exp", 0.25, 1.1, 0),
    "pw_meteoros":   (900, 2.2, 20, "exp", 0.25, 1.1, 0),
    "pw_coloso":     (2500, 2.1, 15, "lin", 4, 0.6, 0),
    "pw_vortice":    (4000, 2.2, 20, "exp", 0.25, 1.1, 0),
    "pw_midas":      (20000, 8, 4, "lin", 1, 1, 0),
    "pw_rayo":       (6000, 2.2, 20, "exp", 0.25, 1.1, 0),
}
# Topes que suben con cada Pulido: id: (niveles extra por pulido, tope absoluto). UpgradeDef.maxLevelPerPrestige / maxLevelLimit
CAPS = {
    "inercia": (1, 60), "iman": (1, 60), "fortuna": (1, 30), "cantera": (1, 30),
    "acolitos": (3.25, 60), "cargas": (0.5, 8), "gemelas": (1, 24),
}
# Ayudantes por hito de ESTA vida: id: (estadística, umbral). UpgradeDef.unlockStat / unlockThreshold
# run_galleries = galerías subidas en esta vida (no cuenta la Memoria); run_volatile = volcánicos rotos en esta vida
MILESTONE = {"acolitos": ("run_galleries", 3), "gemelas": ("run_volatile", 12)}

# id: (baseCost, growth, max, mode, base, per, requires)
TREE = {
    "brillo":   (1, 1.55, 300, "exp", 1, 1.25, []),
    "fuerza":   (2, 1.7, 60, "exp", 1, 1.4, ["brillo"]),
    "ojo":      (3, 2.2, 5, "lin", 0, 1, ["fuerza"]),
    "rebote":   (4, 2.5, 4, "lin", 0.3, 0.1, ["fuerza"]),
    "sismo":    (8, 2.5, 4, "lin", 0, 3, ["rebote"]),
    "metralla": (12, 2.5, 4, "lin", 0, 0.4, ["rebote"]),
    "veta":     (2, 1.8, 100, "exp", 1, 1.5, ["brillo"]),
    "fiebre":   (3, 1.9, 8, "lin", 0, 0.015, ["veta"]),
    "memoria":  (5, 2.0, 10, "lin", 0, 1, ["fiebre"]),
    "eterno":   (2, 2.4, 6, "lin", 0, 1, ["brillo"]),
    "coro":     (3, 1.75, 60, "exp", 1, 1.4, ["eterno"]),
    "paciencia":(2, 2.0, 5, "lin", 0.25, 0.15, ["eterno"]),
    "impulso":  (3, 2.0, 5, "lin", 2.4, -0.3, ["paciencia"]),
    "eco":      (10, 2.5, 4, "lin", 1, 1, ["paciencia"]),
    "perpetua": (14, 2.5, 4, "lin", 0.75, 0.25, ["eco"]),
    "obra":     (30, 3.0, 50, "exp", 1, 2, ["metralla", "memoria", "eco"]),
}

# ---------- ALTAR DE LOS DIOSES (Ícor, permanente) ----------
# id: (baseCost, growth, max, mode, base, per, requires)
ALTAR = {
    # Cada poder es una rama independiente (sin requisitos entre poderes)
    "supernova":  (6, 1, 1, "lin", 0, 1, []),
    "nova_radio": (3, 1.8, 8, "lin", 1, 0.15, ["supernova"]),
    "nova_cd":    (3, 1.9, 6, "lin", 1, -0.07, ["nova_radio"]),
    "codicia":    (6, 1.9, 8, "lin", 2, 0.25, ["nova_cd"]),
    "ingravidez": (7, 1, 1, "lin", 0, 1, []),
    "caida":      (4, 1.8, 10, "exp", 1, 1.35, ["ingravidez"]),
    "ingravidez_cd": (4, 1.9, 6, "lin", 1, -0.07, ["caida"]),
    "meteoros":   (7, 1, 1, "lin", 0, 1, []),
    "enjambre":   (4, 1.8, 8, "lin", 0, 3, ["meteoros"]),
    "meteoros_cd": (4, 1.9, 6, "lin", 1, -0.07, ["enjambre"]),
    "coloso":     (15, 1, 1, "lin", 0, 1, []),
    "titan":      (5, 1.8, 8, "lin", 0, 2, ["coloso"]),
    "coloso_cd":  (6, 1.9, 6, "lin", 1, -0.07, ["titan"]),
    "vortice":    (20, 1, 1, "lin", 0, 1, []),
    "horizonte":  (5, 1.8, 10, "exp", 1, 1.3, ["vortice"]),
    "vortice_cd": (7, 1.9, 6, "lin", 1, -0.07, ["horizonte"]),
    "midas":      (40, 1, 1, "lin", 0, 1, []),
    "reymidas":   (8, 2.0, 6, "lin", 0, 1, ["midas"]),
    "midas_cd":   (10, 1.9, 6, "lin", 1, -0.07, ["reymidas"]),
    "rayo":       (25, 1, 1, "lin", 0, 1, []),
    "cadena":     (5, 1.8, 6, "lin", 0, 1, ["rayo"]),
    "rayo_cd":    (7, 1.9, 6, "lin", 1, -0.07, ["cadena"]),
    "favor":      (2, 1.9, 5, "lin", 1, -0.1, []),
    "inspiracion":(3, 1.8, 6, "lin", 1, 0.25, ["favor"]),
    "ira":        (4, 1.6, 200, "exp", 1, 1.25, []),
    "eternidad":  (20, 2.2, 8, "lin", 1, -0.05, []),
}
# Poderes: id: (recarga s, potencia ×daño de impacto, fracción de bloques activos alcanzados)
POWERS = {
    "supernova":  (120, 45, 0.45),
    "ingravidez": (150, 60, 1.0),
    "meteoros":   (90, 35, 0.9),     # 12 meteoros repartidos (uno por bloque) + Gran Meteoro 150 en área
    "coloso":     (80, 0, 0),        # ×3 daño de impacto durante 10 s
    "vortice":    (120, 25 * 6 + 60, 0.4),  # 25/s durante 6 s (errante) + implosión 60
    "midas":      (140, 0, 0),       # 2 bloques dorados
    "rayo":       (100, 30, 0.6),    # 6 rayos × (3 + Conductor) saltos, -15 % por salto
}
ICOR = dict(statueChance=0.02, goldenChance=0.3, golden=1, musa=1, musaInterval=90, musaCatch=0.6,
            prestigeBase=2, prestigeDiv=10, fame=0.02)
# Mecenazgos: (estadística, umbral, ícor)
ACH = [("statues", 1, 1), ("statues", 25, 1), ("statues", 100, 2), ("statues", 500, 3), ("statues", 2000, 4), ("statues", 10000, 6),
       ("golden", 1, 1), ("golden", 10, 2), ("golden", 50, 4),
       ("gallery", 5, 1), ("gallery", 10, 2), ("gallery", 20, 3), ("gallery", 40, 5), ("gallery", 75, 8),
       ("prestige", 1, 2), ("prestige", 5, 3), ("prestige", 15, 6), ("combo", 15, 1), ("combo", 40, 2), ("combo", 100, 4),
       ("powers", 1, 1), ("powers", 50, 3), ("powers", 500, 6), ("musas", 1, 1), ("musas", 25, 3),
       ("dust", 1e6, 1), ("dust", 1e12, 3), ("dust", 1e24, 6)]

def val(d, lvl):
    _, _, _, mode, b, p = d[:6]
    return b + p * lvl if mode == "lin" else b * p ** lvl

def cost(d, lvl):
    return math.ceil(d[0] * d[1] ** lvl)

class Sim:
    def __init__(s):
        s.lv = {k: 0 for k in DUST}
        s.tr = {k: 0 for k in TREE}
        s.dust = s.run = s.ess = 0.0
        s.g = 0; s.stat = 0; s.prest = 0
        s.t = 0; s.tg = 0
        s.log = []
        s.al = {k: 0 for k in ALTAR}
        s.icor = 0.0; s.icor_life = 0.0
        s.st = dict(statues=0, golden=0, gallery=1, prestige=0, combo=0, powers=0, musas=0, dust=0)
        s.ach = set(); s.milestones = {}; s.cur_kills = 0.1
        s.g0 = 0; s.run_vol = 0.0; s.done_t = None; s.lives = []   # (pulido, min hasta Taller completo, min de la vida)

    def V(s, k):
        if k in DUST: return val(DUST[k], s.lv[k])
        if k in ALTAR: return val(ALTAR[k], s.al[k])
        return val(TREE[k], s.tr[k])

    def fame(s): return 1 + ICOR["fame"] * len(s.ach)

    def dust_cost(s, k):
        c = cost(DUST[k], s.lv[k])
        if CFG["costScaleOn"] and (SCOPE == "all" or DUST[k][2] < 500): c *= (s.V("brillo") * s.V("obra") * s.fame()) ** SCALE_K * CFG["costPrestigeGrowth"] ** (s.prest - CONS_RESET * getattr(s, "cons_at", 0))
        return c

    def dmax(s, k):
        m = DUST[k][2]
        if k in CAPS: m = min(CAPS[k][1], m + int(CAPS[k][0] * s.prest + 1e-9))
        return m

    def dust_open(s, k):
        d = DUST[k]
        if s.g < d[6] and s.prest == 0: return False
        if k.startswith("pw_") and not s.al[k[3:]]: return False
        if k == "vinculo" and not s.lv["gemelas"]: return False
        if k in MILESTONE:
            st, th = MILESTONE[k]
            if s.run_stat(st) < th: return False
        return True

    def run_stat(s, st):
        return s.g - s.g0 if st == "run_galleries" else s.run_vol

    def workshop_done(s):
        return all(s.lv[k] >= s.dmax(k) for k in DUST if DUST[k][2] < 500 and (not k.startswith("pw_") or s.al[k[3:]]))

    def power_kills(s, hp):
        """Bloques/s extra por los poderes (daño = impacto × potencia × Ira) y multiplicador de polvo medio."""
        G = s.V("brillo") * s.V("obra")
        dmg = s.V("masa") * s.V("fuerza") * G * s.V("ira")
        active = 5 + s.lv["cantera"]
        cdm = max(0.3, s.V("eternidad"))
        kills = 0.0; dust_mult = 1.0; casts = 0.0
        for k, (cd, pot, frac) in POWERS.items():
            if not s.al[k]: continue
            c = cd * cdm * max(0.4, s.V("nova_cd" if k == "supernova" else k + "_cd") if (("nova_cd" if k == "supernova" else k + "_cd") in ALTAR) else 1)
            casts += 1 / c
            if k == "supernova": pot_k, fr = pot, min(1, frac * s.V("nova_radio") ** 2)
            elif k == "ingravidez": pot_k, fr = pot * s.V("caida"), frac
            elif k == "meteoros": pot_k, fr = pot * (10 + s.V("enjambre")) / 10, frac
            elif k == "vortice": pot_k, fr = pot * s.V("horizonte"), min(1, frac * s.V("horizonte") ** 0.5)
            elif k == "rayo": pot_k, fr = pot, min(1, frac * (3 + s.V("cadena")) / 3)
            else: pot_k, fr = 0, 0
            if k in ("supernova", "ingravidez", "meteoros", "vortice", "rayo"): pot_k *= s.V("pw_" + k)
            if pot_k: kills += active * fr * min(1, pot_k * dmg / hp) / c
            if k == "supernova": dust_mult += (max(2, s.V("codicia")) - 1) * 6 / c
            if k == "coloso": dust_mult += (s.V("pw_coloso") + s.V("titan")) / c          # ×3 daño ≈ +100 % de ritmo mientras dura
            if k == "midas": dust_mult += (s.V("pw_midas") + s.V("reymidas")) * 12 * s.V("veta") / c / max(0.05, s.cur_kills)
        return kills, dust_mult, casts

    def musa_mult(s):
        if s.g < 1 and s.prest == 0: return 1.0, 0.0
        interval = ICOR["musaInterval"] * max(0.3, s.V("favor"))
        rate = ICOR["musaCatch"] / interval
        insp = s.V("inspiracion")
        frenzy = (1 / 8) * rate * 20 * insp * 4      # 1/8 de bendiciones = Frenesí ×5 20 s
        cornu = (1 / 8) * rate * 90 * insp           # 1/8 = Cornucopia (90 s de polvo al instante)
        legion = (1 / 8) * rate * 20 * insp * 0.3    # 1/8 = Legión (≈ +30 % de ritmo 20 s)
        return 1 + frenzy + cornu + legion, rate

    def add_icor(s, n):
        s.icor += n; s.icor_life += n

    def spend_icor(s):
        bought = True
        while bought:
            bought = False
            best = None
            for k, d in ALTAR.items():
                if s.al[k] >= d[2] or any(s.al[r] == 0 for r in d[6]): continue
                c = cost(d, s.al[k])
                if best is None or c < best[0]: best = (c, k)
            if best and best[0] <= s.icor:
                s.icor -= best[0]; s.al[best[1]] += 1; bought = True
                if best[1] in POWERS and best[1] not in s.milestones:
                    s.milestones[best[1]] = s.t
                    s.log.append(f"  ** PODER {best[1]:10s} a {s.t/60:6.1f} min")

    def check_ach(s):
        for i, (st, th, ic) in enumerate(ACH):
            if i not in s.ach and s.st[st] >= th:
                s.ach.add(i); s.add_icor(ic)

    def dps(s):
        G = s.V("brillo") * s.V("obra")
        dmg = s.V("masa") * s.V("fuerza") * G
        cc = min(1, s.V("fortuna")); cm = CFG["critMult"] + s.V("ojo")
        critF = 1 + cc * (cm - 1)
        speedF = min(1.3, (s.V("inercia") / 11) ** 2 * 0.85)  # (v/vref)^2 medio
        reb = 0.15 * s.V("rebote") / 0.4 if s.tr["rebote"] else 0
        hps = 0.55 + 0.03 * s.V("cantera") + reb + 0.08 * (s.V("cargas") - 1)
        combo = 1.25 + reb
        player = dmg * hps * speedF * combo * critF
        if s.tr["sismo"]: player *= 1 + 0.25 * s.V("sismo") / 3
        if s.tr["metralla"]: player *= 1 + 0.35 * s.V("metralla") / 0.4
        shock = dmg * CFG["shockMult"] * s.V("resonancia") * 2 * hps * CFG["resPerImpact"] * combo
        if s.tr["eco"]:
            n = int(s.V("eco")); shock *= 1 + sum(max(0.25, 0.6 - 0.1 * k) for k in range(1, n + 1)) * 1.3
        if s.tr["perpetua"]: shock *= 1.15 * s.V("perpetua")
        # Discípulas: embisten ~0,35 veces/s con tu daño × Vínculo (sin combo)
        allies = s.V("gemelas") * dmg * s.V("vinculo") * 0.35 * speedF * critF
        aco = (s.V("acolitos") + s.V("eterno")) * s.V("cincel") * s.V("coro") * G / CFG["acolyteInterval"] * (1 + cc * 0.5 * (cm - 1))
        return player + shock + aco + allies

    def block_hp(s):
        c = CFG; gold = c["goldenChance"] + s.V("fiebre")
        avg = (1 - c["largeShare"]) + c["largeShare"] * c["largeMult"]
        mh = MUT["hp"] if MUT["on"] and s.prest > 0 else 1
        return c["baseHP"] * c["hpGrowth"] ** s.g * avg * (1 + gold * (c["goldenHP"] - 1)) * mh, gold

    def reward_per_hp(s):
        c = CFG; _, gold = s.block_hp()
        avg = (1 - c["largeShare"]) + c["largeShare"] * c["largeMult"]
        base = c["baseReward"] * avg * s.V("pureza") * s.V("brillo") * s.V("obra") * s.fame() * c["rewardGrowth"] ** s.g
        statue = 1 + c["statueBonus"] * s.V("veta")
        goldF = 1 + gold * (c["goldenReward"] * s.V("veta") - 1)
        md = MUT["dust"] if MUT["on"] and s.prest > 0 else 1
        return base * statue * goldF * md   # polvo por BLOQUE completo

    def needed(s):
        return min(CFG["statMax"], CFG["statBase"] + CFG["statStep"] * s.g)

    def buy_cheapest(s):
        bought = True
        while bought:
            bought = False
            best = None
            for k, d in DUST.items():
                if s.lv[k] >= s.dmax(k) or not s.dust_open(k): continue
                c = s.dust_cost(k)
                if best is None or c < best[0]: best = (c, k)
            if best and best[0] <= s.dust:
                s.dust -= best[0]; s.lv[best[1]] += 1; bought = True

    def spend_essence(s):
        bought = True
        while bought:
            bought = False
            best = None
            for k, d in TREE.items():
                if s.tr[k] >= d[2] or any(s.tr[r] == 0 for r in d[6]): continue
                c = cost(d, s.tr[k])
                if best is None or c < best[0]: best = (c, k)
            if best and best[0] <= s.ess:
                s.ess -= best[0]; s.tr[best[1]] += 1; bought = True

    def gain(s):
        if s.g < CFG["prestigeMin"]: return 0
        return math.floor(CFG["prestigeBase"] * (s.g - CFG["prestigeMin"] + 3) ** CFG["prestigePow"] * (1 + CONS_BONUS * getattr(s, "cons", 0)))

    def prestige(s):
        gn = s.gain()
        if MUT["on"] and s.prest > 0 and s.g - s.g0 >= MUT["goal"]: gn += max(1, math.floor(gn * MUT["bonus"]))
        s.log.append(f"  >> PULIDO #{s.prest+1} a {s.t/60:6.1f} min | galería {s.g+1} | +{gn} esencia")
        s.lives.append((s.prest, None if s.done_t is None else (s.done_t - s.life_t0) / 60, (s.t - s.life_t0) / 60))
        s.ess += gn; s.prest += 1
        if CONS and s.prest - getattr(s, "cons_at", 0) >= CONS:
            s.cons = getattr(s, "cons", 0) + 1; s.cons_at = s.prest
            s.ess = 0; s.tr = {k: (max(1, int(v * CONS_KEEP)) if v else 0) for k, v in s.tr.items()}
            s.log.append(f"  >> CONSAGRACIÓN #{s.cons} (Esencia a 0, Árbol ×{CONS_KEEP})")
        if HITO and s.prest % HITO == 0:
            s.cons = getattr(s, "cons", 0) + 1
            s.log.append(f"  >> RELIQUIA (hito {s.cons})")
        s.st["prestige"] = s.prest
        s.add_icor(ICOR["prestigeBase"] + gn // ICOR["prestigeDiv"])
        s.dust = s.run = 0; s.stat = 0
        s.lv = {k: 0 for k in DUST}
        s.spend_essence()
        s.g = int(s.V("memoria")); s.tg = s.t
        s.g0 = s.g; s.run_vol = 0.0; s.done_t = None; s.life_t0 = s.t

    def run_sim(s, seconds):
        acc_hp = getattr(s, "acc_hp", 0.0)   # persiste entre llamadas (los cortes de la tabla no pierden la fracción)
        if not hasattr(s, "life_t0"): s.life_t0 = 0
        if not hasattr(s, "stage"): s.stage = 0; s.trial_t = []
        while s.t < seconds:
            s.t += 1
            hp, _ = s.block_hp()
            d = s.dps()
            cap = 0.22 + 0.03 * s.V("cantera") + 0.02 * (s.V("acolitos") + s.V("eterno"))  # bloques/s máximos (física, respawn)
            kills = min(d / hp, cap)
            s.cur_kills = kills
            pk, pmult, casts = s.power_kills(hp)
            mm, musa_rate = s.musa_mult()
            kills += min(pk, cap)          # los poderes rompen bloques aparte del ritmo físico
            acc_hp += kills
            earned = kills * s.reward_per_hp() * pmult * mm
            _, gold = s.block_hp()
            s.add_icor(kills * (ICOR["statueChance"] + gold * ICOR["goldenChance"] * ICOR["golden"]) + musa_rate * ICOR["musa"])
            s.st["statues"] += kills; s.st["golden"] += kills * gold; s.st["musas"] += musa_rate
            s.st["powers"] += casts; s.st["dust"] += earned; s.st["gallery"] = max(s.st["gallery"], s.g + 1)
            s.st["combo"] = 50 if s.t > 120 else 15 if s.t > 40 else 0
            if s.t % 5 == 0: s.check_ach(); s.spend_icor()
            if s.g >= CFG["volatileUnlock"]: s.run_vol += kills * CFG["volatileChance"]
            s.dust += earned; s.run += earned
            while acc_hp >= 1:
                acc_hp -= 1; s.stat += 1
                if s.stat >= s.needed() and s.stage < len(TRIALS) and s.g + 1 >= TRIALS[s.stage]:
                    # Prueba de la sala: jefe = Coloso × TRIAL.hpMult; da la pieza y abre la sala siguiente
                    acc_hp -= BOSS["hpMult"] * TRIAL["hpMult"] / ((1 - CFG["largeShare"]) + CFG["largeShare"] * CFG["largeMult"])
                    s.add_icor(TRIAL["icor"]); s.trial_t.append(s.t); s.boss_g = s.g
                    s.log.append(f"  ## PRUEBA {s.stage+1} superada a {s.t/60:6.1f} min | galería {s.g+1} | pulidos {s.prest}")
                    s.stage += 1; s.stat -= 1
                    continue
                if s.stat >= s.needed() and (s.g + 1) % BOSS["every"] == 0 and getattr(s, "boss_g", -1) != s.g:
                    # Coloso: HP = hpMult bloques pequeños (≈ hpMult / 1,48 bloques medios); cierra la galería
                    s.boss_g = s.g; s.bosses = getattr(s, "bosses", 0) + 1
                    acc_hp -= BOSS["hpMult"] / ((1 - CFG["largeShare"]) + CFG["largeShare"] * CFG["largeMult"])
                    s.dust += s.reward_per_hp() * BOSS["dustMult"] / 1.48 / (1 + CFG["statueBonus"])
                    s.add_icor(BOSS["icor"] + BOSS["icorPerTier"] * ((s.g + 1) // BOSS["every"] - 1))
                    s.stat -= 1
                    continue
                if s.stat >= s.needed():
                    s.stat = 0; s.g += 1
                    s.log.append(f"{s.t/60:7.1f} min  Galería {s.g+1:3d}  (+{(s.t-s.tg)/60:5.1f} min)  dps={d:10.3g}  polvo/s={earned:10.3g}")
                    s.tg = s.t
                    hp, _ = s.block_hp()
            s.acc_hp = acc_hp
            if s.run > 1e300: s.log.append("OVERFLOW"); break
            s.buy_cheapest()
            if s.done_t is None and s.workshop_done(): s.done_t = s.t
            stuck = s.t - s.tg
            gn = s.gain()
            if s.g >= CFG["prestigeMin"] and gn >= 1:
                total_ess = s.ess + sum(sum(cost(TREE[k], l) for l in range(s.tr[k])) for k in TREE)
                if (stuck > 240 and gn >= max(2, 0.5 * total_ess)) or stuck > 600:
                    s.prestige()
        return s

if __name__ == "__main__":
    import os
    s = Sim()
    for m in [2, 5, 10, 20, 30, 45, 60, 90, 120, 180, 240, 360, 480, 720, 1200]:
        if m * 60 > HOURS * 3600: break
        s.run_sim(m * 60)
        print(f"[{m:5d} min] galería {s.g+1:3d} | pulidos {s.prest:2d} | esencia libre {s.ess:7.0f} | árbol {sum(s.tr.values()):4d} niv"
              f" | ícor {s.icor_life:6.0f} (altar {sum(s.al.values()):3d} niv) | mecenazgos {len(s.ach):2d}")
    print("\n".join(l for l in s.log if os.environ.get("SIMLOG") or "PULIDO" in l or "PODER" in l or "PRUEBA" in l))
    prev = 0
    for i, t in enumerate(getattr(s, "trial_t", [])):
        print(f"Prueba {i+1} (galería {TRIALS[i]}): {t/3600:4.2f} h  (sala: {(t-prev)/3600:4.2f} h)"); prev = t
    if len(getattr(s, "trial_t", [])) < len(TRIALS): print(f"Pruebas superadas: {len(s.trial_t)}/{len(TRIALS)} en {HOURS} h")
    print(f"Colosos derrotados: {getattr(s, 'bosses', 0)} | mutador medio: {'sí' if MUT['on'] else 'no'}")
    print("Vida | Taller completo (min) | duración vida (min)")
    for p, done, life in s.lives:
        print(f"  #{p:2d} | {('%6.1f' % done) if done is not None else '   nunca'} | {life:6.1f}")
    print("Árbol: " + ", ".join(f"{k}{v}" for k, v in s.tr.items() if v))
    print("Altar: " + ", ".join(f"{k}{v}" for k, v in s.al.items() if v))
    finite = sum(sum(cost(d, l) for l in range(d[2])) for k, d in ALTAR.items() if d[2] < 100)
    print(f"Ícor para completar el Altar (sin Ira): {finite:.0f}")
