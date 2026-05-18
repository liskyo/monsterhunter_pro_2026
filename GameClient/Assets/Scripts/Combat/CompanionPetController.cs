using System;
using System.Collections;
using MonsterHunter.Controllers;
using MonsterHunter.DataModels;
using UnityEngine;
using UnityEngine.UI;

namespace MonsterHunter.Combat
{
    /// <summary>
    /// 隨行寵物執行期控制器：
    ///   1. 平時平滑跟隨在獵人身旁。
    ///   2. 根據寵物的「定位」（戰鬥、防禦、支援等）每隔數秒獨立執行特色行動：
    ///      - 攻擊魔物（飛撲、爪擊、造成傷害與飄字）
    ///      - 輔助獵人（體力回復加血、或是加攻擊力 Buff）
    ///      - 吸引魔物（挑釁怒吼、短暫強制重寫魔物的 AI 追擊目標至寵物身上，為獵人創造背後輸出機會）
    /// </summary>
    public sealed class CompanionPetController : MonoBehaviour
    {
        private Transform _hunter;
        private Transform _monster;
        private MonsterAiController _monsterAi;
        private PlayerController _playerCtrl;
        private 寵物資料列 _petData;

        private float _actionTimer;
        private float _actionInterval = 4f; 
        private Vector3 _offsetFromHunter = new Vector3(0.65f, -0.15f, 0f);

        private bool _isPerformingAction;

        public void Setup(Transform hunter, Transform monster, MonsterAiController monsterAi, PlayerController playerCtrl, 寵物資料列 petData)
        {
            _hunter = hunter;
            _monster = monster;
            _monsterAi = monsterAi;
            _playerCtrl = playerCtrl;
            _petData = petData;

            // 根據寵物屬性微調行動速度與間隔，屬性越好動作越頻繁
            var star = petData != null ? Mathf.Clamp(petData.基礎數值?.體力 ?? 1, 1, 100) : 5;
            _actionInterval = Mathf.Clamp(4.8f - (star * 0.05f), 2.8f, 5.5f);
        }

        private void Update()
        {
            if (BattleCombatManager.IsBattleConcluded || _hunter == null) return;

            // ✦ 當前沒有執行突進動作時，平滑跟隨在獵人身旁
            if (!_isPerformingAction)
            {
                Vector3 targetPos = _hunter.position + _offsetFromHunter;
                transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * 3.8f);
                
                // 朝向怪物方向
                if (_monster != null)
                {
                    var sr = GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        sr.flipX = _monster.position.x < transform.position.x;
                    }
                }
            }

