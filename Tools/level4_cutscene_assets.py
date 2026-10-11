"""Re-export the supplied Level 4 cutscene media, without changing source files.

Run from any directory with ``python Tools/level4_cutscene_assets.py``. Requires
Pillow. ``--references-only`` extracts existing Aseprite art for inspection.

The requested animated WebP is decoded directly at its native 64 x 64 size.
The rescue preview retains its whole composition and is scaled from 1672 x 941
to the cutscene's 640 x 360 canvas using Lanczos. No source frames are cropped.
Atlases use top-left coordinates in JSON; Unity converts y to bottom-left.
"""

from __future__ import annotations

import argparse
from collections import deque
import json
import re
import struct
import uuid
import zlib
from pathlib import Path

from PIL import Image


PROJECT = Path(__file__).resolve().parents[1]
OUT = PROJECT / "Assets/Resources/Story Cutscene/Level4"
REFERENCES = PROJECT / ".utmp/level4"
RESOURCE_PREFIX = "Story Cutscene/Level4/"


def u16(data, offset):
    return struct.unpack_from("<H", data, offset)[0]


class Aseprite:
    """Read 32-bit RGBA raster and linked cels from the existing project art."""

    def __init__(self, path):
        data = Path(path).read_bytes()
        self.size = struct.unpack_from("<HH", data, 8)
        if u16(data, 4) != 0xA5E0 or u16(data, 12) != 32:
            raise ValueError(f"Expected a 32-bit RGBA Aseprite file: {path}")
        self.layers = []
        self.frames = []
        self.tags = {}
        offset = 128
        for _ in range(u16(data, 6)):
            frame_size, magic, old_chunks, duration = struct.unpack_from("<IHHH", data, offset)
            if magic != 0xF1FA:
                raise ValueError("Invalid Aseprite frame header")
            new_chunks = struct.unpack_from("<I", data, offset + 12)[0]
            count = new_chunks or old_chunks
            position = offset + 16
            cels = {}
            for _ in range(count):
                chunk_size, kind = struct.unpack_from("<IH", data, position)
                chunk = data[position + 6:position + chunk_size]
                if kind == 0x2004:
                    flags, layer_type, child, _, _, blend, opacity = struct.unpack_from("<6HB", chunk)
                    name = chunk[18:18 + u16(chunk, 16)].decode("utf-8")
                    self.layers.append({"name": name, "visible": bool(flags & 1),
                                        "type": layer_type, "opacity": opacity, "blend": blend})
                elif kind == 0x2005:
                    layer, x, y, opacity, cel_type = struct.unpack_from("<HhhBH", chunk)
                    if cel_type == 1:
                        cels[layer] = {"linked": u16(chunk, 16), "x": x, "y": y, "opacity": opacity}
                    elif cel_type in (0, 2):
                        size = struct.unpack_from("<HH", chunk, 16)
                        pixels = chunk[20:] if cel_type == 0 else zlib.decompress(chunk[20:])
                        cels[layer] = {"image": Image.frombytes("RGBA", size, pixels),
                                       "x": x, "y": y, "opacity": opacity}
                elif kind == 0x2018:
                    tag_pos = 10
                    for _ in range(u16(chunk, 0)):
                        first, last, direction = struct.unpack_from("<HHB", chunk, tag_pos)
                        length = u16(chunk, tag_pos + 17)
                        name = chunk[tag_pos + 19:tag_pos + 19 + length].decode("utf-8")
                        self.tags[name] = {"first": first, "last": last, "direction": direction}
                        tag_pos += 19 + length
                position += chunk_size
            self.frames.append({"duration": duration, "cels": cels})
            offset += frame_size

    def cel(self, frame, layer):
        cel = self.frames[frame]["cels"].get(layer)
        if cel is None:
            return None
        if "linked" in cel:
            source = self.cel(cel["linked"], layer)
            return {**cel, "image": source["image"]} if source else None
        return cel

    def render(self, frame, layer_name=None):
        canvas = Image.new("RGBA", self.size)
        for index, layer in enumerate(self.layers):
            if layer["type"] != 0 or not layer["visible"]:
                continue
            if layer_name is not None and layer["name"] != layer_name:
                continue
            if layer["blend"] != 0:
                raise ValueError(f"Unsupported blend mode in {layer['name']}")
            cel = self.cel(frame, index)
            if cel:
                image = cel["image"].copy()
                opacity = cel["opacity"] * layer["opacity"] / 255.0
                if opacity != 255:
                    image.putalpha(image.getchannel("A").point(lambda a: round(a * opacity / 255)))
                canvas.alpha_composite(image, (cel["x"], cel["y"]))
        return canvas


