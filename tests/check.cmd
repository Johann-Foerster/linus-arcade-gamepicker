@echo off
setlocal
cd /d "%~dp0.."
call build.cmd || exit /b 1
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
"%CSC%" /nologo /target:exe /out:bin\Checks.exe /main:Arcade.Checks /r:System.Drawing.dll /r:System.Windows.Forms.dll src\*.cs tests\Checks.cs || exit /b 1
bin\Checks.exe
