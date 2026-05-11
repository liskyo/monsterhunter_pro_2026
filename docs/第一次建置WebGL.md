# 第一次建置 WebGL：Unity Hub 模組 + 場景

依序完成兩段即可；若你已裝好 WebGL、場景也已加入，可跳過該段。

---

## A. Unity Hub：安裝 WebGL Build Support

我無法替你點擊本機視窗，請在電腦上依下列操作（以 Unity Hub 英文介面為例，中文對應同名選項）：

1. 開啟 **Unity Hub**。
2. 左側點 **Installs**（安裝）。
3. 找到你正在用來開本專案 `GameClient` 的 **Unity Editor 版本**（同一列右側）。
4. 點該版本列上的 **齒輪圖示 ⚙** → **Add modules**（新增模組）。
5. 在清單中勾選 **WebGL Build Support**（可能顯示為 *WebGL Build Support (IL2CPP)*，依版本而定）。
6. 點 **Install**／**Continue**，等待下載與安裝結束。
7. **完全關閉** Unity 編輯器後再重新開啟專案（若當時有開著），讓模組被正確偵測。

若找不到 WebGL 選項：確認選的是「正在使用的同一個 Editor 版本」，且 Hub 已更新到較新版本。

---

## B. 場景：加入 Build Settings（倉庫已提供一鍵完成）

本倉庫一開始可能沒有 `.unity` 場景檔；不必手動拖曳時，請在 Unity 內：

1. 用 Hub **開啟專案**，專案路徑選資料夾 **`GameClient`**（內含 `Assets`、`Packages`）。
2. 上方選單點：**Monster Hunter → 準備建置：建立 Bootstrap 場景並加入 Build Settings**。
3. 出現成功對話框後，可到 **File → Build Settings** 確認列表裡有 **`Assets/Scenes/Bootstrap.unity`** 且已勾選。

手動方式（與一鍵擇一即可）：**File → Build Settings → Add Open Scenes**（須先開啟某場景），或把場景從 Project 視窗拖進 **Scenes In Build** 列表並勾選。

---

## C. 下一步

- 根目錄建議執行：`npm run sync:unity-env`（Supabase 與 `.env` 同步）。
- 選單：**Monster Hunter → Build WebGL（iPhone Safari）**，輸出至 `Web/webgl/`。

詳見 `Web/README.md`。
