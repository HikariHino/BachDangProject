# Recorded running footsteps

Running uses six alternating left/right recordings of feet on sand/grass by
Fantozzi, sliced by qubodup. These replace the previous generated impacts.
Walking retains its separate existing clips.

Source: https://opengameart.org/content/fantozzis-footsteps-grasssand-stone

Archive: https://opengameart.org/sites/default/files/Fantozzi-footsteps.7z

License: CC0 1.0, https://creativecommons.org/publicdomain/zero/1.0/

Only the six `Fantozzi-Sand*.flac` recordings are used. The importer trims silence,
filters sub-bass/hiss, converts to mono PCM at 24 kHz and balances levels with a
peak ceiling. It does not synthesize impacts or mix in the former sound.
`Tools/author_running_foley.py` reproduces the conversion with ffmpeg and NumPy.
Originals are extracted under `Logs/RunningFoleySource` and are not game assets.
