@echo off
setlocal EnableExtensions EnableDelayedExpansion
if /I "%EMG_LAUNCHER_ACTIVE%"=="1" (
    echo EMG launcher re-entry blocked. Close the original launcher and retry once it has exited.
    exit /b 5
)
set "EMG_LAUNCHER_ACTIVE=1"

rem EMG Construction ERP - idempotent one-click local launcher.
rem Local secrets belong in BackEnd\src\EMG.WebApi\appsettings.Local.json,
rem which is ignored by Git and loaded by Program.cs.
set "ROOT=%~dp0"
set "FRONTEND=%ROOT%FrontEnd"
set "BACKEND=%ROOT%BackEnd\src\EMG.WebApi"
set "FRONTEND_URL=http://127.0.0.1:3100"
set "BACKEND_URL=http://127.0.0.1:5121"
set "BACKEND_HEALTH=%BACKEND_URL%/api/health"
set "FRONTEND_PROXY=%FRONTEND_URL%/api/account/csrf"
set "LOCAL_CONFIG=%BACKEND%\appsettings.Local.json"

if not exist "%FRONTEND%\package.json" (
    echo Frontend folder is invalid: "%FRONTEND%"
    exit /b 1
)
if not exist "%BACKEND%\EMG.WebApi.csproj" (
    echo Backend project is invalid: "%BACKEND%"
    exit /b 1
)

where powershell.exe >nul 2>&1
if errorlevel 1 (
    echo powershell.exe is required for safe launcher health checks.
    exit /b 1
)

call :probe "%FRONTEND_URL%/"
set "FRONTEND_OK=!ERRORLEVEL!"
call :probe "%BACKEND_HEALTH%"
set "BACKEND_OK=!ERRORLEVEL!"

if "!FRONTEND_OK!"=="0" if "!BACKEND_OK!"=="0" (
    echo EMG is already running.
    echo Frontend: %FRONTEND_URL%
    echo Backend:  %BACKEND_URL%
    echo No duplicate processes were started.
    exit /b 0
)

call :port_pid 3100 FRONTEND_PID
call :port_pid 5121 BACKEND_PID

if not "!FRONTEND_PID!"=="" if "!FRONTEND_OK!"=="1" (
    echo Frontend port 3100 is occupied by an unhealthy process, PID !FRONTEND_PID!.
    echo No process was stopped. Inspect that specific owner, then retry once.
    exit /b 2
)
if not "!BACKEND_PID!"=="" if "!BACKEND_OK!"=="1" (
    echo Backend port 5121 is occupied by an unhealthy process, PID !BACKEND_PID!.
    echo No process was stopped. Inspect that specific owner, then retry once.
    exit /b 2
)

if "!BACKEND_OK!"=="1" call :validate_backend_config
if errorlevel 1 exit /b 3

where wt.exe >nul 2>&1
if errorlevel 1 (
    echo Windows Terminal was not found. Using two visible fallback windows.
    if "!FRONTEND_OK!"=="1" start "EMG FRONTEND" /D "%FRONTEND%" cmd.exe /d /k "npm run start -- --host 127.0.0.1 --port 3100 --strictPort"
    if "!BACKEND_OK!"=="1" start "EMG BACKEND" /D "%BACKEND%" cmd.exe /d /k "dotnet run --launch-profile http"
    goto :wait_for_services
)

if "!FRONTEND_OK!"=="1" if "!BACKEND_OK!"=="1" (
    rem Cold start: one visible Windows Terminal window with real service commands.
    wt.exe -w new new-tab --title "EMG FRONTEND - TOP" --tabColor "#1683D8" -d "%FRONTEND%" cmd.exe /d /k "echo [EMG FRONTEND - TOP] && echo %FRONTEND_URL% && echo API: %BACKEND_URL% && echo. && npm run start -- --host 127.0.0.1 --port 3100 --strictPort" ; split-pane -H --title "EMG BACKEND - BOTTOM" --tabColor "#D97706" -d "%BACKEND%" cmd.exe /d /k "echo [EMG BACKEND - BOTTOM] && echo %BACKEND_URL% && echo. && dotnet run --launch-profile http"
    powershell.exe -NoProfile -Command "Start-Sleep -Seconds 2" >nul 2>&1
    call :has_service frontend
    if errorlevel 1 start "EMG FRONTEND" /D "%FRONTEND%" cmd.exe /d /k "npm run start -- --host 127.0.0.1 --port 3100 --strictPort"
    call :has_service backend
    if errorlevel 1 start "EMG BACKEND" /D "%BACKEND%" cmd.exe /d /k "dotnet run --launch-profile http"
) else (
    rem Start only stopped services; never create a second Vite/API process.
    if "!FRONTEND_OK!"=="1" (
        wt.exe -w new new-tab --title "EMG FRONTEND - TOP" --tabColor "#1683D8" -d "%FRONTEND%" cmd.exe /d /k "echo [EMG FRONTEND - TOP] && echo %FRONTEND_URL% && echo API: %BACKEND_URL% && echo. && npm run start -- --host 127.0.0.1 --port 3100 --strictPort"
        powershell.exe -NoProfile -Command "Start-Sleep -Seconds 2" >nul 2>&1
        call :has_service frontend
        if errorlevel 1 start "EMG FRONTEND" /D "%FRONTEND%" cmd.exe /d /k "npm run start -- --host 127.0.0.1 --port 3100 --strictPort"
    )
    if "!BACKEND_OK!"=="1" (
        wt.exe -w new new-tab --title "EMG BACKEND - BOTTOM" --tabColor "#D97706" -d "%BACKEND%" cmd.exe /d /k "echo [EMG BACKEND - BOTTOM] && echo %BACKEND_URL% && echo. && dotnet run --launch-profile http"
        powershell.exe -NoProfile -Command "Start-Sleep -Seconds 2" >nul 2>&1
        call :has_service backend
        if errorlevel 1 start "EMG BACKEND" /D "%BACKEND%" cmd.exe /d /k "dotnet run --launch-profile http"
    )
)

