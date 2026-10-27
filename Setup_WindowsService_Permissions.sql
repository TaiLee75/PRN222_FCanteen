-- ========================================================
-- Script cấp quyền truy cập SQL Server cho tài khoản chạy Windows Service
-- YC5: Xử lý vấn đề tài khoản chạy service không có quyền truy cập SQL Server
-- ========================================================

USE [master];
GO

-- 1. Đảm bảo Windows Login [NT AUTHORITY\SYSTEM] tồn tại trên SQL Server
IF NOT EXISTS (SELECT * FROM sys.server_principals WHERE name = 'NT AUTHORITY\SYSTEM')
BEGIN
    CREATE LOGIN [NT AUTHORITY\SYSTEM] FROM WINDOWS WITH DEFAULT_DATABASE=[PRN222_Lab_FCanteen];
    PRINT 'Đã tạo Login NT AUTHORITY\SYSTEM trên SQL Server.';
END
GO

USE [PRN222_Lab_FCanteen];
GO

-- 2. Đảm bảo User [NT AUTHORITY\SYSTEM] tồn tại trong Database PRN222_Lab_FCanteen
IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = 'NT AUTHORITY\SYSTEM')
BEGIN
    CREATE USER [NT AUTHORITY\SYSTEM] FOR LOGIN [NT AUTHORITY\SYSTEM];
    PRINT 'Đã tạo User NT AUTHORITY\SYSTEM trong PRN222_Lab_FCanteen.';
END
GO

-- 3. Cấp quyền db_owner để Service có toàn quyền đọc/ghi bảng StockAlerts và DailyReports
ALTER ROLE [db_owner] ADD MEMBER [NT AUTHORITY\SYSTEM];
PRINT 'Đã cấp quyền db_owner cho NT AUTHORITY\SYSTEM thành công!';
GO
