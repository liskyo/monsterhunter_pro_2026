using System;
using System.Collections;
using UnityEngine;

namespace MonsterHunter.Combat
{
    /// <summary>
    /// 魔物血量：2D／部位受擊走 <see cref="TakeDamage(float)"/>；
    /// 教學用方塊可改呼叫 <see cref="TakeDamage(int)"/>（會輸出「砍中了！剩餘血量」並可作受擊閃紅）。
    /// </summary>
    public sealed class MonsterHealth : MonoBehaviour
    {
        [SerializeField] float maxHp = 100f;

        [Header("（選用）受擊視覺")]
        [SerializeField] Renderer _damageFlashRenderer;
        [SerializeField] Color _damageFlashTint = new Color(1f, 0.6f, 0.6f, 0.8f); // 極度弱化的柔和受擊光，避免喧賓奪主
        [SerializeField] float _damageFlashHoldSeconds = 0.04f; // 閃爍時間減半，僅有瞬間打擊感

        float _currentHp;
        bool _depletedInvoked;

        Color? _flashBaseTint;
        Coroutine _flashRoutine;

        /// <summary>目前血量。</summary>
        public float CurrentHp => _currentHp;

        /// <summary>最大血量。</summary>
        public float MaxHp => maxHp;

        /// <summary>是否仍存活（血量 &gt; 0）。</summary>
        public bool IsAlive => _currentHp > 0f;

        /// <summary>成功扣血後觸發（數值為本次實際扣除量，可能與傳入相同；isCrit 供 HUD／浮字）。</summary>
        public event Action<float, bool> DamageApplied;

        /// <summary>血量首次歸零時觸發一次（死亡／討伐流程入口）。</summary>
        public event Action HpDepleted;

        void Awake()
        {
            EnsureCapacityInitialized();

            CacheRendererDefault();
        }

        void EnsureCapacityInitialized()
        {
            if (_currentHp <= 0f && maxHp > 0f)
            {
                _currentHp = maxHp;
                _depletedInvoked = false;
            }
        }

        void CacheRendererDefault()
        {
            if (_damageFlashRenderer == null)
                _damageFlashRenderer = GetComponent<Renderer>()
                    ?? GetComponentInChildren<Renderer>();

            if (_damageFlashRenderer == null || _damageFlashRenderer.material == null)
                return;

            try
            {
                _flashBaseTint = _damageFlashRenderer.material.color;
            }
            catch
            {
                _flashBaseTint = null;
            }
        }

        /// <summary>執行期設定最大血量（例如從 monsters.json 注入）。</summary>
        public void Initialize(float maximumHp, bool refillCurrent = true)
        {
            maxHp = Mathf.Max(0f, maximumHp);
            _depletedInvoked = false;
            if (refillCurrent)
                _currentHp = maxHp;
            else
                _currentHp = Mathf.Min(_currentHp, maxHp);
        }

        /// <summary>integer 入口；成功扣血會閃紅並在 Console 顯示剩餘血量。</summary>
        public void TakeDamage(int damageAmount)
        {
            float amount = Mathf.Max(0f, damageAmount);
            if (ApplyDamageCore(amount, isCrit: false) && amount > 0f)
                OnTutorialStyleHitApplied();
        }

        /// <summary>受到傷害（一般戰鬥流程）。</summary>
        public void TakeDamage(float amount, bool isCrit = false)
        {
            ApplyDamageCore(amount, isCrit);
        }

        bool ApplyDamageCore(float amount, bool isCrit)
        {
            if (amount <= 0f || !IsAlive)
                return false;

            _currentHp = Mathf.Max(0f, _currentHp - amount);
            DamageApplied?.Invoke(amount, isCrit);

            // ✦ 受擊紅色閃爍回饋
            TriggerDamageFlashRoutine();

            // ✦ 噴血粒子效果
            SpawnBloodSplatter(transform.position, isCrit);

            if (_currentHp <= 0f && !_depletedInvoked)
            {
                _depletedInvoked = true;
                HpDepleted?.Invoke();
            }

            return true;
        }

