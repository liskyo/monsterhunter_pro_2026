using MonsterHunter.Combat;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace MonsterHunter.Monster
{
    /// <summary>
    /// 示範用：進入 Play 時建立地面、Runtime Bake NavMesh、生成玩家（Tag=Player）與帶 NavMesh 的魔物。
    /// <list type="bullet">
    ///   <item>可於 Inspector 指定 <c>playerPrefab</c>；未指定時使用內建膠囊＋命中盒。</item>
    ///   <item>內建膠囊為根節點 + <b>Body</b> 子物件（Animator／Slash 應掛在 Body）。</item>
    ///   <item>根節點使用 <see cref="CharacterController"/>，魔物 roots 設實體 <see cref="BoxCollider"/> 與 Agent 止步距離，減少重疊穿模。</item>
    ///   <item><see cref="WeaponHitbox"/> 子物件上加 Kinematic Rigidbody，以符合 Trigger 偵測需求。</item>
    /// </list>
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class MonsterNavDemoBootstrap : MonoBehaviour
    {
        [Header("Runtime NavMesh")]
        [Tooltip("地面在本機軸的縮放（Unity Plane 預設約 10m；再乘上此縮放）。")]
        [SerializeField] Vector3 groundScale = new Vector3(3f, 1f, 3f);

        [Header("玩家")]
        [Tooltip(
            "有指派：在此座標 Instantiate 為玩家。\n留空：使用場景內建膠囊＋WeaponHitbox。\nPrefab 根建議標籤為 Player（否則執行期會嘗試指定），並自行掛 CharacterController／PlayerNavDemoMove／命中盒等。")]
        public GameObject playerPrefab;

        [Header("生成位置")]
        [Tooltip("玩家根節點立在地面（Y=腳底）；先前 Y=1 會讓腳離地。")]
        [SerializeField] Vector3 playerSpawn = new Vector3(-3.5f, 0f, 0f);
        [Tooltip("魔物以 XZ 對齊；實際生成時根節點貼地 Y=0。")]
        [SerializeField] Vector3 monsterSpawn = new Vector3(3.5f, 0f, 0f);

        [Header("相機")]
        [Tooltip("依玩家／魔物對位；直向 viewport（aspect 小於 1）會自動拉遠並提高視野，避免角色在畫面外。")]
        [SerializeField] bool _frameCameraOnActors = true;

        [Header("近戰判定（階段二）")]
        [SerializeField] float _weaponDamage = 12f;
        [SerializeField] KeyCode _attackKey = KeyCode.Mouse0;
        [SerializeField] float _weaponSwingSeconds = 0.14f;
        [SerializeField] bool _weaponHitstopOnHit;

        [Header("除錯")]
        [SerializeField] bool spawnDemoActors = true;
        [Tooltip("按 K 對魔物造成大量傷害（方便測死亡 Trigger / OnMonsterDeath）。")]
        [SerializeField] bool enableKillCheat = true;

        MonsterController _monster;

        void Awake()
        {
            if (!Application.isPlaying || !spawnDemoActors)
                return;

            BuildNavMeshHierarchy();
            var playerGo = CreatePlayer();
            var monsterGo = CreateMonster();

            Vector3 lookAt = monsterGo.transform.position;
            lookAt.y = playerGo.transform.position.y + 1f;
            playerGo.transform.LookAt(lookAt);

            if (enableKillCheat && monsterGo != null)
                _monster = monsterGo.GetComponent<MonsterController>();

            if (_frameCameraOnActors)
                FrameMainCameraOnActors(playerGo.transform, monsterGo.transform);
        }

        void Update()
        {
            if (!enableKillCheat || _monster == null || !_monster || _monster.IsDead)
                return;

            if (Input.GetKeyDown(KeyCode.K))
                _monster.TakeDamage(99999f);
        }

        void BuildNavMeshHierarchy()
        {
            var root = new GameObject("NavMeshBakeRoot");
            root.transform.SetParent(transform, false);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(root.transform, false);
            ground.transform.localScale = groundScale;
            ground.isStatic = true;

            var surface = ground.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.BuildNavMesh();
        }

        GameObject CreatePlayer()
        {
            if (playerPrefab != null)
                return InstantiatePlayerPrefab();

            return CreatePlayerPrimitiveCapsule();
        }

        /// <summary>由 Inspector 指派之 Prefab 生成玩家；攝影機構圖與 <see cref="MonsterController"/> 找 Tag 的流程與 Primitive 版本相同。</summary>
        GameObject InstantiatePlayerPrefab()
        {
            var root = Instantiate(playerPrefab, playerSpawn, Quaternion.identity);
            root.name = playerPrefab.name;
            EnsurePlayerRootTag(root);
            root.transform.position = playerSpawn;
            return root;
        }

        static void EnsurePlayerRootTag(GameObject root)
        {
            if (root == null || root.CompareTag("Player"))
                return;

            try
            {
                root.tag = "Player";
            }
            catch (UnityException ex)
            {
                Debug.LogWarning(
                    $"[MonsterNavDemoBootstrap] 無法將玩家根物件設為 Tag「Player」（請在 Edit → Project Settings → Tags and Layers 建立 Player）：{ex.Message}",
                    root);
            }
        }

        GameObject CreatePlayerPrimitiveCapsule()
        {
            var root = new GameObject("Player");
            root.tag = "Player";
            root.transform.position = playerSpawn;

            var cc = root.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.radius = 0.46f;
            cc.center = new Vector3(0f, 1f, 0f);
            cc.skinWidth = 0.08f;

            root.AddComponent<PlayerNavDemoMove>();

            // 視覺與 Slash 請掛在 Body；Root 只吃位移／面向，可避免揮砍把整個控制器「橫躺下來」。
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            Object.Destroy(body.GetComponent<CapsuleCollider>());

            var wpn = new GameObject("WeaponHitbox");
            wpn.transform.SetParent(body.transform, false);
            wpn.transform.localPosition = new Vector3(0f, 0f, 0.55f);
            wpn.transform.localRotation = Quaternion.identity;
            var box = wpn.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(0.75f, 0.35f, 1.2f);
            box.center = new Vector3(0f, 0f, 0.45f);

            var wrb = wpn.AddComponent<Rigidbody>();
            wrb.isKinematic = true;
            wrb.useGravity = false;
            wrb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            var wh = wpn.AddComponent<WeaponHitbox>();
            wh.ConfigureNavDemoSwing(_weaponDamage, _attackKey, _weaponSwingSeconds, _weaponHitstopOnHit);

            return root;
        }

        GameObject CreateMonster()
        {
            var root = new GameObject("MonsterDemo");
            root.tag = "Monster";
            root.transform.position = new Vector3(monsterSpawn.x, 0f, monsterSpawn.z);

            var blocker = root.AddComponent<BoxCollider>();
            blocker.isTrigger = false;
            blocker.center = new Vector3(0f, 0.6f, 0f);
            blocker.size = new Vector3(1.22f, 1.22f, 1.22f);

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Body";
            cube.transform.SetParent(root.transform, false);
            cube.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            cube.transform.localScale = Vector3.one * 1.2f;
            Object.Destroy(cube.GetComponent<Collider>());

            var rend = cube.GetComponent<Renderer>();
            if (rend != null)
                rend.material.color = new Color(0.75f, 0.2f, 0.15f);

            var hurtChild = new GameObject("MonsterHurtbox");
            hurtChild.transform.SetParent(root.transform, false);
            hurtChild.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            var hurtBox = hurtChild.AddComponent<BoxCollider>();
            hurtBox.isTrigger = true;
            hurtBox.size = new Vector3(1.15f, 1.15f, 1.15f);
            hurtChild.AddComponent<MonsterHurtbox>();

            var agent = root.AddComponent<NavMeshAgent>();
            agent.speed = 4.5f;
            agent.stoppingDistance = 1.75f;
            agent.angularSpeed = 360f;
            agent.acceleration = 20f;
            agent.radius = 0.55f;
            agent.height = 1.35f;

            root.AddComponent<MonsterController>();

            agent.Warp(root.transform.position);

            return root;
        }

        /// <summary>
        /// 對準玩家與魔物中點。9:16 等直向視窗水平可視變窄，若仍用固定鏡位與 FOV，兩側角色容易出框。
        /// 此處依兩者距離推算需要的水平視野，再轉成 Camera 的垂直 FOV。
        /// </summary>
        void FrameMainCameraOnActors(Transform playerTf, Transform monsterTf)
        {
            Camera cam = Camera.main;
            if (cam == null)
                return;

            Vector3 p = playerTf.position;
            Vector3 m = monsterTf.position;
            Vector3 focus = (p + m) * 0.5f;
            focus.y = (p.y + m.y + 0.6f) * 0.5f;

            float spanX = Mathf.Abs(p.x - m.x);
            float spanZ = Mathf.Abs(p.z - m.z);
            float span = Mathf.Max(spanX, spanZ, 2f);

            float aspect = Mathf.Max(cam.aspect, 0.01f);
            const float margin = 1.45f;
            float requiredHalfWidth = span * margin * 0.5f;

            float distance = Mathf.Clamp(span * 2.1f + 5.5f, 11f, 30f);
            float height = Mathf.Clamp(span * 0.42f + 4.8f, 4.5f, 11f);

            float horizontalFovDeg = 2f * Mathf.Atan(requiredHalfWidth / distance) * Mathf.Rad2Deg;
            horizontalFovDeg = Mathf.Clamp(horizontalFovDeg, 52f, 98f);

            float verticalFov = Camera.HorizontalToVerticalFieldOfView(horizontalFovDeg, aspect);
            cam.fieldOfView = Mathf.Clamp(verticalFov, 40f, 88f);

            cam.transform.position = focus + new Vector3(0f, height, -distance);
            cam.transform.LookAt(focus + Vector3.up * 0.15f);
        }
    }
}
