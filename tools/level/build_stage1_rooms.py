"""Builds the Stage 1 rooms in data/runs/honey_labyrinth_stage_1.json (rounds 60-61): fight rooms about two screens
wide with two ledge levels, climb rooms two screens tall, last waves of 5-6 monsters.
Run from the repo root: python tools/level/build_stage1_rooms.py data/runs/honey_labyrinth_stage_1.json"""
import json
import sys

path = sys.argv[1]
data = json.load(open(path, encoding="utf-8"))
rooms = {r["id"]: r for r in data["rooms"]}
P = "assets/backgrounds/chapter_01/honey_labyrinth/platforms/"


def plat(sprite, x, y):
    spec = {
        "ledge_short": (400, 7),
        "ledge_wide": (640, 52),
        "root": (540, 59),
    }[sprite]
    return {"sprite": P + sprite + ".png", "x": x, "y": y, "width": spec[0], "top_inset": spec[1]}


def s(monster, x, y=None):
    spawn = {"monster": monster, "x": x}
    if y is not None:
        spawn["y"] = y
    return spawn


def wave(*spawns):
    return {"spawns": list(spawns)}


LOW, HIGH = 680, 480  # ledge tops on a one-screen room (floor 885): 205 and 200 apart  # ledge tops on a one-screen-tall room (floor 885)

rooms["entrance"].update(width=2880, shrine_x=1500, crates=[900, 2300],
                         platforms=[plat("ledge_wide", 2200, LOW)])

rooms["gate"].update(
    width=3360,
    platforms=[plat("ledge_short", 1250, LOW), plat("ledge_wide", 2100, LOW), plat("ledge_short", 2700, HIGH)],
    crates=[600, 2100, 2900],
    waves=[
        wave(s("honey_slime", 1300), s("honey_slime", 1900), s("sprout_mushroom", 2700, HIGH)),
        wave(s("sprout_mushroom", 1500), s("honey_slime", 2400), s("corrupted_seedling", 2900)),
    ])

rooms["hedge"].update(
    width=3840,
    platforms=[plat("ledge_wide", 1150, LOW), plat("ledge_short", 1800, HIGH), plat("ledge_wide", 2600, LOW), plat("ledge_short", 3300, HIGH)],
    crates=[1150, 3500],
    waves=[
        wave(s("corrupted_seedling", 1500), s("sprout_mushroom", 1900), s("honey_slime", 2600, LOW)),
        wave(s("honey_slime", 900), s("corrupted_seedling", 2300), s("corrupted_seedling", 3000), s("sprout_mushroom", 3300, HIGH)),
        wave(s("sprout_mushroom", 1200), s("honey_slime", 2000), s("honey_slime", 2800), s("corrupted_seedling", 3400), s("honey_slime", 2600, LOW), s("sprout_mushroom", 3300, HIGH)),
    ])

rooms["roots"].update(
    width=3600,
    platforms=[plat("root", 800, LOW), plat("ledge_short", 1500, LOW), plat("root", 2300, HIGH), plat("ledge_wide", 3000, LOW)],
    crates=[300, 2300],
    waves=[
        wave(s("little_treant", 1600), s("honey_slime", 2000), s("honey_slime", 3000, LOW)),
        wave(s("little_treant", 2400), s("sprout_mushroom", 1500, LOW), s("honey_slime", 3200), s("corrupted_seedling", 900)),
    ])

rooms["beetle_run"].update(
    width=4320,
    platforms=[plat("ledge_wide", 1000, LOW), plat("ledge_short", 1650, HIGH), plat("ledge_wide", 2300, LOW), plat("ledge_short", 3000, HIGH), plat("ledge_wide", 3600, LOW)],
    crates=[1650, 3800],
    waves=[
        wave(s("fire_beetle", 2300), s("honey_slime", 1000, LOW)),
        wave(s("fire_beetle", 700), s("fire_beetle", 3400), s("honey_slime", 2000), s("honey_slime", 3600, LOW)),
        wave(s("fire_beetle", 3900), s("corrupted_seedling", 2600), s("sprout_mushroom", 1600), s("honey_slime", 3000, HIGH), s("honey_slime", 2300, LOW), s("corrupted_seedling", 1200)),
    ])

rooms["mixed"].update(
    width=3600,
    platforms=[plat("ledge_short", 1200, LOW), plat("ledge_wide", 1900, HIGH), plat("ledge_short", 2700, LOW)],
    crates=[700, 3200],
    waves=[
        wave(s("little_treant", 1600), s("corrupted_seedling", 2000), s("honey_slime", 2700, LOW)),
        wave(s("fire_beetle", 3100), s("sprout_mushroom", 900), s("sprout_mushroom", 1900, HIGH), s("honey_slime", 2400)),
        wave(s("little_treant", 2500), s("corrupted_seedling", 1200), s("corrupted_seedling", 3000), s("fire_beetle", 600), s("honey_slime", 2700, LOW), s("sprout_mushroom", 1900, HIGH)),
    ])

