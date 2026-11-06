@echo off
cd /d "%~dp0"
if not exist LoqRipple.exe powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Build.ps1"
if exist LoqRipple.exe start "" "%~dp0LoqRipple.exe"
