import { spawnSync } from "child_process";
import { readFileSync, writeFileSync, existsSync, mkdirSync, rmSync, cpSync } from "fs";
import { join } from "path";
import { fileURLToPath } from "url";

const __dirname = fileURLToPath(new URL(".", import.meta.url));
const ROOT = join(__dirname, "..");
const envPath = join(ROOT, ".env");
const envExamplePath = join(ROOT, ".env.example");

console.log("=========================================");
console.log("   Monster Hunter 2026 一鍵測試部署工具   ");
console.log("=========================================\n");

// 1. 確保 .env 檔案存在
if (!existsSync(envPath)) {
  console.log("[-] 找不到 .env，正在複製 .env.example...");
  cpSync(envExamplePath, envPath);
}

// 2. 檢查 Docker 是否正在執行
console.log("[1/6] 檢查 Docker 環境...");
const dockerCheck = spawnSync("docker", ["info"], { shell: true, stdio: "ignore" });
if (dockerCheck.status !== 0) {
  console.error("\n[錯誤] 無法連線至 Docker！請確保 Docker Desktop 已啟動，然後再試一次。");
  process.exit(1);
}
console.log("✓ Docker 正常執行中。");

// 3. 啟動本地 Supabase
console.log("\n[2/6] 正在啟動本地 Supabase (Docker)...");
const startRes = spawnSync("npx", ["--yes", "supabase@latest", "start"], {
  cwd: join(ROOT, "Backend"),
  shell: true,
  stdio: "inherit"
});

if (startRes.status !== 0) {
  console.error("\n[錯誤] 啟動 Supabase 失敗！請確認端口是否被占用，或 Docker 資源是否充足。");
  process.exit(1);
}

// 4. 獲取本地 Supabase 金鑰狀態
console.log("\n[3/6] 獲取本地 Supabase 狀態與金鑰...");
const statusRes = spawnSync("npx", ["--yes", "supabase@latest", "status"], {
  cwd: join(ROOT, "Backend"),
  shell: true,
  encoding: "utf8"
});

const stdout = statusRes.stdout || "";
const urlMatch = stdout.match(/API URL:\s*(https?:\/\/[^\s]+)/i);
const anonMatch = stdout.match(/anon key:\s*([^\s]+)/i);
const serviceRoleMatch = stdout.match(/service_role key:\s*([^\s]+)/i);
const studioMatch = stdout.match(/Studio URL:\s*(https?:\/\/[^\s]+)/i);

if (!urlMatch || !anonMatch || !serviceRoleMatch) {
  console.error("\n[錯誤] 無法解析 Supabase 狀態！輸出內容：\n", stdout);
  process.exit(1);
}

const url = urlMatch[1];
const anon = anonMatch[1];
const serviceRole = serviceRoleMatch[1];
const studioUrl = studioMatch ? studioMatch[1] : "http://127.0.0.1:54323";

console.log(`✓ 成功解析金鑰：`);
console.log(`  - API URL: ${url}`);
console.log(`  - Studio URL: ${studioUrl}`);

// 5. 更新 .env 檔案
console.log("\n[4/6] 正在將金鑰寫入 .env...");
let envContent = readFileSync(envPath, "utf8");

function setEnvVar(content, key, value) {
  const regex = new RegExp(`^#?\\s*${key}=.*$`, "m");
  if (regex.test(content)) {
    return content.replace(regex, `${key}=${value}`);
  } else {
    return content + `\n${key}=${value}`;
  }
}

envContent = setEnvVar(envContent, "SUPABASE_URL", url);
envContent = setEnvVar(envContent, "SUPABASE_ANON_KEY", anon);
envContent = setEnvVar(envContent, "SUPABASE_SERVICE_ROLE_KEY", serviceRole);
writeFileSync(envPath, envContent, "utf8");
console.log("✓ .env 檔案更新成功。");

// 6. 同步金鑰至 Unity 專案
console.log("\n[5/6] 正在同步金鑰至 Unity 專案資產...");
const syncRes = spawnSync("node", ["scripts/sync-unity-supabase-from-env.mjs"], {
  cwd: ROOT,
  shell: true,
  stdio: "inherit"
});
if (syncRes.status !== 0) {
  console.warn("[警告] 同步金鑰至 Unity 失敗，請確認 Unity 專案目錄結構。");
}

// 7. 同步 DesignData 至 Unity StreamingAssets
console.log("\n[6/6] 正在同步 DesignData 至 Unity StreamingAssets...");
const srcData = join(ROOT, "DesignData");
const destData = join(ROOT, "GameClient", "Assets", "StreamingAssets", "DesignData");
try {
  if (existsSync(destData)) {
    rmSync(destData, { recursive: true, force: true });
  }
  mkdirSync(destData, { recursive: true });
  cpSync(srcData, destData, { recursive: true });
  console.log("✓ DesignData 成功同步至 Unity StreamingAssets。");
} catch (e) {
  console.warn("[警告] 同步 DesignData 至 StreamingAssets 失敗：", e.message);
}

// 8. 匯入企劃資料至本地資料庫
console.log("\n[追加] 正在匯入企劃資料至本地資料庫...");
const importRes = spawnSync("node", ["scripts/import-design-data.mjs"], {
  cwd: ROOT,
  shell: true,
  stdio: "inherit"
});
if (importRes.status !== 0) {
  console.warn("[警告] 匯入企劃資料至本地資料庫失敗。");
}

// 9. 寫入 Web 測試端 config.js
const webConfigPath = join(ROOT, "Web", "family-auth", "config.js");
try {
  const configJs = `window.SUPABASE_CONFIG = {
  url: "${url}",
  anon: "${anon}"
};`;
  writeFileSync(webConfigPath, configJs, "utf8");
  console.log("✓ 已自動生成 Web 測試端設定檔：Web/family-auth/config.js");
} catch (e) {
  console.warn("[警告] 無法寫入 Web 測試端設定檔：", e.message);
}

console.log("\n=========================================");
console.log("🎉 一鍵測試環境初始化完成！");
console.log(`👉 Supabase Studio: ${studioUrl}`);
console.log("👉 家人登入網頁測試：Web/family-auth/index.html (已自動填入金鑰)");
console.log("👉 Unity 專案已同步最新金鑰與企劃 JSON 資料！");
console.log("👉 請直接在 Unity 點擊 [Play] 按鈕即可開始遊玩測試！");
console.log("=========================================\n");