rooms["treasure"].update(width=2400, chest_x=1200, crates=[500, 1900], platforms=[plat("ledge_short", 1200, LOW)])
rooms["treasure"]["chest_y"] = LOW

rooms["lair"].update(width=2880, challenge={**rooms["lair"]["challenge"], "lever_x": 520, "chest_x": 1440},
                     platforms=[plat("ledge_wide", 1000, LOW), plat("ledge_wide", 2000, LOW)],
                     waves=[
                         wave(s("honey_slime", 1300), s("corrupted_seedling", 1800), s("sprout_mushroom", 2300), s("honey_slime", 2000, LOW)),
                         wave(s("little_treant", 1600), s("fire_beetle", 2500), s("honey_slime", 1000, LOW), s("sprout_mushroom", 2200)),
                         wave(s("fire_beetle", 700), s("corrupted_seedling", 2400), s("little_treant", 1200), s("honey_slime", 2000, LOW), s("sprout_mushroom", 2600)),
                     ])
rooms["untouched"].update(width=2880, challenge={**rooms["untouched"]["challenge"], "chest_x": 1440},
                          platforms=[plat("ledge_short", 1440, LOW)],
                          waves=[
                              wave(s("little_treant", 1700), s("honey_slime", 2100), s("honey_slime", 1440, LOW)),
                              wave(s("corrupted_seedling", 1900), s("sprout_mushroom", 1300), s("sprout_mushroom", 2400)),
                          ])

# climb rooms: two screens tall (floor at 1965), ledges every ~220 px up to an exit in the ceiling
TALL, FLOOR = 2160, 1965
tiers = [1770, 1575, 1380, 1185, 990, 795, 600, 405, 210]  # 195 apart: Lumi jumps ~300


def climb(xs, exit_x):
    platforms = []
    for i, (x, y) in enumerate(zip(xs, tiers)):
        platforms.append(plat("ledge_wide" if i % 3 == 2 or i == len(tiers) - 1 else ("root" if i % 2 else "ledge_short"), x, y))
    platforms[-1]["x"] = exit_x
    return platforms


rooms["climb_a"].update(
    width=2880, height=TALL, ground_y=FLOOR,
    platforms=climb([600, 1000, 1450, 1900, 2350, 1900, 1450, 1000, 900], 900),
    up={"x": 900, "width": 640, "spawn_x": 900, "spawn_y": -20},
    waves=[
        wave(s("honey_slime", 1300), s("honey_slime", 1900), s("corrupted_seedling", 2300), s("sprout_mushroom", 1500, tiers[2])),
        wave(s("honey_slime", 2000, tiers[3]), s("sprout_mushroom", 1400, tiers[6]), s("corrupted_seedling", 2400, tiers[4]), s("honey_slime", 900)),
    ])
rooms["climb_b"].update(
    width=2880, height=TALL, ground_y=FLOOR,
    platforms=climb([2300, 1850, 1400, 950, 500, 950, 1400, 1850, 2000], 2000),
    up={"x": 2000, "width": 640, "spawn_x": 2000, "spawn_y": -20},
    waves=[
        wave(s("little_treant", 1600), s("honey_slime", 2200), s("honey_slime", 1400, tiers[2]), s("corrupted_seedling", 700)),
        wave(s("fire_beetle", 2400), s("sprout_mushroom", 900, tiers[3]), s("honey_slime", 1450, tiers[6]), s("corrupted_seedling", 1800)),
    ])

rooms["den"].update(width=2880, platforms=[plat("ledge_short", 900, 625), plat("ledge_short", 1980, 625)],
                    waves=[wave(s("raccoon_mask", 2100))])



def layers(wall):
    return [{"sprite": "assets/backgrounds/chapter_01/honey_labyrinth/back.png", "scroll_scale": 0.3, "z_index": -30},
            {"sprite": f"assets/backgrounds/chapter_01/honey_labyrinth/{wall}.png", "scroll_scale": 1.0, "z_index": -10}]


def room(room_id, kind, width, wall, **fields):
    base = {"id": room_id, "width": width, "ground_y": 885, "layers": layers(wall), "platforms": [], "waves": [], "crates": [], "kind": kind}
    base.update(fields)
    rooms[room_id] = base


