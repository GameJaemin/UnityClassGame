# WebP(투명 배경 포함)를 PNG로 바꾼다. Windows 기본 이미지 도구가 WebP를 못 읽어서 Blender로 변환한다.
# 실행: blender --background --factory-startup --python webp_to_png.py -- <입력.webp> <출력.png>
import sys

import bpy

source, output = sys.argv[sys.argv.index("--") + 1:][:2]
image = bpy.data.images.load(source, check_existing=False)
width, height = image.size  # 여기서 실제로 파일을 읽는다
pixels = list(image.pixels)  # RGBA float

# 새 이미지에 픽셀을 옮겨 PNG(RGBA)로 저장
result = bpy.data.images.new("converted", width, height, alpha=True)
result.pixels = pixels
result.filepath_raw = output
result.file_format = "PNG"
result.save()
print("WEBP_DONE", width, height, image.channels)
