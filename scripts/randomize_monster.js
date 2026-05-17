import fs from 'fs';

const filePath = 'C:/Users/liskyo/Desktop/MonsterHunter_2026/GameClient/Assets/Scripts/UI/BattlePreviewBootstrap.cs';
let content = fs.readFileSync(filePath, 'utf8');

// The exact method block to match
const targetCode = `        string ResolveTargetMonsterId(任務資料列 quest, LocalHunterLedger ledgerSnapshot)
        {
            var questMonster = FallbackQuestMonsterId(quest);

            var paintId =
                PreferInspectorOrLedger(_previewPaintballItemId, ledgerSnapshot.PreviewPaintballItemId);
            var traceId =
                PreferInspectorOrLedger(_previewTraceId, ledgerSnapshot.PreviewTraceId);

            var traceRow       = LookupTraceRow(traceId);
            var pbRow          = LookupPaintballRow(paintId);
            var traceMonsterId = traceRow != null ? traceRow.對應魔物編號?.Trim() : null;

            if (string.IsNullOrEmpty(traceMonsterId))
                return questMonster;

            if (pbRow == null)
                return traceMonsterId;

            var starGuess = traceRow != null && traceRow.魔物星級 > 0
                ? traceRow.魔物星級
                : MonsterStarGuess(traceMonsterId);

            starGuess = Mathf.Max(1, starGuess);

            var lo = Mathf.Max(1, pbRow.吸引星級_最低);
            var hi = Mathf.Max(lo, pbRow.吸引星級_最高);

            if (starGuess >= lo && starGuess <= hi)
                return traceMonsterId;

            Debug.LogWarning(
                $"[BattlePreviewBootstrap] 染色球星級區間[{lo}-{hi}] 與痕跡目標約 {starGuess}★ 不符，沿用任務目標。");

            return questMonster;`;

// The replacement C# code
const replacementCode = `        string ResolveTargetMonsterId(任務資料列 quest, LocalHunterLedger ledgerSnapshot)
        {
            var questMonster = FallbackQuestMonsterId(quest);

            var paintId =
                PreferInspectorOrLedger(_previewPaintballItemId, ledgerSnapshot.PreviewPaintballItemId);
            var traceId =
                PreferInspectorOrLedger(_previewTraceId, ledgerSnapshot.PreviewTraceId);

            var traceRow       = LookupTraceRow(traceId);
            var pbRow          = LookupPaintballRow(paintId);
            var traceMonsterId = traceRow != null ? traceRow.對應魔物編號?.Trim() : null;

            string finalId = questMonster;

            if (!string.IsNullOrEmpty(traceMonsterId))
            {
                if (pbRow == null)
                {
                    finalId = traceMonsterId;
                }
                else
                {
                    var starGuess = traceRow != null && traceRow.魔物星級 > 0
                        ? traceRow.魔物星級
                        : MonsterStarGuess(traceMonsterId);

                    starGuess = Mathf.Max(1, starGuess);

                    var lo = Mathf.Max(1, pbRow.吸引星級_最低);
                    var hi = Mathf.Max(lo, pbRow.吸引星級_最高);

                    if (starGuess >= lo && starGuess <= hi)
                        finalId = traceMonsterId;
                    else
                        Debug.LogWarning($"[BattlePreviewBootstrap] 染色球星級區間[{lo}-{hi}] 與痕跡目標約 {starGuess}★ 不符，沿用任務目標。");
                }
            }

            // ✦ 測試專用：若目標魔物是預設的 MON_001，隨機挑選 MON_001 至 MON_020 讓每次試玩體驗完全不同！
            if (finalId == "MON_001" || string.IsNullOrEmpty(finalId))
            {
                var randNum = UnityEngine.Random.Range(1, 21); // 1 ~ 20 隨機
                finalId = "MON_" + randNum.ToString("D3");
                Debug.Log("[BattlePreviewBootstrap] ✦ 觸發魔物隨機試玩！隨機挑選出魔物：" + finalId);
            }

            return finalId;`;

// Normalize line endings to do a robust match
const normalize = str => str.replace(/\r\n/g, '\n').trim();

const normalizedContent = normalize(content);
const normalizedTarget = normalize(targetCode);
const normalizedReplacement = normalize(replacementCode);

if (normalizedContent.includes(normalizedTarget)) {
    // Perform replacement using normalized content, then rewrite with standard Windows CRLF line endings
    const newContent = content.replace(/\r\n/g, '\n').replace(normalizedTarget, normalizedReplacement).replace(/\n/g, '\r\n');
    fs.writeFileSync(filePath, newContent, 'utf8');
    console.log('[Success] C# code successfully updated!');
} else {
    console.error('[Error] Target C# code block not found in BattlePreviewBootstrap.cs!');
    // Let's do a substring match just in case
    const partialTarget = "var questMonster = FallbackQuestMonsterId(quest);";
    if (normalizedContent.includes(partialTarget)) {
        console.log('[Info] Partial target found. The file has correct namespace.');
    }
}
