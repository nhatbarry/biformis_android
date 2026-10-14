"""Generate Biformis's original, short retro speech blips (Python stdlib only).

Run from any directory: python Tools/generate_dialogue_sfx.py
No external recording, text-to-speech service or third-party audio is used.
"""

import math
from pathlib import Path
import random
import struct
import uuid
import wave


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "Assets/Audio/Story SFX/Dialogue"
RATE = 44100
NAMESPACE = uuid.UUID("1cb9e826-9e28-41c5-b8e7-3c0d865f31f2")

# Fundamental, length, second-harmonic weight, breath/rasp, pitch fall.
VOICES = {
    "A": (460, .037, .22, .008, .10),
    "B": (285, .041, .34, .010, .08),
    "Mom": (385, .040, .18, .015, .13),
    "Doctor": (245, .041, .20, .004, .05),
    "Villain": (140, .048, .44, .035, .17),
}


def guid(path):
    return uuid.uuid5(NAMESPACE, path.relative_to(ROOT).as_posix()).hex


def write_clip(name, variant, settings):
    frequency, duration, harmonic, breath, fall = settings
    rng = random.Random(f"{name}-{variant}")
    phase = 0.0
    pcm = bytearray()
    count = round(duration * RATE)
    for sample in range(count):
        t = sample / RATE
        progress = sample / (count - 1)
        # A few millisecond attack and long release leave both edges at zero.
        envelope = min(1, t / .003) * min(1, (count - 1 - sample) / (RATE * .014))
        envelope = max(0, envelope) * math.exp(-1.2 * progress)
        f = frequency * (1 + .035 * variant) * (1 - fall * progress)
        phase += 2 * math.pi * f / RATE
        voice = (math.sin(phase) + harmonic * math.sin(2 * phase + .2)
                 + .12 * math.sin(3 * phase + .4)) / (1 + harmonic + .12)
        # Quiet formants and breath make a syllable-like chirp rather than a pure alert tone.
        voice += .08 * math.sin(2 * math.pi * (1100 + variant * 90) * t) * math.sin(phase) ** 2
        voice += breath * rng.uniform(-1, 1)
        pcm.extend(struct.pack("<h", round(max(-1, min(1, .60 * voice * envelope)) * 32767)))
    path = OUTPUT / f"Voice {name} {variant + 1}.wav"
    with wave.open(str(path), "wb") as wav:
        wav.setnchannels(1)
        wav.setsampwidth(2)
        wav.setframerate(RATE)
        wav.writeframes(pcm)
    Path(str(path) + ".meta").write_text(f"""fileFormatVersion: 2
guid: {guid(path)}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 44100
    compressionFormat: 0
    quality: 1
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {{}}
  forceToMono: 1
  normalize: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 0
  userData: Original procedural speech blip; Tools/generate_dialogue_sfx.py
  assetBundleName:
  assetBundleVariant:
""", encoding="utf-8")
    return guid(path)


def update_profile(speaker, clip_guids, pitch=(.97, 1.03)):
    path = ROOT / f"Assets/Game Data/Story/Typewriter/{speaker}.asset"
    lines = path.read_text(encoding="utf-8").splitlines()
    start = next(i for i, line in enumerate(lines) if line.startswith("  typingSoundClips:"))
    end = next(i for i in range(start + 1, len(lines)) if lines[i].startswith("  minPitch:"))
    lines[start:end] = (["  typingSoundClips:"] +
                        [f"  - {{fileID: 8300000, guid: {g}, type: 3}}" for g in clip_guids]
                        if clip_guids else ["  typingSoundClips: []"])
    for i, line in enumerate(lines):
        if line.startswith("  minPitch:"):
            lines[i] = f"  minPitch: {pitch[0]}"
        elif line.startswith("  maxPitch:"):
            lines[i] = f"  maxPitch: {pitch[1]}"
        elif line.startswith("  stopAudioSource:"):
            lines[i] = "  stopAudioSource: 0"
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    Path(str(OUTPUT) + ".meta").write_text(f"""fileFormatVersion: 2
guid: {guid(OUTPUT)}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
""", encoding="utf-8")
    clips = {name: [write_clip(name, variant, settings) for variant in range(2)]
             for name, settings in VOICES.items()}
    for name, clip_guids in clips.items():
        update_profile(name, clip_guids)
    update_profile("Teen", clips["A"], (1.07, 1.13))
    # Narration and system text remain quiet; the user asked for speech between characters.
    update_profile("Narrator", [])
    update_profile("System", [])
    print(f"Generated {len(clips) * 2} original speech blips in {OUTPUT}")


if __name__ == "__main__":
    main()
