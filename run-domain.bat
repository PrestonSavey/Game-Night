@echo off
REM Builds and tests the domain only. Safe to run while the API is still going, because
REM it never touches the Api project's locked DLLs.
setlocal
cd /d "%~dp0"
set LOG=%~dp0domain-log.txt
echo === domain %DATE% %TIME% === > "%LOG%"
dotnet test tests\GameNight.Domain.Tests\GameNight.Domain.Tests.csproj -v minimal >> "%LOG%" 2>&1
echo TEST_EXIT=%ERRORLEVEL% >> "%LOG%"
echo === DONE === >> "%LOG%"
