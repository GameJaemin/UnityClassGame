$ErrorActionPreference='Stop'
$mapFolder=Join-Path $PSScriptRoot '../../LionIceRun/Assets/StreamingAssets/Maps'
$utf8=[Text.UTF8Encoding]::new($false)
function SaveMap($file,$name,$theme,$description,$rects,$gimmicks) {
    $cells=New-Object 'char[,]' 15,15
    for($z=0;$z -lt 15;$z++){for($x=0;$x -lt 15;$x++){$cells[$x,$z]='.'}}
    foreach($r in $rects){for($z=$r[1];$z -le $r[3];$z++){for($x=$r[0];$x -le $r[2];$x++){$cells[$x,$z]='I'}}}
    $rows=@(); for($z=14;$z -ge 0;$z--){$row='';for($x=0;$x -lt 15;$x++){$row+=$cells[$x,$z]};$rows+=$row}
    $map=[ordered]@{version=1;name=$name;theme=$theme;description=$description;width=15;depth=15;rows=$rows;playerStart=@{x=2;y=2};botStarts=@(@{x=12;y=2},@{x=2;y=12},@{x=12;y=12});gimmicks=@($gimmicks)}
    $path=Join-Path $mapFolder "$file.json"
    [IO.File]::WriteAllText($path,($map|ConvertTo-Json -Depth 10)+[Environment]::NewLine,$utf8)
    if(!(Test-Path "$path.meta")){[IO.File]::WriteAllText("$path.meta","fileFormatVersion: 2`nguid: $([Guid]::NewGuid().ToString('N'))`nTextScriptImporter:`n  externalObjects: {}`n  userData: `n  assetBundleName: `n  assetBundleVariant: `n",$utf8)}
}
function G($kind,$x,$z,$width,$depth,$extra) {
    $g=@{kind=$kind;x=$x;z=$z;width=$width;depth=$depth;period=8;speed=25;dx=0;dz=0;phase=0;angle=0}
    foreach($key in $extra.Keys){$g[$key]=$extra[$key]};return $g
}
# 네 출발 구역은 넓은 외곽 우회로로 연결한다. 중앙 기믹은 위험한 지름길이다.
$ring=@(@(1,1,3,13),@(11,1,13,13),@(4,1,10,3),@(4,11,10,13))
SaveMap 'harbor_ferries' '왕복 항구' 'harbor' '왕복하는 빙판을 타고 바다를 건너요. 긴 외곽 길로 우회할 수도 있어요.' ($ring+@(@(5,4,7,6),@(7,8,9,10))) @((G 'move' 6 5 3 3 @{dx=2;period=7}),(G 'move' 8 9 3 3 @{dx=-2;period=9}))
SaveMap 'clockwork_floe' '시계태엽 빙원' 'clock' '중앙 빙판이 천천히 회전해요. 모서리에서 뛰어내릴 타이밍을 잡아요.' ($ring+ ,@(5,5,9,9)) @((G 'rotate' 7 7 5 5 @{speed=18}))
SaveMap 'piston_pass' '피스톤 협곡' 'factory' '노란 불빛 뒤에 피스톤이 튀어나와요. 통로 사이의 대피 구역을 이용해요.' ($ring+@(@(4,6,10,8),@(6,4,8,10))) @((G 'piston' 5 4.5 2 0.7 @{dz=3;period=5.5}),(G 'piston' 9 9.5 2 0.7 @{dz=3;angle=180;period=6;phase=2.5}))
SaveMap 'candy_carousel' '사탕 회전목마' 'candy' '서로 반대로 회전하는 두 빙판. 얼음이 깨지기 전에 다음 발판으로 이동해요.' ($ring+@(@(5,5,6,6),@(8,8,9,9))) @((G 'rotate' 5.5 5.5 2 2 @{speed=26}),(G 'rotate' 8.5 8.5 2 2 @{speed=-22}))
SaveMap 'conveyor_docks' '택배 빙하' 'cargo' '두 컨베이어가 반대 방향으로 흘러요. 중앙 가로길로 갈아타요.' ($ring+@(@(5,4,6,10),@(8,4,9,10),@(4,7,10,7))) @((G 'conveyor' 5.5 7 2 7 @{speed=1.8}),(G 'conveyor' 8.5 7 2 7 @{speed=1.8;angle=180}),(G 'piston' 7 9.5 1 0.6 @{dz=2;angle=180;period=7}))
SaveMap 'windmill_well' '풍차 우물' 'garden' '회전 막대를 점프로 피하고 네 방향의 좁은 다리에서 균형을 잡아요.' ($ring+@(@(4,6,10,8),@(6,4,8,10))) @((G 'sweeper' 7 7 7 0.35 @{speed=32;period=3}))
SaveMap 'tidal_shuttles' '밀물 셔틀' 'lagoon' '위아래로 오가는 빙판 두 개. 바깥 다리와 이동 지름길을 선택해요.' ($ring+@(@(4,4,6,6),@(8,8,10,10))) @((G 'move' 5 5 3 3 @{dz=2;period=6}),(G 'move' 9 9 3 3 @{dz=-2;period=10}))
SaveMap 'polar_foundry' '극지 공장' 'foundry' '회전 빙판·왕복 발판·피스톤을 모두 통과하는 복합 생존 공장이에요.' ($ring+@(@(5,5,6,6),@(8,7,10,9),@(4,8,6,8))) @((G 'rotate' 5.5 5.5 2 2 @{speed=-20}),(G 'move' 9 8 3 3 @{dz=-1;period=8}),(G 'piston' 4 9.5 2 0.7 @{dz=2;angle=180;period=6.5}))
# 기존 파일과 씬 참조는 보존하고 선택 목록에서만 줄인다.
$keep=@('default_6x6.json','frost_ring.json','hourglass_glacier.json','four_outposts.json')
$new=@('harbor_ferries','clockwork_floe','piston_pass','candy_carousel','conveyor_docks','windmill_well','tidal_shuttles','polar_foundry')
foreach($file in Get-ChildItem -LiteralPath $mapFolder -Filter '*.json') {
    if($new -contains $file.BaseName){continue}
    $json=[IO.File]::ReadAllText($file.FullName)
    $flag=if($keep -contains $file.Name){'false'}else{'true'}
    if($json -match '"archived"\s*:'){$json=[regex]::Replace($json,'("archived"\s*:\s*)(true|false)',('${1}'+$flag))}
    elseif($flag -eq 'true'){$json=$json.TrimEnd();$json=$json.Substring(0,$json.Length-1).TrimEnd()+",`n  `"archived`": true`n}`n"}
    [IO.File]::WriteAllText($file.FullName,$json,$utf8)
}
Write-Output '기믹 맵 8개 추가, 기존 맵 4개 유지, 나머지는 선택 목록에서 보관 처리.'
& (Join-Path $PSScriptRoot 'add_safe_zones.ps1')
