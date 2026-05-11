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

Safari 對 **WASM／快取／麥克風等**行為在 **HTTPS** 下最穩定；公開網址請部署至支援靜態檔案的服務，例如 **Vercel**、Netlify、GitHub Pages。

### Vercel 範例

1. 在本機完成上述 WebGL 建置（`Web/webgl/` 已有檔案）。
2. 將倉庫連結 Vercel，設定 **Root Directory** 為 **`Web/webgl`**（或將 `Web/webgl` 內容作為專案根目錄上傳）。
3. 部署完成後，用 iPhone Safari 開啟該 **https://** 網址即可。

若 Loader 無法下載 `.wasm`／`.br`，請確認平台未錯誤地把所有路徑 Rewrite 成 `index.html`（Unity WebGL 需要實際的 `.wasm`、`.data`、`.js` 路徑）。

### Supabase

- 若使用 **Email 登入**，請在 Supabase Dashboard → **Authentication → URL Configuration**，將正式網址／重新導向網域依需求加入允許清單。
- REST／anon 金鑰與 CORS：一般雲端 Supabase 已允許瀏覽器呼叫；若自架 API 再另設 CORS。

## 5. 勿提交建置產物（預設）

`Web/webgl/` 內建置檔預設由 `.gitignore` 忽略；請在 CI 或本機建置後再上傳部署。若團隊希望 Git 追蹤建置結果，可再調整 ignore 規則。

## 6. 限制說明

- WebGL **不支援**所有原生外掛；若未來加入僅 Android／iOS 的原生 SDK，需在程式中以 `#if !UNITY_WEBGL` 等方式排除或改走網頁 API。
- 效能與記憶體受手機瀏覽器限制，必要時在 Unity **Player Settings → WebGL** 調高 **Initial Memory** 或瘦身資源。
