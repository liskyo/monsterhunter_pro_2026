import { spawnSync } from "child_process";
import { readFileSync, writeFileSync, existsSync, mkdirSync, rmSync, cpSync } from "fs";
import { join } from "path";
import { fileURLToPath } from "url";

const __dirname = fileURLToPath(new URL(".", import.meta.url));
const ROOT = join(__dirname, "..");
const envPath = join(ROOT, ".env");
const envExamplePath = join(ROOT, ".env.example");

// 確保 Windows 平台下強制使用 cmd.exe 執行 shell，避免 PowerShell Execution Policy 封鎖
const cmdShell = process.platform === "win32" ? "cmd.exe" : true;

console.log("=========================================");
console.log("   Monster Hunter 2026 一鍵測試部署工具   ");
console.log("=========================================\n");

// 1. 確保 .env 檔案存在
if (!existsSync(envPath)) {
  console.log("[-] 找不到 .env，正在複製 .env.example...");
  cpSync(envExamplePath, envPath);
}

// 2. 啟動本地 Supabase (Supabase CLI 會自帶偵測 Docker 是否啟動)
console.log("[1/5] 正在啟動本地 Supabase (Docker)...");
const startRes = spawnSync("npx", ["--yes", "supabase@latest", "start"], {
  cwd: join(ROOT, "Backend"),
  shell: cmdShell,
  stdio: "inherit"
});

if (startRes.status !== 0) {
  console.error("\n[錯誤] 啟動 Supabase 失敗！請確認 Docker Desktop 已完全啟動（右下角綠燈），且無端口衝突。");
  process.exit(1);
}
// 強制啟動 edge-runtime 容器，防止因其處於 Exited 狀態導致 status 無法印出金鑰
spawnSync("docker", ["start", "supabase_edge_runtime_monsterhunter_2026"], {
  shell: cmdShell,
  stdio: "ignore"
});
// 3. 獲取本地 Supabase 金鑰狀態
console.log("\n[2/5] 獲取本地 Supabase 狀態與金鑰...");
const statusRes = spawnSync("npx", ["--yes", "supabase@latest", "status"], {
  cwd: join(ROOT, "Backend"),
  shell: cmdShell,
  encoding: "utf8"
});

const stdout = (statusRes.stdout || "") + "\n" + (statusRes.stderr || "");
const urlMatch = stdout.match(/(?:API URL|Project URL)\s*[^a-z0-9\s]*\s*(https?:\/\/[^\s]+)/i);
const anonMatch = stdout.match(/(?:anon key|Publishable)\s*[^a-z0-9\s]*\s*([a-z0-9_\-\.]+)/i);
const serviceRoleMatch = stdout.match(/(?:service_role key|Secret)\s*[^a-z0-9\s]*\s*([a-z0-9_\-\.]+)/i);
const studioMatch = stdout.match(/(?:Studio URL|Studio)\s*[^a-z0-9\s]*\s*(https?:\/\/[^\s]+)/i);

if (!urlMatch || !anonMatch || !serviceRoleMatch) {
  console.error("\n[錯誤] 無法解析 Supabase 狀態！輸出內容：\n", stdout);
  process.exit(1);
}

const url = urlMatch[1].trim();
const anon = anonMatch[1].trim();
const serviceRole = serviceRoleMatch[1].trim();
const studioUrl = studioMatch ? studioMatch[1].trim() : "http://127.0.0.1:54323";

console.log(`✓ 成功解析金鑰：`);
console.log(`  - API URL: ${url}`);
console.log(`  - Studio URL: ${studioUrl}`);

// 4. 更新 .env 檔案
console.log("\n[3/5] 正在將金鑰寫入 .env...");
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

// 5. 同步金鑰至 Unity 專案
console.log("\n[4/5] 正在同步金鑰至 Unity 專案資產...");
const syncRes = spawnSync("node", ["scripts/sync-unity-supabase-from-env.mjs"], {
  cwd: ROOT,
  shell: cmdShell,
  stdio: "inherit"
});
if (syncRes.status !== 0) {
  console.warn("[警告] 同步金鑰至 Unity 失敗，請確認 Unity 專案目錄結構。");
}

// 6. 同步 DesignData 至 Unity StreamingAssets
console.log("\n[5/5] 正在同步 DesignData 至 Unity StreamingAssets...");
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

// 7. 匯入企劃資料至本地資料庫
console.log("\n[追加] 正在匯入企劃資料至本地資料庫...");
const importRes = spawnSync("node", ["scripts/import-design-data.mjs"], {
  cwd: ROOT,
  shell: cmdShell,
  stdio: "inherit"
});
if (importRes.status !== 0) {
  console.warn("[警告] 匯入企劃資料至本地資料庫失敗。");
}

// 8. 寫入 Web 測試端 config.js
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
