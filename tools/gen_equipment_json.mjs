import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, "..");

const CANON = ["大劍", "大錘", "太刀", "雙劍", "操蟲棍", "輕弩", "重弩", "弓"];
const ELEMENT_POOL = ["火", "水", "雷", "冰", "龍"];

function elemCount(mi) {
  if (mi <= 60) return 1;
  if (mi <= 100) return 2;
  return 3;
}

function elementsFor(mrow, mi, n) {
  const attrs = (mrow["屬性"] || []).filter((a) => a && String(a).trim() !== "無");
  const out = [];
  for (const a of attrs) {
    if (!out.includes(a)) out.push(a);
    if (out.length >= n) break;
  }
  let k = 0;
  while (out.length < n) {
    const e = ELEMENT_POOL[(mi * 3 + k) % ELEMENT_POOL.length];
    if (!out.includes(e)) out.push(e);
    k++;
  }
  return out.slice(0, n);
}

const typePhysMul = (t) =>
  ({
    大劍: 1.18,
    大錘: 1.12,
    太刀: 1.0,
    雙劍: 0.82,
    操蟲棍: 0.88,
    輕弩: 0.72,
    重弩: 1.22,
    弓: 0.92,
  })[t];

function recipe(mid3, star, withRare) {
  const ms = [1, 2, 3, 4, 5].map((j) => `MAT_${mid3}_${String(j).padStart(2, "0")}`);
  const [a, , c, d, e] = ms;
  if (withRare) {
    return [
      { 素材編號: c, 需求數量: 8 + star },
      { 素材編號: d, 需求數量: 6 + Math.floor(star / 2) },
      { 素材編號: e, 需求數量: star >= 9 ? 2 : 1 },
    ];
  }
  return [
    { 素材編號: a, 需求數量: 5 + Math.floor(star / 2) },
    { 素材編號: c, 需求數量: 6 + star },
    { 素材編號: d, 需求數量: 5 + Math.floor(star / 2) },
  ];
}

function gold(star, nqty, rare) {
  return Math.floor(900 + star * 140 + nqty * 22 + (rare ? 200 : 0));
}

function mulberry32(a) {
  return function () {
    let t = (a += 0x6d2b79f5);
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

function sampleFive(rng) {
  const ix = CANON.map((_, i) => i);
  for (let i = ix.length - 1; i > 0; i--) {
    const j = Math.floor(rng() * (i + 1));
    [ix[i], ix[j]] = [ix[j], ix[i]];
  }
  const pick = ix.slice(0, 5).sort((a, b) => a - b);
  return pick.map((i) => CANON[i]);
}

const monsters = JSON.parse(
  fs.readFileSync(path.join(ROOT, "DesignData/01_Monsters/monsters.json"), "utf8")
);
const monById = Object.fromEntries(monsters.map((m) => [m["魔物編號"], m]));

const out = [];
let wid = 0;
for (let mi = 1; mi <= 120; mi++) {
  const mid = `MON_${String(mi).padStart(3, "0")}`;
  const mrow = monById[mid];
  const name = mrow["名稱"];
  const star = parseInt(mrow["星級"], 10);
  const ec = elemCount(mi);
  const els = elementsFor(mrow, mi, ec);
  const attrStr = els.join("、");
  const rng = mulberry32(424242 + mi * 10007);
  const pick = sampleFive(rng);
  const rareIdx = Math.floor(rng() * 5);
  const ne = els.length;
  const baseElem = Math.floor(28 + star * 11);
  const elemDmg = Math.floor(baseElem * (1.0 + 0.4 * Math.max(0, ne - 1)));
  const mid3 = String(mi).padStart(3, "0");

  for (let j = 0; j < 5; j++) {
    wid++;
    const t = pick[j];
    const rare = j === rareIdx;
    const mats = recipe(mid3, star, rare);
    const nqty = mats.reduce((s, x) => s + x["需求數量"], 0);
    const phys = Math.floor((210 + star * 48) * typePhysMul(t));
    const ed = ne && attrStr ? elemDmg : 0;
    out.push({
      裝備編號: `WEP_${String(wid).padStart(3, "0")}`,
      名稱: `${name}${t}`,
      裝備類型: t,
      星級: star,
      裝備屬性: attrStr,
      基礎數值: { 物理傷害: phys, 屬性傷害: ed },
      圖片路徑: `Assets/Textures/Equipment/WEP_${String(wid).padStart(3, "0")}.png`,
      圖示路徑: `Assets/Textures/Equipment/WEP_${String(wid).padStart(3, "0")}.png`,
      合成配方: { 所需金幣: gold(star, nqty, rare), 需求素材: mats },
    });
  }
}

// 先寫入 tools 再手動搬移，避免長時間鎖住 DesignData（部分環境防毒會掃描較久）
const outPath = path.join(ROOT, "tools/_equipment_out.json");
fs.writeFileSync(outPath, JSON.stringify(out, null, 2), "utf8");
const rareCt = out.filter((r) =>
  r["合成配方"]["需求素材"].some((m) => m["素材編號"].endsWith("_05"))
).length;
console.log("wrote", outPath, "rows", out.length, "_05", rareCt);
for (const mi of [1, 60, 61, 100, 101, 120]) {
  console.log("MON", mi, out[(mi - 1) * 5]["裝備屬性"]);
}
