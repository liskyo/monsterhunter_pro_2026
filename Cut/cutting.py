"""
將一張「精靈圖表 / 網格圖」依行列均分裁切，並依 JSON 清單依序命名輸出。
僅處理本機檔案；請確保你對輸入圖片有使用權。

若你只有「每隻魔物一張全身圖」、沒有拼好的大表，請改用同資料夾的
organize_full_images.py（批次命名並可選做圖示）。

依賴：pip install pillow

JSON 格式：陣列，每筆至少要有 "id"（例如 MON_001），順序須對應
  由左而右、由上而下的格子。

輸出：預設 id_全身圖.png；加 --make-icons 時另存 id_圖示.png（置中裁方塊再縮放）。
預設網格 4x4；全身／圖示預設寫入 GameClient 對應資料夾。

範例 tiles.json:
  [ {"id": "MON_001"}, {"id": "MON_002"}, ... ]
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

try:
    from PIL import Image, ImageOps
except ImportError:
    print("請先安裝 Pillow：pip install pillow", file=sys.stderr)
    sys.exit(1)


def make_icon_from_crop(crop: Image.Image, size: int) -> Image.Image:
    """由裁切後的全身格圖做方形圖示（置中裁切 + 等比塞滿）。"""
    w, h = crop.size
    side = min(w, h)
    left = (w - side) // 2
    top = (h - side) // 2
    square = crop.crop((left, top, left + side, top + side))
    return ImageOps.fit(square, (size, size), method=Image.Resampling.LANCZOS)


def main() -> None:
    base = Path(__file__).resolve().parent
    repo_root = base.parent
    default_textures = repo_root / "GameClient" / "Assets" / "Textures" / "Monsters"
    default_icons = repo_root / "GameClient" / "Assets" / "UI" / "Icons" / "Monsters"

    parser = argparse.ArgumentParser(description="網格裁切圖片並依 JSON id 命名輸出")
    parser.add_argument(
        "--image",
        type=Path,
        default=base / "source.png",
        help="原始大圖路徑（預設：Cut/source.png）",
    )
    parser.add_argument(
        "--json",
        type=Path,
        default=base / "tiles.json",
        help="含 id 清單的 JSON（預設：Cut/tiles.json）",
    )
    parser.add_argument(
        "--out",
        type=Path,
        default=default_textures,
        help="全身圖輸出資料夾（預設：GameClient/.../Textures/Monsters）",
    )
    parser.add_argument(
        "--suffix-full",
        type=str,
        default="_全身圖",
        help="接在 id 後的全身圖檔名後綴（預設 _全身圖；不需時傳空字串 \"\"）",
    )
    parser.add_argument("--cols", type=int, default=4, help="橫向格數（預設 4）")
    parser.add_argument("--rows", type=int, default=4, help="縱向格數（預設 4）")
    parser.add_argument(
        "--inset",
        type=int,
        default=0,
        help="每格向內縮像素（去邊／分隔線），0 表示不切掉邊",
    )
    parser.add_argument(
        "--make-icons",
        action="store_true",
        help="每格裁切後另存方形縮小圖示（檔名 id_圖示.png）",
    )
    parser.add_argument(
        "--icons-out",
        type=Path,
        default=default_icons,
        help="圖示輸出資料夾（預設：GameClient/.../UI/Icons/Monsters）",
    )
    parser.add_argument(
        "--icon-size",
        type=int,
        default=256,
        help="圖示邊長像素（預設 256）",
    )
    args = parser.parse_args()

    image_path = args.image.resolve()
    json_path = args.json.resolve()
    out_dir = args.out.resolve()
    icons_dir = args.icons_out.resolve()

    if not image_path.is_file():
        print(f"找不到圖片：{image_path}", file=sys.stderr)
        sys.exit(1)
    if not json_path.is_file():
        print(f"找不到 JSON：{json_path}", file=sys.stderr)
        sys.exit(1)

    with open(json_path, "r", encoding="utf-8") as f:
        data = json.load(f)

    if not isinstance(data, list):
        print("JSON 頂層必須是陣列 [...]", file=sys.stderr)
        sys.exit(1)

    img = Image.open(image_path).convert("RGBA")
    w, h = img.size
    cols, rows = max(1, args.cols), max(1, args.rows)
    cell_w, cell_h = w // cols, h // rows

    if cell_w < 1 or cell_h < 1:
        print("圖片太小或行列數太大，無法裁切。", file=sys.stderr)
        sys.exit(1)

    inset = max(0, args.inset)
    if 2 * inset >= min(cell_w, cell_h):
        print("--inset 過大，會讓單格沒有剩餘像素。", file=sys.stderr)
        sys.exit(1)

    out_dir.mkdir(parents=True, exist_ok=True)
    if args.make_icons:
        icons_dir.mkdir(parents=True, exist_ok=True)

    suffix_full = args.suffix_full or ""
    icon_px = max(32, args.icon_size)
    print(
        f"圖片 {w}x{h}，每格約 {cell_w}x{cell_h}，網格 {cols}x{rows}，inset={inset}，"
        f"共 {len(data)} 筆 id；全身後綴「{suffix_full or '(無)'}」"
        + (f"；圖示 {icon_px}px -> {icons_dir}" if args.make_icons else "")
    )

    index = 0
    for row in range(rows):
        for col in range(cols):
            if index >= len(data):
                break

            item = data[index]
            if not isinstance(item, dict) or "id" not in item:
                print(f"第 {index} 筆缺少 \"id\" 欄位", file=sys.stderr)
                sys.exit(1)

            left = col * cell_w + inset
            upper = row * cell_h + inset
            right = (col + 1) * cell_w - inset
            lower = (row + 1) * cell_h - inset

            sprite = img.crop((left, upper, right, lower))
            sprite_id = str(item["id"]).strip()
            if not sprite_id:
                print(f"第 {index} 筆 id 為空", file=sys.stderr)
                sys.exit(1)

            full_name = f"{sprite_id}{suffix_full}.png"
            out_path = out_dir / full_name
            sprite.save(out_path)
            line = f"[{index + 1}/{len(data)}] {out_path.name}"
            if args.make_icons:
                icon_img = make_icon_from_crop(sprite, icon_px)
                icon_path = icons_dir / f"{sprite_id}_圖示.png"
                icon_img.save(icon_path, "PNG")
                line += f" + {icon_path.name}"
            print(line)

            index += 1
        if index >= len(data):
            break

    if index < len(data):
        print(
            f"\n注意：JSON 尚有 {len(data) - index} 筆未裁切（網格僅 {cols}x{rows}={cols * rows} 格）",
            file=sys.stderr,
        )

    print("裁切完成。")


if __name__ == "__main__":
    main()
