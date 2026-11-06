"""Write Lumi's first-pass animations into the rig JSON, import it into Spine and export for Godot.
Run from the repo root: python tools/art/build_lumi_spine.py
This rebuilds the project from scratch, so do not run it after animations were edited by hand in Spine."""
import json
import os
import subprocess

SPINE = r'C:\Program Files\Spine\Spine.com'
FOLDER = 'art_source/characters/lumi/spine/'
RIG = FOLDER + 'lumi_rig_v3.json'
PROJECT = FOLDER + 'lumi_prototype.spine'
EXPORT = 'assets/characters/lumi/spine/'
TICK = 1 / 60
SWORD_SCALE = 1.25
WEAPON_X, WEAPON_Y = 62, -18

REST = {'upperarm_R': 18, 'forearm_R': 22, 'weapon': -80, 'upperarm_L': -6, 'forearm_L': -6}


def rotate(ticks_and_degrees):
    return {'rotate': [{'time': round(t * TICK, 4), 'value': v} for t, v in ticks_and_degrees]}


def clip(rotations, hip_y=None, hip_x=None):
    bones = {name: rotate(keys) for name, keys in rotations.items()}
    if hip_y or hip_x:
        ticks = sorted({t for t, _ in (hip_y or []) + (hip_x or [])})
        ys, xs = dict(hip_y or []), dict(hip_x or [])
        bones['hip'] = {'translate': [{'time': round(t * TICK, 4), 'x': xs.get(t, 0), 'y': ys.get(t, 0)} for t in ticks]}
    return {'bones': bones}


def swing(total, arm, fore, sword, torso, front_leg, back_leg, left_arm):
    """Each list holds (tick, degrees). Every swing starts and ends on the rest pose."""
    rest = lambda bone, keys: [(0, REST.get(bone, 0))] + keys + [(total, REST.get(bone, 0))]
    return clip({
        'upperarm_R': rest('upperarm_R', arm),
        'forearm_R': rest('forearm_R', fore),
        'weapon': rest('weapon', sword),
        'torso': rest('torso', torso),
        'leg_R': rest('leg_R', front_leg),
        'leg_L': rest('leg_L', back_leg),
        'boot_L': rest('boot_L', [(t, -abs(v) * 0.6) for t, v in back_leg]),
        'upperarm_L': rest('upperarm_L', left_arm),
        'head': rest('head', [(t, -v * 0.4) for t, v in torso]),
        'skirt': rest('skirt', [(t, v * 0.5) for t, v in torso]),
        'tail_L': rest('tail_L', [(t, -v) for t, v in torso]),
        'tail_R': rest('tail_R', [(t, -v * 0.7) for t, v in torso]),
    })


def idle():
    return clip({
        'torso': [(0, 0), (40, -2), (80, 0)],
        'upperarm_R': [(0, 18), (40, 21), (80, 18)],
        'forearm_R': [(0, 22), (80, 22)],
        'weapon': [(0, -80), (40, -83), (80, -80)],
        'upperarm_L': [(0, -6), (40, -9), (80, -6)],
        'forearm_L': [(0, -6), (80, -6)],
        'tail_L': [(0, 0), (40, 3), (80, 0)],
        'tail_R': [(0, 0), (40, -3), (80, 0)],
        'ahoge': [(0, 0), (40, 4), (80, 0)],
    }, hip_y=[(0, 0), (40, -8), (80, 0)])


def run():
    a, b = 32, -30
    return clip({
        'torso': [(0, -9), (36, -9)],
        'head': [(0, 6), (36, 6)],
        'leg_R': [(0, a), (9, 2), (18, b), (27, 8), (36, a)],
        'boot_R': [(0, -8), (9, -14), (18, -35), (27, -58), (36, -8)],
        'leg_L': [(0, b), (9, 8), (18, a), (27, 2), (36, b)],
        'boot_L': [(0, -35), (9, -58), (18, -8), (27, -14), (36, -35)],
        'upperarm_R': [(0, 30), (18, 42), (36, 30)],
        'forearm_R': [(0, 35), (36, 35)],
        'weapon': [(0, -125), (18, -135), (36, -125)],
        'upperarm_L': [(0, 26), (18, -40), (36, 26)],
        'forearm_L': [(0, 22), (18, -30), (36, 22)],
        'skirt': [(0, 3), (18, -3), (36, 3)],
        'tail_L': [(0, -14), (18, -22), (36, -14)],
        'tail_R': [(0, -10), (18, -18), (36, -10)],
    }, hip_y=[(0, -12), (9, 8), (18, -12), (27, 8), (36, -12)])