def extract_references():
    REFERENCES.mkdir(parents=True, exist_ok=True)
    room = Aseprite(PROJECT / "Assets/Animations/Story/Dungeon_Dark_Room.aseprite")
    room.render(0, "Room").save(REFERENCES / "Dungeon_Room.png")
    door = room.render(0, "Door")
    door.save(REFERENCES / "Dungeon_Door.png")
    bounds = door.getbbox()
    cropped_door = door.crop(bounds)
    cropped_door.resize((cropped_door.width * 8, cropped_door.height * 8),
                        Image.Resampling.NEAREST).save(REFERENCES / "Dungeon_Door_8x.png")
    room.render(0).resize((room.size[0] * 4, room.size[1] * 4), Image.Resampling.NEAREST).save(
        REFERENCES / "Dungeon_Composite_4x.png")

    actor = Aseprite(PROJECT / "Assets/Animations/A_Bloody.aseprite")
    first = actor.tags["S-Idle"]["first"]
    idle = actor.render(first)
    idle.save(REFERENCES / "A_Bloody_SIdle_Canvas.png")
    idle = idle.crop(idle.getbbox())
    idle.save(REFERENCES / "A_Bloody_SIdle.png")
    idle.resize((idle.width * 8, idle.height * 8), Image.Resampling.NEAREST).save(
        REFERENCES / "A_Bloody_SIdle_8x.png")

    run_tag = actor.tags["S-Run"]
    run = [actor.render(i) for i in range(run_tag["first"], run_tag["last"] + 1)]
    bbox = Image.new("RGBA", actor.size)
    for frame in run:
        bbox.alpha_composite(frame)
    bounds = bbox.getbbox()
    cropped = [frame.crop(bounds) for frame in run]
    montage = Image.new("RGBA", (cropped[0].width * len(cropped), cropped[0].height))
    for i, frame in enumerate(cropped):
        montage.paste(frame, (i * frame.width, 0))
        frame.save(REFERENCES / f"A_Bloody_SRun_{i:02}.png")
    montage.save(REFERENCES / "A_Bloody_SRun.png")
    montage.resize((montage.width * 8, montage.height * 8), Image.Resampling.NEAREST).save(
        REFERENCES / "A_Bloody_SRun_8x.png")
    print(f"Extracted original Aseprite references: {REFERENCES}")


def asset_guid(path):
    meta = Path(str(path) + ".meta")
    if meta.exists():
        match = re.search(r"^guid: ([a-f0-9]{32})$", meta.read_text(), re.MULTILINE)
        if match:
            return match.group(1)
    return uuid.uuid5(uuid.NAMESPACE_URL, "biformis:" + path.relative_to(PROJECT).as_posix()).hex


