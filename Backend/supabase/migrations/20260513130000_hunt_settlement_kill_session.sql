-- 戰鬥結算：圖鑑擊殺 + 當日 SESSION 擊殺計數與第 3 擊難度倍率（daily_hunt_records，daily_quest_id = __SESSION__）

alter table public.daily_hunt_records
  add column if not exists difficulty_multiplier real default 1.0;

comment on column public.daily_hunt_records.difficulty_multiplier is '當日 SESSION 難度倍率；第 3 次擊殺當日完成時 +0.05（須與 DesignData/03_Combat/combat_tuning.json 每日第三擊殺難度倍率增量 一致）。';

create or replace function public.post_monster_kill_session (
  p_local_date date,
  p_monster_id text,
  p_monster_name text
)
returns table (
  kill_count int,
  difficulty_multiplier real
)
language plpgsql
security invoker
set search_path = public
as $$
declare
  v_uid uuid := auth.uid ();
  v_old_kills int;
  v_new_kills int;
  v_mul real;
begin
  if v_uid is null then
    raise exception 'NOT_AUTHENTICATED';
  end if;

  insert into public.player_encyclopedia_progress (
    owner_id,
    monster_id,
    monster_name,
    kill_count,
    capture_count,
    part_break_count,
    research_level,
    unlocked_info
  )
  values (
    v_uid,
    trim(p_monster_id),
    trim(p_monster_name),
    1,
    0,
    0,
    0,
    '{}'::jsonb
  )
  on conflict (owner_id, monster_id)
  do update set
    kill_count = public.player_encyclopedia_progress.kill_count + 1,
    monster_name = excluded.monster_name,
    updated_at = now ();

  select
    d.completion_count,
    coalesce(d.difficulty_multiplier, 1.0::real)
  into v_old_kills, v_mul
  from public.daily_hunt_records d
  where d.owner_id = v_uid
    and d.record_date = p_local_date
    and d.daily_quest_id = '__SESSION__'
  for update;

  if not found then
    v_new_kills := 1;
    v_mul := 1.0;
    insert into public.daily_hunt_records (
      owner_id,
      record_date,
      daily_quest_id,
      completed,
      completion_count,
      rewards_claimed,
      difficulty_multiplier
    )
    values (
      v_uid,
      p_local_date,
      '__SESSION__',
      false,
      v_new_kills,
      false,
      v_mul
    );
  else
    v_new_kills := v_old_kills + 1;
    if v_new_kills = 3 then
      v_mul := v_mul + 0.05;
    end if;
    update public.daily_hunt_records d
    set
      completion_count = v_new_kills,
      difficulty_multiplier = v_mul,
      updated_at = now ()
    where d.owner_id = v_uid
      and d.record_date = p_local_date
      and d.daily_quest_id = '__SESSION__';
  end if;

  return query select v_new_kills, v_mul;
end;
$$;

revoke all on function public.post_monster_kill_session (date, text, text) from public;
grant execute on function public.post_monster_kill_session (date, text, text) to authenticated;
