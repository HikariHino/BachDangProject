# Rations video tutorial

> Historical design, retired on 2026-10-27. The four village trades now use
> [physical 3D interactions](PhysicalVillageTrades.md). The obsolete 2D video,
> player, generator and video-only test were removed during project cleanup.
> The implementation and validation below describe the earlier puzzle version.

The rations instruction paragraph is replaced by an automatic 15-second
demonstration inside the workbench. It shows cursor clicks, enough/missing/
excess colors, whole-group salt, an excess-salt undo and a successful package.
The demonstration has soft original click/result sounds. Replay, pause/resume,
optional hands-on practice and an immediately available Start button remain
visible. ENTER starts the timed round directly, and ESC closes the activity.

The example is separate from live ingredients and progress. Watching or pausing
it does not charge the campaign achievement timer. Start creates the usual
timed round with zero elapsed time; its colors, original recipe requirements
and hidden answers/counters remain unchanged. Existing saved levels are kept.
The optional practice action always opens an untimed sample, including after
loading a completed Easy level; it never accidentally starts timed Medium.

`Tools/generate_rations_tutorial.py` creates all media locally with Pillow,
NumPy and ffmpeg. It draws original goods/cursor UI consistent with the game,
creates the sound cues, and encodes `RationsHowTo.webm` at 960×420/24 fps.
No external video or network service is used. Its source video is packaged
alongside two predecoded frame sheets and a synchronized audio clip.

In-editor native VideoPlayer playback repeatedly remained paused at frame zero
on this Unity installation. The UI therefore plays the prepared frames directly
at 8 fps, synchronized to the audio clip, instead of relying on platform codecs.
Two 3840×2800 RGB sheets use compressed GPU textures, no mipmaps and no read/write
copy. UVs select one frame without per-frame allocations; the final frame is
retained when playback ends. Close stops audio immediately. Replay resets both
the frame clock and sound. Menu blocking pauses both.

`BeachBoatTutorialVideoPlaytest` checks visible frame advancement, packaged media,
pause/replay, complete playback/end behavior, immediate Start, optional practice,
ESC, saved difficulty and unchanged real save hashes. `BeachBoatFeedbackPlaytest`
continues to check timed gameplay, color states and achievement separation.
Reports/renders are ignored in `Logs`, and saves stay under ignored `.utmp`.

Validation on 2026-10-27: 39 full-playback checks and 60 gameplay/save regression
checks passed. The in-game render was inspected at 1280×720. Original manual
and temporary saves stayed byte-identical. Generated media totals about
2.8 MB in the project; no scene or save schema changes are required.
