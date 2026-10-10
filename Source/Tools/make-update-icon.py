# Рисует значок обновления — шестерёнку на прозрачном фоне.
#
# Рисуем вчетверо крупнее и уменьшаем: PIL не сглаживает края многоугольников,
# а без сглаживания зубцы выглядят рваными.
#
# Цвета — из палитры приложения: золото рамок и тёмный контур, чтобы значок
# читался и на кожаной обложке игрового скина, и на светлом фоне классического.
import math
import os

from PIL import Image, ImageDraw

SIZE = 448
SCALE = 4
TEETH = 10

GOLD = (216, 162, 42, 255)        # QuestGold
OUTLINE = (42, 31, 18, 255)       # QuestInk

OUT = [
    r"C:\MY DOC\DEV\GIT\MyDayQuest\Source\MyDayQuest\Resources\Images\update.png",
    r"C:\MY DOC\DEV\GIT\MyDayQuest\Source\MyDayQuest.Avalonia\Assets\update.png",
]


def gear_polygon(cx, cy, r_outer, r_inner, teeth, tooth_ratio=0.46):
    """Контур шестерёнки: зубец — трапеция, впадина между ними такой же ширины."""
    pts = []
    step = 2 * math.pi / teeth
    half = step * tooth_ratio / 2
    flat = step * (1 - tooth_ratio) / 2
    for i in range(teeth):
        a = i * step
        # впадина
        pts.append((cx + r_inner * math.cos(a - flat), cy + r_inner * math.sin(a - flat)))
        # подъём на зубец
        pts.append((cx + r_outer * math.cos(a - half), cy + r_outer * math.sin(a - half)))
        pts.append((cx + r_outer * math.cos(a + half), cy + r_outer * math.sin(a + half)))
        # спуск
        pts.append((cx + r_inner * math.cos(a + flat), cy + r_inner * math.sin(a + flat)))
    return pts


def main():
    s = SIZE * SCALE
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)

    c = s / 2
    r_outer = s * 0.46
    r_inner = s * 0.355
    stroke = max(2, int(s * 0.012))

    d.polygon(gear_polygon(c, c, r_outer, r_inner, TEETH), fill=GOLD,
              outline=OUTLINE, width=stroke)

    # Отверстие: вырезаем насквозь, чтобы фон просвечивал.
    hole = s * 0.16
    d.ellipse([c - hole, c - hole, c + hole, c + hole], fill=(0, 0, 0, 0))
    d.ellipse([c - hole, c - hole, c + hole, c + hole], outline=OUTLINE, width=stroke)

    # Тонкое кольцо внутри — чтобы силуэт не выглядел плоским блином.
    ring = s * 0.255
    d.ellipse([c - ring, c - ring, c + ring, c + ring], outline=OUTLINE, width=stroke)

    img = img.resize((SIZE, SIZE), Image.LANCZOS)

    for path in OUT:
        os.makedirs(os.path.dirname(path), exist_ok=True)
        img.save(path)
        print("записан:", path)


if __name__ == "__main__":
    main()
