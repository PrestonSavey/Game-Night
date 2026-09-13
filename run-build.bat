@echo off
REM Runs restore/build/test and writes everything to build-log.txt so Claude can read it.
REM Safe to delete once the loop is not needed any more.
setlocal
cd /d "%~dp0"
set LOG=%~dp0build-log.txt

echo === GameNight build %DATE% %TIME% === > "%LOG%"
echo --- SDK --- >> "%LOG%"
dotnet --version >> "%LOG%" 2>&1
echo --- RESTORE --- >> "%LOG%"
dotnet restore >> "%LOG%" 2>&1
echo RESTORE_EXIT=%ERRORLEVEL% >> "%LOG%"
echo --- BUILD --- >> "%LOG%"
dotnet build --no-restore -v minimal >> "%LOG%" 2>&1
echo BUILD_EXIT=%ERRORLEVEL% >> "%LOG%"
echo --- TEST --- >> "%LOG%"
dotnet test --no-build -v minimal >> "%LOG%" 2>&1
echo TEST_EXIT=%ERRORLEVEL% >> "%LOG%"
echo === DONE === >> "%LOG%"
