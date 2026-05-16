/**
 * 將 skills.json 中 SKL_021–040「通用技能_XX」改為 MH 常見技能名稱（不改數值／ID／關聯）。
 */
const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..");
const p = path.join(root, "DesignData/02_Equipment/skills.json");

const FLAVOR = [
  ["快吃", "加快吃肉與部分消耗品的使用速度，減少硬直時間。"],
  ["減氣攻擊", "攻擊附帶減氣效果，更容易讓魔物進入疲勞狀態。"],
  ["地質學", "提升採掘與礦點互動效率，較易取得額外掉落。"],
  ["植生學", "草本、蘑菇等採集互動更為得心應手。"],
  ["滿足感", "使用部分消耗品時有機率不減少持有數。"],
  ["隱密", "降低魔物對你的警戒與發現距離。"],
  ["昏厥耐性", "降低陷入昏厥狀態的機率與持續時間影響。"],
  ["睡眠耐性", "降低睡眠狀態對行動的影響。"],
  ["麻痹耐性", "降低陷入麻痹狀態的機率與影響。"],
  ["爆破耐性", "降低爆破異常的累積與爆發傷害影響。"],
  ["納刀術", "收刀與拔刀的動作更為順暢，利於走位與使用道具。"],
  ["體術", "翻滾與固定耐力消耗的動作更為有效率。"],
  ["跑者", "持續消耗耐力的行動（如奔跑）更為省耐。"],
  ["耐暑", "減輕炎熱區域帶來的耐力或體力懲罰。"],
  ["耐寒", "減輕寒冷區域帶來的耐力或體力懲罰。"],
  ["砥石使用高速化", "磨刀等維護行為更迅速，縮短真空期。"],
  ["耐震", "減輕震地類招式造成的硬直與失衡。"],
  ["風壓耐性", "抵抗一般風壓造成的人立足受阻。"],
  ["龍風壓耐性", "抵抗強烈風壓與威壓類阻斷效果。"],
  ["節彈", "弩砲與弓類彈藥／瓶消耗更有效率。"],
];

const skills = JSON.parse(fs.readFileSync(p, "utf8"));

for (let i = 0; i < FLAVOR.length; i++) {
  const sklNum = 21 + i;
  const id = `SKL_${String(sklNum).padStart(3, "0")}`;
  const row = skills.find((s) => s["技能編號"] === id);
  if (!row) {
    console.error("missing", id);
    process.exit(1);
  }
  const [name, desc] = FLAVOR[i];
  row["名稱"] = name;
  row["描述"] = desc;
  for (const lv of row["各等級效果"]) {
    const n = lv["等級"];
    lv["效果描述"] = `${name} Lv.${n}：提升相關數值`;
  }
}

const dump = JSON.stringify(skills, null, 2) + "\n";
fs.writeFileSync(p, dump, "utf8");

const bad = skills.filter((s) => /通用技能/.test(s["名稱"] + (s["描述"] || "")));
if (bad.length) {
  console.error("still has 通用技能:", bad.map((b) => b["技能編號"]));
  process.exit(1);
}
console.error("ok", skills.length, "skills, 通用技能 cleared");
