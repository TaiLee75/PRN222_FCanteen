@echo off
cd /d "%~dp0"

echo ========================================================
echo   CAI DAT FCANTEEN WORKER SERVICE (RUN AS ADMINISTRATOR)
echo ========================================================

set SERVICE_NAME=FCanteenWorker
set BIN_PATH=%~dp0publish_worker\FCanteen.WorkerService.exe

echo Service Name: %SERVICE_NAME%
echo Binary Path : %BIN_PATH%
echo.

sc.exe create %SERVICE_NAME% binPath= "\"%BIN_PATH%\"" start= auto DisplayName= "FCanteen Worker Service"
if %errorlevel% neq 0 (
    echo.
    echo [LOI] Khong the tao Service!
    echo Vui long chac chan ban da click chuot phai va chon "Run as administrator".
    echo.
    pause
    exit /b %errorlevel%
)

sc.exe description %SERVICE_NAME% "FCanteen Background Worker Service for Inventory Alerts and Daily Reports (PRN222 Lab 08)"

echo.
echo [THANH CONG] Da tao Windows Service thanh cong!
echo Dang khoi dong Service...
sc.exe start %SERVICE_NAME%

echo.
echo ========================================================
echo Kiem tra trang thai Service:
sc.exe query %SERVICE_NAME%

echo.
echo ========================================================
echo Hoan tat! Ban co the mo 'services.msc' de xem service dang chay.
pause
