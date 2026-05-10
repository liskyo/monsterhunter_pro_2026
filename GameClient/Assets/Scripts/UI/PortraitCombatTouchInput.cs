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

        void Reset()
        {
            _tuningStore = FindObjectOfType<CombatTuningStore>();
            _player = FindObjectOfType<PlayerController>();
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

            if (Input.touchCount > 0)
            {
                move = ProcessTouches(zoneH, maxR);
            }
            else
            {
#if UNITY_EDITOR || UNITY_STANDALONE
                move = ProcessMouse(zoneH, maxR);
#endif
            }

            _player.MoveInput = move;
        }

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
            }

            if (!_tracking || _fingerId != -2) return Vector2.zero;

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

        static Vector2 StickFromDelta(Vector2 deltaPixels, float maxRadius)
        {
            if (maxRadius <= 0.01f) return Vector2.zero;
            var mag = deltaPixels.magnitude;
            if (mag <= 0.01f) return Vector2.zero;
            var t = Mathf.Min(1f, mag / maxRadius);
            return deltaPixels.normalized * t;
        }
    }
}