            // 行動計時
            _actionTimer += Time.deltaTime;
            if (_actionTimer >= _actionInterval && !_isPerformingAction && _monster != null && _monsterAi != null && _monsterAi.CurrentHp > 0f)
            {
                _actionTimer = 0f;
                PerformPetSpecialAction();
            }
        }

        private void PerformPetSpecialAction()
        {
            if (_petData == null) return;

            // 根據定位或隨機機率決定行動
            string role = (_petData.定位 ?? "支援").Trim();
            float rand = UnityEngine.Random.value;

            if (role.Contains("戰鬥") || role.Contains("攻擊"))
            {
                if (rand < 0.70f) StartCoroutine(ActionLeapAttack());
                else if (rand < 0.85f) StartCoroutine(ActionSupportBuff());
                else StartCoroutine(ActionTauntMonster());
            }
            else if (role.Contains("防禦") || role.Contains("防守") || role.Contains("坦克") || role.Contains("吸引"))
            {
                if (rand < 0.70f) StartCoroutine(ActionTauntMonster());
                else if (rand < 0.85f) StartCoroutine(ActionLeapAttack());
                else StartCoroutine(ActionSupportBuff());
            }
            else // 支援 / 回復定位
            {
                if (rand < 0.70f) StartCoroutine(ActionSupportBuff());
                else if (rand < 0.85f) StartCoroutine(ActionLeapAttack());
                else StartCoroutine(ActionTauntMonster());
            }
        }

        // ── 🐾 行動一：飛撲突擊（攻擊魔物） ──
        private IEnumerator ActionLeapAttack()
        {
            _isPerformingAction = true;
            Vector3 startPos = transform.position;
            Vector3 targetPos = _monster.position + (startPos - _monster.position).normalized * 0.4f;

            // 飛撲向怪物
            float elapsed = 0f;
            float duration = 0.22f;
            while (elapsed < duration)
            {
                if (_monster == null) break;
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(startPos, targetPos, elapsed / duration);
                yield return null;
            }

            // 造成擊中傷害並產生漂浮文字
            if (_monsterAi != null && _monsterAi.CurrentHp > 0f)
            {
                float baseAtk = _petData.基礎數值 != null ? Mathf.Max(10f, _petData.基礎數值.攻擊力) : 35f;
                float finalDmg = Mathf.RoundToInt(baseAtk * UnityEngine.Random.Range(0.85f, 1.25f) * 1.5f);
                _monsterAi.ApplyDamage(finalDmg, false);

                SpawnFloatingText($"🐾 {_petData.名稱} 爪擊！ -{finalDmg}", new Color(1f, 0.4f, 0f));
            }

            // 順滑退回獵本身邊
            elapsed = 0f;
            duration = 0.25f;
            Vector3 retreatStart = transform.position;
            while (elapsed < duration)
            {
                if (_hunter == null) break;
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(retreatStart, _hunter.position + _offsetFromHunter, elapsed / duration);
                yield return null;
            }

            _isPerformingAction = false;
        }

        // ── 💚 行動二：治癒氣場 / 鼓舞戰歌（輔助獵人） ──
        private IEnumerator ActionSupportBuff()
        {
            _isPerformingAction = true;

            // 播放回復動作特效
            Vector3 origPos = transform.position;
            float elapsed = 0f;
            float duration = 0.5f;

            SpawnPetSparkles(new Color(0.1f, 1f, 0.2f, 1f));

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float height = Mathf.Sin(elapsed * Mathf.PI * 4f) * 0.3f;
                transform.position = origPos + new Vector3(0f, height, 0f);
                yield return null;
            }
            transform.position = origPos;

            // 決定是補血還是增加傷害倍率
            if (UnityEngine.Random.value < 0.6f && _playerCtrl != null && _playerCtrl.CurrentHp > 0f)
            {
                float healAmt = _petData.基礎數值 != null ? Mathf.Max(15f, _petData.基礎數值.體力 * 0.15f) : 25f;
                healAmt = Mathf.RoundToInt(healAmt);
                
                _playerCtrl.Heal(healAmt);
                SpawnFloatingText($"💚 {_petData.名稱} 回復！ +{healAmt} HP", new Color(0.12f, 0.95f, 0.2f));
            }
            else if (_playerCtrl != null)
            {
                // 鼓舞傷害提升 35%
                _playerCtrl.SetOutgoingDamageMultiplier(1.35f);
                StartCoroutine(RemoveBuffAfterDelay(3.5f));
                SpawnFloatingText($"🔥 {_petData.名稱} 鼓舞！傷害 +35%!", new Color(1f, 0.85f, 0f));
            }

            _isPerformingAction = false;
        }

        private IEnumerator RemoveBuffAfterDelay(float sec)
        {
            yield return new WaitForSeconds(sec);
            if (_playerCtrl != null)
            {
                _playerCtrl.SetOutgoingDamageMultiplier(1f);
            }
        }

        // ── 📢 行動三：挑釁怒吼（吸引魔物） ──
        private IEnumerator ActionTauntMonster()
        {
            _isPerformingAction = true;

            var sr = GetComponent<SpriteRenderer>();
            Color origCol = sr != null ? sr.color : Color.white;
            
            // 跳躍到魔物周圍進行挑釁
            Vector3 startPos = transform.position;
            Vector3 tauntPos = _monster.position + (Vector3)UnityEngine.Random.insideUnitCircle.normalized * 0.8f;

            float elapsed = 0f;
            float duration = 0.25f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(startPos, tauntPos, elapsed / duration);
                yield return null;
            }

            // 閃爍紅色挑釁光芒並呼叫 Override 目標
            SpawnPetSparkles(new Color(1f, 0.15f, 0.15f, 1f));
            if (sr != null) sr.color = new Color(1f, 0.2f, 0.2f, 1f);

            if (_monsterAi != null)
            {
                _monsterAi.SetOverrideTarget(transform, 3f);
            }

            SpawnFloatingText($"📢 {_petData.名稱} 挑釁！吸引魔物仇恨！", new Color(1f, 0.2f, 0.2f));

            yield return new WaitForSeconds(0.6f);
            if (sr != null) sr.color = origCol;

            // 平滑退回
            elapsed = 0f;
            duration = 0.35f;
            Vector3 retreatStart = transform.position;
            while (elapsed < duration)
            {
                if (_hunter == null) break;
                elapsed += Time.deltaTime;
                transform.position = Vector3.Lerp(retreatStart, _hunter.position + _offsetFromHunter, elapsed / duration);
                yield return null;
            }

            _isPerformingAction = false;
        }

        private void SpawnFloatingText(string text, Color color)
        {
            var hud = FindAnyObjectByType<Canvas>();
            if (hud == null) return;

            var go = new GameObject("PetActionPop", typeof(RectTransform));
            go.transform.SetParent(hud.transform, false);
            var rt = go.GetComponent<RectTransform>();

            Vector2 screenPos = Camera.main.WorldToScreenPoint(transform.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                hud.transform as RectTransform,
                screenPos,
                hud.worldCamera,
                out Vector2 localPos
            );
            localPos.y += 60f;
            rt.anchoredPosition = localPos;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                       ?? Font.CreateDynamicFontFromOSFont(new[] { "Microsoft JhengHei", "Arial" }, 22);

            var txt = go.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = 42; 
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = color;
            txt.text = text;

            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);

            StartCoroutine(FloatAndFadeText(go, rt, txt));
        }

        private IEnumerator FloatAndFadeText(GameObject go, RectTransform rt, Text txt)
        {
            float elapsed = 0f;
            float duration = 1.2f;
            Vector2 startPos = rt.anchoredPosition;

            while (elapsed < duration)
            {
                if (go == null || rt == null || txt == null) yield break;
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                float lift = Mathf.Sin(t * Mathf.PI * 0.5f) * 110f;
                rt.anchoredPosition = startPos + new Vector2(0f, lift);

                var c = txt.color;
                c.a = Mathf.Clamp01(1f - t);
                txt.color = c;

                yield return null;
            }
            if (go != null) Destroy(go);
        }

        private void SpawnPetSparkles(Color color)
        {
            var wave = new GameObject("PetSparkles");
            wave.transform.position = transform.position;
            var sr = wave.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 2200;

            var tex = new Texture2D(16, 16);
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    float dx = x - 7.5f;
                    float dy = y - 7.5f;
                    if (dx * dx + dy * dy <= 45f)
                    {
                        tex.SetPixel(x, y, color);
                    }
                    else
                    {
                        tex.SetPixel(x, y, Color.clear);
                    }
                }
            }
            tex.Apply();
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f));
            wave.transform.localScale = Vector3.zero;

            StartCoroutine(AnimateSparkles(wave, sr));
        }

        private IEnumerator AnimateSparkles(GameObject go, SpriteRenderer sr)
        {
            float elapsed = 0f;
            float duration = 0.5f;
            while (elapsed < duration)
            {
                if (go == null) yield break;
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                go.transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one * 1.8f, t);
                var c = sr.color;
                c.a = 1f - t;
                sr.color = c;
                yield return null;
            }
            if (go != null) Destroy(go);
        }
    }
}
