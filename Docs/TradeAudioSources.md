# Recorded minigame sounds — 28 October 2026

The eight short work clips now use recordings found on YouTube at the player's request. They replace the previous synthesized noise. Full downloads and uploader metadata remain in ignored `Logs/TradeAudioSources`.

| Work | Recording and creator | Permission/source stated by uploader |
|---|---|---|
| Sawing | [Hand Saw Sawing Wood](https://www.youtube.com/watch?v=UJ72WfPN4ek), MJL AV | Description offers sound FX for projects and states Creative Commons licensing; no specific CC variant in metadata. Attribution retained here. |
| Rope movement/tightening | [Tightening Rope](https://www.youtube.com/watch?v=3XnP7nX_Z_s), sound effect | Description expressly permits use in films, sketches and projects. |
| Cloth handling, creasing, soft placement | [Cloth (rustle)](https://www.youtube.com/watch?v=5JhQIIPQaE0), Sound Effects ツ | Description permits use in projects and requests credit when another author is named; none is named for this clip. |
| Scooping/pouring grains | [Pouring Rice in a Pan](https://www.youtube.com/watch?v=s2-CF2g5_q0), Global Sound Archive | Video identifies [BigSoundBank](https://bigsoundbank.com/) as its original source. That library states free royalty-free CC0 recordings. |

These are third-party recordings; creator/source statements are preserved rather than claiming this project recorded them. The previous `author_trade_foley.py` generator is retired. `Tools/prepare_youtube_trade_audio.py` reproduces cuts and filtering, and `Tools/trade_audio_manifest.json` records exact source times, hashes, RMS and peaks.

Processing: mono PCM 32 kHz, high-pass 90 Hz, low-pass 3.2–4.5 kHz, short endpoint fades, measured RMS 0.011–0.027 and maximum peak 0.22. No speech, music or channel intros are included. Unity imports remain preloaded PCM with normalization off. One friction channel and a two-slot fading action channel bound the number of voices; outgoing actions fade instead of being abruptly cut. Playback gain is reduced relative to the old synthesized sounds, with the game's existing master/mute settings retained.
