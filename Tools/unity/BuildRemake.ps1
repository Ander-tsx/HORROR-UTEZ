# Builds the game from the command line (batch mode). In the editor use the menu HORROR-UTEZ > Build instead.
#   ./Tools/unity/BuildRemake.ps1               -> Builds/Remake/Windows/HORROR-UTEZ.exe
#   ./Tools/unity/BuildRemake.ps1 -Isolated     -> same, from a disposable copy so an open editor keeps working
#   ./Tools/unity/BuildRemake.ps1 -Mac          -> also builds the macOS app (needs Mac Build Support (Mono))
#   ./Tools/unity/BuildRemake.ps1 -Method X.Y.Z -> runs any static editor method instead of building
param(
    [string]$Unity = 'C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe',
    [switch]$Isolated,
    [switch]$Mac,
    [string]$Method
)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (!(Test-Path $Unity)) { throw "Unity 6000.6.3f1 not found at $Unity. Pass -Unity <path to Unity.exe>." }
$buildProject = $projectRoot
# A separate project/library keeps an open Unity session usable during builds. Never develop in the copy.
if ($Isolated) {
    $buildProject = Join-Path $projectRoot 'Builds/Remake/ValidationProject'
    foreach ($folder in @('Assets', 'Packages', 'ProjectSettings')) {
        robocopy (Join-Path $projectRoot $folder) (Join-Path $buildProject $folder) /MIR /NFL /NDL /NJH /NJS /R:1 /W:1 | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "Copy failed: $folder" }
    }
}
New-Item -ItemType Directory -Force (Join-Path $projectRoot 'Library') | Out-Null
$executeMethod = if ($Method) { $Method } elseif ($Mac) { 'HorrorUtez.Remake.Editor.RemakeBuild.WindowsAndMac' } else { 'HorrorUtez.Remake.Editor.RemakeBuild.Windows' }
$log = Join-Path $projectRoot $(if ($Method) { 'Library/remake-method.log' } else { 'Library/remake-build.log' })
$arguments = '-batchmode -nographics -quit -projectPath "' + $buildProject + '" -executeMethod ' + $executeMethod + ' -logFile "' + $log + '"'
$job = Start-Process -FilePath $Unity -ArgumentList $arguments -WindowStyle Hidden -PassThru
$job.WaitForExit()
if ($job.ExitCode -ne 0) { throw "Unity failed ($($job.ExitCode)). See $log" }
if ($Isolated -and !$Method) {
    robocopy (Join-Path $buildProject 'Builds/Remake/Windows') (Join-Path $projectRoot 'Builds/Remake/Windows') /E /NFL /NDL /NJH /NJS /R:1 /W:1 | Out-Null
    if ($LASTEXITCODE -ge 8) { throw 'Copying Windows build failed.' }
    if ($Mac) {
        robocopy (Join-Path $buildProject 'Builds/Remake/Mac') (Join-Path $projectRoot 'Builds/Remake/Mac') /MIR /NFL /NDL /NJH /NJS /R:1 /W:1 | Out-Null
        if ($LASTEXITCODE -ge 8) { throw 'Copying macOS build failed.' }
    }
}
# Robocopy returns 1-7 on success; do not leak them as this script's exit code.
$global:LASTEXITCODE = 0
if (!$Method) { Write-Output ('Ready: ' + (Join-Path $projectRoot 'Builds/Remake/Windows/HORROR-UTEZ.exe')) }
