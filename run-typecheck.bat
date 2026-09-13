@echo off
REM Type-checks the client without touching the running dev server.
setlocal
cd /d "%~dp0src\gamenight.web"
set LOG=%~dp0typecheck-log.txt
echo === typecheck %DATE% %TIME% === > "%LOG%"
call npx tsc --noEmit >> "%LOG%" 2>&1
echo TSC_EXIT=%ERRORLEVEL% >> "%LOG%"
echo === DONE === >> "%LOG%"
