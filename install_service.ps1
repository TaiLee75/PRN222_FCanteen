# File cài đặt service bằng PowerShell (Yêu cầu Run as Administrator)
$serviceName = "FCanteenWorker"
$binPath = Join-Path $PSScriptRoot "publish_worker\FCanteen.WorkerService.exe"

Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "  CÀI ĐẶT FCANTEEN WORKER SERVICE QUA POWERSHELL" -ForegroundColor Cyan
Write-Host "========================================================" -ForegroundColor Cyan
Write-Host "Target: $binPath"

# Kiểm tra nếu service đã tồn tại thì dừng và xoá trước
$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Phát hiện service cũ, đang dừng và gỡ bỏ..." -ForegroundColor Yellow
    Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
    & sc.exe delete $serviceName
    Start-Sleep -Seconds 1
}

# Tạo service mới với sc.exe
Write-Host "Đang tạo Service mới..." -ForegroundColor Green
& sc.exe create $serviceName binPath= "`"$binPath`"" start= auto DisplayName= "FCanteen Worker Service"
& sc.exe description $serviceName "FCanteen Background Worker Service for Inventory Alerts and Daily Reports (PRN222 Lab 08)"

# Khởi động service
Write-Host "Đang khởi động Service..." -ForegroundColor Green
Start-Service -Name $serviceName

# Hiển thị trạng thái
Write-Host "`nTrạng thái Service hiện tại:" -ForegroundColor Cyan
Get-Service -Name $serviceName | Format-Table -AutoSize
Write-Host "Hoàn tất! Mở 'services.msc' để kiểm tra." -ForegroundColor Green
