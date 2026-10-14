"""Retouch Bishop's exposed sleeves/hands in the existing pixel-art sources.

Preview by default; --apply writes the three authored assets after invariant checks.
Only limb pixels change. Aseprite metadata/chunks, sprite importer metadata, timelines,
canvas sizes, feet, masks, props and phase-one spell pixels stay authored.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import shutil
import struct
import zlib

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Logs/bishop-art-review"
BACKUP = ROOT / ".utmp/task-bishop-source-backup"
STORY = Path("Assets/Animations/Story/Villain_PlagueDoctor.aseprite")
BOSS = Path("Assets/Sprites/Enemies/Plague Doctor Boss")
CLOTH = (55, 55, 55, 255)
FOLD = (94, 94, 93, 255)
SKIN = (231, 220, 200, 255)
SHADE = (189, 178, 158, 255)
BLACK = (0, 0, 0, 255)
CLEAR = (0, 0, 0, 0)
WHITE = (255, 255, 255, 255)

# Hand right edge/top row and the bent elbow. Endpoints remain authored.
STORY_POSES = {
    8: (27, 28, 25, 26), 9: (26, 28, 24, 26),
    10: (27, 27, 25, 25), 11: (25, 27, 24, 25),
    12: (30, 24, 25, 25), 13: (31, 25, 26, 26),
    14: (30, 24, 25, 25), 15: (31, 25, 26, 26),
    18: (30, 22, 25, 25), 19: (30, 22, 25, 25),
    20: (30, 22, 25, 25), 21: (30, 23, 25, 26),
    22: (30, 24, 25, 26), 23: (30, 25, 25, 27),
    24: (30, 24, 25, 26), 25: (30, 25, 25, 27),
    26: (30, 24, 25, 26), 27: (30, 25, 25, 27),
    28: (31, 22, 26, 25), 29: (31, 23, 26, 26),
    30: (31, 22, 26, 25), 31: (31, 20, 26, 24),
    32: (31, 21, 26, 24), 33: (31, 20, 26, 24),
    34: (33, 17, 27, 23), 35: (33, 18, 27, 24),
}


def digest(data):
    return hashlib.sha256(data).hexdigest()


def pixels(image):
    return [image.getpixel((x, y)) for y in range(image.height) for x in range(image.width)]


def hand(draw, right, top):
    left = right - 2
    draw.rectangle((left, top, right, top + 1), fill=SKIN)
    draw.rectangle((left, top + 2, right, top + 2), fill=SHADE)
    draw.point((left - 1, top + 1), fill=SKIN)


def bent_sleeve(draw, shoulder, elbow, cuff):
    points = [shoulder, elbow, cuff]
    draw.line(points, fill=BLACK, width=6, joint="curve")
    draw.line(points, fill=CLOTH, width=4, joint="curve")
    draw.point((elbow[0], elbow[1] - 1), fill=FOLD)
    # A two-pixel dark cuff joins sleeve to the enlarged hand.
    draw.line((cuff[0], cuff[1] - 1, cuff[0], cuff[1] + 1), fill=FOLD, width=1)


def story_frame(before, index):
    if index not in STORY_POSES:
        return before.copy()
    right, top, ex, ey = STORY_POSES[index]
    layer = Image.new("RGBA", before.size)
    draw = ImageDraw.Draw(layer)
    shoulder = (23, 25)
    if index in (8, 9, 10, 11):
        shoulder = (23, 25)
    cuff = (right - 3, top + 1)
    if index in (34, 35):
        cuff = (30, top + 3)
    bent_sleeve(draw, shoulder, (ex, ey), cuff)
    hand(draw, right, top)
    after = before.copy()
    box = before.getbbox()
    original_tip = (right - 1 if index in (9, 11) else right, top)
    for y in range(box[1], box[3]):
        for x in range(box[0], box[2]):
            color = layer.getpixel((x, y))
            if not color[3]:
                continue
            # Mask/head, coat closures, feet and left/hidden limb stay exact.
            if x <= 28 and y <= 21:
                continue
            if x < (22 if index in (8, 9, 10, 11) else 23) or y >= 31:
                continue
            if before.getpixel((x, y)) in (SKIN, SHADE) and x <= 23 and y >= 30:
                continue
            # A clasp at x21 is separated from these inward walk poses by x22/23.
            if index == 11 and x <= 22 and y >= 27:
                continue
            if index == 9 and x <= 23 and y >= 28:
                continue
            # The small lever handle is part of this approved source pose.
            if x >= 29 and before.getpixel((x, y)) == FOLD:
                continue
            after.putpixel((x, y), color)
    assert after.getbbox() == box, ("story bounds", index, box, after.getbbox())
    # Original fingertip is still in the same place and colour.
    assert after.getpixel(original_tip) == before.getpixel(original_tip) == SKIN
    for y in range(before.height):
        for x in range(before.width):
            old = before.getpixel((x, y))
            if (x <= 28 and y <= 21) or y >= 31 or (x >= 29 and old == FOLD):
                assert after.getpixel((x, y)) == old
            if x <= 22 and y >= 26 and old in (SKIN, SHADE):
                assert after.getpixel((x, y)) == old
    return after


def boss_action(before, index, kind):
    poses = {4: (32, 35, 29, 36), 5: (40, 38, 32, 39),
             6: (40, 41, 32, 39), 7: (35, 41 if kind == "Cast" else 40, 30, 39),
             8: (30, 39 if kind == "Cast" else 38, 27, 37)}
    if index not in poses:
        return before.copy()
    right, top, ex, ey = poses[index]
    layer = Image.new("RGBA", before.size)
    draw = ImageDraw.Draw(layer)
    bent_sleeve(draw, (29, 35), (ex, ey), (right - 3, top + 1))
    hand(draw, right, top)
    after = before.copy()
    box = before.getbbox()
    for y in range(box[1], box[3]):
        for x in range(box[0], box[2]):
            color = layer.getpixel((x, y))
            if not color[3] or x < (27 if index == 8 else 29) or y <= 31 or y >= 44:
                continue
            if before.getpixel((x, y)) == WHITE:
                continue
            after.putpixel((x, y), color)
    assert after.getbbox() == box, ("boss bounds", kind, index, box, after.getbbox())
    assert after.getpixel((right, top)) == before.getpixel((right, top)) == SKIN
    assert set(pixels(after)) <= set(pixels(before))
    for y in range(before.height):
        for x in range(before.width):
            old = before.getpixel((x, y))
            if y <= 31 or y >= 44 or old == WHITE:
                assert after.getpixel((x, y)) == old
    return after


def aseprite_frames(data):
    _, magic, count, width, height, depth = struct.unpack_from("<IHHHHH", data)
    assert magic == 0xA5E0 and depth == 32
    frames = []
    offset = 128
    for index in range(count):
        size, magic, chunks, duration = struct.unpack_from("<IHHH", data, offset)
        assert magic == 0xF1FA
        head = data[offset:offset + 16]
        cursor = offset + 16
        entries = []
        for _ in range(chunks):
            length, kind = struct.unpack_from("<IH", data, cursor)
            chunk = data[cursor:cursor + length]
            entries.append((kind, chunk))
            cursor += length
        assert cursor == offset + size
        frames.append((head, entries, duration))
        offset += size
    assert offset == len(data)
    return width, height, frames


def retouch_aseprite(data):
    width, height, frames = aseprite_frames(data)
    assert (width, height, len(frames)) == (40, 40, 36)
    rebuilt = bytearray(data[:128])
    before_images, after_images, changed = [], [], []
    for index, (header, entries, _) in enumerate(frames):
        chunks = []
        for kind, chunk in entries:
            if kind != 0x2005:
                chunks.append(chunk)
                continue
            body = chunk[6:]
            layer, x, y, opacity, cel_type = struct.unpack_from("<HhhBH", body)
            cw, ch = struct.unpack_from("<HH", body, 16)
            assert (layer, x, y, opacity, cel_type, cw, ch) == (0, 0, 0, 255, 2, 40, 40)
            before = Image.frombytes("RGBA", (cw, ch), zlib.decompress(body[20:]))
            after = story_frame(before, index)
            before_images.append(before)
            after_images.append(after)
            changed_pixels = sum(a != b for a, b in zip(pixels(before), pixels(after)))
            if changed_pixels:
                changed.append({"frame": index, "pixels": changed_pixels, "bounds": before.getbbox()})
            assert set(pixels(after)) <= set(pixels(before))
            encoded = body[:20] + zlib.compress(after.tobytes(), 9)
            chunks.append(struct.pack("<IH", len(encoded) + 6, kind) + encoded)
        frame_header = bytearray(header)
        struct.pack_into("<I", frame_header, 0, 16 + sum(map(len, chunks)))
        rebuilt.extend(frame_header)
        rebuilt.extend(b"".join(chunks))
    struct.pack_into("<I", rebuilt, 0, len(rebuilt))
    output = bytes(rebuilt)
    nw, nh, new_frames = aseprite_frames(output)
    assert (nw, nh) == (width, height)
    for (old_header, old_chunks, old_duration), (new_header, new_chunks, new_duration) in zip(frames, new_frames):
        assert old_header[4:] == new_header[4:] and old_duration == new_duration
        for (old_kind, old), (new_kind, new) in zip(old_chunks, new_chunks):
            assert old_kind == new_kind
            if old_kind != 0x2005:
                assert old == new
            else:
                assert old[6:26] == new[6:26]
    return output, before_images, after_images, changed


def contact(before, after, name, indices=None, columns=6):
    if indices is None:
        indices = range(len(before))
    indices = list(indices)
    cell = 192
    row_height = cell * 2 + 40
    rows = (len(indices) + columns - 1) // columns
    sheet = Image.new("RGBA", (columns * cell, rows * row_height), (29, 32, 35, 255))
    draw = ImageDraw.Draw(sheet)
    for n, index in enumerate(indices):
        x, y = n % columns * cell, n // columns * row_height
        draw.text((x + 4, y + 4), f"{name} {index} BEFORE", fill="white")
        sheet.alpha_composite(before[index].resize((cell, cell), Image.Resampling.NEAREST), (x, y + 20))
        draw.text((x + 4, y + cell + 24), "AFTER", fill="white")
        sheet.alpha_composite(after[index].resize((cell, cell), Image.Resampling.NEAREST), (x, y + cell + 40))
    sheet.save(OUT / f"{name}_before_after.png")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    assets = {}
    report = []
    source = (BACKUP / STORY) if (BACKUP / STORY).exists() else ROOT / STORY
    original = source.read_bytes()
    output, before, after, changed = retouch_aseprite(original)
    assets[STORY] = output
    contact(before, after, "story_representatives", [12, 20, 31, 34], 4)
    contact(before, after, "story_exposed", list(STORY_POSES))
    contact(before, after, "story_all_poses")
    report.append({"path": str(STORY), "sha256Before": digest(original), "sha256After": digest(output), "frames": changed})
    for kind, count in (("Cast", 10), ("Walk Throw", 12)):
        relative = BOSS / (kind + ".png")
        source = (BACKUP / relative) if (BACKUP / relative).exists() else ROOT / relative
        sheet = Image.open(source).convert("RGBA")
        assert sheet.size == (count * 48, 48)
        before, after, changed = [], [], []
        result = sheet.copy()
        for index in range(count):
            frame = sheet.crop((index * 48, 0, (index + 1) * 48, 48))
            updated = boss_action(frame, index, kind)
            before.append(frame)
            after.append(updated)
            result.paste(updated, (index * 48, 0))
            changed_pixels = sum(a != b for a, b in zip(pixels(frame), pixels(updated)))
            if changed_pixels:
                changed.append({"frame": index, "pixels": changed_pixels, "bounds": frame.getbbox()})
        import io
        stream = io.BytesIO()
        result.save(stream, format="PNG")
        output = stream.getvalue()
        assets[relative] = output
        contact(before, after, "boss_" + kind.replace(" ", "_"), [4, 5, 6, 7, 8], 5)
        contact(before, after, "boss_" + kind.replace(" ", "_") + "_all_poses")
        report.append({"path": str(relative), "sha256Before": digest(source.read_bytes()), "sha256After": digest(output), "frames": changed})
    (OUT / "limb_retouch_manifest.json").write_text(json.dumps({"applied": args.apply, "assets": report, "invariants": ["canvas/cel bounds and positions", "frame order and durations", "non-cel Aseprite chunks byte-identical", "existing palette", "mask, hood, feet, interaction fingertips", "lever handle and white spell pixels"]}, indent=2))
    if args.apply:
        for path, data in assets.items():
            target = ROOT / path
            backup = BACKUP / path
            backup.parent.mkdir(parents=True, exist_ok=True)
            if not backup.exists():
                shutil.copy2(target, backup)
            meta = target.with_suffix(target.suffix + ".meta")
            backup_meta = backup.with_suffix(backup.suffix + ".meta")
            if meta.exists() and not backup_meta.exists():
                shutil.copy2(meta, backup_meta)
        for path, data in assets.items():
            target = ROOT / path
            target.write_bytes(data)
    print(json.dumps({"applied": args.apply, "changedFrames": {entry["path"]: len(entry["frames"]) for entry in report}, "changedPixels": {entry["path"]: sum(frame["pixels"] for frame in entry["frames"]) for entry in report}}, indent=2))


if __name__ == "__main__":
    main()
