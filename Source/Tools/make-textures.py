# Процедурные текстуры оформления «журнала заданий»: тёмная кожа обложки и
# медальон со знаком «!». Рисуется с нуля, без чужих материалов.
# Запуск: python Source/Tools/make-textures.py (нужны Pillow и numpy).
import os
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

OUT = [
    r"C:\MY DOC\DEV\GIT\MyDayQuest\Source\MyDayQuest\Resources\Images",
    r"C:\MY DOC\DEV\GIT\MyDayQuest\Source\MyDayQuest.Avalonia\Assets",
]
rng = np.random.default_rng(20260930)


def fractal_noise(size, octaves=6, persistence=0.55):
    """Сумма октав сглаженного шума — даёт мягкие разводы, как на бумаге."""
    total = np.zeros((size, size), dtype=np.float64)
    amp, norm = 1.0, 0.0
    for o in range(octaves):
        n = 2 ** (o + 2)
        small = rng.random((n, n))
        layer = np.array(Image.fromarray((small * 255).astype(np.uint8))
                         .resize((size, size), Image.BICUBIC), dtype=np.float64) / 255.0
        total += layer * amp
        norm += amp
        amp *= persistence
    return total / norm


def vignette(size, strength=0.45, power=2.6):
    y, x = np.mgrid[0:size, 0:size]
    cx = cy = (size - 1) / 2.0
    r = np.sqrt(((x - cx) / cx) ** 2 + ((y - cy) / cy) ** 2) / np.sqrt(2)
    return 1.0 - strength * np.clip(r, 0, 1) ** power


def blend(c1, c2, t):
    c1 = np.array(c1, dtype=np.float64)
    c2 = np.array(c2, dtype=np.float64)
    return c1[None, None, :] + (c2 - c1)[None, None, :] * t[:, :, None]


def parchment(size=512):
    t = fractal_noise(size, octaves=6)
    t = (t - t.min()) / (t.max() - t.min())
    img = blend((0xF4, 0xE6, 0xC6), (0xD2, 0xB6, 0x84), t ** 1.25)

    # волокна бумаги
    fib = rng.random((size, size))
    fib = np.array(Image.fromarray((fib * 255).astype(np.uint8))
                   .filter(ImageFilter.GaussianBlur(0.6)), dtype=np.float64) / 255.0
    img += (fib[:, :, None] - 0.5) * 10

    # редкие тёмные крапины — «возраст» бумаги
    spots = fractal_noise(size, octaves=3)
    img -= np.clip((spots - 0.72) * 6, 0, 1)[:, :, None] * 45

    img *= vignette(size, strength=0.30)[:, :, None]
    return Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), "RGB")


def leather(size=512):
    t = fractal_noise(size, octaves=7, persistence=0.6)
    t = (t - t.min()) / (t.max() - t.min())
    img = blend((0x0D, 0x0A, 0x06), (0x2A, 0x20, 0x13), t ** 1.1)

    grain = rng.random((size, size))
    img += (grain[:, :, None] - 0.5) * 8
    img *= vignette(size, strength=0.55, power=2.0)[:, :, None]
    return Image.fromarray(np.clip(img, 0, 255).astype(np.uint8), "RGB")


def bang_shape(draw, cx, cy, scale, fill, outline, width):
    """Восклицательный знак: сужающаяся ножка и ромбовидная точка."""
    s = scale
    draw.polygon([(cx - 0.20 * s, cy - 0.62 * s), (cx + 0.20 * s, cy - 0.62 * s),
                  (cx + 0.12 * s, cy + 0.16 * s), (cx - 0.12 * s, cy + 0.16 * s)],
                 fill=fill, outline=outline, width=width)
    draw.polygon([(cx, cy + 0.30 * s), (cx + 0.16 * s, cy + 0.46 * s),
                  (cx, cy + 0.62 * s), (cx - 0.16 * s, cy + 0.46 * s)],
                 fill=fill, outline=outline, width=width)


def questmark(size=192):
    img = Image.new("RGBA", (size * 4, size * 4), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    S = size * 4
    m = S * 0.04
    # медальон
    d.ellipse([m, m, S - m, S - m], fill=(0x20, 0x17, 0x0C, 255),
              outline=(0xD8, 0xA2, 0x2A, 255), width=int(S * 0.045))
    d.ellipse([m * 2.4, m * 2.4, S - m * 2.4, S - m * 2.4],
              outline=(0x8A, 0x66, 0x1C, 255), width=int(S * 0.012))
    bang_shape(d, S / 2, S / 2, S * 0.62, (0xF0, 0xC9, 0x55, 255), (0x6B, 0x4A, 0x0E, 255),
               int(S * 0.018))
    return img.resize((size, size), Image.LANCZOS)


def corner(size=96):
    """Угловой завиток рамки (левый верхний угол)."""
    img = Image.new("RGBA", (size * 4, size * 4), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    S = size * 4
    gold = (0xD8, 0xA2, 0x2A, 255)
    w = int(S * 0.055)
    d.line([(S * 0.06, S * 0.55), (S * 0.06, S * 0.06), (S * 0.55, S * 0.06)], fill=gold, width=w)
    d.arc([S * 0.10, S * 0.10, S * 0.82, S * 0.82], 180, 270, fill=gold, width=int(w * 0.8))
    d.ellipse([S * 0.40, S * 0.40, S * 0.56, S * 0.56], fill=gold)
    d.line([(S * 0.20, S * 0.20), (S * 0.34, S * 0.34)], fill=gold, width=int(w * 0.7))
    return img.resize((size, size), Image.LANCZOS)


def save(img, name):
    for folder in OUT:
        os.makedirs(folder, exist_ok=True)
        img.save(os.path.join(folder, name))
    print("saved", name)


save(leather(), "leatherbg.png")
save(questmark(), "questmark.png")
