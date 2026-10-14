# Story and level music loops

`Assets/Resources/Music Loop Ranges.asset` defines the musical body of recordings
with quiet intros or padded/faded outros. `MusicManager.Play(..., loop: true)`
uses it for every caller: level themes, boss phases, music tags and the ending.
The original WAVs and their GUIDs stay intact. Non-looping playback is unchanged.

The recordings contained roughly 4-5 seconds of silence at the ends of
`mo_dau`, `dua_hop`, `hanh_lang_dai` and `chay_ve`. `lv3-4` had a longer quiet
outro; `boss_phase_2` also faded into several seconds of silence. Ranges were
measured from the PCM waveforms, trimming only playback boundaries, preserving
internal musical pauses. Existing level/boss starts (42.7 / 69 seconds) and
shorter Ink sections (such as `ending:0:12.85`) still take precedence.

The congratulation recording plays once in full and is excluded from the loop
catalog. `EndingSequence` holds Red's final picture until the whole recording
has finished naturally, then dissolves to the hospital. It does not stop or
fade that recording early; the hospital and THE END have no celebration music.
Any fade already recorded in the original song remains intact.

Run `python Tools/check_music_loop_ranges.py` to validate recording references,
bounds and audible edges. Unity `MusicTransitionTests` cover real recording
wraps, retained authored sections and natural completion of the full celebration;
the existing tests cover crossfades, interrupted transitions and repeated ranges.

The range wrap still uses the established frame-updated AudioSource seek. This
fix removes multi-second blank sections; it does not add sample-accurate DSP
scheduling or copy long PCM buffers into memory on Android.
