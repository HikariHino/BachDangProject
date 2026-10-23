# Unity Editor startup stability

## Observed failure

Unity 6000.6.0f1 terminated natively while rendering this project on 23 October
2026. Two reports contain these render-sort worker stacks:

- `RenderObjectSorter::operator()` / `QSortBlittableMultiThreadedImpl` /
  `SortSegmentJob` (21:13).
- `QSortBlittableMultiThreadedImpl` / `CopyBucketsFromSegmentsAndSortJob`
  (21:00).

These are native crashes rather than managed Console exceptions. A Console with
zero errors immediately after entering Play Mode does not rule them out.

## Project workaround

`Assets/Editor/BachDangEditorStability.cs` limits `JobsUtility.JobWorkerCount`
to two during editor domain initialization, before Scene/Game views render.
It applies automatically when opening this project normally, including through
Hub, and after Play Mode domain reloads. No launch arguments are required.

The workaround is restricted to the observed editor version, 6000.6.0f1.
It can reduce parallel CPU throughput in the Editor. It is an Editor-only file
and does not change a built game's job configuration or the Unity installation.

This mitigates the observed parallel render-sort failure; the logs alone do not
establish the underlying engine or hardware defect. Reassess the workaround
when changing Unity versions.

API reference: [JobsUtility.JobWorkerCount](https://docs.unity.cn/6000.7/Documentation/ScriptReference/Unity.Jobs.LowLevel.Unsafe.JobsUtility.JobWorkerCount.html).

## Validation

The project was launched without `-job-worker-count`. The probe reported two
active workers with a maximum of 31, confirming that project initialization,
rather than launch arguments, applied the limit.

An Edit Mode probe rendered 54 camera positions around camp, village and harbor
with 13,178 scene renderers. Clicking New Game through UI pointer events succeeded.
A Play Mode probe rendered another 54 positions successfully. The editor then
exited Play Mode, saved and closed gracefully for a second normal launch.
The second launch also reported two workers out of a maximum of 31 and completed
54 Edit Mode camera renders, then entered Play Mode successfully.

Local diagnostic artifacts are in `Logs/EditorStabilityNormalLaunch.log` and
`Logs/EditorStabilityReopen.log` and `Logs/EditorStabilityProbe.cs`; they are not
shipped as game assets.
