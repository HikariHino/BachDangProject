# Optional diagnostic launcher; does not change graphics settings or install an Editor.
[CmdletBinding()]
param(
    [string]$EditorPath,
    [switch]$CheckOnly
)
$ErrorActionPreference = 'Stop'
$projectPath = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$versionFile = Join-Path $projectPath 'ProjectSettings/ProjectVersion.txt'
$versionLine = Get-Content -LiteralPath $versionFile | Where-Object { $_ -match '^m_EditorVersion: ' } | Select-Object -First 1
if (-not $versionLine) { throw 'Cannot read the required Unity version.' }
$version = ($versionLine -split ': ', 2)[1].Trim()

if (-not $EditorPath) {
    $cli = Join-Path $env:LOCALAPPDATA 'Unity/bin/unity.exe'
    if (Test-Path -LiteralPath $cli) {
        $raw = & $cli editors --installed --format json
        if ($LASTEXITCODE -eq 0) {
            $installed = $raw | ConvertFrom-Json
            if ($installed.success) {
                $match = $installed.data | Where-Object { $_.version -eq $version } | Select-Object -First 1
                if ($match) { $EditorPath = $match.location }
            }
        }
    }
    if (-not $EditorPath) {
        $EditorPath = Join-Path $env:ProgramFiles "Unity/Hub/Editor/$version/Editor/Unity.exe"
    }
}
if (-not (Test-Path -LiteralPath $EditorPath -PathType Leaf)) {
    throw "Unity $version not found. Run this script with -EditorPath pointing to that version's Editor/Unity.exe."
}
$EditorPath = (Resolve-Path -LiteralPath $EditorPath).Path
$logPath = Join-Path $projectPath ('Logs/Editor-DX11-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.log')
$launchArgs = '-force-d3d11 -projectPath "' + $projectPath + '" -logFile "' + $logPath + '"'
if ($CheckOnly) {
    [pscustomobject]@{ Editor = $EditorPath; RequiredVersion = $version; Arguments = $launchArgs }
    return
}
# Do not launch another Editor for this project; CLI processes also use the name Unity.exe.
$projectNormal = $projectPath.Replace('\', '/').ToLowerInvariant()
$running = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" | Where-Object {
    $_.CommandLine -and $_.CommandLine.Replace('\', '/').ToLowerInvariant().Contains('/editor/unity.exe') -and
    $_.CommandLine.Replace('\', '/').ToLowerInvariant().Contains($projectNormal)
}
if ($running) {
    Write-Warning 'An Editor for this project is already running. Save and close it before using this launcher.'
    return
}
New-Item -ItemType Directory -Path (Join-Path $projectPath 'Logs') -Force | Out-Null
Start-Process -FilePath $EditorPath -ArgumentList $launchArgs -WorkingDirectory $projectPath -WindowStyle Hidden
Write-Output "Started Unity with Direct3D 11. Log: $logPath"
