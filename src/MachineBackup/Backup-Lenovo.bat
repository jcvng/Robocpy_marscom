@echo off
setlocal

rem Resolve the project relative to this file, regardless of the working directory.
dotnet run --project "%~dp0MachineBackup.csproj" -- lenovo
set "BACKUP_EXIT_CODE=%ERRORLEVEL%"

echo.
if not "%BACKUP_EXIT_CODE%"=="0" echo Backup failed. Exit code: %BACKUP_EXIT_CODE%
pause
exit /b %BACKUP_EXIT_CODE%
