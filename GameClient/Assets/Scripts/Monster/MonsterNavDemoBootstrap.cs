using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace MonsterHunter.Monster
{
    /// <summary>
    /// 示範用：進入 Play 時建立地面、Runtime Bake NavMesh、生成玩家（Tag=Player）與帶 NavMesh 的魔物。
    /// 死亡動畫請先執行選單 Monster Hunter → Nav Demo → Generate Animator Assets。
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class MonsterNavDemoBootstrap : MonoBehaviour
    {
        const string AnimatorResourcesPath = "MonsterNavDemo/MonsterDemoAnimator";

        [Header("Runtime NavMesh")]
        [Tooltip("地面在本機軸的縮放（Unity Plane 預設約 10m；再乘上此縮放）。")]
        [SerializeField] Vector3 groundScale = new Vector3(3f, 1f, 3f);

        [Header("生成位置")]
        [SerializeField] Vector3 playerSpawn = new Vector3(-6f, 1f, 0f);
        [SerializeField] Vector3 monsterSpawn = new Vector3(6f, 1f, 0f);

        [Header("除錯")]
        [SerializeField] bool spawnDemoActors = true;
        [SerializeField] bool logHintIfAnimatorMissing = true;
        [Tooltip("按 K 對魔物造成大量傷害（方便測死亡 Trigger / OnMonsterDeath）。")]
        [SerializeField] bool enableKillCheat = true;

        MonsterController _monster;
        bool _hintedMissingAnimator;

        void Awake()
        {
            if (!Application.isPlaying || !spawnDemoActors)
                return;

            BuildNavMeshHierarchy();
            CreatePlayer();
            var monsterGo = CreateMonster();

            if (enableKillCheat && monsterGo != null)
                _monster = monsterGo.GetComponent<MonsterController>();

            OrbitCameraTowardOrigin();
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
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Player";
            go.tag = "Player";
            go.transform.position = playerSpawn;
            go.GetComponent<Collider>().enabled = true;
            go.AddComponent<PlayerNavDemoMove>();
            return go;
        }

        GameObject CreateMonster()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "MonsterDemo";
            go.transform.position = monsterSpawn;

            var material = go.GetComponent<Renderer>();
            if (material != null)
                material.material.color = new Color(0.75f, 0.2f, 0.15f);

            var agent = go.AddComponent<NavMeshAgent>();
            agent.angularSpeed = 360f;
            agent.acceleration = 20f;

            var animator = go.AddComponent<Animator>();
            var controller = Resources.Load<RuntimeAnimatorController>(AnimatorResourcesPath);
            if (controller != null)
                animator.runtimeAnimatorController = controller;
            else if (logHintIfAnimatorMissing && !_hintedMissingAnimator)
            {
                _hintedMissingAnimator = true;
                Debug.LogWarning(
                    "MonsterNavDemo：找不到 Resources/" + AnimatorResourcesPath +
                    "。請在 Unity 選單執行「Monster Hunter → Nav Demo → Generate Animator Assets」。死亡動畫仍可不套用，但「Die」Trigger 不會有對應狀態。",
                    this);
            }

            go.AddComponent<MonsterController>();
            agent.Warp(monsterSpawn);

            return go;
        }

        void OrbitCameraTowardOrigin()
        {
            var cam = Camera.main;
            if (cam == null)
                return;

            cam.transform.position = new Vector3(0f, 9f, -14f);
            cam.transform.LookAt(new Vector3(0f, 0.5f, 0f));
        }
    }
}
