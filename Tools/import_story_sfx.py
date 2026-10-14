"""Import a small, reproducible selection of Kenney CC0 story sound effects.

Usage: python Tools/import_story_sfx.py [--download]
The optional download flag caches the three small publisher archives in .utmp.
Only the explicitly listed OGG members and publisher licences are imported;
previews, shortcuts and unused sounds never enter Assets.
"""

from __future__ import annotations

import argparse
import hashlib
import struct
import urllib.request
import uuid
from pathlib import Path
from zipfile import ZipFile


ROOT = Path(__file__).resolve().parents[1]
CACHE = ROOT / ".utmp" / "story-sfx"
DESTINATION = ROOT / "Assets" / "Audio" / "Story SFX" / "Effects"
GUID_NAMESPACE = uuid.UUID("38c9d4fa-a649-46c8-994a-ddd42953997c")
PACKS = {
    "rpg-audio": (
        "https://kenney.nl/media/pages/assets/rpg-audio/8e99002d76-1677590336/kenney_rpg-audio.zip",
        "6dbeaf8544da958d8f2adcb4a4a4b76c1ade34a05f8ab9edccd327da7375f38b",
    ),
    "impact-sounds": (
        "https://kenney.nl/media/pages/assets/impact-sounds/87b4ddecda-1677589768/kenney_impact-sounds.zip",
        "029d734af1582474edf3a694d1b0cebc97c1c152f2f39fa34d4c2bafc5de77f8",
    ),
    "digital-audio": (
        "https://kenney.nl/media/pages/assets/digital-audio/216eac4753-1677590265/kenney_digital-audio.zip",
        "24e6ce28b76a6d8c89cff4d331e0965ff5c3de8a73c612028e9d363cc64e4f06",
    ),
}
# Asset filename, publisher pack, exact archive member, intended story use.
SELECTION = (
    ("Door Slam.ogg", "rpg-audio", "Audio/doorClose_1.ogg", "Door close / hard impact"),
    ("Door Bang.ogg", "impact-sounds", "Audio/impactWood_heavy_000.ogg", "Repeated door banging"),
    ("Door Knock.ogg", "impact-sounds", "Audio/impactWood_light_000.ogg", "Weak door knock"),
    ("Metal Latch.ogg", "rpg-audio", "Audio/metalLatch.ogg", "Lever / vent latch"),
    ("Trapdoor Open.ogg", "rpg-audio", "Audio/doorOpen_1.ogg", "Opening trapdoor"),
    ("Cage Rattle.ogg", "rpg-audio", "Audio/metalPot2.ogg", "Captives struggling against metal cage"),
    ("Cloth Rustle.ogg", "rpg-audio", "Audio/cloth1.ogg", "Waking / getting up / hand movement"),
    ("Soft Footstep.ogg", "impact-sounds", "Audio/footstep_carpet_000.ogg", "Quiet footsteps"),
    ("Hard Footstep.ogg", "impact-sounds", "Audio/footstep_concrete_001.ogg", "Running in dungeon / pipe"),
    ("Knife Slice.ogg", "rpg-audio", "Audio/knifeSlice2.ogg", "Villain stab"),
    ("Body Thud.ogg", "impact-sounds", "Audio/impactSoft_heavy_000.ogg", "Landing / collapse / head impact"),
    ("Box Handle.ogg", "rpg-audio", "Audio/handleSmallLeather2.ogg", "Taking / handing over memory box"),
    ("Wood Break.ogg", "impact-sounds", "Audio/impactPlank_medium_000.ogg", "Door splinters"),
    ("Metal Impact.ogg", "impact-sounds", "Audio/impactMetal_heavy_001.ogg", "Heavy metal contact"),
    ("Energy Appear.ogg", "digital-audio", "Audio/phaseJump1.ogg", "Villain materializes"),
    ("Energy Vanish.ogg", "digital-audio", "Audio/phaserDown1.ogg", "Teen / villain disappears"),
    ("Energy Merge.ogg", "digital-audio", "Audio/phaserUp3.ogg", "Captives merge / final split"),
    ("Boss Summon.ogg", "digital-audio", "Audio/powerUp3.ogg", "Boss summons arena"),
    ("Boss Volley.ogg", "digital-audio", "Audio/phaserDown2.ogg", "Brief boss projectile firing"),
)


def guid(path: Path) -> str:
    return uuid.uuid5(GUID_NAMESPACE, path.relative_to(ROOT).as_posix()).hex


def write_verified(path: Path, content: bytes) -> None:
    """Never silently replace a manually changed asset or imported licence."""
    if path.exists():
        if path.read_bytes() != content:
            raise RuntimeError(f"Existing file differs; refusing overwrite: {path}")
        return
    path.write_bytes(content)


