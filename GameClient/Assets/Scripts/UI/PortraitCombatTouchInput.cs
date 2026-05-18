using MonsterHunter.Combat;
using MonsterHunter.Controllers;
using UnityEngine;

namespace MonsterHunter.UI
{
    /// <summary>
    /// 手機直立戰鬥：在螢幕下方（高度由 combat_tuning.json「觸控操作區高度比例」）以隱形浮動搖桿驅動
    /// <see cref="PlayerController.MoveInput"/>；鬆手歸零後即進入靜止普攻流程。
    /// Editor 可用滑鼠左鍵在同一區域模擬。
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class PortraitCombatTouchInput : MonoBehaviour
    {
        [SerializeField] CombatTuningStore _tuningStore;
        [SerializeField] PlayerController _player;

        Vector2 _pressOrigin;
        bool _tracking;
        int _fingerId = -1;
        float _pressTime; // ✦ 新增觸控按壓起始時間，用於精準偵測快速滑動 (Swipe/Flick)

        bool _combatSkillConsumed;

        /// <summary>BattleCombatManager 執行期注入（不需 Inspector 拖拉）。</summary>
        public void Inject(CombatTuningStore ts, PlayerController pc)
        {
            _tuningStore = ts;
            _player = pc;
        }

        /// <summary>
        /// 直立戰鬥：在非搖桿區（上半螢幕）的快速點擊視為觸發專屬技（單發邊緣）。
        /// 編輯器另外可用 F 鍵（由 Player 直接吃掉）；此處仍以觸控路徑補強。
        /// </summary>
        public bool ConsumeCombatSkillPulse()
        {
            if (!_combatSkillConsumed) return false;
            _combatSkillConsumed = false;
            return true;
        }

        static bool FingerNotStick(int fingerIdStick, Touch t) =>
            fingerIdStick < 0 || t.fingerId != fingerIdStick;

        static bool IsCombatScreenZone(Vector2 screenPosPx, float stickZonePxBottom)
            => screenPosPx.y >= Mathf.Max(stickZonePxBottom + 32f, Screen.height * 0.52f);

        void Reset()
        {
            _tuningStore = FindAnyObjectByType<CombatTuningStore>();
            _player      = FindAnyObjectByType<PlayerController>();
        }

        void Update()
        {
            if (_player == null) return;
            var tuning = _tuningStore != null ? _tuningStore.Active : null;
            if (tuning == null)
            {
                _player.MoveInput = Vector2.zero;
                return;
            }

            var zoneH = Mathf.Clamp01(tuning.觸控操作區高度比例);
            var maxR = Mathf.Max(24f, Mathf.Min(Screen.width, Screen.height) * Mathf.Clamp01(tuning.虛擬搖桿最大半徑_螢幕短邊比));

            var move = Vector2.zero;

#if UNITY_EDITOR || UNITY_STANDALONE
            // ✦ MH NOW 鍵盤雙擊方向鍵閃避（如 AA 往左閃、DD 往右閃）
            CheckDoubleTapKeyboardDodge();

            // Space 鍵閃避（優先處理）
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.LeftShift))
                _player.TryDodge(_player.MoveInputSnapshot);

            // WASD / 方向鍵：優先於虛擬搖桿
            move = ProcessKeyboard();
#endif

            var stickPx = Screen.height * zoneH;

            if (move.sqrMagnitude < 0.01f)
            {
                if (Input.touchCount > 0)
                    move = ProcessTouches(zoneH, maxR);
#if UNITY_EDITOR || UNITY_STANDALONE
                else
                    move = ProcessMouse(zoneH, maxR);
#endif
            }

            ProcessCombatZoneTaps(stickPx);
#if UNITY_EDITOR || UNITY_STANDALONE
            ProcessCombatSkillMouseRelease(stickPx);
#endif

