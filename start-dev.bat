@echo off
REM UTF-8 script: keep this file ASCII-only so cmd.exe on all code pages parses lines correctly.
setlocal EnableExtensions EnableDelayedExpansion
chcp 65001 >nul
cd /d "%~dp0"

title MonsterHunter_2026 - 一鍵測試遊玩

REM debug log
call :agent_log H0 "script_entry cwd=!CD!"

echo [MonsterHunter_2026] 正在初始化一鍵測試環境...
echo.

where node >nul 2>&1
if errorlevel 1 (
  call :agent_log H1 "node_cli_missing"
  echo [ERROR] 找不到 Node.js！請安裝 Node.js (https://nodejs.org) 後再試。
  pause
  exit /b 1
)

where docker >nul 2>&1
if errorlevel 1 (
  call :agent_log H1 "docker_cli_missing"
  echo [ERROR] 找不到 Docker！請安裝並啟動 Docker Desktop。
  pause
  exit /b 1
)

REM 執行自動設定腳本 (自動啟動 Supabase、解析金鑰、寫入 .env、同步 Unity 與 Web 測試端)
node scripts/auto-setup-dev.mjs
set "SB_RC=!ERRORLEVEL!"

call :agent_log H2 "supabase_setup_exit=!SB_RC!"

if not "!SB_RC!"=="0" (
  echo.
  echo [ERROR] 測試環境初始化失敗。請確認 Docker 是否已啟動且資源充足。
  echo.
  pause
  exit /b !SB_RC!
)

REM debug log
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\LogStudioPort.ps1" -Root "%~dp0"
call :agent_log H4 "before_browser_launch hint=use_external_chrome_not_embedded"

echo.
echo [OK] 測試環境已就緒！正在啟動測試瀏覽器頁面...
echo.

REM 自動開啟 Supabase Studio
start "" "http://127.0.0.1:54323"

REM 自動開啟家人登入測試網頁（已自動預填本地金鑰）
start "" "Web\family-auth\index.html"

echo ========================================================
echo 🎉 一鍵測試遊玩啟動成功！
echo.
echo 👉 瀏覽器已自動開啟 Supabase Studio 與家人登入測試網頁。
echo 👉 Unity 專案的資料與本地金鑰已全部同步完成。
echo 👉 請直接在 Unity 編輯器按下 [Play] 按鈕開始測試遊玩！
echo ========================================================
echo.
pause
exit /b 0

:agent_log
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\AppendDebugLog.ps1" -HypothesisId "%~1" -Message "%~2" -Root "%~dp0"
exit /b 0
