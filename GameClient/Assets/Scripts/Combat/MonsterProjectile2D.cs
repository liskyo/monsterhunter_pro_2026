using MonsterHunter.Controllers;
using MonsterHunter.DataModels;
using MonsterHunter.UI;
using UnityEngine;

namespace MonsterHunter.Combat
{
    /// <summary>
    /// 魔物發射的 2D 投射物占位：球／方塊／閃電細條，命中獵人一次結算後自毀。
    /// </summary>
    public sealed class MonsterProjectile2D : MonoBehaviour
    {
        enum ProjectileVisual { Orb, Block, Bolt }

        Rigidbody2D _rb;
        SpriteRenderer _sr;
        Vector2 _dir;
        float _speed;
        float _lifeLeft;

        CircleCollider2D _circleCap;
        BoxCollider2D _boxCap;
        CapsuleCollider2D _capsuleCap;
        float _dmg;
        bool _hitDone;

        public static void Fire(Transform caster, Transform target, 魔物招式攻擊項 spec, int damageFlat)
        {
            if (BattleCombatManager.IsBattleConcluded)
                return;

            if (caster == null || target == null || spec == null || damageFlat <= 0) return;

            var speed = spec.投射物速度 > 0.65f ? spec.投射物速度 : 7.2f;

            // ✦ 根據施法魔物的星等，高階魔物的遠程彈道飛行速度越快，越難捉摸！
            var monsterAi = caster.GetComponent<MonsterAiController>();
            if (monsterAi != null && monsterAi.DataRow != null)
            {
                int star = monsterAi.DataRow.星級;
                float starSpeedFactor = 1f + (star - 1) * 0.08f;
                speed *= starSpeedFactor;
            }

            var radius = spec.投射物半徑 > 0.06f ? spec.投射物半徑 : 0.28f;

            // ✦ 必殺大招「落雷角」與「岩塊投擲」投射物史詩級加強：速度與半徑大幅區分！
            if (spec.名稱 == "落雷角")
            {
                speed *= 1.8f;   // 極速！(達到 15.3 速度)
                radius *= 2.5f;  // 超大閃電！
            }
            else if (spec.名稱 == "岩塊投擲")
            {
                speed *= 1.3f;
                radius *= 1.7f;
            }

            var range = Mathf.Max(1f, spec.攻擊距離);
            var lifetime = Mathf.Clamp(range / speed + 1.05f, 1.05f, 9f);

            var toRaw = ((Vector2)target.position - (Vector2)caster.position);
            var toNorm = toRaw.sqrMagnitude > 0.001f ? toRaw.normalized : Vector2.left;

            var muzzleOffset = Mathf.Clamp(radius + 0.42f, 0.42f, 1.15f);
            var start = (Vector2)caster.position + toNorm * muzzleOffset;

            var vis = NormalizeVisual(spec.投射物型別);

            var go = new GameObject("MonsterProjectile");
            go.transform.position = new Vector3(start.x, start.y, 0f);
            var pr = go.AddComponent<MonsterProjectile2D>();
            pr.InitPhysics();
            pr._dmg = damageFlat;

            switch (vis)
            {
                case ProjectileVisual.Bolt:
                    pr.SetupAsBolt(caster.position, target.position, speed, lifetime,
                        Mathf.Max(radius * 6f, 1.15f),
                        Mathf.Max(radius * 0.42f, 0.068f));
                    break;
                case ProjectileVisual.Block:
                    pr.SetupAsBlock(caster.position, target.position, speed, lifetime,
                        Mathf.Max(radius * 2f, 0.42f));
                    break;
                default:
                    pr.SetupAsOrb(caster.position, target.position, speed, lifetime, radius * 2f);
                    break;
            }
        }

        static ProjectileVisual NormalizeVisual(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return ProjectileVisual.Orb;

            raw = raw.Trim();
            foreach (var c in raw)
            {
                switch (c)
                {
                    case '方':
                    case '塊':
                    case '棱':
                        return ProjectileVisual.Block;
                    case '閃':
                    case '電':
                    case '雷':
                    case '弧':
                        return ProjectileVisual.Bolt;
                }
            }

            var low = raw.ToLowerInvariant();
            if (low.Contains("block") || low.Contains("square") || low.Contains("cube"))
                return ProjectileVisual.Block;
            if (low.Contains("bolt") || low.Contains("lightning") || low.Contains("zap"))
                return ProjectileVisual.Bolt;

            return ProjectileVisual.Orb;
        }

