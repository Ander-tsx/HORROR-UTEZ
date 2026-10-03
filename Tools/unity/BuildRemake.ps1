param(
    [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe',
    [switch]$Isolated,
    [switch]$PrepareOnly
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$buildProject = $projectRoot
# A separate project/library keeps the user's open Unity session usable during builds.
if ($Isolated) {
    $buildProject = Join-Path $projectRoot 'Builds/Remake/ValidationProject'
    foreach ($folder in @('Assets', 'Packages', 'ProjectSettings')) {
        robocopy (Join-Path $projectRoot $folder) (Join-Path $buildProject $folder) /E /NFL /NDL /NJH /NJS /R:1 /W:1 | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "Copy failed: $folder" }
    }
}
New-Item -ItemType Directory -Force (Join-Path $projectRoot 'Library') | Out-Null
$method = if ($PrepareOnly) { 'HorrorUtez.Remake.Editor.RemakeBuild.Prepare' } else { 'HorrorUtez.Remake.Editor.RemakeBuild.PrepareAndBuild' }
$log = Join-Path $projectRoot 'Library/remake-build.log'
$arguments = '-batchmode -nographics -quit -projectPath "' + $buildProject + '" -executeMethod ' + $method + ' -logFile "' + $log + '"'
$job = Start-Process -FilePath $Unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
$job.WaitForExit()
if ($job.ExitCode -ne 0) { throw "Unity failed ($($job.ExitCode)). See $log" }
if ($Isolated -and !$PrepareOnly) {
    robocopy (Join-Path $buildProject 'Builds/Remake/Windows') (Join-Path $projectRoot 'Builds/Remake/Windows') /E /NFL /NDL /NJH /NJS /R:1 /W:1 | Out-Null
    if ($LASTEXITCODE -ge 8) { throw 'Copying Windows build failed.' }
}
# Robocopy returns 1-7 on success; do not leak them as this script's exit code.
$global:LASTEXITCODE = 0
Write-Output ('Ready: ' + (Join-Path $projectRoot 'Builds/Remake/Windows/HORROR-UTEZ.exe'))
