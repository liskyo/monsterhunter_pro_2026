"""
僅有「全身圖」單張檔時：批次複製／轉成專案要的檔名，並可選用全身圖自動做方形圖示。

與 cutting.py（精靈表網格裁切）擇一使用；本腳本不裁切網格。

依賴：pip install pillow

預設輸出對齊 monsters.json：
  Assets/Textures/Monsters/MON_XXX_全身圖.png
  Assets/UI/Icons/Monsters/MON_XXX_圖示.png  （僅在指定 --make-icons 時寫入）

來源檔名須能辨識編號，例如：
  MON_001.png、MON_1.jpg、001.png（純數字會變成 MON_001，依 --prefix）
"""

from __future__ import annotations

import argparse
import re
import shutil
import sys
from pathlib import Path

try:
    from PIL import Image, ImageOps
except ImportError:
    print("請先安裝 Pillow：pip install pillow", file=sys.stderr)
    sys.exit(1)

# 從檔名擷取 MON_XXX（編號會補成三位，例如 MON_1 -> MON_001）
MON_PATTERN = re.compile(r"MON_(\d+)", re.IGNORECASE)


def resolve_mon_id(stem: str, prefix: str) -> str | None:
    stem = stem.strip()
    m = MON_PATTERN.search(stem)
    if m:
        return f"MON_{int(m.group(1)):03d}"
    # 檔名純數字：001.png → MON_001
    if stem.isdigit() and prefix:
        pfx = prefix.rstrip("_").upper()
        return f"{pfx}_{int(stem):03d}"
    return None


def load_rgba(path: Path) -> Image.Image:
    return Image.open(path).convert("RGBA")


def make_icon_from_full(img: Image.Image, size: int) -> Image.Image:
    """置中裁成正方形再縮放，避免變形。"""
    w, h = img.size
    side = min(w, h)
    left = (w - side) // 2
    top = (h - side) // 2
    square = img.crop((left, top, left + side, top + side))
    return ImageOps.fit(square, (size, size), method=Image.Resampling.LANCZOS)


def main() -> None:
    cut_dir = Path(__file__).resolve().parent
    repo_root = cut_dir.parent

    parser = argparse.ArgumentParser(description="全身圖批次整理到 Unity 魔物素材路徑")
    parser.add_argument(
        "--src",
        type=Path,
        default=cut_dir / "full",
        help="放原始全身圖的資料夾（預設：Cut/full）",
    )
    parser.add_argument(
        "--textures",
        type=Path,
        default=repo_root / "GameClient" / "Assets" / "Textures" / "Monsters",
        help="全身圖輸出目錄",
    )
    parser.add_argument(
        "--icons",
        type=Path,
        default=repo_root / "GameClient" / "Assets" / "UI" / "Icons" / "Monsters",
        help="圖示輸出目錄（需搭配 --make-icons）",
    )
    parser.add_argument(
        "--prefix",
        type=str,
        default="MON_",
        help="若檔名只有 001.png 這類，會組成 MON_001（預設 MON_）",
    )
    parser.add_argument(
        "--make-icons",
        action="store_true",
        help="由全身圖產生方形圖示（置中裁切再縮放）",
    )
    parser.add_argument(
        "--icon-size",
        type=int,
        default=256,
        help="圖示邊長像素（預設 256）",
    )
    parser.add_argument(
        "--dry-run",
        action="store_true",
        help="只列出將寫入的檔名，不實際寫檔",
    )
    args = parser.parse_args()

    src: Path = args.src.resolve()
    if not src.is_dir():
        print(f"找不到來源資料夾：{src}", file=sys.stderr)
        sys.exit(1)

    exts = {".png", ".jpg", ".jpeg", ".webp", ".bmp"}
    files = sorted(p for p in src.iterdir() if p.is_file() and p.suffix.lower() in exts)
    if not files:
        print(f"{src} 內沒有支援的圖片（{exts}）", file=sys.stderr)
        sys.exit(1)

    textures_out: Path = args.textures.resolve()
    icons_out: Path = args.icons.resolve()

    if not args.dry_run:
        textures_out.mkdir(parents=True, exist_ok=True)
        if args.make_icons:
            icons_out.mkdir(parents=True, exist_ok=True)

    done = 0
    skipped = 0

    for path in files:
        mon_id = resolve_mon_id(path.stem, args.prefix)
        if not mon_id:
            print(f"略過（無法辨識編號）：{path.name}", file=sys.stderr)
            skipped += 1
            continue

        full_name = f"{mon_id}_全身圖.png"
        full_dst = textures_out / full_name

        if args.dry_run:
            print(f"[dry-run] {path.name} -> {full_dst}")
            if args.make_icons:
                print(f"          -> {icons_out / (mon_id + '_圖示.png')}")
            done += 1
            continue

        img = load_rgba(path)
        img.save(full_dst, "PNG")
        print(f"全身圖 {full_dst.name}")

        if args.make_icons:
            icon = make_icon_from_full(img, max(32, args.icon_size))
            icon_path = icons_out / f"{mon_id}_圖示.png"
            icon.save(icon_path, "PNG")
            print(f"  圖示 {icon_path.name}")

        done += 1

    print(f"完成 {done} 張；略過 {skipped} 個檔案。")


if __name__ == "__main__":
    main()
