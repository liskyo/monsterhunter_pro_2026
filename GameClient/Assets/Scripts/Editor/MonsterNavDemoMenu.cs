#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace MonsterHunter.EditorTools
{
    /// <summary>
    /// 產生 <see cref="MonsterHunter.Monster.MonsterController"/> 預設死亡 Trigger「Die」與對應 Animator 狀態（示範用縮放為死亡動畫）。
    /// </summary>
    public static class MonsterNavDemoMenu
    {
        const string ResourceRoot = "Assets/Resources/MonsterNavDemo";

        [MenuItem("Monster Hunter/Nav Demo/Generate Animator Assets")]
        public static void GenerateAnimatorAssets()
        {
            System.IO.Directory.CreateDirectory(ResourceRoot);

            string clipPath = $"{ResourceRoot}/DeathFallback.anim";
            string controllerPath = $"{ResourceRoot}/MonsterDemoAnimator.controller";

            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
                AssetDatabase.DeleteAsset(controllerPath);

            if (AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath) != null)
                AssetDatabase.DeleteAsset(clipPath);

            AssetDatabase.Refresh();

            var clip = new AnimationClip { name = "DeathFallback" };
            var curve = AnimationCurve.Linear(0f, 1f, 0.75f, 0.05f);
            clip.SetCurve(string.Empty, typeof(Transform), "localScale.x", curve);
            clip.SetCurve(string.Empty, typeof(Transform), "localScale.y", curve);
            clip.SetCurve(string.Empty, typeof(Transform), "localScale.z", curve);
            AssetDatabase.CreateAsset(clip, clipPath);

            clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);

            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            controller.AddParameter("Die", AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine root = controller.layers[0].stateMachine;
            AnimatorState idle = root.AddState("Idle");
            root.defaultState = idle;

            AnimatorState death = root.AddState("Death");
            death.motion = clip;

            AnimatorStateTransition anyToDeath = root.AddAnyStateTransition(death);
            anyToDeath.duration = 0.05f;
            anyToDeath.hasExitTime = false;
            anyToDeath.AddCondition(AnimatorConditionMode.If, 0, "Die");

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Monster Nav Demo：已建立 Animator 與 Clip，路徑：{ResourceRoot}", controller);
        }
    }
}
#endif