def ensure_meta(path: Path, kind: str) -> None:
    meta = Path(str(path) + ".meta")
    if meta.exists():
        return  # Keep any GUID already referenced by scenes/data.
    content = f"fileFormatVersion: 2\nguid: {guid(path)}\n"
    if kind == "folder":
        content += "folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n"
    elif kind == "audio":
        content += (
            "AudioImporter:\n"
            "  externalObjects: {}\n"
            "  serializedVersion: 8\n"
            "  defaultSettings:\n"
            "    serializedVersion: 2\n"
            "    loadType: 0\n"
            "    sampleRateSetting: 0\n"
            "    sampleRateOverride: 44100\n"
            "    compressionFormat: 1\n"
            "    quality: 0.65\n"
            "    conversionMode: 0\n"
            "    preloadAudioData: 1\n"
            "  platformSettingOverrides: {}\n"
            "  forceToMono: 1\n"
            "  normalize: 1\n"
            "  loadInBackground: 0\n"
            "  ambisonic: 0\n"
            "  3D: 1\n"
        )
    else:
        content += "TextScriptImporter:\n  externalObjects: {}\n"
    content += "  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    meta.write_text(content, encoding="utf-8")


def ogg_duration(data: bytes) -> float:
    """Validate complete Ogg framing and Vorbis header; read duration from granules."""
    cursor = 0
    last_granule = 0
    rate = None
    while cursor < len(data):
        if len(data) - cursor < 27:
            raise ValueError("Truncated Ogg page")
        if data[cursor : cursor + 4] != b"OggS" or data[cursor + 4] != 0:
            raise ValueError("Invalid Ogg page header")
        count = data[cursor + 26]
        laces = data[cursor + 27 : cursor + 27 + count]
        body = cursor + 27 + count
        end = body + sum(laces)
        if len(laces) != count or end > len(data):
            raise ValueError("Truncated Ogg page body")
        payload = data[body:end]
        if rate is None and payload.startswith(b"\x01vorbis"):
            rate = struct.unpack_from("<I", payload, 12)[0]
        granule = struct.unpack_from("<Q", data, cursor + 6)[0]
        if granule != 0xFFFFFFFFFFFFFFFF:
            last_granule = max(last_granule, granule)
        cursor = end
    if not rate or not last_granule:
        raise ValueError("No Vorbis samples found")
    return last_granule / rate


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--download", action="store_true")
    args = parser.parse_args()
    CACHE.mkdir(parents=True, exist_ok=True)
    DESTINATION.mkdir(parents=True, exist_ok=True)
    for folder in (DESTINATION.parent, DESTINATION):
        ensure_meta(folder, "folder")
    total_bytes = 0
    for pack, (url, expected_hash) in PACKS.items():
        archive_path = CACHE / f"kenney_{pack}.zip"
        if not archive_path.exists():
            if not args.download:
                raise FileNotFoundError(f"Missing {archive_path}. Use --download.")
            with urllib.request.urlopen(url, timeout=30) as response:
                archive_data = response.read(5 * 1024 * 1024 + 1)
            if len(archive_data) > 5 * 1024 * 1024:
                raise ValueError("Publisher archive exceeds the expected small pack size")
            archive_path.write_bytes(archive_data)
        archive_data = archive_path.read_bytes()
        archive_hash = hashlib.sha256(archive_data).hexdigest()
        if expected_hash and archive_hash != expected_hash:
            raise ValueError(f"Publisher archive changed: {pack}: {archive_hash}")
        with ZipFile(archive_path) as archive:
            license_data = archive.read("License.txt").decode("utf-8").replace("\r\r\n", "\n").replace("\r\n", "\n")
            if "CC0" not in license_data or "commercial" not in license_data:
                raise ValueError(f"Expected CC0 commercial-use licence missing: {pack}")
            license_path = DESTINATION / f"LICENSE-Kenney-{pack}.txt"
            write_verified(license_path, license_data.encode("utf-8"))
            ensure_meta(license_path, "text")
            for filename, selected_pack, member, purpose in SELECTION:
                if selected_pack != pack:
                    continue
                data = archive.read(member)  # Also checks the ZIP member CRC.
                duration = ogg_duration(data)
                if duration > 5:
                    raise ValueError(f"Unexpectedly long effect: {member} ({duration:.2f}s)")
                path = DESTINATION / filename
                write_verified(path, data)
                ensure_meta(path, "audio")
                total_bytes += len(data)
                actual_guid = Path(str(path) + ".meta").read_text().split("guid: ")[1].splitlines()[0]
                print(f"{filename}: {duration:.3f}s, {len(data)} bytes, {actual_guid} ({purpose})")
        print(f"{pack} archive SHA-256: {archive_hash}")
    print(f"Imported {len(SELECTION)} clips, {total_bytes:,} bytes of audio.")


if __name__ == "__main__":
    main()
