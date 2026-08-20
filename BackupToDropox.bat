@echo off
setlocal enabledelayedexpansion

set "DEST=%USERPROFILE%\Dropbox\Marscom"
set "SRC1=%USERPROFILE%\Documents"
set "SRC2=%USERPROFILE%\.claude\skills"
set "SRC3=C:\Projects\Testing\GraveyardsMvc\Graveyards\Graveyards.Web\Properties\PublishProfiles"
set "SRC4=%USERPROFILE%\Pictures"
set "SRC5=%USERPROFILE%\Videos"

set "LOG=%TEMP%\BackupToDropox_%RANDOM%.log"
if exist "%LOG%" del "%LOG%"

echo ==========================================
echo  Backup to Dropbox - starting %DATE% %TIME%
echo  MIRROR mode: files removed from source are deleted in Dropbox
echo ==========================================
echo.

echo [1/5] Documents  -^>  %DEST%\Documents
call :mirror "%SRC1%" "%DEST%\Documents"
if errorlevel 8 goto :failed

echo.
echo [2/5] .claude\skills  -^>  %DEST%\skills
call :mirror "%SRC2%" "%DEST%\skills"
if errorlevel 8 goto :failed

echo.
echo [3/5] GraveyardsPublishProfiles  -^>  %DEST%\GraveyardsPublishProfiles
call :mirror "%SRC3%" "%DEST%\GraveyardsPublishProfiles"
if errorlevel 8 goto :failed

echo.
echo [4/5] Pictures  -^>  %DEST%\Pictures
call :mirror "%SRC4%" "%DEST%\Pictures"
if errorlevel 8 goto :failed

echo.
echo [5/5] Videos  -^>  %DEST%\Videos
call :mirror "%SRC5%" "%DEST%\Videos"
if errorlevel 8 goto :failed

echo.
echo ==========================================
echo  CHANGE SUMMARY
echo ==========================================

echo.
echo --- ADDED (new in source) ---
findstr /c:"New File" "%LOG%" > "%TEMP%\_bk_added.txt"
call :printlist "%TEMP%\_bk_added.txt"

echo.
echo --- CHANGED (updated content) ---
findstr /c:"Newer" "%LOG%" > "%TEMP%\_bk_changed.txt"
call :printlist "%TEMP%\_bk_changed.txt"

echo.
echo --- REMOVED (deleted from Dropbox) ---
findstr /c:"EXTRA File" "%LOG%" > "%TEMP%\_bk_removed.txt"
call :printlist "%TEMP%\_bk_removed.txt"

del "%TEMP%\_bk_added.txt" "%TEMP%\_bk_changed.txt" "%TEMP%\_bk_removed.txt" 2>nul
del "%LOG%" 2>nul

echo.
echo ==========================================
echo  Backup finished OK - %DATE% %TIME%
echo ==========================================
goto :end

:mirror
robocopy %1 %2 /MIR /XJ /DCOPY:T /R:1 /W:1 /NP /NDL /NC /NS /TEE /LOG+:"%LOG%"
exit /b %errorlevel%

:printlist
set "count=0"
for /f "usebackq delims=" %%L in ("%~1") do (
    echo    %%L
    set /a count+=1
)
if !count!==0 echo    (none)
exit /b 0

:failed
echo.
echo ==========================================
echo  BACKUP FAILED - see robocopy output above
echo ==========================================
del "%LOG%" 2>nul

:end
echo.
pause
