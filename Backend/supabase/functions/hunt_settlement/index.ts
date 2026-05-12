import "jsr:@supabase/functions-js/edge-runtime.d.ts";

/** 與 DesignData/01_Monsters/drop_rates.json 同步；邏輯對齊 GameClient DropRewardResolver。 */
interface DropRateRow {
  魔物編號: string;
  素材編號: string;
  素材名稱: string;
  掉落機率: number;
  掉落條件: string;
}

interface ConsumedItem {
  item_id: string;
  quantity: number;
}

interface HuntSettlementRequest {
  player_id?: string;
  monster_id?: string;
  battle_duration_seconds?: number;
  consumed_items?: ConsumedItem[];
  /** 與客戶端 DropRewardResolver 條件字串一致，如「基本擊殺」「破壞部位」「切斷尾巴」。未送時後端預設僅「基本擊殺」。 */
  fulfilled_drop_conditions?: string[];
}

interface MaterialEntry {
  item_id: string;
  name: string;
  quantity: number;
}

let _cachedDropRows: DropRateRow[] | null = null;

async function loadDropRates(): Promise<DropRateRow[]> {
  if (_cachedDropRows) return _cachedDropRows;
  const url = new URL("./drop_rates.json", import.meta.url);
  const text = await Deno.readTextFile(url);
  _cachedDropRows = JSON.parse(text) as DropRateRow[];
  return _cachedDropRows;
}

function resolveDrops(
  monsterId: string,
  conditions: Set<string>,
  rows: DropRateRow[],
): MaterialEntry[] {
  const hits: MaterialEntry[] = [];
  for (const row of rows) {
    if (row.魔物編號 !== monsterId) continue;
    if (!row.掉落條件 || !conditions.has(row.掉落條件)) continue;
    if (row.掉落機率 <= 0) continue;
    if (Math.random() >= row.掉落機率) continue;
    hits.push({
      item_id: row.素材編號,
      name: row.素材名稱,
      quantity: 1,
    });
  }
  return mergeQuantities(hits);
}

function mergeQuantities(items: MaterialEntry[]): MaterialEntry[] {
  const map = new Map<string, MaterialEntry>();
  for (const it of items) {
    const prev = map.get(it.item_id);
    if (prev) prev.quantity += it.quantity;
    else map.set(it.item_id, { ...it });
  }
  return [...map.values()];
}

Deno.serve(async (req: Request) => {
  if (req.method === "OPTIONS") {
    return new Response("ok", { headers: corsHeaders() });
  }

  if (req.method !== "POST") {
    return jsonResponse(405, {
      ok: false,
      message: "Method not allowed; use POST with JSON body.",
    });
  }

  let body: HuntSettlementRequest;
  try {
    body = (await req.json()) as HuntSettlementRequest;
  } catch {
    return jsonResponse(400, { ok: false, message: "Invalid JSON body" });
  }

  const monsterId = body.monster_id?.trim();
  if (!monsterId) {
    return jsonResponse(400, { ok: false, message: "monster_id is required" });
  }

  const rawConds = body.fulfilled_drop_conditions;
  const conds = new Set<string>(
    rawConds && rawConds.length > 0
      ? rawConds.map((c) => c.trim()).filter(Boolean)
      : ["基本擊殺"],
  );

  let rows: DropRateRow[];
  try {
    rows = await loadDropRates();
  } catch (e) {
    console.error("drop_rates load failed", e);
    return jsonResponse(500, {
      ok: false,
      message: "Server: failed to load drop_rates.json",
    });
  }

  const materials = resolveDrops(monsterId, conds, rows);

  return jsonResponse(200, {
    ok: true,
    message: "hunt_settlement",
    player_id: body.player_id ?? null,
    monster_id: monsterId,
    battle_duration_seconds: body.battle_duration_seconds ?? null,
    fulfilled_drop_conditions: [...conds],
    consumed_items: body.consumed_items ?? [],
    materials,
    /** 與 materials 相同資料，供客戶端別名解析 */
    rewards: materials,
  });
});

function jsonResponse(status: number, data: Record<string, unknown>) {
  return new Response(JSON.stringify(data), {
    status,
    headers: { ...corsHeaders(), "Content-Type": "application/json" },
  });
}

function corsHeaders(): Record<string, string> {
  return {
    "Access-Control-Allow-Origin": "*",
    "Access-Control-Allow-Headers":
      "authorization, x-client-info, apikey, content-type",
    "Access-Control-Allow-Methods": "POST, OPTIONS",
  };
}
