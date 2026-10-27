@echo off
cd /d "%~dp0"

echo ========================================================
echo   GO BO FCANTEEN WORKER SERVICE (RUN AS ADMINISTRATOR)
echo ========================================================

set SERVICE_NAME=FCanteenWorker

echo Dang dung service: %SERVICE_NAME%...
sc.exe stop %SERVICE_NAME%

echo Dang xoa service: %SERVICE_NAME%...
sc.exe delete %SERVICE_NAME%

echo.
echo [THANH CONG] Da go bo Service thanh cong!
pause
