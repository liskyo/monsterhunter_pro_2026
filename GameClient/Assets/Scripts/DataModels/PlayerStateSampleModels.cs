using System;

namespace MonsterHunter.DataModels
{
    /// <summary>對應 PlayerState/players.json（單一根物件；本機範例，執行時以 Supabase 為準）。</summary>
    [Serializable]
    public class 玩家狀態本機範例
    {
        public string player_id;
        public string name;
        public int rank;
        public int experience;
        public int zenny;
        public 玩家裝備配置 current_equipment;
        public 玩家寵物配置 active_pets;
        public 玩家腰包項目[] pouch;
        public string current_canteen_buff;
        public string last_login;
    }

    [Serializable]
    public class 玩家裝備配置
    {
        public string main_weapon;
        public string sub_weapon;
        public string armor_set;
        public string[] active_skills;
    }

    [Serializable]
    public class 玩家寵物配置
    {
        public string palico_id;
        public string palamute_id;
    }

    [Serializable]
    public class 玩家腰包項目
    {
        public string item_id;
        public string name;
        public int quantity;
    }

    /// <summary>對應 PlayerState/warehouse.json。</summary>
    [Serializable]
    public class 倉庫狀態本機範例
    {
        public string player_id;
        public int storage_slots;
        public 倉庫物品項[] items;
    }

    [Serializable]
    public class 倉庫物品項
    {
        public string item_id;
        public string name;
        public int quantity;
    }

    /// <summary>對應 PlayerState/player_encyclopedia_progress.json。</summary>
    [Serializable]
    public class 圖鑑進度本機範例
    {
        public string player_id;
        public 圖鑑魔物項[] discovered_monsters;
    }

    [Serializable]
    public class 圖鑑魔物項
    {
        public string monster_id;
        public string monster_name;
        public int kill_count;
        public int capture_count;
        public int part_break_count;
        public int research_level;
        public 圖鑑解鎖資訊 unlocked_info;
    }

    [Serializable]
    public class 圖鑑解鎖資訊
    {
        public bool basic_stats;
        public bool weakness;
        public bool drop_table;
    }

    /// <summary>對應 PlayerState/daily_hunt_records.json（hunts 結構可依企劃擴充）。</summary>
    [Serializable]
    public class 每日狩獵紀錄本機範例
    {
        public string player_id;
        public string record_date;
        public 每日狩獵項目[] hunts;
    }

    [Serializable]
    public class 每日狩獵項目
    {
    }
}
