# 실제 감속 곡선을 검사해 프레임 수에 관계없이 중앙 카드에 멈추는지 확인한다.
$ErrorActionPreference = 'Stop'
$taskRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$source = [IO.File]::ReadAllText((Join-Path $taskRoot 'LionIceRun\Assets\Scripts\MapRoulette.cs'), [Text.Encoding]::UTF8)
$start = $source.IndexOf('internal static class MapRouletteMotion')
if ($start -lt 0) { throw 'MapRouletteMotion을 찾지 못했습니다.' }
$checks = @'
public static class RouletteChecks
{
    public static int Run()
    {
        double from = 135.7, spacing = 440, speed = 2300, duration = 3.5;
        double to = System.Math.Ceiling((from + speed * duration / 3) / spacing) * spacing;
        double braking = (to - from) * 3 / speed;
        if (MapRouletteMotion.Position(from, to, 0) != from) throw new System.Exception("시작 위치");
        if (MapRouletteMotion.Position(from, to, 1) != to || to % spacing != 0) throw new System.Exception("중앙 카드 정렬");
        double previous = from, previousStep = double.PositiveInfinity;
        for (int i = 1; i <= 60; i++)
        {
            double current = MapRouletteMotion.Position(from, to, (double)i / 60);
            double step = current - previous;
            if (step < 0 || step > previousStep + 0.00001) throw new System.Exception("단조 감속");
            previous = current; previousStep = step;
        }
        double initial = (MapRouletteMotion.Position(from, to, 0.000001) - from) / (braking * 0.000001);
        if (System.Math.Abs(initial - speed) > 0.01) throw new System.Exception("버튼 직후 가속 방지");
        for (int sequence = -20; sequence <= 20; sequence++)
        {
            int index = MapRouletteMotion.CardIndex(sequence, 4);
            if (index < 0 || index >= 4 || index != MapRouletteMotion.CardIndex(sequence + 4, 4)) throw new System.Exception("반복 카드 인덱스");
        }
        if (MapRouletteMotion.CardIndex(0, 1) != 0 || MapRouletteMotion.CardIndex(1, 0) != -1) throw new System.Exception("단일/빈 맵 목록");
        if (MapRouletteMotion.Position(from, to, 2) != to) throw new System.Exception("마지막 프레임 초과");
        return 7;
    }
}
'@
# Windows PowerShell의 기본 컴파일러에서도 읽도록 단일 표현식 메서드를 일반 본문으로 바꾼다.
$motion = $source.Substring($start).Replace('internal static int CardIndex(int sequence, int count) => count > 0 ? ((-sequence % count) + count) % count : -1;', 'internal static int CardIndex(int sequence, int count) { return count > 0 ? ((-sequence % count) + count) % count : -1; }')
Add-Type -TypeDefinition ($motion + $checks) -Language CSharp
$count = [RouletteChecks]::Run()
Write-Output "맵 선택 감속 검사 $count 개 통과"
