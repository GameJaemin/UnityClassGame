# UI 시트(D:\UnityClassGame\UI)에서 게임에 쓸 요소를 잘라 Unity 프로젝트(Assets/UI/Sprites)에 저장한다.
# 원본 시트는 Assets/UI/Source로 복사한다 (UI (8)은 UI (4)와 같은 파일이라 제외).
# 실행: powershell -ExecutionPolicy Bypass -File Tools\ui\slice_ui.ps1
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$sheets = Join-Path $root "UI"
$sprites = Join-Path $root "LionIceRun\Assets\UI\Sprites"
$source = Join-Path $root "LionIceRun\Assets\UI\Source"
New-Item -ItemType Directory -Force -Path $sprites, $source | Out-Null
Add-Type -Path (Join-Path $PSScriptRoot "SliceSheet.cs") -ReferencedAssemblies System.Drawing

foreach ($n in 1..7) { Copy-Item (Join-Path $sheets "UI ($n).png") (Join-Path $source "UI_Sheet$n.png") -Force }

function Region($sheet, $index) {
    $found = [SliceSheet]::FindRegions((Join-Path $sheets "UI ($sheet).png"), 40, 4, 30)
    return $found[$index]
}
function Save($sheet, $rect, $name) {
    [SliceSheet]::Crop((Join-Path $sheets "UI ($sheet).png"), $rect, 4, (Join-Path $sprites "$name.png"))
    Write-Host "$name  ($($rect.Width)x$($rect.Height))"
}

# 시트 3: 글자가 들어간 버튼, 결과 배너, 조작키 안내
Save 3 (Region 3 4)  "Btn_Start"       # 게임 시작
Save 3 (Region 3 5)  "Btn_Retry"       # 다시 하기
Save 3 (Region 3 6)  "Btn_MainMenu"    # 메인 메뉴로
Save 3 (Region 3 24) "Banner_GameOver" # 아쉽네요!
# 조작키 안내 3개는 붙어 있어서 위치를 직접 지정
Save 3 (New-Object System.Drawing.Rectangle(680, 388, 352, 112)) "Hint_Move"
Save 3 (New-Object System.Drawing.Rectangle(680, 502, 352, 96))  "Hint_Jump"
Save 3 (New-Object System.Drawing.Rectangle(680, 598, 352, 98))  "Hint_Dive"

# 시트 4: 아이콘, 빈 버튼(9-slice 배경)
Save 4 (Region 4 6)  "Icon_Stopwatch"
Save 4 (Region 4 2)  "Icon_Restart"
Save 4 (Region 4 10) "Icon_Check"
Save 4 (Region 4 14) "Panel_Yellow"
Save 4 (Region 4 15) "Panel_Blue"
Save 4 (Region 4 16) "Panel_Pink"

# 시트 7: 타이틀 로고 (나중에 타이틀 화면에서)
Save 7 (Region 7 0)  "Logo_Title"

# 시트 1: 아이콘만 있고 글자 자리가 빈 버튼 (글자는 Unity에서 TextMeshPro로 올림)
Save 1 (Region 1 4)  "Btn_YellowRetry"   # 노랑 + 다시 하기 아이콘
Save 1 (Region 1 0)  "Btn_YellowPlay"    # 노랑 + 재생 아이콘 (타이틀 화면용)
Save 1 (Region 1 7)  "Btn_BlueHome"      # 파랑 + 집 아이콘 (메인 메뉴용)

# 시트 6: 얼음 큐브 아이콘 (남은 얼음 수 표시용)
Save 6 (New-Object System.Drawing.Rectangle(36, 648, 108, 110)) "Icon_IceCube"  # 옆 조각이 안 걸리게 직접 지정

# 코드로 그린 배경 (시안의 흰 타이머 배지, 반투명 남색 패널)
Add-Type -Path (Join-Path $PSScriptRoot "PanelGen.cs") -ReferencedAssemblies System.Drawing
$navy = [System.Drawing.Color]::FromArgb(255, 27, 42, 92)
[PanelGen]::RoundedPanel((Join-Path $sprites "Gen_BadgeWhite.png"), 320, 160, 64, 10,
    [System.Drawing.Color]::White, $navy, [System.Drawing.Color]::FromArgb(255, 221, 231, 245), $false)
[PanelGen]::RoundedPanel((Join-Path $sprites "Gen_PillNavy.png"), 320, 128, 52, 8,
    [System.Drawing.Color]::FromArgb(235, 30, 50, 102), [System.Drawing.Color]::FromArgb(255, 14, 26, 61),
    [System.Drawing.Color]::FromArgb(0, 0, 0, 0), $true)
[PanelGen]::RoundedPanel((Join-Path $sprites "Gen_PanelNavy.png"), 256, 256, 44, 6,
    [System.Drawing.Color]::FromArgb(205, 24, 40, 86), [System.Drawing.Color]::FromArgb(255, 78, 107, 176),
    [System.Drawing.Color]::FromArgb(0, 0, 0, 0), $false)
[PanelGen]::RoundedPanel((Join-Path $sprites "Gen_CardLight.png"), 256, 256, 44, 8,
    [System.Drawing.Color]::FromArgb(255, 240, 246, 255), $navy, [System.Drawing.Color]::FromArgb(255, 222, 232, 248), $false)
Write-Host "Gen_BadgeWhite, Gen_PillNavy, Gen_PanelNavy, Gen_CardLight"