            _player.MoveInput = move;
        }

        void ProcessCombatZoneTaps(float stickPxFromBottom)
        {
            for (var i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (!FingerNotStick(_fingerId, t)) continue;
                if (!IsCombatScreenZone(t.position, stickPxFromBottom)) continue;
                if (t.phase == TouchPhase.Ended && t.deltaPosition.sqrMagnitude < 2600f)
                    _combatSkillConsumed = true;
            }
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        void ProcessCombatSkillMouseRelease(float stickPxFromBottom)
        {
            if (!Input.GetMouseButtonUp(0)) return;

            var p = (Vector2)Input.mousePosition;
            // 由下往上操作的搖桿區鬆手不觸發專屬技
            if (p.y <= stickPxFromBottom + 48f)
                return;

            _combatSkillConsumed = true;
        }
#endif

        Vector2 ProcessTouches(float zoneH, float maxR)
        {
            var zonePx = Screen.height * zoneH;

            if (!_tracking)
            {
                for (var i = 0; i < Input.touchCount; i++)
                {
                    var t = Input.GetTouch(i);
                    if (t.phase != TouchPhase.Began) continue;
                    if (t.position.y > zonePx) continue;
                    _tracking = true;
                    _fingerId = t.fingerId;
                    _pressOrigin = t.position;
                    _pressTime = Time.time; // ✦ 記錄按壓起始時間
                    break;
                }

                if (!_tracking) return Vector2.zero;
            }

            var found = false;
            Vector2 current = _pressOrigin;
            for (var i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.fingerId != _fingerId) continue;
                found = true;
                current = t.position;

                // ✦ MH NOW 滑動手勢判定：當滑動距離大於 80 像素，且花費時間少於 0.22 秒
                var delta = current - _pressOrigin;
                if (delta.magnitude > 80f && (Time.time - _pressTime) <= 0.22f)
                {
                    _player.TryDodge(delta.normalized);
                    // 觸發閃避後，立即打斷搖桿追蹤，防止獵人起滾後立刻走動，保證手感乾淨俐落！
                    _tracking = false;
                    _fingerId = -1;
                    return Vector2.zero;
                }

                if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                {
                    _tracking = false;
                    _fingerId = -1;
                    return Vector2.zero;
                }

                break;
            }

            if (!found)
            {
                _tracking = false;
                _fingerId = -1;
                return Vector2.zero;
            }

            return StickFromDelta(current - _pressOrigin, maxR);
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        Vector2 ProcessMouse(float zoneH, float maxR)
        {
            var zonePx = Screen.height * zoneH;
            var pos = (Vector2)Input.mousePosition;

            if (Input.GetMouseButtonDown(0) && pos.y <= zonePx)
            {
                _tracking = true;
                _fingerId = -2;
                _pressOrigin = pos;
                _pressTime = Time.time; // ✦ 記錄按壓起始時間
            }

            if (!_tracking || _fingerId != -2) return Vector2.zero;

            // ✦ MH NOW 電腦滑鼠滑動判定：當快速划動大於 80 像素，且時間少於 0.22 秒
            var delta = pos - _pressOrigin;
            if (delta.magnitude > 80f && (Time.time - _pressTime) <= 0.22f)
            {
                _player.TryDodge(delta.normalized);
                _tracking = false;
                _fingerId = -1;
                return Vector2.zero;
            }

            if (Input.GetMouseButton(0))
                return StickFromDelta(pos - _pressOrigin, maxR);

            if (Input.GetMouseButtonUp(0))
            {
                _tracking = false;
                _fingerId = -1;
            }

            return Vector2.zero;
        }
#endif

#if UNITY_EDITOR || UNITY_STANDALONE
        static Vector2 ProcessKeyboard()
        {
            var x = 0f;
            var y = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  y -= 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    y += 1f;
            var v = new Vector2(x, y);
            return v.sqrMagnitude > 0.01f ? v.normalized : Vector2.zero;
        }
#endif

        static Vector2 StickFromDelta(Vector2 deltaPixels, float maxRadius)
        {
            if (maxRadius <= 0.01f) return Vector2.zero;
            var mag = deltaPixels.magnitude;
            if (mag <= 0.01f) return Vector2.zero;
            var t = Mathf.Min(1f, mag / maxRadius);
            return deltaPixels.normalized * t;
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        // ✦ MH NOW 鍵盤雙擊方向鍵 (W W, A A, S S, D D) 閃避判定
        private enum KeyDirection { None, Up, Down, Left, Right }
        private KeyDirection _lastPressedDir = KeyDirection.None;
        private float _lastPressTime = 0f;

        private void CheckDoubleTapKeyboardDodge()
        {
            KeyDirection pressedThisFrame = KeyDirection.None;
            
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
                pressedThisFrame = KeyDirection.Left;
            else if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
                pressedThisFrame = KeyDirection.Right;
            else if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
                pressedThisFrame = KeyDirection.Up;
            else if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
                pressedThisFrame = KeyDirection.Down;

            if (pressedThisFrame != KeyDirection.None)
            {
                float now = Time.time;
                if (pressedThisFrame == _lastPressedDir && (now - _lastPressTime) <= 0.25f)
                {
                    Vector2 dodgeDir = Vector2.zero;
                    switch (pressedThisFrame)
                    {
                        case KeyDirection.Left:  dodgeDir = Vector2.left;  break;
                        case KeyDirection.Right: dodgeDir = Vector2.right; break;
                        case KeyDirection.Up:    dodgeDir = Vector2.up;    break;
                        case KeyDirection.Down:  dodgeDir = Vector2.down;  break;
                    }
                    _player.TryDodge(dodgeDir);
                    
                    // 觸發後重設，防止高頻誤觸
                    _lastPressedDir = KeyDirection.None;
                    _lastPressTime = 0f;
                }
                else
                {
                    _lastPressedDir = pressedThisFrame;
                    _lastPressTime = now;
                }
            }
        }
#endif
    }
}
