"""Generates the WinDownloader / Win Setup logo PNGs and .ico files (run once; outputs are committed)."""
from PIL import Image, ImageDraw
import os

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def draw_logo(size: int, setup: bool = False) -> Image.Image:
    s = size
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    # rounded square, blue gradient-ish (two tones)
    r = int(s * 0.22)
    d.rounded_rectangle([0, 0, s - 1, s - 1], radius=r, fill=(37, 99, 235, 255))
    d.rounded_rectangle([int(s * 0.04), int(s * 0.04), s - 1 - int(s * 0.04), int(s * 0.42)], radius=r, fill=(96, 165, 250, 40))
    w = max(2, int(s * 0.11))  # stroke
    cx = s / 2
    if not setup:
        # down arrow into a tray
        d.line([(cx, s * 0.22), (cx, s * 0.58)], fill="white", width=w)
        d.line([(s * 0.32, s * 0.42), (cx, s * 0.60), (s * 0.68, s * 0.42)], fill="white", width=w, joint="curve")
        d.line([(s * 0.22, s * 0.68), (s * 0.22, s * 0.80), (s * 0.78, s * 0.80), (s * 0.78, s * 0.68)], fill="white", width=w, joint="curve")
    else:
        # a box with a check mark (installer)
        d.rounded_rectangle([s * 0.2, s * 0.26, s * 0.8, s * 0.8], radius=int(s * 0.08), outline="white", width=w)
        d.line([(s * 0.34, s * 0.54), (s * 0.46, s * 0.66), (s * 0.68, s * 0.42)], fill="white", width=w, joint="curve")
    return img


def save_set(name: str, setup: bool, out_dir: str):
    os.makedirs(out_dir, exist_ok=True)
    big = draw_logo(512, setup)
    big.save(os.path.join(out_dir, "logo.png"))
    sizes = [16, 24, 32, 48, 64, 128, 256]
    frames = [draw_logo(z, setup) for z in sizes]
    frames[-1].save(os.path.join(out_dir, "app.ico"), format="ICO", sizes=[(z, z) for z in sizes], append_images=frames[:-1])
    print("wrote", name, "->", out_dir)


if __name__ == "__main__":
    save_set("WinDownloader", False, os.path.join(ROOT, "src", "WinDownloader", "Assets"))
    save_set("WinSetup", True, os.path.join(ROOT, "src", "WinSetup", "Assets"))
