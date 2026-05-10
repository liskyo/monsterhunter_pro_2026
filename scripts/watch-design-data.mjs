/**
 * 開發用：監聽 DesignData/**/*.json，變更後自動執行與「npm run import-design」相同的匯入（覆寫雲端列）。
 *
 * 用法：
 *   npm run import-design:watch
 *   npm run import-design:watch -- --initial   # 啟動時先完整匯入一次
 *
 * 節流：預設 800ms 內多次存檔只觸發一次；可設環境變數 DESIGN_IMPORT_DEBOUNCE_MS=1200
 */

import chokidar from "chokidar";
import { spawn } from "child_process";
import { join } from "path";
import { fileURLToPath } from "url";

const __dirname = fileURLToPath(new URL(".", import.meta.url));
const ROOT = join(__dirname, "..");
const DESIGN_ROOT = join(ROOT, "DesignData");
const IMPORT_SCRIPT = join(ROOT, "scripts/import-design-data.mjs");

const DEBOUNCE_MS = Math.max(
  200,
  Number.parseInt(String(process.env.DESIGN_IMPORT_DEBOUNCE_MS || "800"), 10) || 800
);

const syncOnStart = process.argv.slice(2).includes("--initial");

let timer = null;
let running = false;
let pending = false;

function runImport() {
  if (running) {
    pending = true;
    return;
  }
  running = true;
  console.log("\n[design-watch] 執行 import-design …\n");
  const child = spawn(process.execPath, [IMPORT_SCRIPT], {
    cwd: ROOT,
    stdio: "inherit",
    env: process.env,
  });
  child.on("exit", (code) => {
    running = false;
    if (code !== 0) console.error("[design-watch] import-design 結束碼", code);
    if (pending) {
      pending = false;
      scheduleImport();
    }
  });
}

function scheduleImport() {
  clearTimeout(timer);
  timer = setTimeout(runImport, DEBOUNCE_MS);
}

const watcher = chokidar.watch(DESIGN_ROOT, {
  ignoreInitial: true,
  persistent: true,
  awaitWriteFinish: { stabilityThreshold: 200, pollInterval: 100 },
});

watcher.on("all", (event, filePath) => {
  if (!String(filePath).toLowerCase().endsWith(".json")) return;
  if (event === "error") return;
  const rel = filePath.replaceAll("\\", "/");
  console.log(`[design-watch] ${event}: ${rel}`);
  scheduleImport();
});

console.log(`[design-watch] 監聽：${DESIGN_ROOT.replaceAll("\\", "/")}`);
console.log(
  `[design-watch] 儲存 JSON 後約 ${DEBOUNCE_MS}ms 會自動覆寫 Supabase（等同 npm run import-design）`
);
console.log("[design-watch] 結束請按 Ctrl+C\n");

if (syncOnStart) {
  console.log("[design-watch] --initial：先執行一次完整匯入\n");
  runImport();
}
