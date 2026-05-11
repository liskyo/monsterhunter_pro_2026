/**
 * 將專案根目錄 .env 的 SUPABASE_URL、SUPABASE_ANON_KEY 寫入
 * GameClient/Assets/Resources/SupabaseRuntimeConfig.asset（YAML）。
 *
 * 用途：本機 Docker 或雲端 Supabase 切換時，Unity Play 與 Node 腳本共用同一組設定。
 *
 * 用法：npm run sync:unity-env
 */

import dotenv from "dotenv";
import { readFileSync, writeFileSync } from "fs";
import { join } from "path";
import { fileURLToPath } from "url";

const __dirname = fileURLToPath(new URL(".", import.meta.url));
const ROOT = join(__dirname, "..");
const ASSET = join(
  ROOT,
  "GameClient",
  "Assets",
  "Resources",
  "SupabaseRuntimeConfig.asset"
);

dotenv.config({ path: join(ROOT, ".env") });

const url = process.env.SUPABASE_URL?.trim() ?? "";
const anon = process.env.SUPABASE_ANON_KEY?.trim() ?? "";

if (!url || !anon) {
  console.error(
    "缺少 SUPABASE_URL 或 SUPABASE_ANON_KEY。\n" +
      "請複製 .env.example 為 .env 並填入；本機 Docker 可執行 Backend 目錄下的 npx supabase status 取得。"
  );
  process.exit(1);
}

let yaml;
try {
  yaml = readFileSync(ASSET, "utf8");
} catch (e) {
  console.error("無法讀取 Unity 資產：", ASSET, e);
  process.exit(1);
}

const next = yaml
  .replace(/^  SupabaseUrl:.*$/m, `  SupabaseUrl: ${escapeYamlScalar(url)}`)
  .replace(/^  SupabaseAnonKey:.*$/m, `  SupabaseAnonKey: ${escapeYamlScalar(anon)}`);

if (next === yaml) {
  console.warn(
    "[sync-unity-supabase] 未替換任何欄位；請確認資產內含 '  SupabaseUrl:' 與 '  SupabaseAnonKey:' 行。"
  );
}

writeFileSync(ASSET, next, "utf8");
console.log("[sync-unity-supabase] 已更新", ASSET);
console.log("  SupabaseUrl:", url);
console.log("  （anon key 已寫入資產，請勿將含正式金鑰的變更提交至公開倉庫）");

/** @param {string} s */
function escapeYamlScalar(s) {
  if (/[:#\[\]{}"']/.test(s) || /^\s|\s$/.test(s)) {
    return `"${s.replace(/\\/g, "\\\\").replace(/"/g, '\\"')}"`;
  }
  return s;
}
