const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..");
const src = path.join(root, "DesignData/02_Equipment/skills.json");
const out = path.join(root, "DesignData/02_Equipment/技能.md");

const list = JSON.parse(fs.readFileSync(src, "utf8"));
const lines = list.map((o, i) => `${i + 1}.${o["名稱"]}`);
fs.writeFileSync(out, lines.join("\n") + "\n", "utf8");
console.error("wrote", out, lines.length);