def write_simple_meta(path, importer="TextScriptImporter", folder=False):
    prefix = f"fileFormatVersion: 2\nguid: {asset_guid(path)}\n"
    if folder:
        prefix += "folderAsset: yes\n"
    Path(str(path) + ".meta").write_text(prefix + f"{importer}:\n  externalObjects: {{}}\n"
                                       "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def write_png_meta(path):
    # These atlases are sampled by UI sprites, so no CPU-side pixel copy is needed.
    template = (PROJECT / "Assets/Resources/Story Ending/A_Bloody.png.meta").read_text()
    template = re.sub(r"^guid: [a-f0-9]{32}", f"guid: {asset_guid(path)}", template, flags=re.MULTILINE)
    template = re.sub(r"maxTextureSize: \d+", "maxTextureSize: 8192", template)
    template = template.replace("  isReadable: 1", "  isReadable: 0")
    template = template.replace("    textureFormat: -1", "    textureFormat: 4")  # RGBA32
    # Android uses the same uncompressed RGBA/Point/Clamp settings explicitly.
    android_start = template.index("    buildTarget: Android")
    android_end = template.index("  spriteSheet:", android_start)
    android = template[android_start:android_end].replace("overridden: 0", "overridden: 1")
    template = template[:android_start] + android + template[android_end:]
    Path(str(path) + ".meta").write_text(template)


def export_animation(source, stem, clip_name, size, columns, rows):
    source_image = Image.open(source)
    atlas = Image.new("RGBA", (size[0] * columns, size[1] * rows))
    frames = []
    if source_image.n_frames != columns * rows:
        raise ValueError("The source frame count does not fit the expected atlas layout")
    for index in range(source_image.n_frames):
        source_image.seek(index)
        source_image.load()  # WebP frame duration is set by Pillow after decoding.
        duration = source_image.info.get("duration", 100) / 1000.0
        frame = source_image.convert("RGBA")
        if frame.size != size:
            frame = frame.resize(size, Image.Resampling.LANCZOS)
        x, y = index % columns * size[0], index // columns * size[1]
        atlas.paste(frame, (x, y))
        data = {"texture": RESOURCE_PREFIX + stem + "Atlas", "x": x, "y": y,
                "width": size[0], "height": size[1], "duration": duration}
        if stem == "Box":
            data.update(box_geometry(frame))
        frames.append(data)
    atlas_path = OUT / f"{stem}Atlas.png"
    atlas.save(atlas_path)
    write_png_meta(atlas_path)
    manifest = {
        "source": source.relative_to(PROJECT).as_posix(),
        "sourceWidth": source_image.width, "sourceHeight": source_image.height,
        "frameWidth": size[0], "frameHeight": size[1],
        "resample": "none" if source_image.size == size else "Lanczos (whole frame, no crop)",
        "coordinateOrigin": "top-left", "totalDuration": sum(f["duration"] for f in frames),
        "clips": [{"name": clip_name, "loop": True, "frames": frames}],
    }
    manifest_path = OUT / f"{stem}.json"
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n")
    write_simple_meta(manifest_path)
    print(f"Exported {source.name}: {len(frames)} frames -> {atlas_path.name} {atlas.size}")


def box_geometry(frame):
    """Native UI geometry: black faces underneath the exact current white edges.

    The source's live cube edges are alpha 255; its motion trails use 22/44/76.
    Keep the atlas unchanged. A convex hull fills the faces and opaque pixel runs
    draw their exact original white border back over the fill in Unity.
    """
    pixels = frame.load()
    points = sorted((x + 0.5, y + 0.5) for y in range(frame.height)
                    for x in range(frame.width) if pixels[x, y][3] == 255)

    def cross(origin, a, b):
        return (a[0] - origin[0]) * (b[1] - origin[1]) - (a[1] - origin[1]) * (b[0] - origin[0])

    def half_hull(ordered):
        result = []
        for point in ordered:
            while len(result) >= 2 and cross(result[-2], result[-1], point) <= 0:
                result.pop()
            result.append(point)
        return result

    hull = half_hull(points)[:-1] + half_hull(reversed(points))[:-1]
    runs = []
    for y in range(frame.height):
        x = 0
        while x < frame.width:
            if pixels[x, y][3] != 255:
                x += 1
                continue
            start = x
            while x < frame.width and pixels[x, y][3] == 255:
                x += 1
            runs.append({"x": start, "y": y, "length": x - start})
    return {"hull": [{"x": x, "y": y} for x, y in hull], "edgeRuns": runs}


def export_dungeon():
    source = Aseprite(PROJECT / "Assets/Animations/Story/Dungeon_Dark_Room.aseprite")
    room = source.render(0, "Room")
    door = source.render(0, "Door")
    bounds = door.getbbox()
    room_path = OUT / "DungeonRoom.png"
    door_path = OUT / "DungeonDoor.png"
    room.save(room_path)
    door.crop(bounds).save(door_path)
    write_png_meta(room_path)
    write_png_meta(door_path)
    # The source has three neutral stone colors and five leaf/bracing colors.
    # Preserve the exact leaf polygon, including its diagonal top/bottom edges.
    stone = {(56, 56, 59, 255), (66, 66, 68, 255), (72, 72, 74, 255)}
    leaf = Image.new("RGBA", source.size)
    frame = door.copy()
    leaf_pixels = leaf.load()
    frame_pixels = frame.load()
    for y in range(source.size[1]):
        for x in range(source.size[0]):
            pixel = door.getpixel((x, y))
            if pixel[3] and pixel not in stone:
                leaf_pixels[x, y] = pixel
                frame_pixels[x, y] = (10, 10, 14, 255)
    leaf_bounds = leaf.getbbox()
    leaf_path = OUT / "DungeonDoorLeaf.png"
    frame_path = OUT / "DungeonDoorFrame.png"
    leaf.crop(leaf_bounds).save(leaf_path)
    frame.save(frame_path)
    write_png_meta(leaf_path)
    write_png_meta(frame_path)
    leaf.crop(leaf_bounds).resize(((leaf_bounds[2] - leaf_bounds[0]) * 8,
                                 (leaf_bounds[3] - leaf_bounds[1]) * 8),
                                Image.Resampling.NEAREST).save(REFERENCES / "Dungeon_DoorLeaf_8x.png")
    inspection = room.copy()
    inspection.alpha_composite(frame)
    inspection.resize((source.size[0] * 4, source.size[1] * 4), Image.Resampling.NEAREST).save(
        REFERENCES / "Dungeon_OpenDoor_4x.png")
    geometry_path = OUT / "DoorGeometry.json"
    geometry_path.write_text(json.dumps({"x": leaf_bounds[0], "y": leaf_bounds[1],
                                        "width": leaf_bounds[2] - leaf_bounds[0],
                                        "height": leaf_bounds[3] - leaf_bounds[1],
                                        "canvasWidth": source.size[0],
                                        "canvasHeight": source.size[1]}, indent=2) + "\n")
    write_simple_meta(geometry_path)
    print(f"Exported original dungeon Room {room.size}; Door bounds {bounds}; leaf bounds {leaf_bounds}")


def export_recovery(source):
    """Slice the generated poses and import them at the original A_bloody scale.

    This is sprite packing: it does not redraw or recolor the generated poses.
    A single scale from the final standing pose keeps relative proportions and a
    shared ground line. Native standing A is 17 pixels tall, its feet 8 pixels
    below the original canvas-centre pivot.
    """
    sheet = Image.open(source).convert("RGBA")
    poses = []
    for index in range(12):
        column, row = index % 4, index // 4
        cell = sheet.crop((round(column * sheet.width / 4), round(row * sheet.height / 3),
                           round((column + 1) * sheet.width / 4), round((row + 1) * sheet.height / 3)))
        #Very faint generation fringes do not define the pose's registration box.
        bounds = main_pose_bounds(cell)
        if bounds is None:
            raise ValueError(f"Recovery pose {index} is empty")
        poses.append(cell.crop(bounds))
    scale = 17 / poses[-1].height
    atlas = Image.new("RGBA", (128, 96))
    exported = []
    for index, pose in enumerate(poses):
        size = (max(1, round(pose.width * scale)), max(1, round(pose.height * scale)))
        if size[0] > 32 or size[1] > 25:
            raise ValueError(f"Pose {index} does not fit the native animation canvas")
        pose = pose.resize(size, Image.Resampling.NEAREST)
        cell = Image.new("RGBA", (32, 32))
        cell.paste(pose, ((32 - size[0]) // 2, 24 - size[1]))
        x, y = index % 4 * 32, index // 4 * 32
        atlas.paste(cell, (x, y))
        exported.append({"texture": RESOURCE_PREFIX + "ARecoveryAtlas", "x": x, "y": y,
                         "width": 32, "height": 32})
    atlas_path = OUT / "ARecoveryAtlas.png"
    atlas.save(atlas_path)
    write_png_meta(atlas_path)
    clips = []
    for name, indices, durations, loop, next_clip in [
        ("fall", range(4), [0.16, 0.16, 0.18, 0.18], False, "prone"),
        ("prone", [4], [0.1], True, ""),
        ("getup", range(5, 12), [0.28] * 7, False, "idle"),
    ]:
        clips.append({"name": name, "loop": loop, "next": next_clip,
                      "frames": [{**exported[i], "duration": duration}
                                 for i, duration in zip(indices, durations)]})
    manifest_path = OUT / "ARecovery.json"
    manifest_path.write_text(json.dumps({"source": source.relative_to(PROJECT).as_posix(),
                                        "frameWidth": 32, "frameHeight": 32,
                                        "coordinateOrigin": "top-left", "groundY": 24,
                                        "standingHeight": 17, "clips": clips}, indent=2) + "\n")
    write_simple_meta(manifest_path)
    atlas.resize((1024, 768), Image.Resampling.NEAREST).save(REFERENCES / "ARecovery_Packed_8x.png")
    print(f"Packed generated recovery poses at original A_bloody scale: {atlas_path}")


def main_pose_bounds(image):
    """Register the character, rather than isolated translucent generation specks."""
    mask = image.getchannel("A")
    pixels = mask.load()
    visited = set()
    largest = []
    for y in range(image.height):
        for x in range(image.width):
            if (x, y) in visited or pixels[x, y] < 128:
                continue
            queue = deque([(x, y)])
            visited.add((x, y))
            component = []
            while queue:
                px, py = queue.popleft()
                component.append((px, py))
                for nx, ny in ((px - 1, py), (px + 1, py), (px, py - 1), (px, py + 1)):
                    if (0 <= nx < image.width and 0 <= ny < image.height and
                            (nx, ny) not in visited and pixels[nx, ny] >= 128):
                        visited.add((nx, ny))
                        queue.append((nx, ny))
            if len(component) > len(largest):
                largest = component
    if not largest:
        return None
    return (min(x for x, y in largest), min(y for x, y in largest),
            max(x for x, y in largest) + 1, max(y for x, y in largest) + 1)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--references-only", action="store_true")
    parser.add_argument("--box-only", action="store_true", help="Export only the supplied box animation and black-face geometry")
    parser.add_argument("--recovery-only", type=Path, help="Pack an existing generated 4x3 recovery sheet only")
    args = parser.parse_args()
    if not args.box_only:
        extract_references()
    if args.references_only:
        return
    OUT.mkdir(parents=True, exist_ok=True)
    write_simple_meta(OUT.parent, "DefaultImporter", folder=True)
    write_simple_meta(OUT, "DefaultImporter", folder=True)
    if args.box_only:
        export_animation(PROJECT / "outputs/cube-pixel-left-up-64-transparent.webp",
                         "Box", "idle", (64, 64), 8, 8)
        return
    if args.recovery_only:
        export_recovery(args.recovery_only.resolve())
        return
    export_dungeon()
    export_animation(PROJECT / "outputs/cube-pixel-left-up-64-transparent.webp",
                     "Box", "idle", (64, 64), 8, 8)
    export_animation(PROJECT / "red-rescues-blue-preview.gif",
                     "Rescue", "intro", (640, 360), 4, 10)
    recovery = PROJECT / "outputs/level4-A-bloody-recovery-source.png"
    if recovery.exists():
        export_recovery(recovery)


if __name__ == "__main__":
    main()
