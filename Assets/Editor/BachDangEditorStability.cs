using System;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

/// <summary>Project-scoped workaround for native parallel render-sort crashes in 6000.6.0f1.</summary>
[InitializeOnLoad]
public static class BachDangEditorStability
{
    public const int WorkerLimit=2;

    static BachDangEditorStability()
    {
        // Apply before Scene/Game views render, including projects opened through Hub.
        // This matches the worker limit used by the successful campaign playtests.
        // Limit this workaround to the engine version observed in the crash reports.
        if(!string.Equals(Application.unityVersion,"6000.6.0f1",StringComparison.Ordinal))return;
        JobsUtility.JobWorkerCount=Math.Min(WorkerLimit,JobsUtility.JobWorkerMaximumCount);
    }
}
