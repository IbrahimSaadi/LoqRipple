$ErrorActionPreference = 'Stop'
$env:PATHEXT = '.COM;.EXE;.BAT;.CMD'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /optimize+ /win32icon:"$PSScriptRoot\assets\LoqRipple.ico" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Management.dll /out:"$PSScriptRoot\LoqRipple.exe" "$PSScriptRoot\LoqRipple.cs" "$PSScriptRoot\AudioSpectrum.cs" "$PSScriptRoot\Advanced.cs" "$PSScriptRoot\Notifications.cs" "$PSScriptRoot\HealthIndicators.cs" "$PSScriptRoot\EffectDetails.cs" "$PSScriptRoot\StudioUI.cs" "$PSScriptRoot\DetailsEditor.cs" "$PSScriptRoot\ScreenSampler.cs" "$PSScriptRoot\NewEffects.cs" "$PSScriptRoot\UtilityIndicators.cs" "$PSScriptRoot\EffectGroups.cs" "$PSScriptRoot\IndicatorUI.cs" "$PSScriptRoot\UnifiedControls.cs" "$PSScriptRoot\Appearance.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
Write-Host 'Built LoqRipple.exe'
