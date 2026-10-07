$ErrorActionPreference='Stop'
$folder=Join-Path $PSScriptRoot '../../LionIceRun/Assets/StreamingAssets/Maps'
$encoding=[Text.UTF8Encoding]::new($false)
foreach($file in Get-ChildItem -LiteralPath $folder -Filter '*.json') {
    $json=[IO.File]::ReadAllText($file.FullName); $map=$json|ConvertFrom-Json
    if($map.archived){continue}
    $count=([regex]::Matches(($map.rows -join ''),'S')).Count
    if($count -ge 2){continue}
    $spawns=@($map.playerStart)+@($map.botStarts)
    $candidates=@();$chosen=@()
    for($z=1;$z -lt $map.depth-1;$z++){for($x=1;$x -lt $map.width-1;$x++){
        if($map.rows[$map.depth-1-$z][$x] -ne 'I'){continue}
        $blocked=$false
        foreach($p in $spawns){if([Math]::Abs($x-$p.x)+[Math]::Abs($z-$p.y) -lt 2){$blocked=$true}}
        foreach($g in @($map.gimmicks)) {
            if($g.kind -in @('move','rotate') -and [Math]::Abs($x-$g.x) -lt $g.width/2 -and [Math]::Abs($z-$g.z) -lt $g.depth/2){$blocked=$true}
        }
        if($blocked){continue}
        $centerDist=[Math]::Abs($x-($map.width-1)/2)+[Math]::Abs($z-($map.depth-1)/2)
        $candidates+=@{x=$x;z=$z;score=$centerDist}
    }}
    foreach($p in ($candidates|Sort-Object -Property @{Expression={$_.score};Descending=$true},@{Expression={$_.z}},@{Expression={$_.x}})) {
        $tooClose=$false
        foreach($q in $chosen){if([Math]::Abs($p.x-$q.x)+[Math]::Abs($p.z-$q.z) -lt [Math]::Min($map.width,$map.depth)/2){$tooClose=$true}}
        if($tooClose){continue}
        $row=$map.rows[$map.depth-1-$p.z].ToCharArray();$row[$p.x]='S';$map.rows[$map.depth-1-$p.z]=-join $row
        $chosen+=,$p;$count++
        if($count -ge 2){break}
    }
    if($count -lt 2){throw "안전지대 위치 부족: $($file.Name)"}
    $rowsText='"rows": ['+"`n"+(($map.rows|ForEach-Object{'    '+($_|ConvertTo-Json -Compress)}) -join ",`n")+"`n  ]"
    $json=[regex]::Replace($json,'"rows"\s*:\s*\[[\s\S]*?\]',$rowsText)
    [IO.File]::WriteAllText($file.FullName,$json,$encoding)
    Write-Output "$($file.Name): 안전지대 $count 칸"
}
