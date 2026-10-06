# 실제 BotBrain.cs의 물리 없는 탐색 코드를 읽어 작은 맵에서 회귀 검사한다.
$ErrorActionPreference = 'Stop'
$taskRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$brainPath = Join-Path $taskRoot 'LionIceRun\Assets\Scripts\BotBrain.cs'
$source = [IO.File]::ReadAllText($brainPath, [Text.Encoding]::UTF8)
$start = $source.IndexOf('internal static class BotRouteSearch')
if ($start -lt 0) { throw 'BotRouteSearch를 찾지 못했습니다.' }
$search = 'using System; using System.Collections.Generic;' + $source.Substring($start)
$checks = @'
public static class BotRouteChecks
{
    static int passed;
    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        passed++;
    }
    public static int Run()
    {
        var oneGap = BotRouteSearch.Build(new float[] { 1, -1, 1 }, 3, 1, 0, true);
        Check(oneGap.ContainsKey(2) && oneGap[2].jump && oneGap[2].from == 0, "빈칸 하나 점프");
        var twoGaps = BotRouteSearch.Build(new float[] { 1, -1, -1, 1 }, 4, 1, 0, true);
        Check(!twoGaps.ContainsKey(3), "빈칸 두 개는 넘지 않음");
        var noJump = BotRouteSearch.Build(new float[] { 1, -1, 1 }, 3, 1, 0, false);
        Check(!noJump.ContainsKey(2), "점프 거리가 부족하면 고립 칸 제외");
        var detour = BotRouteSearch.Build(new float[] { 1, -1, 1, 1, 1, 1 }, 3, 2, 0, true);
        Check(detour[2].cost == 4 && !detour[2].jump, "안전한 우회로가 빈칸 점프보다 우선");
        var aggressive = BotRouteSearch.Build(new float[] { 1, -1, 1, 1, 1, 1 }, 3, 2, 0, true, 1.8f);
        Check(aggressive[2].jump && aggressive[2].from == 0 && aggressive[2].cost < 4, "같은 맵에서 공격형은 빈칸 점프 지름길 선택");
        var aggressiveTwoGaps = BotRouteSearch.Build(new float[] { 1, -1, -1, 1 }, 4, 1, 0, true, 1.8f);
        Check(!aggressiveTwoGaps.ContainsKey(3), "공격형도 빈칸 두 개 점프는 금지");
        var cracked = BotRouteSearch.Build(new float[] { 1, 9, 1, 1, 1, 1 }, 3, 2, 0, true);
        Check(cracked[2].cost == 4 && cracked[2].from == 5, "심하게 금 간 칸보다 깨끗한 우회로 선택");
        var border = BotRouteSearch.Build(new float[] { -1, -1, 1, 1, -1, -1 }, 3, 2, 2, false);
        Check(!border.ContainsKey(3), "오른쪽 끝에서 다음 행으로 이어지는 가짜 이웃 없음");
        var isolated = BotRouteSearch.Build(new float[] { 1, -1, -1, -1, -1, -1, -1, -1, 1 }, 3, 3, 0, true);
        Check(isolated.Count == 1, "도달 불가능한 목표 칸 제외");
        Check(BotRouteSearch.Build(new float[] { 1 }, 1, 1, 2, true).Count == 0, "잘못된 시작 칸 처리");
        return passed;
    }
}
'@
Add-Type -TypeDefinition ($search + $checks) -Language CSharp
$count = [BotRouteChecks]::Run()
Write-Output "AI 경로 검사 $count 개 통과"