        void InitPhysics()
        {
            _rb = gameObject.AddComponent<Rigidbody2D>();
            _rb.bodyType       = RigidbodyType2D.Kinematic;
            _rb.gravityScale   = 0f;
            _rb.freezeRotation = true;

            _circleCap           = gameObject.AddComponent<CircleCollider2D>();
            _circleCap.isTrigger = true;
            _circleCap.enabled   = false;

            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.sortingOrder     = 48;
            _sr.sortingLayerName = "Default";
        }

        void Aim(Vector3 from, Vector3 to, float speed)
        {
            var d = ((Vector2)to - (Vector2)from);
            _dir   = d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.left;
            _speed = speed;
        }

        void SetupAsOrb(Vector3 from, Vector3 to, float speed, float life, float diameterWorld)
        {
            Aim(from, to, speed);
            _lifeLeft = life;

            if (_capsuleCap != null)
            {
                Destroy(_capsuleCap);
                _capsuleCap = null;
            }

            if (_boxCap != null)
            {
                Destroy(_boxCap);
                _boxCap = null;
            }

            _circleCap.enabled = true;
            _circleCap.radius  = Mathf.Max(diameterWorld * 0.5f, 0.06f);

            _sr.sprite     = PlaceholderSpriteFactory.GetSharedOrbSprite();
            _sr.color      = new Color(1f, 0.52f, 0.62f);
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg);
            transform.localScale =
                Vector3.one * Mathf.Max(diameterWorld / 2f, 0.18f);
        }

        void SetupAsBlock(Vector3 from, Vector3 to, float speed, float life, float edgeWorld)
        {
            Aim(from, to, speed);
            _lifeLeft = life;

            _circleCap.enabled = false;
            if (_capsuleCap != null)
            {
                Destroy(_capsuleCap);
                _capsuleCap = null;
            }

            if (_boxCap == null) _boxCap = gameObject.AddComponent<BoxCollider2D>();
            _boxCap.enabled   = true;
            _boxCap.isTrigger = true;
            _boxCap.size      = new Vector2(edgeWorld, edgeWorld);

            _sr.sprite = PlaceholderSpriteFactory.GetSharedBlockSprite();
            _sr.color  = new Color(0.92f, 0.78f, 1f);
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg);
            transform.localScale = Vector3.one * Mathf.Max(edgeWorld / 2f, 0.22f);
        }

        void SetupAsBolt(Vector3 from, Vector3 to, float speed, float life, float lengthWorld,
            float thicknessWorld)
        {
            Aim(from, to, speed);
            _lifeLeft = life;

            _circleCap.enabled = false;
            if (_boxCap != null) Destroy(_boxCap);
            _boxCap = null;

            if (_capsuleCap != null)
                Destroy(_capsuleCap);

            _capsuleCap = gameObject.AddComponent<CapsuleCollider2D>();
            _capsuleCap.direction = CapsuleDirection2D.Horizontal;
            _capsuleCap.isTrigger = true;
            _capsuleCap.size = new Vector2(lengthWorld, Mathf.Max(thicknessWorld, 0.052f));

            _sr.sprite = PlaceholderSpriteFactory.GetSharedBoltSprite();
            _sr.color  = new Color(1f, 0.95f, 0.35f);
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg);
            transform.localScale =
                Vector3.one * Mathf.Max(lengthWorld / 4f, 0.22f);
        }

        void FixedUpdate()
        {
            if (BattleCombatManager.IsBattleConcluded)
            {
                Destroy(gameObject);
                return;
            }

            _lifeLeft -= Time.fixedDeltaTime;
            if (_lifeLeft <= 0f || _hitDone)
            {
                Destroy(gameObject);
                return;
            }

            var step = _dir * (_speed * Time.fixedDeltaTime);
            _rb.MovePosition(_rb.position + step);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (BattleCombatManager.IsBattleConcluded)
                return;

            if (_hitDone || other == null) return;

            var pc = other.GetComponentInParent<PlayerController>();
            if (pc == null) return;

            _hitDone = true;
            pc.ApplyDamage(_dmg, false);
            Destroy(gameObject);
        }
    }
}
