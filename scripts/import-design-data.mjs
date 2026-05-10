/**
 * 讀取 DesignData 下所有 .json，經 Supabase API 寫入 public.design_data_rows。
 * 需先在雲端執行 migration：Backend/supabase/migrations/20260511090000_design_data_rows.sql
 *
 * 環境變數（專案根目錄 .env）：
 *   SUPABASE_URL
 *   SUPABASE_SERVICE_ROLE_KEY  （匯入需繞過 RLS；勿放入遊戲客戶端）
 *
 * 用法：
 *   npm run import-design
 *   npm run import-design -- --dry-run
 *   npm run import-design:watch              # 開發時監聽 DesignData，變更後自動匯入
 *   npm run import-design:watch -- --initial
 */

import { createClient } from "@supabase/supabase-js";
import dotenv from "dotenv";
import { readdir, readFile } from "fs/promises";
import { join, relative } from "path";
import { fileURLToPath } from "url";

const __dirname = fileURLToPath(new URL(".", import.meta.url));
const ROOT = join(__dirname, "..");
const DESIGN_DATA_DIR = join(ROOT, "DesignData");

dotenv.config({ path: join(ROOT, ".env") });

const BATCH_SIZE = 400;
const TABLE = "design_data_rows";

/** @param {string | undefined} key */
function assertServiceRoleKey(key) {
  const k = key?.trim() ?? "";
  if (!k) {
    console.error(
      "SUPABASE_SERVICE_ROLE_KEY 為空。請到 Supabase Dashboard → Project Settings → API，\n" +
        "複製「Secret」金鑰（sb_secret_…）或 Legacy 的 service_role JWT（長串 eyJ…），勿使用 Publishable（sb_publishable_）。"
    );
    process.exit(1);
  }
  if (k.startsWith("sb_publishable_")) {
    console.error(
      "目前使用的是 Publishable（公開）金鑰，無法做伺服端寫入。請改為同一頁的 Secret（sb_secret_…）或 Legacy service_role JWT。"
    );
    process.exit(1);
  }
  const looksJwt = k.startsWith("eyJ") && k.split(".").length === 3;
  const looksNewSecret = k.startsWith("sb_secret_");
  if (!looksJwt && !looksNewSecret) {
    console.error(
      "SUPABASE_SERVICE_ROLE_KEY 格式不正確（過短或不像 Secret）。\n" +
        "請貼上完整一行的 Secret（sb_secret_…）或 legacy service_role JWT，勿截斷、勿填專案代號等非金鑰文字。"
    );
    process.exit(1);
  }
}

function parseArgs(argv) {
  return { dryRun: argv.includes("--dry-run") };
}

async function collectJsonFiles(dir, acc = []) {
  const entries = await readdir(dir, { withFileTypes: true });
  for (const ent of entries) {
    const full = join(dir, ent.name);
    if (ent.isDirectory()) {
      await collectJsonFiles(full, acc);
    } else if (ent.isFile() && ent.name.toLowerCase().endsWith(".json")) {
      acc.push(full);
    }
  }
  return acc;
}

function rowsFromParsed(relativePath, data) {
  if (Array.isArray(data)) {
    return data.map((payload, row_index) => ({
      source_path: relativePath,
      row_index,
      payload,
    }));
  }
  return [{ source_path: relativePath, row_index: 0, payload: data }];
}

async function main() {
  const { dryRun } = parseArgs(process.argv.slice(2));

  const url = process.env.SUPABASE_URL?.trim();
  const serviceKey = process.env.SUPABASE_SERVICE_ROLE_KEY?.trim();

  if (!dryRun) {
    if (!url) {
      console.error("請在 .env 設定 SUPABASE_URL。");
      process.exit(1);
    }
    assertServiceRoleKey(serviceKey);
  }

  const files = await collectJsonFiles(DESIGN_DATA_DIR);
  if (files.length === 0) {
    console.error(`找不到 JSON：${DESIGN_DATA_DIR}`);
    process.exit(1);
  }

  files.sort();

  const supabase =
    !dryRun && url && serviceKey
      ? createClient(url, serviceKey, {
          auth: { persistSession: false, autoRefreshToken: false },
        })
      : null;

  let totalRows = 0;

  for (const abs of files) {
    const relToDesign = relative(DESIGN_DATA_DIR, abs).split("\\").join("/");
    const raw = await readFile(abs, "utf8");
    let parsed;
    try {
      parsed = JSON.parse(raw);
    } catch (e) {
      console.error(`JSON 解析失敗：${relToDesign}`, e.message);
      process.exit(1);
    }

    const rows = rowsFromParsed(relToDesign, parsed);
    totalRows += rows.length;

    if (dryRun) {
      console.log(`[dry-run] ${relToDesign} → ${rows.length} 列`);
      continue;
    }

    const { error: delErr } = await supabase
      .from(TABLE)
      .delete()
      .eq("source_path", relToDesign);

    if (delErr) {
      console.error(`刪除舊資料失敗：${relToDesign}`, delErr.message);
      process.exit(1);
    }

    for (let i = 0; i < rows.length; i += BATCH_SIZE) {
      const chunk = rows.slice(i, i + BATCH_SIZE);
      const { error: insErr } = await supabase.from(TABLE).insert(chunk);
      if (insErr) {
        console.error(`寫入失敗：${relToDesign}（列 ${i}–${i + chunk.length}）`, insErr.message);
        process.exit(1);
      }
    }

    console.log(`✓ ${relToDesign} → ${rows.length} 列`);
  }

  if (dryRun) {
    console.log(`\n[dry-run] 共 ${files.length} 個檔案，約 ${totalRows} 列（未寫入資料庫）`);
  } else {
    console.log(`\n完成：${files.length} 個檔案，${totalRows} 列已寫入 ${TABLE}`);
  }
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