def z1():
    return swing(30,
                 arm=[(7, 125), (11, 60), (15, 20)], fore=[(7, 35), (11, 5), (15, -5)],
                 sword=[(7, -100), (11, -150), (15, -130)], torso=[(7, 8), (11, -12), (15, -14)],
                 front_leg=[(7, -6), (11, 20), (15, 22)], back_leg=[(7, 6), (11, -16), (15, -18)],
                 left_arm=[(7, 20), (11, -40), (15, -50)])


def z2():
    return swing(30,
                 arm=[(7, 10), (11, 95), (15, 135)], fore=[(7, -15), (11, 10), (15, 25)],
                 sword=[(7, -150), (11, -115), (15, -110)], torso=[(7, -10), (11, 4), (15, 10)],
                 front_leg=[(7, 16), (11, 8), (15, -4)], back_leg=[(7, -14), (11, -6), (15, 6)],
                 left_arm=[(7, -35), (11, 10), (15, 25)])


def z3():
    return swing(33,
                 arm=[(9, 140), (13, 70), (17, 15)], fore=[(9, 40), (13, 5), (17, -10)],
                 sword=[(9, -120), (13, -160), (17, -135)], torso=[(9, 12), (13, -18), (17, -22)],
                 front_leg=[(9, -10), (13, 28), (17, 32)], back_leg=[(9, 10), (13, -24), (17, -26)],
                 left_arm=[(9, 30), (13, -55), (17, -65)])


def z_dash():
    return swing(29,
                 arm=[(6, -10), (9, 75), (13, 85)], fore=[(6, 30), (9, 10), (13, 5)],
                 sword=[(6, -60), (9, -170), (13, -175)], torso=[(6, 10), (9, -20), (13, -24)],
                 front_leg=[(6, -8), (9, 34), (13, 38)], back_leg=[(6, 8), (9, -30), (13, -34)],
                 left_arm=[(6, 20), (9, -60), (13, -70)])


def guard():
    return clip({
        'torso': [(0, -5), (30, -3), (60, -5)],
        'head': [(0, 4), (60, 4)],
        'upperarm_R': [(0, 70), (60, 70)],
        'forearm_R': [(0, 70), (60, 70)],
        'weapon': [(0, -125), (30, -122), (60, -125)],
        'upperarm_L': [(0, 50), (60, 50)],
        'forearm_L': [(0, 40), (60, 40)],
        'leg_R': [(0, 14), (60, 14)],
        'leg_L': [(0, -16), (60, -16)],
        'boot_L': [(0, -10), (60, -10)],
        'tail_L': [(0, 4), (30, 7), (60, 4)],
        'tail_R': [(0, -4), (30, -7), (60, -4)],
    }, hip_y=[(0, -16), (30, -20), (60, -16)])


def jump():
    return clip({
        'torso': [(0, -4), (15, -4)],
        'leg_R': [(0, 10), (15, 38)],
        'boot_R': [(0, -10), (15, -55)],
        'leg_L': [(0, -6), (15, -12)],
        'boot_L': [(0, -10), (15, -42)],
        'upperarm_R': [(0, 18), (15, 34)],
        'forearm_R': [(0, 22), (15, 30)],
        'weapon': [(0, -80), (15, -105)],
        'upperarm_L': [(0, -6), (15, -55)],
        'tail_L': [(0, 0), (15, 16)],
        'tail_R': [(0, 0), (15, -16)],
        'skirt': [(0, 0), (15, 4)],
    })


def fall():
    return clip({
        'torso': [(0, 3), (30, 3)],
        'leg_R': [(0, 18), (30, 18)],
        'boot_R': [(0, -25), (30, -25)],
        'leg_L': [(0, -12), (30, -12)],
        'boot_L': [(0, -30), (30, -30)],
        'upperarm_R': [(0, 52), (30, 52)],
        'forearm_R': [(0, 28), (30, 28)],
        'weapon': [(0, -125), (30, -125)],
        'upperarm_L': [(0, -72), (15, -66), (30, -72)],
        'tail_L': [(0, -24), (15, -30), (30, -24)],
        'tail_R': [(0, 24), (15, 30), (30, 24)],
        'ahoge': [(0, 12), (15, 18), (30, 12)],
        'skirt': [(0, 3), (15, -3), (30, 3)],
    })


