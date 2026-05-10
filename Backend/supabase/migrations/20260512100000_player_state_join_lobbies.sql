-- PlayerState 雲端表：每位使用者資料隔離（RLS + auth.uid）
-- Join Code：供 Host/Guest 透過 RPC 建立／加入遊戲房（不含 NAT 穿透）

-- ---------------------------------------------------------------------------
-- profiles（對應 auth.users）
-- ---------------------------------------------------------------------------
create table if not exists public.profiles (
  id uuid primary key references auth.users (id) on delete cascade,
  display_name text,
  hr_rank int not null default 1,
  experience bigint not null default 0,
  zeni bigint not null default 0,
  current_equipment jsonb not null default '{}'::jsonb,
  active_pets jsonb not null default '{}'::jsonb,
  pouch jsonb not null default '[]'::jsonb,
  current_canteen_buff jsonb,
  last_login_at timestamptz,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

-- 若專案裡已存在舊版 public.profiles（例如僅有部分欄位），CREATE TABLE IF NOT EXISTS 不會更新結構，
-- 後續建立索引會報 column "display_name" does not exist。下列將缺的欄位補齊。
alter table public.profiles add column if not exists display_name text;
alter table public.profiles add column if not exists hr_rank integer default 1;
alter table public.profiles add column if not exists experience bigint default 0;
alter table public.profiles add column if not exists zeni bigint default 0;
alter table public.profiles add column if not exists current_equipment jsonb default '{}'::jsonb;
alter table public.profiles add column if not exists active_pets jsonb default '{}'::jsonb;
alter table public.profiles add column if not exists pouch jsonb default '[]'::jsonb;
alter table public.profiles add column if not exists current_canteen_buff jsonb;
alter table public.profiles add column if not exists last_login_at timestamptz;
alter table public.profiles add column if not exists created_at timestamptz default now ();
alter table public.profiles add column if not exists updated_at timestamptz default now ();

create index if not exists profiles_display_name_idx on public.profiles (display_name);

-- ---------------------------------------------------------------------------
-- warehouse_items（倉庫堆疊：每位使用者獨立）
-- ---------------------------------------------------------------------------
create table if not exists public.warehouse_items (
  id uuid primary key default gen_random_uuid (),
  owner_id uuid not null references auth.users (id) on delete cascade,
  item_id text not null,
  quantity int not null check (quantity >= 0),
  slot_index int,
  updated_at timestamptz not null default now (),
  unique (owner_id, item_id)
);

-- 舊表若已存在但缺欄位（CREATE TABLE IF NOT EXISTS 不會升級 schema）
alter table public.warehouse_items add column if not exists owner_id uuid references auth.users (id) on delete cascade;
alter table public.warehouse_items add column if not exists item_id text;
alter table public.warehouse_items add column if not exists quantity integer default 0 check (quantity >= 0);
alter table public.warehouse_items add column if not exists slot_index integer;
alter table public.warehouse_items add column if not exists updated_at timestamptz default now ();

create index if not exists warehouse_items_owner_idx on public.warehouse_items (owner_id);

-- ---------------------------------------------------------------------------
-- player_encyclopedia_progress（圖鑑：每位使用者／每隻魔物一列）
-- ---------------------------------------------------------------------------
create table if not exists public.player_encyclopedia_progress (
  id uuid primary key default gen_random_uuid (),
  owner_id uuid not null references auth.users (id) on delete cascade,
  monster_id text not null,
  monster_name text,
  kill_count int not null default 0,
  capture_count int not null default 0,
  part_break_count int not null default 0,
  research_level int not null default 0,
  unlocked_info jsonb not null default '{}'::jsonb,
  updated_at timestamptz not null default now (),
  unique (owner_id, monster_id)
);

alter table public.player_encyclopedia_progress add column if not exists owner_id uuid references auth.users (id) on delete cascade;
alter table public.player_encyclopedia_progress add column if not exists monster_id text;
alter table public.player_encyclopedia_progress add column if not exists monster_name text;
alter table public.player_encyclopedia_progress add column if not exists kill_count integer default 0;
alter table public.player_encyclopedia_progress add column if not exists capture_count integer default 0;
alter table public.player_encyclopedia_progress add column if not exists part_break_count integer default 0;
alter table public.player_encyclopedia_progress add column if not exists research_level integer default 0;
alter table public.player_encyclopedia_progress add column if not exists unlocked_info jsonb default '{}'::jsonb;
alter table public.player_encyclopedia_progress add column if not exists updated_at timestamptz default now ();

create index if not exists encyclopedia_owner_idx on public.player_encyclopedia_progress (owner_id);

-- ---------------------------------------------------------------------------
-- daily_hunt_records（每日狩獵挑戰：每人每天每個任務一列 → 兩人家計數獨立）
-- ---------------------------------------------------------------------------
create table if not exists public.daily_hunt_records (
  id uuid primary key default gen_random_uuid (),
  owner_id uuid not null references auth.users (id) on delete cascade,
  record_date date not null,
  daily_quest_id text not null,
  completed boolean not null default false,
  completion_count int not null default 0 check (completion_count >= 0),
  rewards_claimed boolean not null default false,
  reset_at timestamptz,
  updated_at timestamptz not null default now (),
  unique (owner_id, record_date, daily_quest_id)
);

alter table public.daily_hunt_records add column if not exists owner_id uuid references auth.users (id) on delete cascade;
alter table public.daily_hunt_records add column if not exists record_date date;
alter table public.daily_hunt_records add column if not exists daily_quest_id text;
alter table public.daily_hunt_records add column if not exists completed boolean default false;
alter table public.daily_hunt_records add column if not exists completion_count integer default 0 check (completion_count >= 0);
alter table public.daily_hunt_records add column if not exists rewards_claimed boolean default false;
alter table public.daily_hunt_records add column if not exists reset_at timestamptz;
alter table public.daily_hunt_records add column if not exists updated_at timestamptz default now ();

create index if not exists daily_hunt_owner_date_idx on public.daily_hunt_records (owner_id, record_date);

-- ---------------------------------------------------------------------------
-- join_lobbies（Join Code 配對；列表不外露，請用 RPC）
-- ---------------------------------------------------------------------------
create table if not exists public.join_lobbies (
  id uuid primary key default gen_random_uuid (),
  join_code text not null unique,
  host_user_id uuid not null references auth.users (id) on delete cascade,
  guest_user_id uuid references auth.users (id) on delete set null,
  status text not null default 'open' check (
    status in ('open', 'full', 'cancelled', 'expired')
  ),
  created_at timestamptz not null default now (),
  updated_at timestamptz not null default now (),
  expires_at timestamptz not null
);

alter table public.join_lobbies add column if not exists join_code text;
alter table public.join_lobbies add column if not exists host_user_id uuid references auth.users (id) on delete cascade;
alter table public.join_lobbies add column if not exists guest_user_id uuid references auth.users (id) on delete set null;
alter table public.join_lobbies add column if not exists status text default 'open';
alter table public.join_lobbies add column if not exists created_at timestamptz default now ();
alter table public.join_lobbies add column if not exists updated_at timestamptz default now ();
alter table public.join_lobbies add column if not exists expires_at timestamptz;

create index if not exists join_lobbies_code_idx on public.join_lobbies (join_code);

-- ---------------------------------------------------------------------------
-- 新使用者自動建立 profiles
-- ---------------------------------------------------------------------------
create or replace function public.handle_new_user ()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
  insert into public.profiles (id, display_name)
  values (
    new.id,
    coalesce(
      new.raw_user_meta_data ->> 'display_name',
      split_part(coalesce(new.email, new.id::text), '@', 1),
      '獵人'
    )
  )
  on conflict (id) do nothing;
  return new;
end;
$$;

drop trigger if exists on_auth_user_created on auth.users;
create trigger on_auth_user_created
after insert on auth.users
for each row execute function public.handle_new_user ();

-- ---------------------------------------------------------------------------
-- Join Code：建立（Host）
-- ---------------------------------------------------------------------------
create or replace function public.create_join_lobby (p_ttl_minutes int default 60)
returns text
language plpgsql
security definer
set search_path = public
as $$
declare
  v_uid uuid := auth.uid ();
  v_code text;
  v_attempt int := 0;
begin
  if v_uid is null then
    raise exception 'NOT_AUTHENTICATED';
  end if;

  loop
    select string_agg(
      substr('ABCDEFGHJKLMNPQRSTUVWXYZ23456789', floor(random() * 32)::int + 1, 1),
      ''
    )
    into v_code
    from generate_series(1, 6);

    begin
      insert into public.join_lobbies (join_code, host_user_id, guest_user_id, status, expires_at)
      values (
        v_code,
        v_uid,
        null,
        'open',
        now() + ((greatest (1, least (p_ttl_minutes, 1440))::text || ' minutes')::interval)
      );
      exit;
    exception
      when unique_violation then
        v_attempt := v_attempt + 1;
        if v_attempt > 30 then
          raise exception 'JOIN_CODE_GENERATION_FAILED';
        end if;
    end;
  end loop;

  return v_code;
end;
$$;

revoke all on function public.create_join_lobby (int) from public;
grant execute on function public.create_join_lobby (int) to authenticated;

-- ---------------------------------------------------------------------------
-- Join Code：查詢開放中的房（供 Client 驗證代碼；只回一筆）
-- ---------------------------------------------------------------------------
create or replace function public.peek_open_lobby_by_code (p_code text)
returns table (
  lobby_id uuid,
  join_code text,
  host_user_id uuid,
  guest_user_id uuid,
  status text,
  expires_at timestamptz
)
language plpgsql
security definer
set search_path = public
as $$
declare
  v_norm text := upper(trim(p_code));
begin
  if length(v_norm) < 4 then
    raise exception 'INVALID_CODE';
  end if;

  return query
  select
    j.id,
    j.join_code,
    j.host_user_id,
    j.guest_user_id,
    j.status,
    j.expires_at
  from public.join_lobbies j
  where j.join_code = v_norm
    and j.status = 'open'
    and j.expires_at > now ()
    and j.guest_user_id is null
  limit 1;
end;
$$;

revoke all on function public.peek_open_lobby_by_code (text) from public;
grant execute on function public.peek_open_lobby_by_code (text) to authenticated;

-- ---------------------------------------------------------------------------
-- Join Code：加入（Guest）
-- ---------------------------------------------------------------------------
create or replace function public.join_lobby_by_code (p_code text)
returns table (
  lobby_id uuid,
  join_code text,
  host_user_id uuid,
  guest_user_id uuid,
  status text,
  expires_at timestamptz
)
language plpgsql
security definer
set search_path = public
as $$
declare
  v_uid uuid := auth.uid ();
  v_norm text := upper(trim(p_code));
  j public.join_lobbies%rowtype;
begin
  if v_uid is null then
    raise exception 'NOT_AUTHENTICATED';
  end if;

  select * into j
  from public.join_lobbies
  where join_code = v_norm
    and status = 'open'
    and expires_at > now ()
    and guest_user_id is null
  for update;

  if j.id is null then
    raise exception 'LOBBY_NOT_AVAILABLE';
  end if;

  if j.host_user_id = v_uid then
    raise exception 'CANNOT_JOIN_OWN_LOBBY';
  end if;

  update public.join_lobbies
  set guest_user_id = v_uid,
      status = 'full',
      updated_at = now()
  where id = j.id;

  return query
  select l.id, l.join_code, l.host_user_id, l.guest_user_id, l.status, l.expires_at
  from public.join_lobbies l
  where l.id = j.id;
end;
$$;

revoke all on function public.join_lobby_by_code (text) from public;
grant execute on function public.join_lobby_by_code (text) to authenticated;

-- ---------------------------------------------------------------------------
-- RLS
-- ---------------------------------------------------------------------------
alter table public.profiles enable row level security;
alter table public.warehouse_items enable row level security;
alter table public.player_encyclopedia_progress enable row level security;
alter table public.daily_hunt_records enable row level security;
alter table public.join_lobbies enable row level security;

create policy "profiles_select_own"
  on public.profiles for select to authenticated
  using (id = auth.uid ());

create policy "profiles_update_own"
  on public.profiles for update to authenticated
  using (id = auth.uid ())
  with check (id = auth.uid ());

create policy "profiles_insert_own"
  on public.profiles for insert to authenticated
  with check (id = auth.uid ());

create policy "warehouse_select_own"
  on public.warehouse_items for select to authenticated
  using (owner_id = auth.uid ());

create policy "warehouse_insert_own"
  on public.warehouse_items for insert to authenticated
  with check (owner_id = auth.uid ());

create policy "warehouse_update_own"
  on public.warehouse_items for update to authenticated
  using (owner_id = auth.uid ())
  with check (owner_id = auth.uid ());

create policy "warehouse_delete_own"
  on public.warehouse_items for delete to authenticated
  using (owner_id = auth.uid ());

create policy "encyclopedia_select_own"
  on public.player_encyclopedia_progress for select to authenticated
  using (owner_id = auth.uid ());

create policy "encyclopedia_insert_own"
  on public.player_encyclopedia_progress for insert to authenticated
  with check (owner_id = auth.uid ());

create policy "encyclopedia_update_own"
  on public.player_encyclopedia_progress for update to authenticated
  using (owner_id = auth.uid ())
  with check (owner_id = auth.uid ());

create policy "encyclopedia_delete_own"
  on public.player_encyclopedia_progress for delete to authenticated
  using (owner_id = auth.uid ());

create policy "daily_hunt_select_own"
  on public.daily_hunt_records for select to authenticated
  using (owner_id = auth.uid ());

create policy "daily_hunt_insert_own"
  on public.daily_hunt_records for insert to authenticated
  with check (owner_id = auth.uid ());

create policy "daily_hunt_update_own"
  on public.daily_hunt_records for update to authenticated
  using (owner_id = auth.uid ())
  with check (owner_id = auth.uid ());

create policy "daily_hunt_delete_own"
  on public.daily_hunt_records for delete to authenticated
  using (owner_id = auth.uid ());

-- 僅允許當事人讀取自己的大廳列（RPC 已處理加入流程；Host 可在客戶端查自己的房）
create policy "lobby_select_participants"
  on public.join_lobbies for select to authenticated
  using (
    host_user_id = auth.uid ()
    or guest_user_id = auth.uid ()
  );

-- 建立／更新大廳僅能透過 SECURITY DEFINER RPC，避免客戶端任意插房
revoke insert, update on public.join_lobbies from authenticated;
