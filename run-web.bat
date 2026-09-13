@echo off
REM Installs client packages, type-checks, then leaves the Vite dev server running.
REM Close this window to stop the app.
setlocal
cd /d "%~dp0src\gamenight.web"
set LOG=%~dp0web-log.txt

echo === web %DATE% %TIME% === > "%LOG%"
call npm install >> "%LOG%" 2>&1
echo INSTALL_EXIT=%ERRORLEVEL% >> "%LOG%"
call npx tsc --noEmit >> "%LOG%" 2>&1
echo TSC_EXIT=%ERRORLEVEL% >> "%LOG%"
echo === CHECKS DONE === >> "%LOG%"
call npm run dev >> "%LOG%" 2>&1
