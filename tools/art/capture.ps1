# 목표 샷 캡처 (VisualCaptureTests.CaptureFieldTargets)를 돌리고 목표와 나란히 놓은 비교표와 수치를 만든다.
#   powershell -File tools/art/capture.ps1 -name it01                # 캡처 + 비교표 + 수치
#   powershell -File tools/art/capture.ps1 -name it01 -all           # 이어서 PlayMode 전체 테스트 (기대값: 24 통과, 0 실패, 4 건너뜀)
# 결과: <out>\<name>\t01_road.png ..., <out>\<name>-compare.png
param(
    [Parameter(Mandatory = $true)][string]$name,
    [string]$out = "$env:TEMP\manyworld-art",
    [string]$tests = "CaptureFieldTargets",
    [switch]$all,
    [string]$unity = "C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe"
)
$repo = (Resolve-Path "$PSScriptRoot\..\..").Path
New-Item -ItemType Directory -Force $out | Out-Null
$filter = ($tests.Split(',') | ForEach-Object { "Manyworld.Tests.VisualCaptureTests.$_" }) -join ';'
$env:MANYWORLD_CAPTURE_DIR = "$out\$name"
$env:MANYWORLD_CAPTURE_FX = ""
$p = Start-Process -FilePath $unity -ArgumentList @('-batchmode', '-projectPath', "`"$repo`"", '-runTests', '-testPlatform', 'PlayMode', '-testFilter', $filter, '-testResults', "`"$out\$name.xml`"", '-logFile', "`"$out\$name.log`"") -PassThru
$p.WaitForExit(2400000) | Out-Null
Select-String -Path "$out\$name.log" -Pattern "error CS" | Select-Object -First 4 | ForEach-Object { $_.Line }
[xml]$x = Get-Content "$out\$name.xml"; $r = $x.'test-run'; "capture result $($r.result) passed $($r.passed) failed $($r.failed)"
if (Test-Path "$out\$name\t01_road.png") {
    python -I "$PSScriptRoot\compare.py" "$out\$name" "$out\$name-compare.png"
    python -I "$PSScriptRoot\stats.py" "$out\$name"
}
if ($all) {
    $env:MANYWORLD_CAPTURE_DIR = ""
    $p = Start-Process -FilePath $unity -ArgumentList @('-batchmode', '-projectPath', "`"$repo`"", '-runTests', '-testPlatform', 'PlayMode', '-testResults', "`"$out\$name-all.xml`"", '-logFile', "`"$out\$name-all.log`"") -PassThru
    $p.WaitForExit(3600000) | Out-Null
    [xml]$x = Get-Content "$out\$name-all.xml"; $r = $x.'test-run'
    "ALL result $($r.result) passed $($r.passed) failed $($r.failed) skipped $($r.skipped)"
    $x.SelectNodes("//test-case") | Where-Object { $_.result -eq 'Failed' } | ForEach-Object { "FAIL $($_.fullname): $($_.failure.message.'#cdata-section')" }
}