def land():
    return clip({
        'torso': [(0, -12), (5, -14), (18, 0)],
        'head': [(0, 6), (18, 0)],
        'leg_R': [(0, 18), (5, 22), (18, 0)],
        'boot_R': [(0, -22), (5, -30), (18, 0)],
        'leg_L': [(0, -16), (5, -20), (18, 0)],
        'boot_L': [(0, -18), (5, -26), (18, 0)],
        'upperarm_R': [(0, 40), (18, 18)],
        'forearm_R': [(0, 30), (18, 22)],
        'weapon': [(0, -110), (18, -80)],
        'upperarm_L': [(0, -50), (18, -6)],
        'forearm_L': [(0, -6), (18, -6)],
        'tail_L': [(0, 10), (18, 0)],
        'tail_R': [(0, -10), (18, 0)],
    }, hip_y=[(0, -28), (5, -42), (18, 0)])


def dash():
    return clip({
        'torso': [(0, -14), (12, -14)],
        'head': [(0, 12), (12, 12)],
        'leg_R': [(0, 30), (12, 30)],
        'boot_R': [(0, -20), (12, -20)],
        'leg_L': [(0, -32), (12, -32)],
        'boot_L': [(0, -40), (12, -40)],
        'upperarm_R': [(0, -25), (12, -25)],
        'forearm_R': [(0, 10), (12, 10)],
        'weapon': [(0, 100), (12, 100)],
        'upperarm_L': [(0, -30), (12, -30)],
        'tail_L': [(0, -50), (12, -50)],
        'tail_R': [(0, -45), (12, -45)],
        'skirt': [(0, -10), (12, -10)],
    }, hip_y=[(0, -8), (12, -8)])


def sprint():
    a, b = 45, -42
    return clip({
        'torso': [(0, -18), (24, -18)],
        'head': [(0, 10), (24, 10)],
        'leg_R': [(0, a), (6, 4), (12, b), (18, 12), (24, a)],
        'boot_R': [(0, -10), (6, -20), (12, -40), (18, -70), (24, -10)],
        'leg_L': [(0, b), (6, 12), (12, a), (18, 4), (24, b)],
        'boot_L': [(0, -40), (6, -70), (12, -10), (18, -20), (24, -40)],
        'upperarm_R': [(0, -25), (24, -25)],
        'forearm_R': [(0, 10), (24, 10)],
        'weapon': [(0, 100), (24, 100)],
        'upperarm_L': [(0, 30), (12, -50), (24, 30)],
        'forearm_L': [(0, 25), (12, -35), (24, 25)],
        'skirt': [(0, -8), (12, -14), (24, -8)],
        'tail_L': [(0, -36), (12, -46), (24, -36)],
        'tail_R': [(0, -30), (12, -40), (24, -30)],
    }, hip_y=[(0, -18), (6, 6), (12, -18), (18, 6), (24, -18)])


def main():
    changed = subprocess.run(['git', 'status', '--porcelain', PROJECT], capture_output=True, text=True).stdout.strip()
    if changed:
        raise SystemExit(f'{PROJECT} has uncommitted edits. Save them first; this script rebuilds the project.')

    rig = json.load(open(RIG, encoding='utf-8'))
    next(b for b in rig['bones'] if b['name'] == 'weapon').update(x=WEAPON_X, y=WEAPON_Y)
    sword = rig['skins'][0]['attachments']['weapon']['weapon_sword']
    sword.update(scaleX=SWORD_SCALE, scaleY=SWORD_SCALE, y=round(sword['height'] * SWORD_SCALE * 0.34))
    rig['animations'] = {
        'idle': idle(), 'run': run(), 'sprint': sprint(), 'jump': jump(), 'fall': fall(), 'land': land(), 'dash': dash(),
        'guard': guard(), 'z1': z1(), 'z2': z2(), 'z3': z3(), 's1': z_dash(),
    }
    json.dump(rig, open(RIG, 'w', encoding='utf-8', newline='\n'), indent=1)
    if os.path.exists(PROJECT):
        os.remove(PROJECT)
    subprocess.run([SPINE, '-i', RIG, '-o', PROJECT, '-r'], check=True)
    subprocess.run([SPINE, '-i', PROJECT, '-o', EXPORT, '-e', 'binary+pack'], check=True)


if __name__ == '__main__':
    main()
