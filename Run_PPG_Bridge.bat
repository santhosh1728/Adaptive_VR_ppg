@echo off
title Underwater Reef Rescue - ESP32 PPG Live Bridge
color 0b
echo ==============================================================================
echo       UNDERWATER REEF RESCUE - ESP32 PPG HARDWARE BRIDGE
echo ==============================================================================
echo.
echo  Detected ESP32 COM Port: COM3 @ 115200 Baud
echo  Target Unity Port:       UDP 9000 (OSC) & UDP 5005 (Raw)
echo.
echo  INSTRUCTIONS:
echo   1. Place your index finger firmly on the PPG optical sensor LED.
echo   2. Keep your hand still for 3-5 seconds while it locks onto your pulse.
echo   3. Launch Unity Play Mode (or run the Windows Demo / Procus VR APK).
echo.
echo ==============================================================================
echo.
python "%~dp0Tools\ppg_to_unity.py" --port COM3
pause