        void SpawnBloodSplatter(Vector2 position, bool isCrit)
        {
            int count = isCrit ? 15 : 8; // 暴擊噴更多！
            for (int i = 0; i < count; i++)
            {
                var particle = new GameObject("BloodSplatterParticle");
                particle.transform.position = position + new Vector2(UnityEngine.Random.Range(-0.15f, 0.15f), UnityEngine.Random.Range(-0.15f, 0.15f));
                
                var sr = particle.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 2500; // 確保在背景和魔物之上，但在 HUD 之下
                
                // 動態生成 16x16 的圓形血滴 Texture，極度安全無外部依賴
                var tex = new Texture2D(16, 16);
                for (int y = 0; y < 16; y++)
                {
                    for (int x = 0; x < 16; x++)
                    {
                        float dx = x - 7.5f;
                        float dy = y - 7.5f;
                        if (dx * dx + dy * dy <= 45f) // 圓形判定
                        {
                            // 暴擊使用更鮮豔亮紅，普通使用深紅
                            tex.SetPixel(x, y, isCrit ? new Color(1f, 0.15f, 0.15f, 1f) : new Color(0.72f, 0.05f, 0.05f, 1f));
                        }
                        else
                        {
                            tex.SetPixel(x, y, Color.clear);
                        }
                    }
                }
                tex.Apply();
                sr.sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f));
                
                // 隨機微小尺寸，呈現水滴四散層次感
                float size = UnityEngine.Random.Range(0.12f, 0.26f);
                if (isCrit) size *= 1.35f;
                particle.transform.localScale = new Vector3(size, size, size);

                var rb = particle.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f; // 2D 俯視無重力
                rb.linearDamping = 4.5f; // 阻尼減速，營造噴濺阻力
#pragma warning disable CS0618
                rb.drag = 4.5f; // 相容舊版
#pragma warning restore CS0618

                // 向四周 360 度隨機角度強力噴出
                float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                float speed = UnityEngine.Random.Range(3.5f, 8.5f);
                rb.linearVelocity = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed);

                StartCoroutine(FadeAndDestroyBlood(particle, sr));
            }
        }

        IEnumerator FadeAndDestroyBlood(GameObject go, SpriteRenderer sr)
        {
            float elapsed = 0f;
            float duration = UnityEngine.Random.Range(0.28f, 0.52f);
            Color startColor = sr.color;
            Vector3 startScale = go.transform.localScale;

            while (elapsed < duration)
            {
                if (go == null) yield break;
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // 漸變淡出與縮小
                sr.color = Color.Lerp(startColor, new Color(startColor.r, startColor.g, startColor.b, 0f), t);
                go.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        void OnTutorialStyleHitApplied()
        {
            TriggerDamageFlashRoutine();
            Debug.Log($"砍中了！剩餘血量：{_currentHp:0}", this);
        }

        void TriggerDamageFlashRoutine()
        {
            // ✦ 避免受擊閃紅蓋掉魔物的大招發光前搖！
            var ai = GetComponent<MonsterHunter.Controllers.MonsterAiController>();
            if (ai != null && ai.IsTelegraphing)
                return;

            if (_damageFlashRenderer == null || _damageFlashRenderer.material == null)
                return;

            if (_flashBaseTint == null)
                CacheRendererDefault();

            if (_flashRoutine != null)
                StopCoroutine(_flashRoutine);

            _flashRoutine = StartCoroutine(HitFlashRoutine());
        }

        IEnumerator HitFlashRoutine()
        {
            var mat = _damageFlashRenderer.material;
            Color baseTint = _flashBaseTint ?? mat.color;
            mat.color = _damageFlashTint;
            yield return new WaitForSeconds(_damageFlashHoldSeconds);
            mat.color = baseTint;
            _flashRoutine = null;
        }
    }
}
