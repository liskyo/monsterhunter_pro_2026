const fs = require("fs");
const path = require("path");

const root = path.resolve(__dirname, "..");
const src = path.join(root, "DesignData/02_Equipment/equipment.json");
const out = path.join(root, "DesignData/02_Equipment/武器.md");

const list = JSON.parse(fs.readFileSync(src, "utf8"));
const lines = list.map((o, i) => `${i + 1}.${o["名稱"]}`);
fs.writeFileSync(out, lines.join("\n") + "\n", "utf8");
console.log("wrote", out, lines.length);
