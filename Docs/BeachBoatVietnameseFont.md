# Vietnamese text in beachBoat

The default LiberationSans SDF atlas did not contain the full Vietnamese alphabet. Its dynamic fallback had no saved glyphs. Story UI depended on that fallback, while world labels and the IMGUI HUD did not explicitly select a Vietnamese-capable font.

## Fix

- `StoryFont` assigns a shared, pre-baked TMP font to the introduction, dialogue, objectives, interaction hints, transition subtitles and world labels.
- `VietnameseSDF.asset` contains 249 characters: the full Vietnamese alphabet in upper/lower case, combining marks, ASCII and story punctuation. It uses one static 1024×1024 Alpha8 atlas with no runtime atlas growth. Unity reported approximately 2 MiB for the atlas including its CPU/GPU copies; this is not a measurement of total game memory.
- World labels use TextMeshPro with the same atlas and shared material.
- `FontSettings.asset` references the existing bundled LiberationSans TTF for SaBanHUD and the fallback interaction prompt. Unsupported decorative HUD emoji were removed.
- Global TMP settings and TestQuest scene assets are unchanged.

## Validation

- All 20 scene TMP text components, including 11 world labels, use the expected font and contain no unsupported characters.
- The legacy HUD font covers Vietnamese upper/lowercase letters and its up/down indicators.
- Inspected Play-mode screenshots of the introduction, dialogue, HUD and world label.
- All 39 existing gameplay smoke checks passed, covering dialogue, wood collection, transitions, stake planting, tour mode, movement and jumping.
- Reimported the saved font and checked its glyphs, material and atlas; repeated the font checks in a fresh Play session.

## Maintenance

The font ships with the project and does not depend on a font installed on the player's computer. Vietnamese text changes do not require regeneration. If new scripts introduce characters outside the baked set, update `BeachBoatVietnameseFont.RequiredCharacters()` and deliberately regenerate the font in Edit Mode. The menu `Bach Dang/Create Vietnamese Story Font` creates missing assets and refuses to overwrite an existing font; it is not a routine startup step.
