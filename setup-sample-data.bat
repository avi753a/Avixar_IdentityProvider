@echo off
REM =====================================================================================
REM Avixar Identity Provider - Quick Setup Script
REM =====================================================================================
REM This script helps you quickly set up sample data and start testing
REM =====================================================================================

echo.
echo ========================================
echo Avixar Identity Provider - Quick Setup
echo ========================================
echo.

REM Check if PostgreSQL is accessible
echo [1/4] Checking PostgreSQL connection...
psql -U postgres -d avidevdb -c "SELECT version();" >nul 2>&1
if %errorlevel% neq 0 (
    echo ERROR: Cannot connect to PostgreSQL database 'avidevdb'
    echo Please ensure PostgreSQL is running and database exists
    pause
    exit /b 1
)
echo ✓ PostgreSQL connection successful
echo.

REM Insert sample data
echo [2/4] Inserting sample OAuth client data...
psql -U postgres -d avidevdb -f "Avixar.Data\scripts\SampleData.sql"
if %errorlevel% neq 0 (
    echo ERROR: Failed to insert sample data
    pause
    exit /b 1
)
echo ✓ Sample data inserted successfully
echo.

REM Verify data
echo [3/4] Verifying sample data...
psql -U postgres -d avidevdb -c "SELECT client_id, client_name FROM clients WHERE client_id = 'test_client_123';"
echo.

echo [4/4] Setup complete!
echo.
echo ========================================
echo Next Steps:
echo ========================================
echo 1. Import Postman collection:
echo    File: Avixar_IdentityProvider_API.postman_collection.json
echo.
echo 2. Start the applications:
echo    Terminal 1: dotnet run --project Avixar.API
echo    Terminal 2: dotnet run --project Avixar.UI
echo.
echo 3. Open Postman and start testing!
echo    See API_TESTING_GUIDE.md for details
echo.
echo Sample Credentials:
echo    Client ID: test_client_123
echo    Client Secret: test_secret_456
echo    Test Email: testuser@example.com
echo    Test Password: Test@123456
echo ========================================
echo.
pause
