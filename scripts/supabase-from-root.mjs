/**
 * 在 Backend 目錄執行 Supabase CLI（與 start-dev.bat 相同工作目錄）。
 *
 * 用法：
 *   npm run supabase:start
 *   npm run supabase:status
 *   node scripts/supabase-from-root.mjs db reset
 */

import { spawnSync } from "child_process";
import { join } from "path";
import { fileURLToPath } from "url";

const __dirname = fileURLToPath(new URL(".", import.meta.url));
const ROOT = join(__dirname, "..");
const BACKEND = join(ROOT, "Backend");

const extra = process.argv.slice(2);
const result = spawnSync(
  "npx",
  ["--yes", "supabase@latest", ...extra],
  {
    cwd: BACKEND,
    stdio: "inherit",
    shell: true,
    env: process.env,
  }
);

process.exit(result.status ?? 1);
