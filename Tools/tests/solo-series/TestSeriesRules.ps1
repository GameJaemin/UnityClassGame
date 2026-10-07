$ErrorActionPreference = 'Stop'
$taskRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
$seriesSource = [IO.File]::ReadAllText((Join-Path $taskRoot 'LionIceRun\Assets\Scripts\OnlineSeries.cs'))
$stub = 'public class OnlineSpawn { public ulong actorId; public string nickname; public bool bot; }'
Add-Type -TypeDefinition ($seriesSource + $stub) -Language CSharp
$series = New-Object OnlineSeries
$roster = New-Object OnlineSpawn
$roster.actorId = 1; $roster.nickname = '혼자'
$series.Begin([OnlineSpawn[]]@($roster))
for ($round = 1; $round -le 5; $round++) {
    if (!$series.CompleteRound($round, [ulong[]]@(1))) { throw "단독 참가자 $round 판 집계 실패" }
}
if (!$series.Complete -or $series.Scores[0].total -ne 20) { throw '단독 참가자 5판 합산 실패' }
if ($series.CompleteRound(6, [ulong[]]@(1))) { throw '6판 집계 허용' }
Write-Output 'SOLO_SINGLE_PARTICIPANT_RULES_PASSED: 5판·20점·6판 차단'