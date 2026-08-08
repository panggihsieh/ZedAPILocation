@echo off
chcp 65001 >NUL
cd /d "%~dp0"

echo ================================================================
echo   Zed API Location Analysis - Publish (Self-contained single exe)
echo ================================================================
echo.

echo [1/2] Restoring packages...
dotnet restore ZedCredentialAuditGui.csproj
if errorlevel 1 (
    echo --- RESTORE FAILED ---
    exit /b 1
)

echo.
echo [2/2] Publishing single-file self-contained exe...
dotnet publish ZedCredentialAuditGui.csproj ^
  -c Release ^
  -o publish ^
  --no-restore
if errorlevel 1 (
    echo --- PUBLISH FAILED ---
    exit /b 1
)

echo.
echo --- PUBLISH OK ---
echo Output: %CD%\publish\ZedCredentialAuditGui.exe
echo.
echo The exe is fully self-contained. No .NET runtime install needed
echo on the target machine (Windows 10 1607+ x64).
echo.
