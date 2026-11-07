"""Turn the picked ver12 images into game-ready PNGs under assets/. Run from the repo root:
python tools/art/prepare_prototype_art.py
Needs the local .documents/ folder. Picks: .documents/sample_pictures/ver12/README.md"""
import os

import cv2
import numpy as np
from PIL import Image

PICS = '.documents/sample_pictures/'
V = PICS + 'ver12/'
GAME_W, GAME_H = 1920, 1080
FOREST = 'assets/backgrounds/chapter_01/honey_forest/'
LABYRINTH = 'assets/backgrounds/chapter_01/honey_labyrinth/'
MONSTERS = 'assets/monsters/chapter_01/'
PROPS = 'assets/props/common/'
SKY_KEY_DISTANCE = 40
WHITE_CUTOFF = 244
SKY_DISTANCE = 26
FRONT_GRASS_DROP = 190
FRONT_EDGE_FADE = 220.0


def rgb(path):
    return np.asarray(Image.open(path).convert('RGB'))


def save(array, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    Image.fromarray(array).save(path)


def fit_height(a):
    width = int(a.shape[1] * GAME_H / a.shape[0])
    return cv2.resize(a, (width, GAME_H), interpolation=cv2.INTER_AREA)


def key_sky(a):
    """Remove the sky: pixels near the sky color connected to the top edge."""
    sky_color = a[: a.shape[0] // 2, a.shape[1] // 2].mean(0)
    lab = cv2.cvtColor(a, cv2.COLOR_RGB2LAB).astype(np.float32)
    ref = cv2.cvtColor(np.uint8([[sky_color]]), cv2.COLOR_RGB2LAB).astype(np.float32)[0, 0]
    near = (np.linalg.norm(lab - ref, axis=2) < SKY_DISTANCE).astype(np.uint8)
    _, labels = cv2.connectedComponents(near)
    top = np.unique(labels[0][near[0] > 0])
    sky = np.isin(labels, top[top > 0])
    alpha = 1 - cv2.GaussianBlur(sky.astype(np.float32), (0, 0), 1.0)
    return np.dstack([a, (alpha * 255).astype(np.uint8)])


def cut_white(a):
    """White outside the drawing becomes transparent; white inside it stays."""
    drawn = (a.astype(np.int32) < WHITE_CUTOFF).any(2).astype(np.uint8)
    _, labels = cv2.connectedComponents(1 - drawn)
    edge = np.unique(np.r_[labels[0], labels[-1], labels[:, 0], labels[:, -1]])
    outside = np.isin(labels, edge[edge > 0])
    alpha = cv2.GaussianBlur((~outside).astype(np.float32), (0, 0), 0.7)
    return np.dstack([a, (np.clip(alpha, 0, 1) * 255).astype(np.uint8)])


def crop_alpha(a):
    ys, xs = np.where(a[..., 3] > 8)
    return a[ys.min():ys.max() + 1, xs.min():xs.max() + 1]


def fade_white(a):
    """Soft blurred art on white: alpha comes from how far a pixel is from white."""
    pixels = a.astype(np.float32)
    alpha = np.clip((1 - pixels.min(2, keepdims=True) / 255) * 1.6, 0, 1)
    color = np.clip((pixels - (1 - alpha) * 255) / np.maximum(alpha, 1e-3), 0, 255)
    return np.dstack([color, alpha * 255]).astype(np.uint8)


def black_to_alpha(a):
    pixels = a.astype(np.float32)
    alpha = pixels.max(2, keepdims=True) / 255
    color = np.clip(pixels / np.maximum(alpha, 1e-3), 0, 255)
    return np.dstack([color, alpha * 255]).astype(np.uint8)


def panels(path, count=3):
    a = rgb(path)
    width = a.shape[1] // count
    return [crop_alpha(cut_white(a[:, i * width + 4:(i + 1) * width - 4])) for i in range(count)]


def key_color(a):
    """Remove the sky everywhere, windows in walls included."""
    sky = a[:60, a.shape[1] // 2 - 60:a.shape[1] // 2 + 60].reshape(-1, 3).mean(0)
    keep = (np.abs(a.astype(np.float32) - sky).sum(2) >= SKY_KEY_DISTANCE).astype(np.float32)
    return np.dstack([a, (cv2.GaussianBlur(keep, (0, 0), 0.8) * 255).astype(np.uint8)])


def pieces(path, count):
    """The largest drawings on a white sheet, in reading order. Thin frame lines are ignored."""
    a = rgb(path)
    drawn = (a.astype(np.int32) < WHITE_CUTOFF).any(2).astype(np.uint8)
    solid = cv2.morphologyEx(drawn, cv2.MORPH_OPEN, np.ones((5, 5), np.uint8))
    solid = cv2.dilate(solid, np.ones((25, 25), np.uint8))
    n, labels, stats, _ = cv2.connectedComponentsWithStats(solid)
    largest = sorted(range(1, n), key=lambda i: -stats[i, cv2.CC_STAT_AREA])[:count]
    row = a.shape[0] // 3
    boxes = sorted(largest, key=lambda i: (stats[i, cv2.CC_STAT_TOP] // row, stats[i, cv2.CC_STAT_LEFT]))
    out = []
    for i in boxes:
        x, y, w, h = stats[i, :4]
        out.append(crop_alpha(cut_white(a[y:y + h, x:x + w])))
    return out


def row_items(path, count):
    """The drawings in one row on a white sheet. Columns split at empty bands; the narrowest bands
    are joined until `count` remain, so the loose planks of a broken crate stay one drawing."""
    a = rgb(path)
    used = (a.astype(np.int32) < WHITE_CUTOFF).any(2).any(0)
    edges = np.flatnonzero(np.diff(np.r_[0, used.astype(np.int8), 0]))
    spans = [list(span) for span in edges.reshape(-1, 2)]
    while len(spans) > count:
        i = min(range(len(spans) - 1), key=lambda k: spans[k + 1][0] - spans[k][1])
        spans[i:i + 2] = [[spans[i][0], spans[i + 1][1]]]
    return [crop_alpha(cut_white(a[:, x0:x1])) for x0, x1 in spans]


def props():
    """Crate pair from M_1 (straight side view), chest pair from M_5 (red wood stands apart from the honey crates),
    tokens from M_2 (yellow star reads best at token size)."""
    crate, broken = row_items(V + 'M/ver12_M_1.png', 6)[:2]
    save(crate, PROPS + 'crate.png')
    save(broken, PROPS + 'crate_broken.png')
    chest, chest_open = row_items(V + 'M/ver12_M_5.png', 6)[2:4]
    save(chest, PROPS + 'chest.png')
    save(chest_open, PROPS + 'chest_open.png')
    heart, star = row_items(V + 'M/ver12_M_2.png', 6)[4:6]
    save(heart, PROPS + 'hp_token.png')
    save(star, PROPS + 'mp_token.png')


def labyrinth():
    save(fit_height(rgb(V + 'L/L1/ver12_L1_1.png')), LABYRINTH + 'back.png')
    for name, number in (('wall_a', 4), ('wall_b', 15), ('wall_c', 14)):
        save(key_color(fit_height(rgb(f'{V}L/L2/ver12_L2_{number}.png'))), f'{LABYRINTH}{name}.png')
    short, wide, root, _ = pieces(V + 'L/L3/ver12_L3_15.png', 4)
    save(short, LABYRINTH + 'platforms/ledge_short.png')
    save(wide, LABYRINTH + 'platforms/ledge_wide.png')
    save(root, LABYRINTH + 'platforms/root.png')
    save(pieces(V + 'L/L3/ver12_L3_6.png', 4)[3], LABYRINTH + 'door.png')


def first_run_monsters():
    for name, path in (('corrupted_seedling', 'J/ver12_J1_1.png'), ('little_treant', 'J/ver12_J2_2.png'), ('fire_beetle', 'J/ver12_J3_3.png')):
        idle, windup, healed = pieces(V + path, 3)
        save(idle, f'{MONSTERS}{name}/idle.png')
        save(windup, f'{MONSTERS}{name}/windup.png')
        save(healed, f'{MONSTERS}{name}/healed.png')
    idle, windup, _ = pieces(V + 'K/ver12_K_22.png', 3)
    save(idle, MONSTERS + 'raccoon_mask/idle.png')
    save(windup, MONSTERS + 'raccoon_mask/windup.png')
    save(pieces(V + 'K/ver12_K_17.png', 3)[2], MONSTERS + 'raccoon_mask/healed.png')


def background():
    save(fit_height(rgb(V + 'A/A3/ver12_A3_1.png')), FOREST + 'far.png')
    save(key_sky(fit_height(rgb(V + 'A/A2/ver12_A2_20.png'))), FOREST + 'mid.png')
    save(key_sky(fit_height(rgb(V + 'A/A1/ver12_A1_11.png'))), FOREST + 'play.png')
    front = cv2.resize(rgb(V + 'I/ver12_I_2.png'), (GAME_W, GAME_H), interpolation=cv2.INTER_AREA)
    save(prepare_front(fade_white(front)), FOREST + 'front.png')


def prepare_front(a):
    """Grass sinks so it never hides feet; side edges fade so the layer repeats with no seam."""
    out = a.copy()
    half = GAME_H // 2
    out[half:] = 0
    out[half + FRONT_GRASS_DROP:] = a[half:GAME_H - FRONT_GRASS_DROP]
    fade = np.clip(np.minimum(np.arange(GAME_W), GAME_W - 1 - np.arange(GAME_W)) / FRONT_EDGE_FADE, 0, 1)
    out[..., 3] = (out[..., 3] * fade[None, :]).astype(np.uint8)
    return out


def platforms():
    a = cut_white(rgb(V + 'H/ver12_H_3.png'))
    h, w = a.shape[:2]
    cells = {'ledge_short': a[:h // 2, :w // 2 - 120], 'ledge_wide': a[:h // 2, w // 2 - 120:],
             'log': a[h // 2:, :w // 2], 'bridge': a[h // 2:, w // 2:]}
    for name, cell in cells.items():
        save(crop_alpha(cell), f'{FOREST}platforms/{name}.png')


def monsters():
    idle, windup, healed = panels(V + 'D/D1/ver12_D1_HoneySlime_1.png')
    folder = 'assets/monsters/chapter_01/honey_slime/'
    save(idle, folder + 'idle.png')
    save(windup, folder + 'windup.png')
    save(healed, folder + 'healed.png')
    idle, _, healed = panels(V + 'D/D2/ver12_D2_SproutMushroom_1.png')
    folder = 'assets/monsters/chapter_01/sprout_mushroom/'
    save(idle, folder + 'idle.png')
    save(healed, folder + 'healed.png')
    idle, windup, healed = panels(V + 'D/D1/ver12_D1_HoneySlime_2.png')
    folder = 'assets/monsters/common/test_flyer/'  # stand-in until a real flying monster is drawn
    save(idle, folder + 'idle.png')
    save(windup, folder + 'windup.png')
    save(healed, folder + 'healed.png')
    idle, hit, knocked = panels(V + 'B/ver12_B_14.png')
    folder = 'assets/monsters/common/training_dummy/'
    save(idle, folder + 'idle.png')
    save(hit, folder + 'hit.png')
    save(knocked, folder + 'knocked.png')


def effect(path, row, rows, name):
    a = rgb(path)
    cell_w, cell_h = a.shape[1] // 4, a.shape[0] // rows
    for frame in range(4):
        cell = a[row * cell_h:(row + 1) * cell_h, frame * cell_w:(frame + 1) * cell_w]
        save(black_to_alpha(cell), f'assets/vfx/common/{name}/frame_{frame}.png')


def effects():
    effect(V + 'C/ver12_C_Slash_1.png', 0, 3, 'slash_horizontal')
    effect(V + 'C/ver12_C_Slash_1.png', 1, 3, 'slash_diagonal')
    effect(V + 'C/ver12_C_Slash_2.png', 2, 3, 'slash_ring')
    effect(V + 'C/ver12_C_Hit_1.png', 0, 2, 'hit_light')
    effect(V + 'C/ver12_C_Hit_2.png', 1, 2, 'hit_heavy')


if __name__ == '__main__':
    background()
    platforms()
    monsters()
    effects()
    labyrinth()
    first_run_monsters()
    props()
