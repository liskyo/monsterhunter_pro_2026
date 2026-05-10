-- 以複合唯一鍵 upsert，避免客戶端誤用 PostgREST 僅依 uuid PK 合併而重複插入

create or replace function public.upsert_daily_hunt_record (
  p_record_date date,
  p_daily_quest_id text,
  p_completion_count int,
  p_completed boolean,
  p_rewards_claimed boolean
)
returns void
language plpgsql
security definer
set search_path = public
as $$
declare
  v_uid uuid := auth.uid ();
begin
  if v_uid is null then
    raise exception 'NOT_AUTHENTICATED';
  end if;

  insert into public.daily_hunt_records (
    owner_id,
    record_date,
    daily_quest_id,
    completion_count,
    completed,
    rewards_claimed,
    updated_at
  )
  values (
    v_uid,
    p_record_date,
    trim(p_daily_quest_id),
    greatest (0, p_completion_count),
    p_completed,
    p_rewards_claimed,
    now()
  )
  on conflict (owner_id, record_date, daily_quest_id)
  do update set
    completion_count = excluded.completion_count,
    completed = excluded.completed,
    rewards_claimed = excluded.rewards_claimed,
    updated_at = now ();
end;
$$;

revoke all on function public.upsert_daily_hunt_record (date, text, int, boolean, boolean) from public;
grant execute on function public.upsert_daily_hunt_record (date, text, int, boolean, boolean) to authenticated;
