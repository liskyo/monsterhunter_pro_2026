@echo off
REM UTF-8 script: keep this file ASCII-only so cmd.exe on all code pages parses lines correctly.
setlocal EnableExtensions EnableDelayedExpansion
chcp 65001 >nul
cd /d "%~dp0"

title MonsterHunter_2026 - Local Dev Setup

REM debug log
call :agent_log H0 "script_entry cwd=!CD!"

echo [MonsterHunter_2026] Initializing local test environment...
echo.

where node >nul 2>&1
if errorlevel 1 (
  call :agent_log H1 "node_cli_missing"
  echo [ERROR] node not found. Install Node.js from https://nodejs.org/
  pause
  exit /b 1
)

where docker >nul 2>&1
if errorlevel 1 (
  call :agent_log H1 "docker_cli_missing"
  echo [ERROR] docker not found. Install and start Docker Desktop.
  pause
  exit /b 1
)

REM Run the setup Node script
node scripts/auto-setup-dev.mjs
set "SB_RC=!ERRORLEVEL!"

call :agent_log H2 "supabase_setup_exit=!SB_RC!"

if not "!SB_RC!"=="0" (
  echo.
  echo [ERROR] Setup failed. Make sure Docker is running and try again.
  echo.
  pause
  exit /b !SB_RC!
)

REM debug log
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\LogStudioPort.ps1" -Root "%~dp0"
call :agent_log H4 "before_browser_launch hint=use_external_chrome_not_embedded"

echo.
echo [OK] Test environment is ready. Launching browser tabs...
echo.

REM Automatically open Supabase Studio
start "" "http://127.0.0.1:54323"

REM Automatically open local Family Auth test page
start "" "Web\family-auth\index.html"

echo ========================================================
echo [OK] Environment initialized successfully!
echo.
echo - Supabase Studio and Family Auth page opened in browser.
echo - Unity credentials and DesignData have been synchronized.
echo - Please open the Unity project and click [Play] to test!
echo ========================================================
echo.
pause
exit /b 0

:agent_log
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\AppendDebugLog.ps1" -HypothesisId "%~1" -Message "%~2" -Root "%~dp0"
exit /b 0
