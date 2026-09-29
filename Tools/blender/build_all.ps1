# 모든 복셀 캐릭터를 다시 만들고 미리보기 시트를 합친다.
# 실행: powershell -ExecutionPolicy Bypass -File Tools\blender\build_all.ps1
param(
    [string]$Blender = "C:\Program Files\Blender Foundation\Blender 5.2\blender.exe"
)

$here = $PSScriptRoot
$renders = Join-Path $env:TEMP "voxel_character_renders"
# FBX와 색상표는 Unity 프로젝트로 바로 내보낸다
$unityModels = Join-Path $here "..\..\LionIceRun\Assets\Models\Characters"
New-Item -ItemType Directory -Force -Path $unityModels | Out-Null
$characters = @(
    @{ Script = "make_lion.py";      Name = "Lion" },
    @{ Script = "make_polarbear.py"; Name = "PolarBear" },
    @{ Script = "make_tiger.py";     Name = "Tiger" }
)

foreach ($c in $characters) {
    $extra = @()
    # 애니메이션은 모든 캐릭터가 같으므로 사자만 영상으로 확인
    if ($c.Name -eq "Lion") { $extra = @("--anim-preview", $renders) }
    & $Blender --background --factory-startup --python (Join-Path $here $c.Script) -- `
        --fbx (Join-Path $unityModels "$($c.Name).fbx") `
        --palette (Join-Path $unityModels "$($c.Name)_palette.txt") `
        --blend (Join-Path $here "$($c.Name).blend") `
        --preview $renders @extra 2>&1 | Select-String -Pattern "Error", "Traceback", "CHARACTER_DONE" -SimpleMatch
}
Copy-Item (Join-Path $renders "lion_anim.mp4") (Join-Path $here "animations_preview.mp4") -Force

# 캐릭터마다 정면 / 옆 / 뒤 3장을 한 줄로 붙이고, 전체 시트도 만든다
Add-Type -AssemblyName System.Drawing
$views = "front", "side", "back"
$crop = New-Object System.Drawing.Rectangle(100, 130, 500, 680)
$sheet = New-Object System.Drawing.Bitmap(1500, (680 * $characters.Count))
$sheetGfx = [System.Drawing.Graphics]::FromImage($sheet)
for ($row = 0; $row -lt $characters.Count; $row++) {
    $name = $characters[$row].Name.ToLower()
    $strip = New-Object System.Drawing.Bitmap(1500, 680)
    $stripGfx = [System.Drawing.Graphics]::FromImage($strip)
    for ($i = 0; $i -lt 3; $i++) {
        $img = [System.Drawing.Image]::FromFile((Join-Path $renders "${name}_$($views[$i]).png"))
        $stripGfx.DrawImage($img, (New-Object System.Drawing.Rectangle(($i * 500), 0, 500, 680)), $crop, [System.Drawing.GraphicsUnit]::Pixel)
        $sheetGfx.DrawImage($img, (New-Object System.Drawing.Rectangle(($i * 500), ($row * 680), 500, 680)), $crop, [System.Drawing.GraphicsUnit]::Pixel)
        $img.Dispose()
    }
    $strip.Save((Join-Path $here "${name}_preview.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $stripGfx.Dispose(); $strip.Dispose()
}
$sheet.Save((Join-Path $here "characters_preview.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$sheetGfx.Dispose(); $sheet.Dispose()

# 애니메이션 정지 컷 4장을 세로로 이어 붙인다 (절반 크기)
$frames = "00", "03", "06", "09"
$animSheet = New-Object System.Drawing.Bitmap(800, (320 * $frames.Count))
$animGfx = [System.Drawing.Graphics]::FromImage($animSheet)
$animGfx.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
for ($i = 0; $i -lt $frames.Count; $i++) {
    $img = [System.Drawing.Image]::FromFile((Join-Path $renders "lion_anim_f$($frames[$i]).png"))
    $animGfx.DrawImage($img, (New-Object System.Drawing.Rectangle(0, ($i * 320), 800, 320)))
    $img.Dispose()
}
$animSheet.Save((Join-Path $here "animations_preview.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$animGfx.Dispose(); $animSheet.Dispose()
Write-Host "Preview: $(Join-Path $here 'characters_preview.png')"
