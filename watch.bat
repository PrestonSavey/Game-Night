@echo off
REM ---------------------------------------------------------------------------
REM  Leave this window open and Claude can build, test and restart the API on
REM  its own - no clicking, no approvals.
REM
REM  It watches for a file called build-request.txt in this folder. Claude can
REM  write that file (it already has access to this folder), and this script
REM  does the rest. Close the window to take the ability away again.
REM ---------------------------------------------------------------------------
setlocal
cd /d "%~dp0"
set LOG=%~dp0build-log.txt

echo Game Night build watcher. Close this window to stop.
echo Watching for build-request.txt in %~dp0
echo.

:loop
if exist "%~dp0build-request.txt" (
    del /q "%~dp0build-request.txt"
    echo [%TIME%] Build requested.

    echo === build %DATE% %TIME% === > "%LOG%"

    REM The running API locks its own DLLs, so it has to go first.
    taskkill /IM GameNight.Api.exe /F >nul 2>&1
    if not errorlevel 1 echo API stopped. >> "%LOG%"

    echo --- BUILD --- >> "%LOG%"
    dotnet build -v minimal >> "%LOG%" 2>&1
    echo BUILD_EXIT=%ERRORLEVEL% >> "%LOG%"

    echo --- TEST --- >> "%LOG%"
    dotnet test --no-build -v minimal >> "%LOG%" 2>&1
    echo TEST_EXIT=%ERRORLEVEL% >> "%LOG%"

    echo --- TYPECHECK --- >> "%LOG%"
    pushd "%~dp0src\gamenight.web"
    call npx tsc --noEmit >> "%LOG%" 2>&1
    echo TSC_EXIT=%ERRORLEVEL% >> "%LOG%"
    popd

    echo === DONE === >> "%LOG%"
    echo [%TIME%] Done. Restarting API.

    REM Back up in its own window so this one stays free to watch.
    start "GameNight API" cmd /c "dotnet run --no-build --project src\GameNight.Api >> api-log.txt 2>&1"
)

timeout /t 2 /nobreak >nul
goto loop
