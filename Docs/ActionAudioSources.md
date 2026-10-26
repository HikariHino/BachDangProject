# Action sounds cut from YouTube recordings

Only short edited action clips are included in Resources. The original WebM/M4A files and full metadata stay in ignored `Logs/ActionAudioSources`. Retrieval date: 2026-10-26. These are third-party recordings, not original recordings by this project.

| Action | Video / creator | Permission stated in the video description |
| --- | --- | --- |
| Walking and running on grass | [FREE Footsteps on grass](https://www.youtube.com/watch?v=nbcNpkmUC0Q), Ambient Sound's @ 48khz | Free Foley sound for films, videos or other work; free to use anywhere. |
| Walking on ship decks | [Footsteps on wood](https://www.youtube.com/watch?v=Ae6DnC84jaY), ProFX; original recording by martian | Uploader identifies the recording as [martian's CC0 wood foley](https://freesound.org/people/martian/sounds/19292/). |
| Oar entering/pulling water | [Rowing Boat Sound Effect (Royalty Free)](https://www.youtube.com/watch?v=h-zpC8JTLbM), Free Stock Footages and Sound Effects / Adam | Free to use anywhere, no attribution required. |
| Bow release and arrow flesh impact | [Bow & Arrow FREE Sound FX](https://www.youtube.com/watch?v=K9HLsnmZoic), The Audio Horizon | Designed by the uploader's team; royalty free, use without restrictions. |
| Sword swing | [Sword Swing Sound Effect 2](https://www.youtube.com/watch?v=70IZTBE03mU), Movie & Game Sounds | Free download, with express permission to use in video games, movies and videos. |

These permissions are creator statements; the videos do not expose a standardized Creative Commons license in their metadata. The wood recording is identified as CC0 in its description. Attribution is retained here regardless. Jochi SFX's bow video was considered but not used because it prohibits redistributing the audio separately.

Processing: mono downmix, 32 kHz resampling, short individual cuts, gentle rumble/hiss filtering, RMS matching, peak ceiling 0.72 and short fades. Running uses separate source takes with a small speed adjustment. No music, speech, subscription prompts or long loops are included. `Tools/prepare_youtube_action_audio.py` reproduces 31 cuts from the five source WebM/M4A files. `Tools/action_audio_manifest.json` records source timestamps, measured levels and output hashes.

Unity imports these short clips as preloaded, decompressed PCM. WAV assets total 927,216 bytes. The Unity CLI audio/battle playtest passed 106 checks on 2026-10-26, including imported sample data, varied action playback, mute, footsteps relative to a moving deck, actual sword/arrow hits, and wreck/combat regression checks. The generated report is `Logs/AudioBattlePlaytest.json`.
