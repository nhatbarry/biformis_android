"""Reuse Adam's drawn recovery poses for the Level 3 floor wake-up.

Preview: python Tools/reuse_adam_floor_wakeup.py
Apply:   python Tools/reuse_adam_floor_wakeup.py --apply

MC_Fall_GetUp supplies complete lying, sitting and rising poses; the hospital
wake-up's existing light-coloured surprise/breath FX remain on the floor actor.
No pose is rotated or stretched. Preserve the floor's canvas, frame slots, tags
and durations, so its existing Unity sprite references and Ink timing still fit.
"""

import argparse
import struct
import zlib
from pathlib import Path

from PIL import Image, ImageDraw

from level4_cutscene_assets import Aseprite


ROOT = Path(__file__).resolve().parents[1]
FLOOR = ROOT / "Assets/Animations/Story/MC_WakeUp_Floor.aseprite"
RECOVERY = ROOT / "Assets/Animations/Story/MC_Fall_GetUp.aseprite"
OUT = ROOT / ".utmp/adam-wakeup"
# Sleep, then nine genuine elbow/sit/kneel/rise drawings, then breathing.
# The final two floor idle frames already match the gameplay character.
POSES = [0, 4, *range(6, 15), 15, 16, 15, 16]


def chunks(data):
    offset = 128
    for _ in range(struct.unpack_from("<H", data, 6)[0]):
        size, magic, old_count = struct.unpack_from("<IHH", data, offset)
        assert magic == 0xF1FA
        count = struct.unpack_from("<I", data, offset + 12)[0] or old_count
        cursor = offset + 16
        entries = []
        for _ in range(count):
            length, kind = struct.unpack_from("<IH", data, cursor)
            entries.append((kind, data[cursor:cursor + length]))
            cursor += length
        assert cursor == offset + size
        yield data[offset:offset + 16], entries
        offset += size
    assert offset == len(data)


def hair_anchor(image):
    # Both native assets use the same red hair palette.
    points = [(x, y) for y in range(image.height) for x in range(image.width)
              if image.getpixel((x, y)) == (241, 78, 82, 255)]
    assert points
    return ((min(x for x, _ in points) + max(x for x, _ in points)) // 2,
            min(y for _, y in points))


def encode_cel(original, image):
    body = bytearray(original[6:22])
    bounds = image.getbbox()
    assert bounds
    x, y, right, bottom = bounds
    pixels = image.crop(bounds)
    struct.pack_into("<hh", body, 2, x, y)
    struct.pack_into("<H", body, 7, 2)  # compressed raster
    body += struct.pack("<HH", right - x, bottom - y)
    body += zlib.compress(pixels.tobytes(), 9)
    return struct.pack("<IH", len(body) + 6, 0x2005) + body


def rebuild(original):
    floor, recovery = Aseprite(FLOOR), Aseprite(RECOVERY)
    assert floor.size == (48, 40) and len(floor.frames) == 17
    assert recovery.size == (32, 24)
    assert [layer["name"] for layer in floor.layers] == ["MC", "FX"]
    output = bytearray(original[:128])
    before, after = [], []
    for index, (header, entries) in enumerate(chunks(original)):
        old_mc = floor.render(index, "MC")
        mc, fx = old_mc, floor.render(index, "FX")
        if index < len(POSES):
            mc = Image.new("RGBA", floor.size)
            # Native source motion is kept intact. Its standing centre x=15
            # lands at x=24; alpha bottom y=24 lands on the floor at y=39.
            mc.alpha_composite(recovery.render(POSES[index], "MC"), (9, 15))
            assert mc.getbbox()[3] == 39
            if fx.getbbox():
                old_x, old_y = hair_anchor(old_mc)
                new_x, new_y = hair_anchor(mc)
                moved = Image.new("RGBA", floor.size)
                moved.alpha_composite(fx, (new_x - old_x, new_y - old_y))
                assert sum(moved.getchannel("A").tobytes()) == sum(fx.getchannel("A").tobytes()), "FX clipped"
                fx = moved
        encoded = []
        for kind, chunk in entries:
            if kind == 0x2005 and index < len(POSES):
                layer = struct.unpack_from("<H", chunk, 6)[0]
                encoded.append(encode_cel(chunk, mc if layer == 0 else fx))
            else:
                encoded.append(chunk)
        new_header = bytearray(header)
        struct.pack_into("<I", new_header, 0, 16 + sum(map(len, encoded)))
        output += new_header + b"".join(encoded)
        before.append(floor.render(index))
        composite = mc.copy()
        composite.alpha_composite(fx)
        after.append(composite)
    struct.pack_into("<I", output, 0, len(output))
    # Keep every non-raster chunk, layer order, tag and frame duration intact.
    for (old_header, old), (new_header, new) in zip(chunks(original), chunks(output)):
        assert old_header[4:] == new_header[4:]
        assert len(old) == len(new)
        for (kind, old_chunk), (new_kind, new_chunk) in zip(old, new):
            assert kind == new_kind
            if kind != 0x2005:
                assert old_chunk == new_chunk
    return bytes(output), before, after


def preview(frames):
    scale, columns = 6, 6
    cell_width, cell_height = 48 * scale, 40 * scale + 24
    sheet = Image.new("RGB", (cell_width * columns, cell_height * 3), "#131925")
    draw = ImageDraw.Draw(sheet)
    for i, frame in enumerate(frames):
        x, y = i % columns * cell_width, i // columns * cell_height
        draw.text((x + 6, y + 4), f"{i}: " + (f"recovery {POSES[i]}" if i < len(POSES) else "floor idle"), fill="white")
        enlarged = frame.resize((48 * scale, 40 * scale), Image.Resampling.NEAREST)
        sheet.paste(enlarged, (x, y + 24), enlarged)
    sheet.save(OUT / "Floor_Reused_Poses.png")
    durations = [f["duration"] for f in Aseprite(FLOOR).frames]
    cycle = list(range(11)) + list(range(11, 15)) * 3 + [15, 16]
    animated = []
    for i in cycle:
        background = Image.new("RGBA", (48, 40), "#131925")
        background.alpha_composite(frames[i])
        animated.append(background.resize((384, 320), Image.Resampling.NEAREST))
    animated[0].save(OUT / "Floor_Reused_Poses.webp", save_all=True,
                     append_images=animated[1:], duration=[durations[i] for i in cycle], loop=0, lossless=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    original = FLOOR.read_bytes()
    output, _, after = rebuild(original)
    preview(after)
    if args.apply:
        backup = OUT / (FLOOR.name + ".before")
        if not backup.exists():
            backup.write_bytes(original)
        FLOOR.write_bytes(output)
        exported = Aseprite(FLOOR)
        assert all(exported.render(i).tobytes() == frame.tobytes() for i, frame in enumerate(after))
    print(f"{'Applied' if args.apply else 'Previewed'} 15 reused floor poses; 17 frame slots and timing preserved. Preview: {OUT}")


if __name__ == "__main__":
    main()
