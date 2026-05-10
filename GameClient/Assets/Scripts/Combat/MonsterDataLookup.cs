using System;
using MonsterHunter.DataModels;
using Newtonsoft.Json;
using UnityEngine;

namespace MonsterHunter.Combat
{
    public static class MonsterDataLookup
    {
        public static bool TryFind(TextAsset monstersJson, string 魔物編號, out 魔物資料列 row)
        {
            row = null;
            if (monstersJson == null || string.IsNullOrEmpty(魔物編號)) return false;
            try
            {
                var rows = JsonConvert.DeserializeObject<魔物資料列[]>(monstersJson.text);
                if (rows == null) return false;
                foreach (var r in rows)
                {
                    if (r != null && r.魔物編號 == 魔物編號)
                    {
                        row = r;
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MonsterDataLookup] " + e.Message);
            }

            return false;
        }
    }
}
