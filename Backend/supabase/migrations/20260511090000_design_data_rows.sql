-- 企劃靜態資料：對應 DesignData/**/*.json 匯入列（payload 保留完整 JSON 結構）
create table if not exists public.design_data_rows (
  id uuid primary key default gen_random_uuid(),
  source_path text not null,
  row_index integer not null check (row_index >= 0),
  payload jsonb not null,
  updated_at timestamptz not null default now(),
  unique (source_path, row_index)
);

create index if not exists design_data_rows_source_path_idx
  on public.design_data_rows (source_path);

comment on table public.design_data_rows is 'DesignData JSON 匯入列：source_path 為相對 DesignData 的路徑（例如 01_Monsters/monsters.json）。';

alter table public.design_data_rows enable row level security;

-- 客戶端僅需讀取企劃資料；寫入交由 service_role（匯入腳本）繞過 RLS
create policy "design_data_rows_select_anon_authenticated"
  on public.design_data_rows
  for select
  to anon, authenticated
  using (true);
