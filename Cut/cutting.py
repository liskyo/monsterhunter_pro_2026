#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
將大圖依固定格數裁切成小 PNG，不需 JSON。

**MAT 類預設（格子、檔名列／行、裁切區間、前綴、預設輸入圖）請只改下方的**
``MAT = MatCropSettings(...)`` **一次改齊。** 命令列 `-r/-c/-p` 會覆寫格子與前綴；區間／列號起算仍以 `MAT` 為準（除非日後再加參數）。

用法範例：
  python Cut/cutting.py 素材總覽.png -r 5 -c 5 -p MAT
  python Cut/cutting.py *.png -r 8 -c 8 -p MAT -o Assets/UI/Materials/icons
  python Cut/cutting.py --folder ./原始圖 -r 5 -c 5 -p ICON
  # 防具 8 張 5×5 → ARM_001～ARM_200（依序列出大圖，勿只用 --folder 而依賴預設字串排序）
  python Cut/cutting.py Cut/防具_1-25.png Cut/防具_26-50.png ... -r 5 -c 5 -p ARM --mon-flat -o GameClient/Assets/Textures/Equipment --bg-key 255,255,255 --trim
  python Cut/cutting.py --folder ./Cut -r 5 -c 5 -p ARM --mon-flat -o ...  # 資料夾內檔名改為「自然排序」
  python Cut/cutting.py --folder ./新魔物 -r 2 -c 2 -p MON --mon-flat --mon-start 57 \\
      -o GameClient/Assets/Textures/Monsters --bg-key 255,255,255 --bg-tol 22 --trim
  # 技能：上下內縮去鄰列尖角；左右勿內縮（易切掉最左欄邊框）。置中畫布可對齊六角：
  python Cut/cutting.py Cut/技能_1-40.png -r 8 -c 5 -p SKL --mon-flat -o GameClient/Assets/Textures/Skills --fixed-cell --sheet-trim-alpha --cell-inset 10,0,14,0 --cell-center
  # 手動再裁四邊（上,右,下,左 pixel，裁在格切之前，可與 --sheet-trim-alpha 併用）：
  python Cut/cutting.py ... --sheet-inset 0,0,0,0
