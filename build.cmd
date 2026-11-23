@echo off
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist bin mkdir bin
"%CSC%" /nologo /optimize+ /target:winexe /out:bin\ArcadePicker.exe /main:Arcade.Picker /r:System.Drawing.dll /r:System.Windows.Forms.dll src\*.cs || exit /b 1
"%CSC%" /nologo /optimize+ /target:winexe /out:bin\ArcadeSetup.exe /main:Arcade.Setup /r:System.Drawing.dll /r:System.Windows.Forms.dll src\*.cs || exit /b 1
echo Built bin\ArcadePicker.exe and bin\ArcadeSetup.exe
