# WebGL（iPhone Safari／瀏覽器）遊玩

Android APK 以外，可用 **Unity WebGL** 建置後，以 **iPhone Safari**（或其他瀏覽器）開啟同一套客戶端邏輯（HTTP API／Supabase 與 Editor Play 相同）。

## 1. 前置

- 已安裝 **Unity**，並在 Unity Hub 為該版本安裝 **WebGL Build Support**（逐步說明：**`docs/第一次建置WebGL.md`** 段落 A）。
- 已在 Build Settings 加入場景：選單 **Monster Hunter → 準備建置：建立 Bootstrap 場景並加入 Build Settings**，或手動於 **File → Build Settings** 加入並勾選（詳見同文件段落 B）。
- Supabase：建置前建議先 `npm run sync:unity-env`，讓 `SupabaseRuntimeConfig` 與根目錄 `.env` 一致（雲端 HTTPS 專案較適合手機直連；本機 `http://127.0.0.1` 在真機上通常無法連到你電腦，除非另行內網穿透）。

## 2. 建置步驟（Unity）

1. 開啟 Unity 專案資料夾 **`GameClient`**。
2. 選單：**Monster Hunter → Build WebGL（iPhone Safari）**。
3. 完成後輸出會在倉庫 **`Web/webgl/`**（含 `index.html`、`.wasm`、`.data` 等）。

建置流程會套用較適合壓縮與行動瀏覽器的預設 Player Settings，並在 `index.html` 注入 Safari 用 viewport／觸控樣式。

## 3. 本機快速預覽（電腦）

在倉庫根目錄：

```bash
npm run serve:webgl
```

瀏覽器開啟終端機印出的本機網址（預設埠 **4173**）。此方式主要給桌面瀏覽器驗證；**真機 iPhone** 請見下一節（需 HTTPS 或區網情境）。

## 4. 給 iPhone Safari 開（正式建議：HTTPS）

Safari 對 **WASM／快取／麥克風等**行為在 **HTTPS** 下最穩定；公開網址請部署至支援靜態檔案的服務（本專案以 **§8 Vercel** 為主）。

若 Loader 無法下載 `.wasm`／`.br`，請確認托管端未把所有路徑錯誤 Rewrite 成 `index.html`（Unity WebGL 需要實際的 `.wasm`、`.data`、`.js` 路徑）。

### Supabase

- 若使用 **Email 登入**，請在 Supabase Dashboard → **Authentication → URL Configuration**，將正式網址／重新導向網域依需求加入允許清單。
- REST／anon 金鑰與 CORS：一般雲端 Supabase 已允許瀏覽器呼叫；若自架 API 再另設 CORS。

## 5. 勿提交建置產物（預設）

`Web/webgl/` 內建置檔預設由 `.gitignore` 忽略；請在 CI 或本機建置後再上傳部署。若團隊希望 Git 追蹤建置結果，可再調整 ignore 規則。

## 6. 限制說明

- WebGL **不支援**所有原生外掛；若未來加入僅 Android／iOS 的原生 SDK，需在程式中以 `#if !UNITY_WEBGL` 等方式排除或改走網頁 API。
- 效能與記憶體受手機瀏覽器限制，必要時在 Unity **Player Settings → WebGL** 調高 **Initial Memory** 或瘦身資源。

## 7. DesignData 與封裝

- 任一平台建置前，會由 `IPreprocessBuild` **將倉庫根的 `DesignData/` 整批複製**到 **`GameClient/Assets/StreamingAssets/DesignData/`**，再隨播放器封進包／WebGL 的資料檔執行期讀取（`UnityWebRequest` + `StreamingAssets`）。
- 僅需在 Unity Editor 先手動對齊時，可用選單 **Monster Hunter → Sync DesignData → StreamingAssets**。

## 8. Vercel 靜態部署（本專案詳細流程）

本專案 WebGL **輸出目錄**為倉庫 **`Web/webgl/`**。Vercel 只托管靜態檔；**戰鬥與畫面在瀏覽器 WebAssembly 內執行**，無需另行架「遊戲伺服器」。

### 為什麼多半是「本機建置 + 再上傳」

- **`Web/webgl/` 預設被 `.gitignore` 忽略**，Git 推到 Vercel 時資料夾常是空的。
- Vercel 雲端**沒有 Unity**，無法只靠「連 Git → 自動建置」就產出 WebGL。

因此實務流程是：**本機 Unity 建好 → 將 `Web/webgl` 內容送到 Vercel**。下面 **方式 A** 最省事；若要 **Git push 自動部署** 請看 **方式 B**。

---

### 方式 A（建議）：Vercel CLI，從本機資料夾部署

適合：**第一次上線／不想把建置結果 commit 進 Git**。

1. **準備**：依 §1～§2 建好 WebGL，`Web/webgl/` 內已有 `index.html`、`Build/` 等。
2. **（選用 Supabase／雲端 API）** 在倉庫根執行 `npm run sync:unity-env`，再於 Unity **`GameClient`** 內執行一次 **Monster Hunter → Build WebGL（iPhone Safari）**，確保設定編進這次輸出。
3. **本機驗證**：倉庫根執行 `npm run serve:webgl`，用瀏覽器確認可進入遊戲。
4. **安裝 CLI 並登入**：

   ```bash
   npm i -g vercel
   vercel login
   ```

5. **首次連結專案**（在 `Web/webgl` 目錄內執行，路徑以你電腦上倉庫為準）：

   ```bash
   cd Web/webgl
   vercel
   ```

   - **Set up and deploy**：選是  
   - **Scope**：選個人／Team  
   - **Link**：首次選建新專案；之後選連到既有專案  
   - **Project name**：自訂（例如 `monsterhunter-webgl`）  

   CLI 會印出預覽用 **https://** 網址，用手機／桌面再打開試一次。

6. **正式上線（Production）**：

   ```bash
   cd Web/webgl
   vercel --prod
   ```

   或在**倉庫根目錄**：

   ```bash
   npm run deploy:vercel
   ```

7. **之後改版**：每次 **Unity 重建 WebGL** → 再執行 `vercel --prod` 或 `npm run deploy:vercel`。

---

### 方式 B：Vercel 綁 Git（自動部署）

若希望 **`git push` 就部署**，須確保 **`Web/webgl/` 在 push 的那一版裡真的有檔案**，例如：

- 修改 `.gitignore` 放行 `Web/webgl` 並提交建置產物（簡單但儲存庫會变大）；或  
- 使用 **GitHub Actions**（或類似 CI）在雲端跑 Unity build，再在 workflow 呼叫 `vercel deploy Web/webgl`。

Vercel 專案設定：**Root Directory** = **`Web/webgl`**，**Framework**：**Other**，**Install / Build Command** 多半可空白（純靜態）。

---

### 部署後檢查清單

- **桌面**：開 **`https://專案名.vercel.app`**，開發者工具 **Network**：`Build/` 下 `.wasm`、`.data`、`.js` 為 **200**，非被轉址成 HTML。
- **iPhone Safari**：同上；必要時私密瀏覽避免舊快取。
- **Supabase**：Dashboard → **Authentication → URL Configuration** 加入該 **`https://...vercel.app`**（若有自訂網域再加一條）。
- **勿**在 Vercel 設定會把 **`/Build/**`** 統一 Rewrite 成 `index.html` 的 SPA 規則。

### 自訂網域（選用）

Vercel 專案 → **Settings → Domains**。綁定後，將 **`https://你的網域`** 同步寫進 Supabase Auth 設定。
