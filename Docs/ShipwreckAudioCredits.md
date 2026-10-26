# Audio added for title, combat and shipwrecks

Title music, entry seal, timber/bronze button feedback and arrow flesh impact are original synthesized audio, authored by `Tools/author_lobby_audio.py`. They use no third-party samples. Sword impacts retain the project's existing `Assets/Audio/chem trung.mp3` recording.

Three short human cries are adapted from **Male Grunt/Yelling sounds**, by **HaelDB**, published April 12, 2012:
https://opengameart.org/content/male-gruntyelling-sounds

The author offers the pack under CC0 (also lists OGA-BY 3.0). This project uses the **CC0** option:
https://creativecommons.org/publicdomain/zero/1.0/

Source files: `1yell4.wav`, `2yell2.wav`, `3yell5.wav` in `yelling sounds.zip`. Processing: trim silence, mono downmix, resample to 32 kHz, peak normalization and short fades. Reproduction: `Tools/prepare_wreck_voices.py`. The full downloaded archive stays in ignored Logs, outside game assets.
