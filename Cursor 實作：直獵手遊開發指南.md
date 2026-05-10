使用 **Cursor** 來協助開發這款《魔物獵人》風格的直立式動作手遊，是極度明智的決定！Cursor 這類 AI 驅動的編輯器最擅長處理\*\*「架構清晰、資料結構明確、邏輯規則嚴謹」\*\*的專案，而我們前面梳理的 16 個 JSON 檔案與系統架構，正是它最渴望的完美「上下文（Context）」。  
為了讓 Cursor 發揮 100% 的實力，並避免它產生幻覺或寫出義大利麵條般的混亂程式碼，你需要在開發初期餵給它以下 **四大核心資料與指令**：

### 第一步：建立 .cursorrules (專案全局規則檔)

在你的專案根目錄建立一個名為 .cursorrules 的純文字檔。Cursor 每次生成程式碼前都會讀取這個檔案，確保它了解你的遊戲精神與技術限制。  
**請將以下內容直接複製貼上給 Cursor（以 Unity 或 Godot 為例，請依你的決定修改）：**  
\# 專案概述：直立式動作狩獵手遊 (Project Overview)  
\- 遊戲類型：2D/3D Top-down 動作狩獵遊戲  
\- 螢幕方向：直立式 (Portrait)  
\- 開發引擎：\[填入 Unity C\# / Godot GDScript / Cocos Creator\]  
\- 後端服務：Supabase (PostgreSQL, Edge Functions)  
\- 多人連線：最多 2 人 (Host-Client P2P 架構)

\# 核心架構原則 (Architecture Guidelines)  
1\. 嚴格遵守「資料驅動 (Data-Driven)」：所有靜態數值必須從 \`DesignData\` 資料夾下的 JSON 讀取，程式碼中不可寫死數值 (No Hardcoding)。  
2\. UI 架構：遵守「60/40 戰鬥視角模型」，UI 必須支援「九宮格錨點 (9-point anchoring)」與「安全區 (Safe Area)」適應。  
3\. 戰鬥架構：強制將 Hitbox (攻擊判定) 與 Hurtbox (受擊判定) 徹底分離。大型魔物必須擁有多重 Hurtbox (頭、翼、尾)。  
4\. 傷害公式：最終傷害 \= (武器基礎物理 \* 動作值 MV \* 物理肉質 HZV) \+ (武器屬性 \* 屬性肉質 HZV)。  
5\. 模組化：魔物與寵物 AI 優先使用行為樹 (Behavior Trees)，而非有限狀態機 (FSM)。

### 第二步：提供專案目錄結構指令

告訴 Cursor 你的專案長什麼樣子，讓它知道生成的腳本該放在哪裡。你可以用對話的方式告訴它：  
**💬 給 Cursor 的對話指令：**  
「這是我規劃的專案資料夾結構，請你在後續生成程式碼或資源時，嚴格遵守這個路徑規範來存放檔案：

1. /DesignData：存放所有的企劃 16 個 JSON 檔。  
2. /GameClient/Scripts/DataModels：存放用來反序列化 JSON 的資料類別。  
3. /GameClient/Scripts/Combat：存放 Hitbox, Hurtbox, 傷害計算等核心邏輯。  
4. /GameClient/Scripts/UI：存放雷達介面、虛擬搖桿、裝備樹狀圖的控制邏輯。  
5. /Backend/supabase/functions：存放後端 Edge Functions 邏輯。」

### 第三步：匯入我們設計的「16 個 JSON 資料庫」

Cursor 有很強的 @Files 功能。你應該先把我們之前設計好的 monsters.json、equipment.json、weapon\_movesets.json 等 16 個檔案建立在 /DesignData 資料夾中。  
接著，你可以下達第一個實作指令，讓 Cursor 自動幫你寫出所有的資料綁定（Data Binding）程式碼：  
**💬 給 Cursor 的實作指令（資料層）：**  
「請讀取 @DesignData 目錄下的所有 JSON 檔案。我的遊戲引擎是 填入你的引擎。請幫我為這些 JSON 寫出對應的反序列化腳本（Data Models / ScriptableObjects / Resources）。要求：

1. 欄位型別必須正確對應（例如 star\_rating 是 int，drop\_rate 是 float）。  
2. 幫我寫一個 DataManager 的單例 (Singleton) 或全域管理器，負責在遊戲啟動時載入這些 JSON 資料到內存中，供戰鬥系統零延遲讀取。」

### 第四步：依據模組「分階段」給予實作指令

不要一次叫 Cursor 寫出整個遊戲！請依照我們之前討論的架構，**一個模組一個模組地請它實作**。以下是幾個可以直接使用的精準 Prompt 範例：

#### 實作模組 A：戰鬥判定系統 (Hitbox & Hurtbox)

**💬 給 Cursor 的指令：**  
「我們現在要實作戰鬥底層的碰撞系統。請幫我寫出 Hitbox 與 Hurtbox 兩個核心腳本。規則：

1. Hitbox 帶有參數：Weapon\_Stats (攻擊力/屬性)、Attack\_ID (用來查詢 weapon\_movesets.json 的動作值 MV)。  
2. Hurtbox 帶有參數：Part\_Name (頭/翼/尾)、HZV\_Physical (物理肉質)、HZV\_Elemental (屬性肉質)。  
3. 當 Hitbox 進入 Hurtbox 的範圍時，由 Hurtbox 觸發 TakeDamage 事件，並帶入我們設計的傷害公式進行計算。  
4. 這些判定區不能與地形的物理引擎發生碰撞（純 Overlap 偵測）。」

#### 實作模組 B：「動靜結合」與 UI 控制系統

**💬 給 Cursor 的指令：**  
「請參考 @weapon\_movesets.json 實作玩家的『動靜結合』操作控制器。規則：

1. 畫面下半部 40% 是操作區。當玩家手指觸碰並拖曳時，生成隱形的虛擬搖桿 (Invisible Joystick) 進行移動，此時不可攻擊。  
2. 當手指鬆開 (Release) 時，進入靜止狀態，角色自動面向最近的魔物（或鎖定的 Hurtbox）並根據武器類型觸發普攻連段。  
3. 在畫面上點擊單次 (Tap) 或長按 (Hold) 時，讀取 json 中的動作倍率執行特殊技能（例如大劍蓄力斬）。」

#### 實作模組 C：雷達介面與 API Payload

**💬 給 Cursor 的指令：**  
「我要實作村莊的雷達 UI 介面。

1. 請實作一個中央有大按鈕的介面，旁邊有一個 \+ 號插槽。  
2. 點擊 \+ 號會打開清單，讀取玩家背包的 @monster\_traces.json (魔物痕跡)。  
3. 按下發射時，組裝一個 JSON Payload (包含玩家ID、染色球ID、附加痕跡ID)，並寫一個非同步函數 (Async) 模擬發送 API 請求給 Supabase。」

### 開發小訣竅 (Cursor Best Practices)：

* **利用 @ 提及功能**：當你要 Cursor 寫大劍的連招邏輯時，一定要在對話框輸入 @weapon\_movesets.json，讓它準確知道大劍的 MV（動作值）是多少。  
* **定義「不知道就問」**：在 Prompt 最後加上一句：「*如果在實作過程中，你發現欠缺某個變數或 JSON 設定，請不要自己瞎掰（Hallucinate），請停下來問我。*」

你現在已經準備好所有的「設計藍圖」了！**你最終決定使用哪一款遊戲引擎（Unity, Godot, 還是基於 Web 的框架）來搭配 Cursor 呢？** 決定後，我們就可以把上面這些指令轉化為該引擎專屬的精確程式碼了！  
