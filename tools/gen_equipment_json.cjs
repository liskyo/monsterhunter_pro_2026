/** 產生 equipment.json：內嵌魔物名稱表 + 星級／屬性規則（避免讀檔被鎖） */
const fs = require("fs");
const path = require("path");
const ROOT = path.resolve(__dirname, "..");

/** 與 monsterlist.json MON_001–MON_120 順序一致 */
const MON_NAMES = [
  "青熊獸", "纏蛙", "大豬王", "搔鳥", "闢獸", "白兔獸", "赤甲獸", "大怪鳥", "毒妖鳥", "水獸",
  "土砂龍", "大名盾蟹", "奇猿狐", "潛口龍", "岩龍", "浮空龍", "泥魚龍", "蠻顎龍", "砂海龍", "飛雷龍",
  "雌火龍", "影蜘蛛", "鎌蟹", "化鮫", "跳狗龍", "眠狗龍", "毒狗龍", "慘爪龍", "風漂龍", "櫻火龍",
  "刺花蜘蛛", "冰牙龍", "迅龍", "電龍", "泡狐龍", "巨獸", "斬龍", "雄火龍", "角龍", "赫猿獸",
  "雷狼龍", "轟龍", "碎龍", "爆鎚龍", "尾錘龍", "炎戈龍", "油泥龍", "熔岩龍", "鎧龍", "黑狼鳥",
  "千刃龍", "海龍", "怨虎龍", "爆鱗龍", "蒼火龍", "黑蝕龍", "剛纏獸", "冰狼龍", "波龍", "鋼龍",
  "炎王龍", "霞龍", "麒麟", "恐暴龍", "金獅子", "滅盡龍", "冰咒龍", "天彗龍", "爵銀龍", "煌雷龍",
  "冥燈龍", "屍套龍", "溟龍", "天迴龍", "金火龍", "銀火龍", "獄狼龍", "炎妃龍", "黑星龍", "猛爆碎龍",
  "激昂金獅子", "紅蓮爆鱗龍", "戰損黑狼鳥", "怒食恐暴龍", "霸龍", "崩龍", "大海龍", "嵐龍", "浮岳龍", "老山龍",
  "巨戟龍", "蛇王龍", "骸龍", "大巖龍", "鏖魔角龍", "燼滅刃斬龍", "白疾風迅龍", "天眼泡狐龍", "青電主電龍", "銀嶺巨獸",
  "怨虎龍特殊個體", "傀異克服霞龍", "傀異克服鋼龍", "傀異克服炎王龍", "傀異克服天彗龍", "傀異克服天迴龍", "原初爵銀龍", "鎖刃龍", "冥赤龍", "絢輝龍",
  "地啼龍", "天地煌啼龍", "冥淵龍", "凍峰龍", "煌黑龍", "紅黑龍", "祖龍", "黑龍", "至天黑龍", "極致禁忌：白主",
];
const CANON = ["大劍", "大錘", "太刀", "雙劍", "操蟲棍", "輕弩", "重弩", "弓"];
const ELEMENT_POOL = ["火", "水", "雷", "冰", "龍"];

/** 與 monsters.json 一致之魔物編號 1–120 星級分段 */
function starFor(mi) {
  if (mi <= 10) return 1;
  if (mi <= 20) return 2;
  if (mi <= 30) return 3;
  if (mi <= 55) return 4;
  if (mi <= 80) return 5;
  if (mi <= 90) return 7;
  if (mi <= 100) return 8;
  if (mi <= 110) return 9;
  return 10;
}

/** 非「無」之魔物本體屬性（多屬性個體列完整）；其餘預設 ["無"] */
const ATTR_OVERRIDES = {
  6: ["冰"],
  7: ["火"],
  8: ["火"],
  9: ["毒"],
  10: ["水"],
  17: ["水"],
  18: ["火"],
  19: ["水"],
  20: ["雷"],
  21: ["火"],
  22: ["毒"],
  24: ["冰"],
  27: ["毒"],
  28: ["龍"],
  29: ["冰"],
  30: ["火"],
  31: ["毒"],
  32: ["冰"],
  34: ["雷"],
  35: ["水"],
  37: ["火"],
  38: ["火"],
  40: ["火"],
  41: ["雷"],
  43: ["火"],
  44: ["火"],
  46: ["火"],
  48: ["火"],
  49: ["火"],
  50: ["火"],
  52: ["雷"],
  53: ["龍"],
  54: ["火"],
  55: ["火"],
  56: ["龍"],
  58: ["冰"],
  59: ["水"],
  60: ["冰"],
  61: ["火"],
  62: ["龍"],
  63: ["雷"],
  64: ["龍"],
  65: ["雷"],
  66: ["龍"],
  67: ["冰"],
  68: ["龍"],
  69: ["龍"],
  70: ["雷"],
  71: ["龍"],
  72: ["龍"],
  73: ["水"],
  74: ["龍"],
  75: ["火"],
  76: ["火"],
  77: ["龍"],
  78: ["火"],
  79: ["龍"],
  80: ["火"],
  81: ["雷"],
  82: ["火"],
  83: ["火"],
  84: ["龍"],
  85: ["龍"],
  86: ["冰"],
  87: ["水"],
  88: ["龍"],
  91: ["龍"],
  93: ["龍"],
  96: ["火"],
  98: ["水"],
  99: ["雷"],
  100: ["冰"],
  101: ["龍"],
  102: ["龍"],
  103: ["冰"],
  104: ["火"],
  105: ["龍"],
  106: ["龍"],
  107: ["龍"],
  108: ["龍"],
  109: ["龍"],
  110: ["火"],
  111: ["龍"],
  112: ["火"],
  114: ["冰", "龍"],
  115: ["火", "冰", "雷"],
  116: ["龍"],
  117: ["龍"],
  118: ["龍"],
  119: ["龍"],
  120: ["龍"],
};

function baseAttrs(mi) {
  return ATTR_OVERRIDES[mi] ? [...ATTR_OVERRIDES[mi]] : ["無"];
}

function elemCount(mi) {
  if (mi <= 60) return 1;
  if (mi <= 100) return 2;
  return 3;
}

function elementsFor(mi, n) {
  const attrs = baseAttrs(mi).filter((a) => a && a !== "無");
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

const out = [];
let wid = 0;
for (let mi = 1; mi <= 120; mi++) {
  const name = MON_NAMES[mi - 1];
  const star = starFor(mi);
  const ec = elemCount(mi);
  const els = elementsFor(mi, ec);
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

const outPath = path.join(ROOT, "DesignData/02_Equipment/equipment.json");
const payload = JSON.stringify(out, null, 2);
fs.writeFileSync(outPath, payload, "utf8");
const rareCt = out.filter((r) =>
  r["合成配方"]["需求素材"].some((m) => m["素材編號"].endsWith("_05"))
).length;
console.error("equipment.json:", out.length, "筆武器,", rareCt, "筆含 MAT_*_05");
