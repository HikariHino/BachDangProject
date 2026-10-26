# Recorded footsteps

Current gameplay uses six grass walking takes, six separate grass running takes,
and six wood takes for walking on ship decks, edited from YouTube recordings.
See [ActionAudioSources.md](ActionAudioSources.md) for sources, permission notes
and the reproducible 31-clip action audio manifest. Standing on a moving deck
does not trigger footsteps; movement is measured relative to that deck.

## Previous fallback recordings

Running uses six alternating left/right recordings of feet on sand/grass by
Fantozzi, sliced by qubodup. These replace the previous generated impacts.
These remain a fallback if the newer running recordings are unavailable.

Source: https://opengameart.org/content/fantozzis-footsteps-grasssand-stone

Archive: https://opengameart.org/sites/default/files/Fantozzi-footsteps.7z

License: CC0 1.0, https://creativecommons.org/publicdomain/zero/1.0/

Only the six `Fantozzi-Sand*.flac` recordings are used. The importer trims silence,
filters sub-bass/hiss, converts to mono PCM at 24 kHz and balances levels with a
peak ceiling. It does not synthesize impacts or mix in the former sound.
`Tools/author_running_foley.py` reproduces the conversion with ffmpeg and NumPy.
Originals are extracted under `Logs/RunningFoleySource` and are not game assets.
