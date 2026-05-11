# Monster Hunter PRO 2026（本機開發）

直立式狩獵手遊：Unity 客戶端（`GameClient/`）＋ Supabase 後端（`Backend/supabase/`）。

## 在本機看到遊戲（Unity Play）

1. 安裝 **Unity**（版本請依團隊約定；開過專案後可由 Unity Hub／專案設定確認）。
2. 用 Unity Hub **新增並開啟** 資料夾 `GameClient`。
3. 設定 Supabase 連線（擇一）：
   - **與根目錄 `.env` 一致（建議）**：在專案根目錄建立 `.env`（可由 `.env.example` 複製），填入 `SUPABASE_URL`、`SUPABASE_ANON_KEY`，然後執行：
     ```bash
     npm install
     npm run sync:unity-env
     ```
     會將這兩個值寫入 `GameClient/Assets/Resources/SupabaseRuntimeConfig.asset`，Unity **Play** 時會讀取該資源。
   - **只在 Unity 裡改**：選取 `Assets/Resources/SupabaseRuntimeConfig`，在 Inspector 填入 URL 與 Publishable（anon）key。
4. 在 Unity 編輯器按下 **Play**。

若元件上有獨立指派的 `SupabaseRuntimeConfig` 且欄位非空，會優先使用該指派；否則會 fallback 到 Resources 裡的預設資產（見 `SupabaseRuntimeConfig.ResolveFor`）。

## 連後端：本機 Docker Supabase

前置：**Docker Desktop** 已安裝並正在執行。

1. 專案根目錄第一次請複製 `.env.example` → `.env`，依提示填入（本機可先留白待步驟 3 取得）。
2. 啟動本機 Supabase（擇一）：
   - **Windows**：雙擊或執行 `start-dev.bat`
   - **任意 OS（含從終端機操作）**：
     ```bash
     npm install
     npm run supabase:start
     ```
3. 取得本機 API 與 anon key：
   ```bash
   npm run supabase:status
   ```
   將 **API URL**（通常為 `http://127.0.0.1:54321`）與 **Publishable／anon key** 貼到根目錄 `.env` 的 `SUPABASE_URL`、`SUPABASE_ANON_KEY`。
4. 同步到 Unity 並 Play：
   ```bash
   npm run sync:unity-env
   ```

Studio 介面一般在 **http://127.0.0.1:54323**。請用一般瀏覽器分頁開啟（避免內嵌 Simple Browser 造成載入問題）。

停止本機栈：`npm run supabase:stop`。

## 連後端：僅使用雲端 Supabase

1. 在 [Supabase Dashboard](https://supabase.com/dashboard) 取得 **Project URL** 與 **Publishable（anon）key**。
2. 填入根目錄 `.env` 的 `SUPABASE_URL`、`SUPABASE_ANON_KEY`。
3. 執行 `npm run sync:unity-env`，再在 Unity **Play**。

（雲端資料表／RPC 需依 `Backend/supabase/migrations/` 與團隊文件自行套用。）

## 常用 npm 指令

| 指令 | 說明 |
|------|------|
| `npm run supabase:start` | 本機啟動 Supabase（Docker） |
| `npm run supabase:status` | 查看本機 URL、anon key、埠號 |
| `npm run supabase:stop` | 停止本機 Supabase |
| `npm run sync:unity-env` | 由 `.env` 更新 Unity `SupabaseRuntimeConfig.asset` |
| `npm run import-design` | 將 `DesignData/` 匯入雲端（需 `SUPABASE_SERVICE_ROLE_KEY`） |

## iPhone Safari／瀏覽器遊玩（WebGL）

不需安裝 Android APK 時，可用 **Unity WebGL** 建置後用手機瀏覽器開啟：

1. Unity Hub 為該 Editor 版本安裝 **WebGL Build Support**（逐步截圖說明見 **`docs/第一次建置WebGL.md`**）。
2. 在 Unity 選單執行 **Monster Hunter → 準備建置：建立 Bootstrap 場景並加入 Build Settings**（會建立 `Assets/Scenes/Bootstrap.unity` 並加入 Build Settings）；或自行在 **File → Build Settings** 加入並勾選場景。
3. （建議）`npm run sync:unity-env`，讓 Supabase 與 `.env` 一致。
4. Unity 選單：**Monster Hunter → Build WebGL（iPhone Safari）**，輸出至 `Web/webgl/`。
5. 將 `Web/webgl/` 部署到 **HTTPS**（例如 Vercel 靜態站台），用 iPhone Safari 開啟網址。本機桌面預覽可執行 `npm run serve:webgl`。

完整注意事項（Supabase 網址、真機 HTTPS、Vercel 目錄設定）見 **`Web/README.md`**。

## 其他說明

- 家人測試與帳號流程見 `家人共同測試遊戲的流程.md`。
- `Web/family-auth/index.html` 為瀏覽器登入測試頁，非 Unity 遊戲本體。
