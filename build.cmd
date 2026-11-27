@echo off
rem Builds dist\NJ-Sort.exe (icon and logo embedded). C# 5 with csc from .NET Framework 4; WPF + WinForms. No SDK, no NuGet.
cd /d "%~dp0"
set FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
if not exist dist mkdir dist
"%FW%\csc.exe" /nologo /target:winexe /optimize+ /win32icon:assets\nj-sort.ico /resource:assets\nj-sort.png,nj-sort.png ^
  /resource:assets\sfx\sort.wav,sort.wav /resource:assets\sfx\save.wav,save.wav /resource:assets\sfx\undo.wav,undo.wav /resource:assets\sfx\info.wav,info.wav /resource:assets\sfx\error.wav,error.wav ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:Microsoft.VisualBasic.dll /r:System.Xaml.dll ^
  /r:"%FW%\WPF\PresentationFramework.dll" /r:"%FW%\WPF\PresentationCore.dll" /r:"%FW%\WPF\WindowsBase.dll" ^
  /out:dist\NJ-Sort.exe /recurse:src\*.cs
