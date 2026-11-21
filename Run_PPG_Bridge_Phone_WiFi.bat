@echo off
title Underwater Reef Rescue - ESP32 PPG Phone Wi-Fi Streamer
color 0a
echo ==============================================================================
echo       ESP32 PPG TO SMARTPHONE (PROCUS VR) WI-FI STREAMER
echo ==============================================================================
echo.
set /p PHONE_IP="Enter your Smartphone IP Address (e.g. 192.168.1.15): "
if "%PHONE_IP%"=="" (
    echo [Error] No IP entered. Exiting...
    pause
    exit /b
)
echo.
echo  Streaming ESP32 PPG (COM3) to Phone IP: %PHONE_IP% ...
echo.
python "%~dp0Tools\ppg_to_unity.py" --port COM3 --ip %PHONE_IP%
pause
