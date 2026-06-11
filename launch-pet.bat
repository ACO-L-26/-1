@echo off
cd /d "%~dp0"

:: Kill any existing server
taskkill /F /IM node.exe >nul 2>&1
timeout /t 1 /nobreak >nul

:: Start server
echo Starting server...
start /MIN "" node server.js
timeout /t 3 /nobreak >nul

:: Launch desktop pet
echo Launching Music Pet...
start "" MusicPet.exe

echo Done! The pet should appear at the bottom-right of your screen.
