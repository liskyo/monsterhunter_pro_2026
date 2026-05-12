using System;
using System.IO;
using MonsterHunter.Combat;
using MonsterHunter.DataModels;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.AI;

namespace MonsterHunter.Monster
{
    /// <summary>
    /// 魔物 3D 場景 AI：NavMesh 追擊、企劃載入、死亡事件。
    /// <list type="bullet">
    ///   <item>場景必須有可走的 NavMesh（可用 <c>NavMeshSurface.BuildNavMesh()</c> 於 Runtime Bake，或 Window → AI → Navigation）。示範場景：<c>Assets/Scenes/MonsterNavDemo.unity</c>。</item>
    ///   <item>將玩家拖入 Inspector 的「玩家」，或確認 Tag 為 Player 的物件。</item>
    ///   <item>Animator 需有「死亡動畫觸發名稱」對應的 Trigger（預設 Die）。</item>
    /// </list>
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class MonsterController : MonoBehaviour, IDamageReceiver
    {
        [Header("企劃識別")]
        [Tooltip("對應 monsterlist.json / monsters.json 的魔物編號，例如 MON_001")]
        [SerializeField] string monsterId = "MON_001";

        [Header("追擊")]
        [SerializeField] Transform player;
        [SerializeField] string playerTag = "Player";
        [SerializeField] float detectRadius = 12f;

        [Header("動畫")]
        [SerializeField] Animator animator;
        [SerializeField] string deathTriggerName = "Die";

        [Header("除錯")]
        [SerializeField] bool logLoadErrors = true;

        /// <summary>根目錄 monsterlist.json 的一筆（僅編號與名稱；不含血量/攻擊）。</summary>
        [Serializable]
        public class MonsterListEntry
        {
            public string 魔物編號;
            public string 名稱;
        }

        /// <summary>
        /// JsonUtility 無法直接反序列化「根為 JSON 陣列」的檔案，需包一層：
        /// <code>
        /// [Serializable] class MonsterListRoot { public MonsterListEntry[] items; }
        /// // 將 JSON 改為 {"items":[...]} 後：JsonUtility.FromJson&lt;MonsterListRoot&gt;(text);
        /// </code>
        /// 本專案使用 Newtonsoft.Json 可直接：JsonConvert.DeserializeObject&lt;MonsterListEntry[]&gt;(text);
        /// </summary>
        public static class MonsterListJson
        {
            public static MonsterListEntry[] Deserialize(string json) =>
                JsonConvert.DeserializeObject<MonsterListEntry[]>(json);
        }

        public event Action OnMonsterDeath;

        public string MonsterId => monsterId;
        public string DisplayName => _displayName;
        public float MaxHp => _maxHp;
        public float CurrentHp => _currentHp;
        public float BaseAttack => _baseAttack;
        public bool IsDead => _dead;

        float _maxHp;
        float _currentHp;
        float _baseAttack;
        string _displayName;
        bool _dead;

        NavMeshAgent _agent;

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (player == null && !string.IsNullOrEmpty(playerTag))
            {
                var go = GameObject.FindGameObjectWithTag(playerTag);
                if (go != null)
                    player = go.transform;
            }

            TryLoadStatsFromDesign();
        }

        void Update()
        {
            if (_dead || _agent == null)
                return;

            if (player == null)
                return;

            float dist = Vector3.Distance(transform.position, player.position);
            if (dist <= detectRadius)
            {
                _agent.isStopped = false;
                if (_agent.isOnNavMesh)
                    _agent.SetDestination(player.position);
            }
            else
            {
                _agent.isStopped = true;
            }
        }

        public void ApplyDamage(float amount, bool isCrit) => TakeDamage(amount);

        public void TakeDamage(float amount)
        {
            if (_dead || amount <= 0f)
                return;

            _currentHp = Mathf.Max(0f, _currentHp - amount);
            if (_currentHp <= 0f)
                Die();
        }

        void Die()
        {
            if (_dead)
                return;

            _dead = true;

            if (_agent != null)
            {
                _agent.isStopped = true;
                _agent.enabled = false;
            }

            if (animator != null && !string.IsNullOrEmpty(deathTriggerName))
                animator.SetTrigger(deathTriggerName);

            OnMonsterDeath?.Invoke();
        }

        void TryLoadStatsFromDesign()
        {
            _displayName = monsterId;
            _maxHp = 100f;
            _baseAttack = 10f;
            _currentHp = _maxHp;

            if (string.IsNullOrEmpty(monsterId))
            {
                LogWarn("MonsterController：未設定 monsterId。");
                return;
            }

            if (!TryLoadMonsterListEntry(monsterId, out var listEntry))
            {
                LogWarn($"MonsterController：在 monsterlist.json 找不到或未讀取 {monsterId}。");
            }
            else
            {
                _displayName = string.IsNullOrEmpty(listEntry.名稱) ? monsterId : listEntry.名稱;
            }

            if (!TryFindMonsterRow(monsterId, out var row))
            {
                LogWarn($"MonsterController：在 DesignData/01_Monsters/monsters.json 找不到 {monsterId}，使用預設血量/攻擊。");
                _currentHp = _maxHp;
                return;
            }

            _maxHp = Mathf.Max(1f, row.最大血量);
            int normalDamage = row.普通攻擊 != null ? row.普通攻擊.傷害 : 0;
            _baseAttack = Mathf.Max(0f, normalDamage);
            _currentHp = _maxHp;
        }

        static bool TryLoadMonsterListEntry(string id, out MonsterListEntry entry)
        {
            entry = null;
            string path = FindUpwards("monsterlist.json", 10);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            string json = File.ReadAllText(path);
            var arr = MonsterListJson.Deserialize(json);
            if (arr == null)
                return false;

            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] != null && arr[i].魔物編號 == id)
                {
                    entry = arr[i];
                    return true;
                }
            }

            return false;
        }

        static bool TryFindMonsterRow(string id, out 魔物資料列 row)
        {
            row = null;
            string path = FindMonstersJsonPath();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            string json = File.ReadAllText(path);
            var rows = JsonConvert.DeserializeObject<魔物資料列[]>(json);
            if (rows == null)
                return false;

            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] != null && rows[i].魔物編號 == id)
                {
                    row = rows[i];
                    return true;
                }
            }

            return false;
        }

        /// <summary>自 GameClient/Assets 往上找 repo 根或包含 DesignData 的目錄。</summary>
        static string FindMonstersJsonPath()
        {
            return FindUpwards(Path.Combine("DesignData", "01_Monsters", "monsters.json"), 12);
        }

        static string FindUpwards(string relativePath, int maxHops)
        {
            var dir = new DirectoryInfo(Application.dataPath);
            for (int i = 0; i < maxHops && dir != null; i++)
            {
                string full = Path.Combine(dir.FullName, relativePath);
                if (File.Exists(full))
                    return full;
                dir = dir.Parent;
            }

            return null;
        }

        void LogWarn(string msg)
        {
            if (logLoadErrors)
                Debug.LogWarning(msg, this);
        }
    }
}
