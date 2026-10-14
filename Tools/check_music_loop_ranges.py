"""Check the shared music ranges against the original PCM WAV recordings.

Run from the project root: python Tools/check_music_loop_ranges.py
Requires NumPy, like the project's artwork/audio tools.
"""

from pathlib import Path
import re
import wave

import numpy as np


ROOT = Path(__file__).resolve().parents[1]
MUSIC = ROOT / "Assets/Audio/Musics"
CATALOG = ROOT / "Assets/Resources/Music Loop Ranges.asset"


def main():
    recordings = {}
    for path in MUSIC.glob("*.wav"):
        guid = re.search(r"^guid: (\w+)", path.with_suffix(".wav.meta").read_text(), re.M).group(1)
        recordings[guid] = path

    entries = re.findall(
        r"  - clip: \{fileID: 8300000, guid: (\w+), type: 3\}\s+"
        r"startTime: ([\d.]+)\s+endTime: ([\d.]+)", CATALOG.read_text()
    )
    assert entries, "The resource has no measured loop ranges."
    assert len(entries) == len({guid for guid, _, _ in entries}), "Duplicate recording in the catalog."
    for guid, start_text, end_text in entries:
        path = recordings[guid]
        start, end = float(start_text), float(end_text)
        with wave.open(str(path), "rb") as recording:
            rate, channels = recording.getframerate(), recording.getnchannels()
            duration = recording.getnframes() / rate
            assert recording.getsampwidth() == 2, f"{path.name}: expected 16-bit PCM"
            samples = np.frombuffer(recording.readframes(recording.getnframes()), np.int16)
        samples = samples.reshape(-1, channels).astype(np.float32) / 32768
        assert 0 <= start < end <= duration + 0.0001, f"{path.name}: invalid playback bounds"
        first = samples[round(start * rate):round((start + .1) * rate)]
        last = samples[round((end - .1) * rate):round(end * rate)]
        first_rms, last_rms = np.sqrt(np.mean(first**2)), np.sqrt(np.mean(last**2))
        assert first_rms > .0005 and last_rms > .0005, f"{path.name}: silent loop seam"
        print(f"{path.name}: {start:.3f}-{end:.3f}s; "
              f"skips {start:.3f}s intro / {max(0, duration - end):.3f}s outro")
    print(f"Validated {len(entries)} music ranges; source recordings remain unchanged.")


if __name__ == "__main__":
    main()
