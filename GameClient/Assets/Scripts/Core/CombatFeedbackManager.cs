using System.Collections;
using UnityEngine;

namespace MonsterHunter.Core
{
    /// <summary>
    /// 戰鬥打擊回饋（Hitstop 等）全域單例。第一次存取時會建立常駐場景物件並 <c>DontDestroyOnLoad</c>。
    /// </summary>
    public sealed class CombatFeedbackManager : MonoBehaviour
    {
        public const float HitstopTimeScale = 0.05f;

        static CombatFeedbackManager _instance;

        /// <summary>全域單例；若尚不存在會自動建立承載物件。</summary>
        public static CombatFeedbackManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject(nameof(CombatFeedbackManager));
                    _instance = go.AddComponent<CombatFeedbackManager>();
                    DontDestroyOnLoad(go);
                }

                return _instance;
            }
        }

        Coroutine _hitstopRoutine;

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        /// <summary>
        /// 觸發打擊定格：將 <see cref="Time.timeScale"/> 設為 <see cref="HitstopTimeScale"/>，
        /// 經過 <paramref name="duration"/> 秒（<strong>實時間</strong>，不受 timeScale 影響）後還原為 1。
        /// 若上一次 Hitstop 仍在進行，會先停止該協程再開始新的，避免時間狀態疊加錯亂。
        /// </summary>
        public void TriggerHitstop(float duration)
        {
            if (_hitstopRoutine != null)
            {
                StopCoroutine(_hitstopRoutine);
                _hitstopRoutine = null;
                Time.timeScale = 1f;
            }

            if (duration <= 0f)
                return;

            _hitstopRoutine = StartCoroutine(HitstopRoutine(duration));
        }

        IEnumerator HitstopRoutine(float duration)
        {
            Time.timeScale = HitstopTimeScale;
            // timeScale 很低時必須用實時間等待，否則 WaitForSeconds 也會被放慢而失真
            yield return new WaitForSecondsRealtime(duration);
            Time.timeScale = 1f;
            _hitstopRoutine = null;
        }
    }
}
