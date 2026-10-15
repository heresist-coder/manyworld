# 숲 조명 굽기 (Manyworld > Field > Bake Forest Lighting)를 배치로 돌린다.
# 같은 PC에서 다른 프로젝트가 그래픽 카드를 쓰면 프로브 배치 중 장치가 빠질 때가 있어 d3d12·d3d11을 번갈아 네 번까지 다시 굽는다.
# 실패한 굽기는 숲이 통째로 든 20MB 씬을 남기므로 매번 git으로 되돌린다 (그 씬은 절대 커밋하지 않는다).
#   powershell -File tools/art/bake.ps1 -out C:\temp\art
param(
    [string]$out = "$env:TEMP\manyworld-art",
    [string]$unity = "C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Unity.exe"
)
$repo = (Resolve-Path "$PSScriptRoot\..\..").Path
New-Item -ItemType Directory -Force $out | Out-Null
$log = "$out\bake.log"
$apis = @('', '-force-d3d11', '', '-force-d3d11')
for ($try = 0; $try -lt $apis.Length; $try++) {
    $a = @('-batchmode', '-quit')
    if ($apis[$try]) { $a += $apis[$try] }
    $a += @('-projectPath', "`"$repo`"", '-executeMethod', 'Manyworld.EditorTools.ForestLightingBake.Bake', '-logFile', "`"$log`"")
    $p = Start-Process -FilePath $unity -ArgumentList $a -PassThru
    $p.WaitForExit(3600000) | Out-Null
    if (Select-String -Path $log -Pattern '굽기 완료' -Quiet -Encoding UTF8) { "bake ok (try $($try + 1) $($apis[$try]))"; exit 0 }
    "bake failed (try $($try + 1) $($apis[$try])), retry"
    git -C $repo checkout -- Assets/_Project/Scenes/ForestLighting.unity
    Start-Sleep 20
}
"bake failed 4 times: the scene was restored to the committed bake"
exit 1
