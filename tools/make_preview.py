#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""生成 mod 预览图 preview.png（256x256）。

用法：
    <venv>/Scripts/python.exe make_preview.py [输出路径]

依赖 Pillow。字体直接用 Windows 自带的微软雅黑。
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFont

SIZE = 256
BG = (21, 24, 29)          # 背景
CARD = (30, 35, 43)        # 卡片
SLOT_OLD = (44, 50, 59)    # 原有格子
SLOT_NEW = (224, 163, 68)  # 扩容出来的格子（琥珀）
TITLE_FG = (255, 255, 255)
SUB_FG = (139, 149, 163)

FONT_DIR = r"C:\Windows\Fonts"
TITLE_FONT = os.path.join(FONT_DIR, "msyhbd.ttc")
SUB_FONT = os.path.join(FONT_DIR, "msyh.ttc")


def load(path, size, fallback=None):
    try:
        return ImageFont.truetype(path, size)
    except Exception:
        if fallback is not None:
            return fallback
        return ImageFont.load_default()


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "preview.png"

    img = Image.new("RGB", (SIZE, SIZE), BG)
    d = ImageDraw.Draw(img)

    # 卡片
    d.rounded_rectangle([10, 10, SIZE - 10, SIZE - 10], radius=18, fill=CARD)

    # 标题
    title_font = load(TITLE_FONT, 34)
    title = "鸭窝扩容"
    bbox = d.textbbox((0, 0), title, font=title_font)
    d.text(((SIZE - (bbox[2] - bbox[0])) / 2 - bbox[0], 34 - bbox[1]),
           title, font=title_font, fill=TITLE_FG)

    # 仓库网格：前 2 行是原容量，后 3 行是扩出来的
    cols, rows = 8, 5
    cell, gap = 22, 4
    gw = cols * cell + (cols - 1) * gap
    gh = rows * cell + (rows - 1) * gap
    x0 = (SIZE - gw) // 2
    y0 = 88

    for r in range(rows):
        for c in range(cols):
            x = x0 + c * (cell + gap)
            y = y0 + r * (cell + gap)
            fill = SLOT_OLD if r < 2 else SLOT_NEW
            d.rounded_rectangle([x, y, x + cell, y + cell], radius=4, fill=fill)

    # 副标题
    sub_font = load(SUB_FONT, 15)
    sub = "仓库 400 格 · 堆叠 ×2"
    bbox = d.textbbox((0, 0), sub, font=sub_font)
    d.text(((SIZE - (bbox[2] - bbox[0])) / 2 - bbox[0], 216 - bbox[1]),
           sub, font=sub_font, fill=SUB_FG)

    img.save(out, "PNG")
    print("written:", os.path.abspath(out), img.size)


if __name__ == "__main__":
    main()
