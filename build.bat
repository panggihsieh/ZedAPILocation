@echo off
chcp 65001 >NUL
call "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat" >NUL
if errorlevel 1 (
    echo Failed to load vcvars64.bat
    exit /b 1
)
echo Compiling...
cl /nologo /EHsc /std:c++17 /O2 "C:\Users\hsieh\Desktop\api\ZedCredentialAudit.cpp" /Fe:"C:\Users\hsieh\Desktop\api\ZedCredentialAudit.exe"
if errorlevel 1 (
    echo BUILD FAILED
    exit /b 1
)
del "C:\Users\hsieh\Desktop\api\ZedCredentialAudit.obj" 2>NUL
echo --- BUILD OK ---
"C:\Users\hsieh\Desktop\api\ZedCredentialAudit.exe"
