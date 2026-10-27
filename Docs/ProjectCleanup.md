# Project cleanup — 2026-10-27

Cleanup is restricted to this game project. Removed 131 files totaling
972,536,045 bytes (927.48 MiB). This is the size of deleted files, not a promise
that regenerating Unity caches or running new tests will free the same net space.

| Removed content | MiB | Evidence |
|---|---:|---|
| Vendor example scenes and their private lighting/terrain/textures | 77.75 | Unity dependency closure of 3,849 retained asset roots; no retained dependency uses the 25 demo assets |
| Nature Renderer installer archive and metadata | 363.13 | Package already installed; installed-version marker and editor/runtime DLLs retained |
| Seven old automated repair scene snapshots and metadata | 448.11 | Prior repair changes are committed and tested; these paths are backup outputs, not loading inputs |
| Old standalone compiler outputs | 3.14 | Temporary DLL/PDB/reference/response files under `.utmp/PursuitCompile` |
| Old editor/CLI logs | 31.79 | Retained JSON test reports, source downloads, screenshots and the current cleanup log |
| Retired 2D village UI/tutorial code, media and intermediate outputs | 3.55 | Replaced by the physical 3D trades; no scene/prefab GUID or surviving code references the removed components |

All deletion targets were resolved and checked to be regular files inside the
project before removal. Asset metadata was removed with its asset. The detailed
local manifest includes paths, byte counts, reasons, asset SHA-256 hashes and
deleted GUIDs: `Logs/ProjectCleanupManifest.json`.

`BeachBoatProjectCleanupAudit` is a read-only editor utility available under
**Tools → Bach Dang → Audit unused demo assets**. It retains all assets outside
explicitly scoped vendor examples, follows Unity's recursive dependency graph,
and protects direct asset-path references in editor/runtime code. Its report is
`Logs/ProjectCleanupDependencyAudit.json`; it never deletes files itself.

The remaining source library is intentionally retained: active terrain/vegetation,
character and ship models, graphics authoring inputs, licenses, Resources,
game scenes, project settings, original audio sources and recovery scenes.
The 7.8 GB Unity Library cache is regenerable but is used by the editor; deleting
it would force a full reimport. Git history and all user save files are retained.

Validation after removal: compilation succeeded; all 33 deleted asset GUIDs have
zero references in surviving readable project assets; 144 checks passed (84
physical village/achievement/interaction/save-load checks and 60 Quest 3 fleet,
tide, combat, withdrawal and save-load checks). Real user saves remained byte
identical. Test saves use isolated temporary directories. Unity's VFX Graph
package emitted missing-script warnings while importing its own editor templates;
the tests reported no gameplay errors, and those package files were not deleted.
