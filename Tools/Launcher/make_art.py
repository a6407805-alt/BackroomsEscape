"""Generates the Backrooms Escape launcher's built-in art (background.png, emblem.png, launcher.ico).
The launcher swaps the background for launcher-art.jpg from the latest release once the Unity publisher uploads one.
Run: python3 Tools/Launcher/make_art.py  (writes into Launcher/Assets/)"""
import math, os, random
from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Launcher", "Assets")
os.makedirs(OUT, exist_ok=True)
random.seed(3)
WALL = (196, 176, 96); CARPET = (122, 104, 52); CEIL = (214, 205, 160); LIGHT = (255, 252, 228)


def lerp(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def background(w=1920, h=1080):
    img = Image.new("RGB", (w, h), (20, 18, 10))
    d = ImageDraw.Draw(img)
    vx, vy = w * 0.64, h * 0.47           # vanishing point (right of centre, behind the UI's right column)
    # one-point perspective corridor: draw nested frames from far to near
    steps = 26
    for i in range(steps, -1, -1):
        t = i / steps
        s = 0.035 + (1 - t) ** 2.2 * 1.25   # scale of this slice
        hw, hh = w * 0.42 * s, h * 0.55 * s
        x0, x1, y0, y1 = vx - hw, vx + hw, vy - hh * 0.92, vy + hh
        fog = t ** 0.8
        wall = lerp(WALL, (40, 36, 18), fog * 0.9)
        d.polygon([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], fill=lerp(CARPET, (30, 27, 14), fog))
        d.rectangle([x0, y0, x1, y0 + (y1 - y0) * 0.5], fill=lerp(CEIL, (40, 38, 24), fog))
        d.rectangle([x0, y0 + (y1 - y0) * 0.08, x0 + hw * 0.06, y1 - (y1 - y0) * 0.06], fill=wall)
        d.rectangle([x1 - hw * 0.06, y0 + (y1 - y0) * 0.08, x1, y1 - (y1 - y0) * 0.06], fill=wall)
    # corridor walls (left/right trapezoids) with stripes
    near = 1.3
    for side in (-1, 1):
        pts = [(vx + side * w * 0.42 * 0.035, vy - h * 0.55 * 0.035 * 0.92), (vx + side * w * 0.42 * near, vy - h * 0.55 * near * 0.92),
               (vx + side * w * 0.42 * near, vy + h * 0.55 * near), (vx + side * w * 0.42 * 0.035, vy + h * 0.55 * 0.035)]
        d.polygon(pts, fill=WALL)
        for k in range(40):
            f = (k / 40) ** 2
            sx = vx + side * w * 0.42 * (0.035 + f * near)
            d.line([(sx, vy - h * 0.55 * (0.035 + f * near) * 0.92), (sx, vy + h * 0.55 * (0.035 + f * near))], fill=(170, 150, 76), width=max(1, int(f * 6)))
    # ceiling and floor planes
    for top in (True, False):
        k = -0.92 if top else 1.0
        pts = [(vx - w * 0.42 * 0.035, vy + k * h * 0.55 * 0.035), (vx + w * 0.42 * 0.035, vy + k * h * 0.55 * 0.035),
               (vx + w * 0.42 * near, vy + k * h * 0.55 * near), (vx - w * 0.42 * near, vy + k * h * 0.55 * near)]
        d.polygon(pts, fill=CEIL if top else CARPET)
    # fluorescent panels on the ceiling, receding
    glow = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    for k in range(1, 14):
        f = (k / 14) ** 2.0
        s = 0.035 + f * near
        cy = vy - h * 0.55 * s * 0.92 * 0.8
        pw, ph = w * 0.42 * s * 0.18, h * 0.55 * s * 0.05
        flick = 0.35 if k == 9 else 1.0
        gd.rectangle([vx - pw, cy - ph, vx + pw, cy + ph], fill=LIGHT + (int(255 * flick),))
    halo = glow.filter(ImageFilter.GaussianBlur(28))
    img.paste(halo, (0, 0), halo); img.paste(glow, (0, 0), glow)
    # far silhouette standing in the corridor
    sil = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    sd = ImageDraw.Draw(sil)
    hx, hy, sc = vx + 18, vy + 30, 1.0
    sd.ellipse([hx - 7, hy - 78, hx + 7, hy - 62], fill=(8, 7, 4, 235))
    sd.polygon([(hx - 10, hy - 62), (hx + 10, hy - 62), (hx + 8, hy - 20), (hx + 6, hy + 30), (hx + 2, hy + 30), (hx, hy - 10), (hx - 2, hy + 30), (hx - 6, hy + 30), (hx - 8, hy - 20)], fill=(8, 7, 4, 235))
    sd.line([(hx - 10, hy - 58), (hx - 17, hy + 6)], fill=(8, 7, 4, 235), width=4)
    sd.line([(hx + 10, hy - 58), (hx + 17, hy + 6)], fill=(8, 7, 4, 235), width=4)
    img.paste(sil, (0, 0), sil.filter(ImageFilter.GaussianBlur(0.8)))
    # darken the left side for the UI, vignette, film grain
    over = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    od = ImageDraw.Draw(over)
    for x in range(w):
        a = int(max(0, 1 - x / (w * 0.62)) ** 1.3 * 215)
        od.line([(x, 0), (x, h)], fill=(10, 9, 5, a))
    img.paste(over, (0, 0), over)
    vig = Image.new("L", (w, h), 0)
    vd = ImageDraw.Draw(vig)
    for r in range(60):
        vd.ellipse([-w * 0.2 + r * 12, -h * 0.3 + r * 7, w * 1.2 - r * 12, h * 1.3 - r * 7], fill=int(r * 3.2))
    vig = vig.filter(ImageFilter.GaussianBlur(60))
    img = Image.composite(img, Image.new("RGB", (w, h), (0, 0, 0)), vig)
    noise = Image.effect_noise((w, h), 22).convert("RGB")
    img = Image.blend(img, noise, 0.06)
    return img


def emblem(size=256):
    im = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    s = size
    d.rounded_rectangle([s * 0.06, s * 0.06, s * 0.94, s * 0.94], radius=s * 0.12, fill=(28, 24, 12, 255), outline=(201, 162, 39, 255), width=max(2, s // 28))
    # exit door with light spilling out
    d.rectangle([s * 0.33, s * 0.24, s * 0.67, s * 0.82], fill=(255, 246, 200, 255))
    d.rectangle([s * 0.33, s * 0.24, s * 0.5, s * 0.82], fill=(120, 100, 40, 255))
    d.ellipse([s * 0.44, s * 0.5, s * 0.48, s * 0.54], fill=(30, 26, 12, 255))
    d.rectangle([s * 0.24, s * 0.82, s * 0.76, s * 0.86], fill=(201, 162, 39, 255))
    return im


bg = background()
bg.save(os.path.join(OUT, "background.png"), optimize=True)
em = emblem(256)
em.save(os.path.join(OUT, "emblem.png"))
emblem(256).save(os.path.join(OUT, "launcher.ico"), sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
print("wrote", OUT)
