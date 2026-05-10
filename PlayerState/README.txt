此資料夾僅供本機範例／企劃對照，執行時 PlayerState 請以 Supabase 雲端為準：

- profiles、warehouse_items、player_encyclopedia_progress、daily_hunt_records 表
- 每位家人使用「電子郵件註冊／登入」後取得獨立 owner_id（auth.uid）
- daily_hunt_records：唯一鍵 (owner_id, record_date, daily_quest_id)，兩人挑戰次數分開計算

Migration：Backend/supabase/migrations/20260512100000_player_state_join_lobbies.sql