# round 61: two more fight rooms and a platforming room, so a run can hold 14 rooms
room("thorns", "fight", 3840, "wall_c",
     platforms=[plat("root", 900, LOW), plat("ledge_wide", 1700, HIGH), plat("ledge_short", 2500, LOW), plat("ledge_wide", 3200, HIGH)],
     crates=[400, 2500],
     waves=[
         wave(s("honey_slime", 1500), s("honey_slime", 1900), s("sprout_mushroom", 1700, HIGH)),
         wave(s("little_treant", 2600), s("corrupted_seedling", 1400), s("honey_slime", 900, LOW), s("sprout_mushroom", 3200, HIGH)),
         wave(s("fire_beetle", 3500), s("honey_slime", 1200), s("honey_slime", 2500, LOW), s("corrupted_seedling", 2200), s("sprout_mushroom", 2900), s("honey_slime", 1700, HIGH)),
     ])
room("bramble_bridge", "fight", 3360, "wall_b",
     platforms=[plat("ledge_wide", 800, LOW), plat("ledge_wide", 1700, LOW), plat("ledge_wide", 2600, LOW), plat("ledge_short", 1250, HIGH), plat("ledge_short", 2150, HIGH)],
     crates=[1250, 3000],
     waves=[
         wave(s("corrupted_seedling", 1600), s("corrupted_seedling", 2400), s("honey_slime", 1700, LOW)),
         wave(s("sprout_mushroom", 1200), s("sprout_mushroom", 2800), s("honey_slime", 2600, LOW), s("honey_slime", 800, LOW)),
         wave(s("little_treant", 1700), s("fire_beetle", 3000), s("corrupted_seedling", 2200), s("honey_slime", 1250, HIGH), s("honey_slime", 2150, HIGH), s("sprout_mushroom", 600)),
     ])
# Little Noah's "explore the ruins from end to end": no monsters, a ledge path, the chest waits high at the far end
room("trail", "treasure", 4320, "wall_a",
     platforms=[plat("ledge_short", 700, LOW), plat("root", 1200, HIGH), plat("ledge_short", 1750, LOW), plat("ledge_wide", 2350, HIGH),
                plat("ledge_short", 2950, LOW), plat("root", 3450, HIGH), plat("ledge_wide", 3950, 280)],
     crates=[1200, 2950], chest_x=3950, chest_y=280, reward_on_clear=False)


# round 65: a mini boss room, one big calm fight between the crowded rooms
room("honey_court", "elite", 2880, "wall_c",
     platforms=[plat("ledge_short", 700, LOW), plat("ledge_short", 2180, LOW)],
     crates=[300, 2600],
     waves=[wave(s("honey_king", 1800), s("honey_slime", 700, LOW), s("honey_slime", 2180, LOW))])


# round 67: keys on high ledges reward exploring; elite rooms drop one; sky rooms (side paths) hold locked chests
rooms["hedge"]["keys"] = [{"x": 3300, "y": HIGH}]
rooms["thorns"]["keys"] = [{"x": 3200, "y": HIGH}]
rooms["mixed"]["keys"] = [{"x": 1900, "y": HIGH}]
rooms["honey_court"]["key_on_clear"] = True
rooms["sky_nest"]["chest_locked"] = True
rooms["sky_bridge"].update(chest_x=1360, chest_y=660, chest_locked=True)


def cells(*items):
    return [{"x": x, "y": y, "kind": kind} for x, y, kind in items]


E, F, T, R, S, B, M = "entrance", "fight", "trial", "treasure", "sky", "boss", "elite"
data["layouts"] = [
    {"id": "long_nest", "cells": cells((0, 0, E), (1, 0, F), (2, 0, F), (3, 0, T), (4, 0, F), (4, -1, S), (5, 0, F), (6, 0, R),
                                       (7, 0, M), (8, 0, F), (9, 0, T), (10, 0, F), (11, 0, R), (12, 0, B))},
    {"id": "long_high_road", "cells": cells((0, 0, E), (1, 0, F), (2, 0, T), (3, 0, F), (3, -1, S), (4, -1, S), (4, 0, F), (5, 0, R),
                                            (6, 0, F), (7, 0, M), (8, 0, T), (9, 0, F), (10, 0, R), (11, 0, B))},
    {"id": "long_climbs", "cells": cells((0, 0, E), (1, 0, F), (1, -1, S), (2, 0, F), (3, 0, R), (4, 0, T), (5, 0, F), (6, 0, M),
                                         (7, 0, F), (7, -1, S), (8, 0, T), (9, 0, F), (10, 0, R), (11, 0, B))},
]
data["rooms"] = list(rooms.values())

json.dump(data, open(path, "w", encoding="utf-8"), indent=2, ensure_ascii=False)
open(path, "a", encoding="utf-8").write("\n")
print("rooms:", {r["id"]: (r["width"], r.get("height", 1080), sum(len(w["spawns"]) for w in r["waves"])) for r in data["rooms"]})
