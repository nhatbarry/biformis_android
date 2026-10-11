"""Builds the ending's pictures (run from the project root with `python -I Tools/ending_art.py <step>`).

  glow   Assets/Sprites/Story/Ending/Ending Glow.png - the dark stage after the glass breaks: black with a soft,
         dithered light around the floor where the boss and the brothers stand (160x90, shown x4).
  world  Assets/Sprites/Story/Ending/Ending Run World.png - what the brothers run through, left to right:
         black | the cage room with its open door of light | Level 2 | 3 | 4 | 5 | the dark (transparent).
         Reads Logs/Environment Snapshots/Ending Run - Level Story N.png (the [Explicit] CutsceneFrameCapture
         test) and Assets/Sprites/Story/Ending/Cage Room.png.
"""
import os
import random
import sys

from PIL import Image

OUT = "Assets/Sprites/Story/Ending"
BAYER = [[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]

# geometry shared with the ink knot "Ending" (canvas units = 4 per art pixel)
BLACK = 240          # black left of the room: the rest of the world has already gone
ROOM = 160
LEVEL = 224
LEVELS = ["Level Story 2", "Level Story 3", "Level Story 4", "Level Story 5"]
VOID = 200
SEAM = 24
DOOR_X = 3           # door frame's left column inside the room
FLOOR_Y = 58


def hex_rgb(h):
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def glow():
    bands = [hex_rgb(h) for h in ("000000", "0a0c12", "141824", "1f2536", "2b3349", "37415c")]
    w, h = 160, 90
    im = Image.new("RGBA", (w, h))
    px = im.load()
    cx, cy, rx, ry = 80, 52, 74, 40
    for y in range(h):
        for x in range(w):
            d = ((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2
            level = max(0.0, 1.0 - d ** 0.5) * (len(bands) - 1)
            base = int(level)
            frac = level - base
            # 2x2 art blocks so the dither reads as the game's chunky pixels
            step = BAYER[(y // 2) % 4][(x // 2) % 4] / 16.0
            idx = min(len(bands) - 1, base + (1 if frac > step else 0))
            px[x, y] = bands[idx] + (255,)
    im.save(os.path.join(OUT, "Ending Glow.png"))


def blend(c, target, t):
    return tuple(int(round(a + (b - a) * t)) for a, b in zip(c[:3], target)) + (255,)


def door(room):
    """An open door at the room's left end, strong light pouring out onto the wall and the floor."""
    px = room.load()
    w, h = room.size
    frame, edge = hex_rgb("1c2638"), hex_rgb("606e7a")
    core, warm = hex_rgb("fffbf0"), hex_rgb("ffe7b0")
    top, left, right = 27, DOOR_X, DOOR_X + 18          # frame, outer
    checker = lambda x, y: (x // 2 + y // 2) % 2 == 0     # 2x2 art-pixel checker for the outer, fainter light
    # halo on the wall around the frame: a solid band, then a checkered one
    for y in range(top - 7, FLOOR_Y):
        for x in range(0, right + 10):
            dx = max(0, left - x, x - right)
            dy = max(0, top - y)
            d = max(dx, dy)
            if d == 0 or d > 6:
                continue
            if d <= 2:
                px[x, y] = blend(px[x, y], warm, 0.38)
            elif checker(x, y):
                px[x, y] = blend(px[x, y], warm, 0.2)
    # light on the floor: three nested straight-edged fans, brightest by the door
    for y in range(FLOOR_Y, h):
        depth = y - FLOOR_Y
        for x in range(left + 2, w):
            if depth < 14 and x <= right - 3 + depth * 0.45:
                px[x, y] = blend(px[x, y], warm, 0.6)
            elif depth < 24 and x <= right - 2 + depth * 0.75:
                px[x, y] = blend(px[x, y], warm, 0.36)
            elif x <= right - 1 + depth * 1.05 and checker(x, y):
                px[x, y] = blend(px[x, y], warm, 0.2)
    # frame and opening
    for y in range(top, FLOOR_Y):
        for x in range(left, right + 1):
            if x in (left, right) or y == top:
                px[x, y] = frame + (255,)
            elif x in (left + 1, right - 1) or y == top + 1:
                px[x, y] = edge + (255,)
            else:
                inner = min(x - left - 2, right - 2 - x, y - top - 2)
                px[x, y] = (core if inner >= 2 else warm) + (255,)
    # the door leaf, swung open into the room: its lit edge faces the light
    leaf, leaf_lit, leaf_edge = hex_rgb("2a0e15"), hex_rgb("8a4a4a"), hex_rgb("1c080d")
    for i, x in enumerate(range(right + 1, right + 7)):
        y0 = top - i // 2
        y1 = FLOOR_Y + (i + 1) // 3
        for y in range(y0, y1):
            colour = leaf_lit if i == 0 else leaf_edge if (i == 5 or y in (y0, y1 - 1)) else leaf
            px[x, y] = colour + (255,)
    px[right + 4, (top + FLOOR_Y) // 2 + 1] = edge + (255,)          # handle
    return room


def world():
    snaps = "Logs/Environment Snapshots"
    room = door(Image.open(os.path.join(OUT, "Cage Room.png")).convert("RGBA"))
    levels = [Image.open(os.path.join(snaps, f"Ending Run - {name}.png")).convert("RGBA") for name in LEVELS]
    width = BLACK + ROOM + LEVEL * len(levels) + VOID
    strip = Image.new("RGBA", (width, 90), (0, 0, 0, 0))
    # black left of the room, opaque so nothing behind the stage shows through
    strip.paste(Image.new("RGBA", (BLACK, 90), (0, 0, 0, 255)), (0, 0))
    pictures = [room] + levels
    starts = [BLACK]
    for i in range(len(levels)):
        starts.append(BLACK + ROOM + LEVEL * i)
    for picture, x in zip(pictures, starts):
        strip.paste(picture, (x, 0))
    # block seams: each boundary between two pictures (and from Level 5 into the dark) frays over SEAM pixels
    rng = random.Random(11)
    src = strip.copy()
    out = strip.load()
    boundaries = starts[1:] + [BLACK + ROOM + LEVEL * len(levels)]
    for b in boundaries:
        right_is_void = b == boundaries[-1]
        for by in range(0, 90, 2):
            for bx in range(b - SEAM, b + SEAM, 2):
                t = (bx + 1 - (b - SEAM)) / (2 * SEAM)            # 0 = left picture, 1 = right picture
                take_right = rng.random() < t
                for y in range(by, by + 2):
                    for x in range(bx, bx + 2):
                        if right_is_void:
                            out[x, y] = (0, 0, 0, 0) if take_right else src.getpixel((x, y))
                        else:
                            # sample across the boundary: the right picture continues leftwards, the left one rightwards
                            if take_right and x < b:
                                out[x, y] = src.getpixel((min(b + (b - 1 - x), width - 1), y))
                            elif not take_right and x >= b:
                                out[x, y] = src.getpixel((max(b - 1 - (x - b), 0), y))
    strip.save(os.path.join(OUT, "Ending Run World.png"))
    print("world", strip.size, "room centre px", BLACK + ROOM // 2,
          "void centre px", BLACK + ROOM + LEVEL * len(levels) + VOID // 2)


if __name__ == "__main__":
    {"glow": glow, "world": world}[sys.argv[1]]()
