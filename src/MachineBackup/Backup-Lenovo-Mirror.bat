@echo off
setlocal

echo MIRROR backup: destination-only files and folders will be deleted.
echo.
dotnet run --project "%~dp0MachineBackup.csproj" -- lenovo --mirror
set "BACKUP_EXIT_CODE=%ERRORLEVEL%"

echo.
if not "%BACKUP_EXIT_CODE%"=="0" echo Backup failed. Exit code: %BACKUP_EXIT_CODE%
pause
exit /b %BACKUP_EXIT_CODE%
