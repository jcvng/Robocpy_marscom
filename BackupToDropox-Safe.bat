@echo off
setlocal enabledelayedexpansion

set "DEST=%USERPROFILE%\Dropbox\Marscom"
set "SRC1=%USERPROFILE%\Documents"
set "SRC2=%USERPROFILE%\.claude\skills"
set "SRC3=C:\Projects\Testing\GraveyardsMvc\Graveyards\Graveyards.Web\Properties\PublishProfiles"
set "SRC4=%USERPROFILE%\Pictures"
set "SRC5=%USERPROFILE%\Videos"

set "LOG=%TEMP%\BackupToDropoxSafe_%RANDOM%.log"
if exist "%LOG%" del "%LOG%"

echo ==========================================
echo  Backup to Dropbox (SAFE) - starting %DATE% %TIME%
echo  ADDITIVE mode: nothing is ever deleted from Dropbox
echo ==========================================
echo.

echo [1/5] Documents  -^>  %DEST%\Documents
call :copy "%SRC1%" "%DEST%\Documents"
if errorlevel 8 goto :failed

echo.
echo [2/5] .claude\skills  -^>  %DEST%\skills
call :copy "%SRC2%" "%DEST%\skills"
if errorlevel 8 goto :failed

echo.
echo [3/5] GraveyardsPublishProfiles  -^>  %DEST%\GraveyardsPublishProfiles
call :copy "%SRC3%" "%DEST%\GraveyardsPublishProfiles"
if errorlevel 8 goto :failed

echo.
echo [4/5] Pictures  -^>  %DEST%\Pictures
call :copy "%SRC4%" "%DEST%\Pictures"
if errorlevel 8 goto :failed

echo.
echo [5/5] Videos  -^>  %DEST%\Videos
call :copy "%SRC5%" "%DEST%\Videos"
if errorlevel 8 goto :failed

echo.
echo ==========================================
echo  CHANGE SUMMARY
echo ==========================================

echo.
echo --- ADDED (new in source) ---
findstr /c:"New File" "%LOG%" > "%TEMP%\_bks_added.txt"
call :printlist "%TEMP%\_bks_added.txt"

echo.
echo --- CHANGED (updated content) ---
findstr /c:"Newer" "%LOG%" > "%TEMP%\_bks_changed.txt"
call :printlist "%TEMP%\_bks_changed.txt"

echo.
echo --- EXTRA (in Dropbox, not in source - NOT deleted in safe mode) ---
findstr /c:"EXTRA File" "%LOG%" > "%TEMP%\_bks_extra.txt"
call :printlist "%TEMP%\_bks_extra.txt"

del "%TEMP%\_bks_added.txt" "%TEMP%\_bks_changed.txt" "%TEMP%\_bks_extra.txt" 2>nul
del "%LOG%" 2>nul

echo.
echo ==========================================
echo  Backup finished OK - %DATE% %TIME%
echo ==========================================
goto :end

:copy
rem Pass 1: real copy. /E adds and updates but never deletes.
robocopy %1 %2 /E /XJ /DCOPY:T /R:1 /W:1 /NP /NDL /NC /NS /TEE /LOG+:"%LOG%"
set "rc=%errorlevel%"
rem Pass 2: list-only (/L) mirror scan just to LOG extras present in Dropbox
rem but not in source. /L means nothing is copied or deleted here.
robocopy %1 %2 /MIR /L /XJ /R:0 /W:0 /NP /NDL /NC /NS /NJH /NJS /LOG+:"%LOG%" >nul
exit /b %rc%

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