"""

from __future__ import annotations

import argparse
import re
from dataclasses import dataclass
from pathlib import Path

from PIL import Image


def _natural_sort_key(p: Path) -> list:
    """檔名路徑自然排序：防具_2 在 防具_10 之前。"""
    s = str(p).lower()
    return [int(x) if x.isdigit() else x for x in re.split(r"(\d+)", s)]


# ═══════════════════════════════════════════════════════════════
# 【MAT 整合區】★ 只改這一段：``MatCropSettings(...)`` ★
#
# rows／cols　：大圖切成幾橫列 × 幾直行（與視覺格線一致）。
# asset_prefix ：檔名前綴，例如 MAT → MAT_001_01.png（命令列 -p 可覆寫）。
# name_row_first_1／name_col_first_1
#              ：左上角「第一格」在企劃上的列號／行號（例 6、1 → MAT_006_01）。
# crop_*_0     ：要「跳過」的格子時，設 0-based 半開區間 [start, end)；
#               全輸出就通通 None。（例只想 _01〜_04：crop_col_end_excl_0 = 4）
# input_if_no_cli：未傳圖、未用 --folder 時自動裁這張（"" = 一定要先命令列給檔）。
#
# 例：MAT_006_01〜MAT_010_04（大圖 5×5、不要第 5 直行）
#     name_row_first_1=6, crop_col_end_excl_0=4，crop 其餘 None。
# ═══════════════════════════════════════════════════════════════


@dataclass(frozen=True)
class MatCropSettings:
    rows: int = 5
    cols: int = 5
    asset_prefix: str = "MAT"
    name_row_first_1: int = 1
    name_col_first_1: int = 1
    crop_row_start_0: int | None = None
    crop_row_end_excl_0: int | None = None
    crop_col_start_0: int | None = None
    crop_col_end_excl_0: int | None = None
    input_if_no_cli: str = ""


MAT = MatCropSettings(
    rows=5,
    cols=5,
    asset_prefix="MAT",
    name_row_first_1=6,
    name_col_first_1=1,
    crop_row_start_0=None,
    crop_row_end_excl_0=None,
    crop_col_start_0=None,
    crop_col_end_excl_0=None,
    input_if_no_cli=(
        r"C:\Users\gc\Desktop\MonsterHunter_PRO_2026\monsterhunter_pro_2026\Cut\掉落物6-10.png"
    ),
)


def build_output_filename(
    prefix: str,
    *,
    mat_row_num_1: int,
    mat_col_num_1: int,
) -> str:
    pfx = prefix.strip("_") or MAT.asset_prefix.strip("_") or "MAT"
    return f"{pfx}_{mat_row_num_1:03d}_{mat_col_num_1:02d}.png"


def build_mon_flat_filename(prefix: str, index: int) -> str:
    """單一編號：MON_057.png（與 monsters.json 慣例一致）。"""
    pfx = prefix.strip("_") or "MON"
    return f"{pfx}_{index:03d}.png"


def chroma_to_transparent(
    im: Image.Image,
    key_rgb: tuple[int, int, int],
    tol: int,
) -> Image.Image:
    """將接近指定 RGB 的像素改為全透明（簡易去背／去底色）。"""
    im = im.convert("RGBA")
    kr, kg, kb = key_rgb
    tol = max(0, tol)
    tol_sq = float(tol * tol)
    px = im.load()
    w, h = im.size
    for y in range(h):
        for x in range(w):
            r, g, b, a = px[x, y]
            dr, dg, db = r - kr, g - kg, b - kb
            dist_sq = dr * dr + dg * dg + db * db
            if dist_sq <= tol_sq and a > 0:
                px[x, y] = (r, g, b, 0)
    return im


def trim_to_alpha_bbox(im: Image.Image) -> Image.Image:
    """依 alpha 非零區域裁掉多餘留白（去背後可讓精靈更貼邊）。注意：各張輸出尺寸會不一致。"""
    im = im.convert("RGBA")
    alpha = im.split()[3]
    bbox = alpha.getbbox()
    if bbox is None:
        return im
    return im.crop(bbox)


def _apply_sheet_trim_and_inset(
    img: Image.Image,
    *,
    trim_alpha_bbox: bool,
    inset_trbl: tuple[int, int, int, int] | None,
) -> Image.Image:
    """大圖先做有效區裁切，再均分格子；避免四週透明區讓格線與美術網格錯位。"""
    if trim_alpha_bbox:
        bb = img.split()[3].getbbox()
        if bb:
            img = img.crop(bb)
    if inset_trbl is not None:
        t, r, b, l = inset_trbl
        w0, h0 = img.size
        img = img.crop((l, t, w0 - r, h0 - b))
    return img


def _range_or_full(start: int | None, end_excl: int | None, max_n: int) -> range:
    """start=None 當 0；end_excl=None 當 max_n（用滿這一向度）。"""
    s = 0 if start is None else max(0, start)
    e = max_n if end_excl is None else min(max_n, end_excl)
    if e <= s:
        raise ValueError(f"裁切區間無效：start={start!r} end_excl={end_excl!r} max={max_n}")
    return range(s, e)


def _grid_cell_bounds(
    w: int, h: int, col: int, row: int, cols: int, rows: int
) -> tuple[int, int, int, int]:
    """將 [0,w)×[0,h) 無縫分割為 cols×rows：避免用固定 floor(w/cols) 丟餘數造成列／欄逐漸錯位。"""
    x0 = col * w // cols
    x1 = (col + 1) * w // cols
    y0 = row * h // rows
    y1 = (row + 1) * h // rows
    return x0, y0, x1, y1


def _apply_cell_inset(
    x0: int,
    y0: int,
    x1: int,
    y1: int,
    inset_trbl: tuple[int, int, int, int] | None,
) -> tuple[int, int, int, int]:
    """在格線矩形內再內縮（上,右,下,左），去掉六角／接縫伸進鄰格造成的上下尖角溢色。"""
    if inset_trbl is None:
        return x0, y0, x1, y1
    t, r, b, l = inset_trbl
    nx0 = x0 + l
    ny0 = y0 + t
    nx1 = x1 - r
    ny1 = y1 - b
    if nx1 <= nx0 or ny1 <= ny0:
        raise ValueError(
            f"--cell-inset 過大，裁切後寬或高為 0：格 ({x0},{y0})-({x1},{y1}) inset {(t,r,b,l)}"
        )
    return nx0, ny0, nx1, ny1


def _center_content_in_cell(sprite: Image.Image, fill_rgba: tuple[int, int, int, int]) -> Image.Image:
    """依 alpha 裁出格內圖示，再置於同尺寸畫布正中（避免左右欄因內縮切邊／視覺偏心）。"""
    sprite = sprite.convert("RGBA")
    w, h = sprite.size
    bb = sprite.split()[3].getbbox()
    if bb is None:
        return Image.new("RGBA", (w, h), fill_rgba)
    core = sprite.crop(bb)
    cw, ch = core.size
    if cw > w or ch > h:
        return sprite.copy()
    out = Image.new("RGBA", (w, h), fill_rgba)
    ox = (w - cw) // 2
    oy = (h - ch) // 2
    out.paste(core, (ox, oy), core)
    return out


def slice_one(
    image_path: Path,
    *,
    cols: int,
    rows: int,
    prefix: str,
    output_dir: Path | None,
    profile: MatCropSettings = MAT,
    mon_flat_start: int | None = None,
    bg_key: tuple[int, int, int] | None = None,
    bg_tol: int = 30,
    trim_alpha_bbox: bool = False,
    sheet_trim_alpha: bool = False,
    sheet_inset_trbl: tuple[int, int, int, int] | None = None,
    cell_inset_trbl: tuple[int, int, int, int] | None = None,
    cell_center: bool = False,
    cell_canvas_fill_rgba: tuple[int, int, int, int] = (255, 255, 255, 255),
) -> tuple[Path, int]:
    """裁切單張圖，回傳 (輸出資料夾, 產出張數)。mon_flat_start 有值時檔名為 MON_057.png 連號。"""
    cols = max(1, cols)
    rows = max(1, rows)

    img = Image.open(image_path).convert("RGBA")
    img = _apply_sheet_trim_and_inset(
        img,
        trim_alpha_bbox=sheet_trim_alpha,
        inset_trbl=sheet_inset_trbl,
    )
    w, h = img.size
    if w < cols or h < rows:
        raise ValueError(f"圖片太小或格子數過多：{w}x{h} / {cols}x{rows}")

    out = output_dir if output_dir is not None else Path("Cut") / image_path.stem
    out.mkdir(parents=True, exist_ok=True)
    prefix = prefix.strip("_") or profile.asset_prefix.strip("_") or "MAT"

    row_iter = _range_or_full(
        profile.crop_row_start_0,
        profile.crop_row_end_excl_0,
        rows,
    )
    col_iter = _range_or_full(
        profile.crop_col_start_0,
        profile.crop_col_end_excl_0,
        cols,
    )

    n = 0
    mon_idx = mon_flat_start
    for row in row_iter:
        for col in col_iter:
            x0, y0, x1, y1 = _grid_cell_bounds(w, h, col, row, cols, rows)
            x0, y0, x1, y1 = _apply_cell_inset(x0, y0, x1, y1, cell_inset_trbl)
            sprite = img.crop((x0, y0, x1, y1))
            if bg_key is not None:
                sprite = chroma_to_transparent(sprite, bg_key, bg_tol)
            if trim_alpha_bbox:
                sprite = trim_to_alpha_bbox(sprite)
            if cell_center:
                sprite = _center_content_in_cell(sprite, cell_canvas_fill_rgba)
            if mon_idx is not None:
                fname = build_mon_flat_filename(prefix, mon_idx)
                mon_idx += 1
            else:
                fname = build_output_filename(
                    prefix,
                    mat_row_num_1=profile.name_row_first_1 + row,
                    mat_col_num_1=profile.name_col_first_1 + col,
                )
            dest = out / fname
            sprite.save(dest)
            n += 1
            print(f"  [{n:3}] {dest}")

    print(f"完成：{image_path.name} → {n} 張，資料夾 {out.resolve()}")
    return out, n


def collect_inputs(paths: list[str], folder: str | None) -> list[Path]:
    ps: list[Path] = []
    for p in paths:
        pi = Path(p)
        if not pi.exists():
            print(f"[略過] 找不到檔案：{p}")
            continue
        ps.append(pi)
    had_explicit = len(ps) > 0

    if folder:
        fd = Path(folder)
        if not fd.is_dir():
            raise SystemExit(f"不是資料夾：{folder}")
        for ext in ("*.png", "*.PNG", "*.jpg", "*.jpeg", "*.webp"):
            ps.extend(fd.glob(ext))

    seen = set()
    uniq: list[Path] = []
    for p in ps:
        if p.is_file() and p.resolve() not in seen:
            seen.add(p.resolve())
            uniq.append(p)
    # 命令列明定的檔案順序必須保留（例如 ARM 連號對應 1–25、26–50…）；
    # 僅 --folder 時才依檔名自然排序。
    if not had_explicit:
        uniq.sort(key=_natural_sort_key)
    return uniq


def main() -> None:
    ap = argparse.ArgumentParser(
        description="格狀裁圖並自動命名（不需 JSON）。區間／列號起算見程式內 MAT。",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    ap.add_argument(
        "images",
        nargs="*",
        help=f"要裁切的圖；皆空則試用程式內 MAT.input_if_no_cli 或搭配 --folder",
    )
    ap.add_argument(
        "-folder",
        "--folder",
        metavar="DIR",
        dest="folder",
        help="此資料夾內所有 png/jpg/webp 各別裁一份",
    )
    ap.add_argument(
        "-r",
        "--rows",
        type=int,
        default=MAT.rows,
        help=f"縱向格數（default: MAT.rows=%(default)s）",
    )
    ap.add_argument(
        "-c",
        "--cols",
        type=int,
        default=MAT.cols,
        help=f"橫向格數（default: MAT.cols=%(default)s）",
    )
    ap.add_argument(
        "-p",
        "--prefix",
        default=MAT.asset_prefix,
        help=f"檔名前綴（default: MAT.asset_prefix=%(default)s）",
    )
    ap.add_argument(
        "-o",
        "--output",
        metavar="DIR",
        dest="output",
        help="輸出根目錄（單檔時可省略，預設 Cut/<檔名不含副檔>）",
    )
    ap.add_argument(
        "--mon-flat",
        action="store_true",
        help="檔名改為連號 MON_057.png（需搭配 --mon-start；多張輸入圖時序號延續）",
    )
    ap.add_argument(
        "--mon-start",
        type=int,
        default=1,
        metavar="N",
        help="--mon-flat 時第一張輸出編號（default: %(default)s）",
    )
    ap.add_argument(
        "--bg-key",
        metavar="R,G,B",
        dest="bg_key",
        default="",
        help="去背：鍵色 RGB（0–255），例 255,255,255；留空則不做鍵色去背",
    )
    ap.add_argument(
        "--bg-tol",
        type=int,
        default=38,
        help="鍵色距離容許值（愈大則吃掉愈多近似色；default: %(default)s）",
    )
    ap.add_argument(
        "--trim",
        action="store_true",
        help="依透明／alpha 外框裁掉多餘留白（各格輸出寬高會不同；要同尺寸請勿加此參數或使用 --fixed-cell）",
    )
    ap.add_argument(
        "--fixed-cell",
        action="store_true",
        help="每格輸出同寬高：僅依大圖寬÷列數、高÷行數裁矩形；強制不做鍵色去背、不做 alpha 裁邊（覆寫 --trim/--bg-key）",
    )
    ap.add_argument(
        "--sheet-trim-alpha",
        action="store_true",
        help="格切前先依整張大圖的 alpha 外框裁一刀（去除四週透明，格線較易對齊美術網格）",
    )
    ap.add_argument(
        "--sheet-inset",
        metavar="T,R,B,L",
        dest="sheet_inset",
        default="",
        help="格切前再裁四邊：上,右,下,左 像素；例 4,2,4,2；可與 --sheet-trim-alpha 併用",
    )
    ap.add_argument(
        "--cell-inset",
        metavar="T,R,B,L",
        dest="cell_inset",
        default="",
        help="每格矩形再內縮：上,右,下,左 像素。六角接縫建議只縮上下，左右填 0，例 10,0,14,0。",
    )
    ap.add_argument(
        "--cell-center",
        action="store_true",
        help="每格裁完後依 alpha 取內容，置於同寬高畫布正中（底為不透明白，與總管預覽一致）",
    )

    args = ap.parse_args()

    def _parse_bg_key(s: str) -> tuple[int, int, int] | None:
        s = (s or "").strip()
        if not s:
            return None
        parts = [p.strip() for p in s.replace("，", ",").split(",")]
        if len(parts) != 3:
            raise SystemExit(f"--bg-key 需三個數字：R,G,B，收到：{s!r}")
        return (int(parts[0]), int(parts[1]), int(parts[2]))

    def _parse_inset_trbl(s: str) -> tuple[int, int, int, int] | None:
        s = (s or "").strip()
        if not s:
            return None
        parts = [p.strip() for p in s.replace("，", ",").split(",")]
        if len(parts) != 4:
            raise SystemExit(f"--sheet-inset 需四個數字：上,右,下,左，收到：{s!r}")
        t, r, b, l = (int(parts[0]), int(parts[1]), int(parts[2]), int(parts[3]))
        if min(t, r, b, l) < 0:
            raise SystemExit("--sheet-inset 不可為負數")
        return (t, r, b, l)

    bg_key_parsed = _parse_bg_key(args.bg_key)
    sheet_inset_parsed = _parse_inset_trbl(args.sheet_inset)
    cell_inset_parsed = _parse_inset_trbl(args.cell_inset)
    trim_alpha = args.trim
    if args.fixed_cell:
        bg_key_parsed = None
        trim_alpha = False

    imgs: list[str] = list(args.images)
    if not imgs and not args.folder:
        d = (MAT.input_if_no_cli or "").strip()
        if d:
            imgs.append(d)

    targets = collect_inputs(imgs, args.folder)
    if not targets:
        ap.print_help()
        raise SystemExit(
            "請指定至少一張圖片、或 --folder，或在 MAT.input_if_no_cli 填入預設圖。"
        )

    root_out = Path(args.output) if args.output else None

    mon_next: int | None = args.mon_start if args.mon_flat else None

    for tp in targets:
        if root_out is None:
            od = None
        elif len(targets) == 1 or args.mon_flat:
            # 連號 MON 多張大圖時一律輸出到同一根目錄，避免每張大圖一層子資料夾
            od = root_out
        else:
            od = root_out / tp.stem

        out_dir, n_out = slice_one(
            tp,
            cols=args.cols,
            rows=args.rows,
            prefix=args.prefix,
            output_dir=od,
            profile=MAT,
            mon_flat_start=mon_next,
            bg_key=bg_key_parsed,
            bg_tol=args.bg_tol,
            trim_alpha_bbox=trim_alpha,
            sheet_trim_alpha=args.sheet_trim_alpha,
            sheet_inset_trbl=sheet_inset_parsed,
            cell_inset_trbl=cell_inset_parsed,
            cell_center=args.cell_center,
        )
        if mon_next is not None:
            mon_next += n_out


if __name__ == "__main__":
    main()
