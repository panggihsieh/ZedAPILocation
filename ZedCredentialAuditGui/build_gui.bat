@echo off
chcp 65001 >NUL
cd /d "C:\Users\hsieh\Desktop\api\ZedCredentialAuditGui"
echo Compiling with Roslyn csc.exe (MSBuild 2022) ...
"C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe" ^
  /nologo ^
  /target:winexe ^
  /platform:x64 ^
  /langversion:9.0 ^
  /out:ZedCredentialAuditGui.exe ^
  /reference:System.dll ^
  /reference:System.Core.dll ^
  /reference:System.Data.dll ^
  /reference:System.Drawing.dll ^
  /reference:System.Windows.Forms.dll ^
  Program.cs
if errorlevel 1 (
    echo --- BUILD FAILED ---
    exit /b 1
)
echo --- BUILD OK ---
echo Output: %CD%\ZedCredentialAuditGui.exe
