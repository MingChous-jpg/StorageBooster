#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""把 AI 生成的方图处理成 mod 预览图：裁切 → 缩到 256 → 叠中文标题。

用法：
    <venv>/Scripts/python.exe make_preview_ai.py <源图> [标题] [副标题] [输出]

依赖 Pillow。中文用 msyhbd.ttc（雅黑 Bold）。
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFont, ImageFilter

SIZE = 256
FONT_DIR = r"C:\Windows\Fonts"
TITLE_FONT = os.path.join(FONT_DIR, "msyhbd.ttc")
SUB_FONT = os.path.join(FONT_DIR, "msyh.ttc")

ACCENT = (255, 214, 102)   # 暖黄，呼应鸭子
INK = (38, 30, 20)


def font(path, size):
    try:
        return ImageFont.truetype(path, size)
    except Exception:
        return ImageFont.load_default()


def main():
    src = sys.argv[1]
    title = sys.argv[2] if len(sys.argv) > 2 else "鸭窝扩容"
    sub = sys.argv[3] if len(sys.argv) > 3 else "仓库 400 格 · 堆叠 ×2"
    out = sys.argv[4] if len(sys.argv) > 4 else "preview.png"

    img = Image.open(src).convert("RGB")

    # 裁切策略：从原图里取一个偏上的正方形取景框，把主体（鸭子）留在画面上半部，
    # 下方的箱子堆/地面正好当文字底衬。同时把右下角的生成水印挤出画外。
    w, h = img.size
    side = min(w, h)
    crop_side = int(side * 0.90)
    left = (w - crop_side) // 2
    top = int(h * 0.10)
    top = max(0, min(top, h - crop_side))
    img = img.crop((left, top, left + crop_side, top + crop_side))
    img = img.resize((SIZE, SIZE), Image.LANCZOS)

    # 底部压暗，给文字腾出可读的底
    overlay = Image.new("RGB", (SIZE, SIZE), (0, 0, 0))
    mask = Image.new("L", (SIZE, SIZE), 0)
    md = ImageDraw.Draw(mask)
    band_top = int(SIZE * 0.60)
    for y in range(band_top, SIZE):
        # 线性渐深：0 → 225
        md.line([(0, y), (SIZE, y)], fill=int(225 * (y - band_top) / (SIZE - band_top)))
    img = Image.composite(overlay, img, mask)
    img = img.filter(ImageFilter.SMOOTH_MORE)

    d = ImageDraw.Draw(img)

    # 标题（带描边，保证任何底色上都可读）
    tf = font(TITLE_FONT, 38)
    bbox = d.textbbox((0, 0), title, font=tf)
    tx = (SIZE - (bbox[2] - bbox[0])) / 2 - bbox[0]
    ty = SIZE - 96 - bbox[1]
    d.text((tx, ty), title, font=tf, fill=(255, 255, 255),
           stroke_width=4, stroke_fill=INK)

    # 副标题
    sf = font(SUB_FONT, 16)
    bbox = d.textbbox((0, 0), sub, font=sf)
    sx = (SIZE - (bbox[2] - bbox[0])) / 2 - bbox[0]
    sy = SIZE - 42 - bbox[1]
    d.text((sx, sy), sub, font=sf, fill=ACCENT,
           stroke_width=3, stroke_fill=INK)

    # 顶部一条细装饰线
    d.rectangle([0, 0, SIZE, 4], fill=ACCENT)

    img.save(out, "PNG")
    print("written:", os.path.abspath(out), img.size)


if __name__ == "__main__":
    main()
