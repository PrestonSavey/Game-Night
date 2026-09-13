@echo off
REM Builds the whole solution, runs the tests, then starts the API on http://localhost:5080.
REM Close this window to stop it.
setlocal
cd /d "%~dp0"
set LOG=%~dp0api-log.txt
echo === api %DATE% %TIME% === > "%LOG%"
dotnet build -v minimal >> "%LOG%" 2>&1
echo BUILD_EXIT=%ERRORLEVEL% >> "%LOG%"
dotnet test --no-build -v minimal >> "%LOG%" 2>&1
echo TEST_EXIT=%ERRORLEVEL% >> "%LOG%"
echo === STARTING === >> "%LOG%"
dotnet run --no-build --project src\GameNight.Api >> "%LOG%" 2>&1
