using System.Collections.Generic;
using System.IO;
using MonsterHunter.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MonsterHunter.Editor
{
    /// <summary>
    /// 產生「遊戲標題→貓飯食堂→Bootstrap」之手機開場場景並插入 Build Settings 最前面。
    /// </summary>
    public static class VillageIntroSceneSetup
    {
        const string ScenePath = "Assets/Scenes/VillageIntro.unity";
        const string MenuPath = "Monster Hunter/場景：建立開場（遊戲標題→貓飯食堂）並加入 Build 最前";

        [MenuItem(MenuPath)]
        static void BuildVillageIntroScene()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            var fullPath = Path.Combine(Application.dataPath, "Scenes", "VillageIntro.unity");
            if (File.Exists(fullPath))
            {
                if (!EditorUtility.DisplayDialog(
                        "覆寫 VillageIntro",
                        $"已存在 {ScenePath}，要重新建立並覆寫嗎？",
                        "覆寫", "取消"))
                    return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // EventSystem（舊輸入：Standalone Input Module）
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();

            // Canvas 根（直立參考取 PlayerSettings）
            var canvasGo = new GameObject("IntroCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<GraphicRaycaster>();
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var flow = canvasGo.AddComponent<MobileVillageIntroFlow>();

            var invoker = canvasGo.AddComponent<IntroFlowAdvanceInvoker>();
            var soInv = new SerializedObject(invoker);
            soInv.FindProperty("_flow").objectReferenceValue = flow;
            soInv.ApplyModifiedPropertiesWithoutUndo();

            var panelTitle = CreateScreenPanel(canvasGo.transform, "Panel_Title", true);
            var panelCanteen = CreateScreenPanel(canvasGo.transform, "Panel_Canteen", false);
            ConfigureTitlePanel(panelTitle, "遊戲標題", invoker);
            ConfigureCanteenPanel(panelCanteen, "貓飯食堂", flow);

            var soFlow = new SerializedObject(flow);
            soFlow.FindProperty("_panelTitle").objectReferenceValue = panelTitle;
            soFlow.FindProperty("_panelCanteen").objectReferenceValue = panelCanteen;
            soFlow.FindProperty("_nextSceneName").stringValue = "Bootstrap";
            soFlow.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EnsureFirstInBuildSettings(ScenePath);

            AssetDatabase.Refresh();
            EditorSceneManager.OpenScene(ScenePath);
            var intro = GameObject.Find("IntroCanvas");
            if (intro != null)
                Selection.activeGameObject = intro;
            EditorUtility.DisplayDialog(
                "已完成",
                "已建立並已開啟：\n" + ScenePath +
                    "\n\n• Build Settings：此場景已排在最前面（真機從這裡起）。\n" +
                    "• 編輯器：Play 會播放「目前開著的場景」──現已切到 VillageIntro，按 Play 即從標題開始。",
                "確定");

            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath));
        }

        const string OpenMenuPath = "Monster Hunter/開啟 VillageIntro 場景（Play 請開這裡）";

        [MenuItem(OpenMenuPath)]
        static void OpenVillageIntroScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                EditorUtility.DisplayDialog(
                    "尚無 VillageIntro",
                    "請先執行：\n「Monster Hunter → 場景：建立開場（遊戲標題→貓飯食堂）並加入 Build 最前」",
                    "確定");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem(OpenMenuPath, true)]
        static bool ValidateOpenMenu() => !Application.isPlaying;

        [MenuItem(MenuPath, true)]
        static bool ValidateMenu() => !Application.isPlaying;

        static GameObject CreateScreenPanel(Transform parent, string name, bool active)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            StretchFull(rt);
            go.SetActive(active);
            return go;
        }

        static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            rt.localPosition = Vector3.zero;
        }

        static void ConfigureVillageBackground(GameObject panel, string villageKey)
        {
            var boundsGo = new GameObject("CoverBounds");
            boundsGo.transform.SetParent(panel.transform, false);
            var boundsRt = boundsGo.AddComponent<RectTransform>();
            StretchFull(boundsRt);

            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(boundsGo.transform, false);
            var bgRt = bgGo.AddComponent<RectTransform>();
            StretchFull(bgRt);

            var image = bgGo.AddComponent<Image>();
            image.sprite = null;
            image.color = Color.white;

            var vbg = bgGo.AddComponent<VillageBackgroundUiDisplay>();
            var soV = new SerializedObject(vbg);
            soV.FindProperty("_預設村莊鍵").stringValue = villageKey;
            soV.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>標題畫：背景 + 「MONSTER HUNTER」「2006」+ 進入遊戲按鈕（不整張螢幕亂點）。</summary>
        static void ConfigureTitlePanel(GameObject panel, string villageKey, IntroFlowAdvanceInvoker invoker)
        {
            ConfigureVillageBackground(panel, villageKey);

            var chrome = new GameObject("TitleChrome");
            chrome.transform.SetParent(panel.transform, false);
            var chromeRt = chrome.AddComponent<RectTransform>();
            StretchFull(chromeRt);

            Font titleFont = GetTitleFont();

            {
                var go = CreateOutlinedTitleLine(chrome.transform, "TitleLine_Main", "MONSTER HUNTER", 64, titleFont,
                    new Color(0.97f, 0.96f, 0.995f));
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -190f);
                rt.sizeDelta = new Vector2(1080f, 160f);
            }
            {
                var go = CreateOutlinedTitleLine(chrome.transform, "TitleLine_Year", "2006", 124, titleFont,
                    new Color(1f, 0.88f, 0.38f));
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -350f);
                rt.sizeDelta = new Vector2(1080f, 200f);
            }

            CreateEnterGameButton(panel.transform, invoker);
        }

        static void ConfigureCanteenPanel(GameObject panel, string villageKey, MobileVillageIntroFlow flow)
        {
            ConfigureVillageBackground(panel, villageKey);

            var tapGo = new GameObject("TapToAdvance");
            tapGo.transform.SetParent(panel.transform, false);
            var tapRt = tapGo.AddComponent<RectTransform>();
            StretchFull(tapRt);
            var tapImg = tapGo.AddComponent<Image>();
            tapImg.color = new Color(0f, 0f, 0f, 0f);
            tapImg.raycastTarget = true;

            var tap = tapGo.AddComponent<IntroScreenTapAdvance>();
            var soTap = new SerializedObject(tap);
            soTap.FindProperty("_flow").objectReferenceValue = flow;
            soTap.ApplyModifiedPropertiesWithoutUndo();
        }

        static Font GetTitleFont()
        {
            try
            {
                return Font.CreateDynamicFontFromOSFont(new[] { "Impact", "Arial Black", "Segoe UI Black", "Arial" }, 72);
            }
            catch
            {
                return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
        }

        static GameObject CreateOutlinedTitleLine(Transform parent, string name, string line, int fontSize, Font font, Color fill)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            var txt = go.AddComponent<Text>();
            txt.text = line;
            txt.fontSize = fontSize;
            txt.font = font;
            txt.fontStyle = FontStyle.Bold;
            txt.color = fill;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;

            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.82f);
            sh.effectDistance = new Vector2(5f, -5f);

            var ol = go.AddComponent<Outline>();
            ol.effectColor = new Color(0f, 0.02f, 0.08f, 0.93f);
            ol.effectDistance = new Vector2(5f, -5f);

            return go;
        }

        static void CreateEnterGameButton(Transform panel, IntroFlowAdvanceInvoker invoker)
        {
            var btnGo = new GameObject("Button_EnterGame");
            btnGo.transform.SetParent(panel, false);
            var btnRt = btnGo.AddComponent<RectTransform>();
            btnRt.anchorMin = btnRt.anchorMax = new Vector2(0.5f, 0f);
            btnRt.pivot = new Vector2(0.5f, 0f);
            btnRt.anchoredPosition = new Vector2(0f, 300f);
            btnRt.sizeDelta = new Vector2(580f, 128f);

            var btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.1f, 0.09f, 0.07f, 0.95f);
            btnImg.raycastTarget = true;

            var button = btnGo.AddComponent<Button>();
            button.targetGraphic = btnImg;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            var cb = button.colors;
            cb.fadeDuration = 0.06f;
            cb.highlightedColor = new Color(0.92f, 0.92f, 0.92f);
            cb.pressedColor = new Color(0.75f, 0.75f, 0.75f);
            button.colors = cb;

            var evt = button.onClick;
            evt.RemoveAllListeners();
            UnityEventTools.AddVoidPersistentListener(evt, invoker.Advance);

            EditorUtility.SetDirty(button);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(btnGo.transform, false);
            var labelRt = labelGo.AddComponent<RectTransform>();
            StretchFull(labelRt);
            var label = labelGo.AddComponent<Text>();
            label.text = "進入遊戲";
            label.fontSize = 40;
            label.fontStyle = FontStyle.Bold;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.color = new Color(0.96f, 0.9f, 0.78f);
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
        }

        static void EnsureFirstInBuildSettings(string path)
        {
            var previous = EditorBuildSettings.scenes;
            var rest = new List<EditorBuildSettingsScene>(previous.Length);
            foreach (var s in previous)
            {
                if (s.path == path)
                    continue;
                rest.Add(s);
            }

            rest.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = rest.ToArray();
        }
    }
}
