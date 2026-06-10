@echo off
:: Start the server if not running
node server.js &
timeout /t 2 /nobreak >nul

:: Open pet in Chrome app mode (frameless window)
start "" chrome --app=http://127.0.0.1:8080/pet.html --window-size=340,500 --window-position=bottom-right

:: Wait for window to open
timeout /t 3 /nobreak >nul

:: Pin the window to be always on top
powershell -ExecutionPolicy Bypass -File "%~dp0pin-window.ps1"