:wait_for_services
echo Waiting for EMG services...
call :wait_for "%FRONTEND_URL%/"
if errorlevel 1 (
    echo Frontend did not become healthy on port 3100.
    exit /b 4
)
call :wait_for "%BACKEND_HEALTH%"
if errorlevel 1 (
    echo Backend did not become healthy on port 5121.
    exit /b 4
)
call :probe "%FRONTEND_PROXY%"
if errorlevel 1 (
    echo Frontend proxy did not reach the Backend.
    exit /b 4
)
echo EMG started successfully.
echo Frontend: %FRONTEND_URL%
echo Backend:  %BACKEND_URL%
exit /b 0

:validate_backend_config
if not exist "%LOCAL_CONFIG%" (
    echo Backend is stopped and no local configuration was found.
    echo Create the ignored file:
    echo   %LOCAL_CONFIG%
    echo Set ConnectionStrings:EmgDb to an authorized non-5432 QA database.
    echo Do not put credentials in tracked files or this launcher.
    exit /b 1
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$j=ConvertFrom-Json (Get-Content -Raw -LiteralPath '%LOCAL_CONFIG%'); $cs=[string]$j.ConnectionStrings.EmgDb; if([string]::IsNullOrWhiteSpace($cs)){exit 1}; if($cs -notmatch '(?i)(^|;)Port\s*=\s*\d+'){exit 2}; if($cs -match '(?i)(^|;)Port\s*=\s*5432(?:;|$)'){exit 3}" >nul 2>&1
if not errorlevel 1 exit /b 0
echo Local configuration is missing, invalid, or points to protected port 5432.
echo Supply an authorized non-5432 QA connection in the ignored appsettings.Local.json.
exit /b 1

:probe
powershell.exe -NoProfile -Command "try{$r=Invoke-WebRequest -UseBasicParsing -Uri '%~1' -TimeoutSec 2; if($r.StatusCode -ge 200 -and $r.StatusCode -lt 400){exit 0}else{exit 1}}catch{exit 1}" >nul 2>&1
if errorlevel 1 exit /b 1
exit /b 0

:wait_for
set /a WAIT_TRIES=0
:wait_loop
call :probe "%~1"
if not errorlevel 1 exit /b 0
set /a WAIT_TRIES+=1
if !WAIT_TRIES! GEQ 30 exit /b 1
ping -n 2 127.0.0.1 >nul
goto :wait_loop

:port_pid
set "%~2="
for /f "delims=" %%P in ('powershell.exe -NoProfile -Command "$c=Get-NetTCPConnection -LocalPort %~1 -State Listen -ErrorAction SilentlyContinue; if($c){$c[0].OwningProcess}"') do set "%~2=%%P"
exit /b 0

:has_service
if /I "%~1"=="frontend" goto :has_frontend
powershell.exe -NoProfile -Command "if(Get-NetTCPConnection -LocalPort 5121 -State Listen -ErrorAction SilentlyContinue){exit 0}; $p=Get-CimInstance Win32_Process | Where-Object { ($_.Name -eq 'EMG.WebApi.exe') -or ($_.Name -eq 'dotnet.exe' -and $_.CommandLine -match 'launch-profile http') }; if($p){exit 0}; exit 1" >nul 2>&1
exit /b %ERRORLEVEL%

:has_frontend
powershell.exe -NoProfile -Command "if(Get-NetTCPConnection -LocalPort 3100 -State Listen -ErrorAction SilentlyContinue){exit 0}; $p=Get-CimInstance Win32_Process | Where-Object { $_.Name -eq 'node.exe' -and $_.CommandLine -match 'vite' }; if($p){exit 0}; exit 1" >nul 2>&1
exit /b %ERRORLEVEL%
