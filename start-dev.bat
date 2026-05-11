@echo off
REM UTF-8 script: keep this file ASCII-only so cmd.exe on all code pages parses lines correctly.
setlocal EnableExtensions EnableDelayedExpansion
chcp 65001 >nul
cd /d "%~dp0"

title MonsterHunter_2026 - local dev

REM debug log
call :agent_log H0 "script_entry cwd=!CD!"

if not exist ".env.example" (
  echo [ERROR] Missing .env.example
  pause
  exit /b 1
)

if not exist ".env" (
  echo [MonsterHunter_2026] First run: copying .env.example to .env
  copy /Y ".env.example" ".env" >nul
  echo Edit .env: set SUPABASE_URL and SUPABASE_ANON_KEY from Supabase Dashboard.
  start "" notepad ".env"
  echo Save .env, then run this script again to start local Supabase.
  pause
  exit /b 0
)

echo [MonsterHunter_2026] Starting local Supabase (Docker Desktop must be running)
echo.

where docker >nul 2>&1
if errorlevel 1 (
  call :agent_log H1 "docker_cli_missing"
  echo [ERROR] docker not found. Install Docker Desktop and ensure it is on PATH.
  echo https://www.docker.com/products/docker-desktop/
  echo.
  pause
  exit /b 1
)

docker info >nul 2>&1
set "DOCKER_RC=!ERRORLEVEL!"
call :agent_log H1 "docker_info_exit=!DOCKER_RC!"
if not "!DOCKER_RC!"=="0" (
  echo [ERROR] Cannot reach Docker engine. Start Docker Desktop, wait until it is ready, then retry.
  echo If it still fails: run Docker as admin, or enable WSL2 / virtualization in Windows.
  echo.
  pause
  exit /b 1
)

pushd "Backend" || exit /b 1
npx --yes supabase@latest start
set "SB_RC=!ERRORLEVEL!"
popd

call :agent_log H2 "supabase_start_exit=!SB_RC!"

if not "!SB_RC!"=="0" (
  echo.
  echo [HINT] supabase start failed. Common causes: Docker not running, port in use, or CLI error.
  echo For cloud-only dev, skip this and set SUPABASE_URL / anon key in .env instead.
  echo.
  pause
  exit /b !SB_RC!
)

REM debug log
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\LogStudioPort.ps1" -Root "%~dp0"
call :agent_log H4 "before_browser_launch hint=use_external_chrome_not_embedded"

echo.
echo [OK] Local Supabase is up. Studio: http://127.0.0.1:54323 (or http://localhost:54323)
echo Open in a normal browser tab; avoid Cursor/VS Code Simple Browser (chrome-error frame issues).
echo API URL and anon key: run  npm run supabase:status  (or  npx supabase status  in the Backend folder).
echo After copying URL + anon into .env, sync Unity asset:  npm run sync:unity-env
echo.
start "" "http://127.0.0.1:54323"
pause
exit /b 0

:agent_log
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\AppendDebugLog.ps1" -HypothesisId "%~1" -Message "%~2" -Root "%~dp0"
exit /b 0
