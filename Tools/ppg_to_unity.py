"""
==============================================================================
 Underwater Reef Rescue - ESP32 PPG to Unity / Mobile VR Bridge
==============================================================================
 Bridges hardware Photoplethysmography (PPG) from an ESP32 / Arduino
 to Unity (Desktop PC, Meta Quest 3, or Procus Smartphone VR) via UDP/OSC.

 Usage:
   1. Local Unity Editor:
        python ppg_to_unity.py --port COM3
   2. Procus Mobile VR (Phone inside headset on Wi-Fi):
        python ppg_to_unity.py --port COM3 --ip 192.168.1.15
   3. Demo / Simulation Mode (No hardware connected):
        python ppg_to_unity.py --demo
==============================================================================
"""

import socket
import time
import math
import sys
import argparse
import random
import re

try:
    import serial
    import serial.tools.list_ports
    HAS_SERIAL = True
except ImportError:
    HAS_SERIAL = False

UDP_PORT_OSC = 9000
UDP_PORT_RAW = 5005

def find_esp32_port():
    if not HAS_SERIAL:
        return None
    ports = serial.tools.list_ports.comports()
    for p in ports:
        desc = (p.description or "").lower()
        if "ch340" in desc or "cp210" in desc or "ftdi" in desc or "usb-serial" in desc or "esp32" in desc:
            return p.device
    if len(ports) > 0:
        return ports[0].device
    return None

def build_osc_message(address, float_val):
    # Minimal binary OSC packet: address (null-padded to 4 bytes) + ",f" + big-endian float
    addr_bytes = address.encode('utf-8') + b'\x00'
    while len(addr_bytes) % 4 != 0:
        addr_bytes += b'\x00'
    tag_bytes = b',f\x00\x00'
    import struct
    val_bytes = struct.pack('>f', float_val)
    return addr_bytes + tag_bytes + val_bytes

def main():
    parser = argparse.ArgumentParser(description="ESP32 PPG to Unity / Procus VR Bridge")
    parser.add_argument("--port", type=str, default=None, help="Serial COM port (e.g., COM3, COM4, /dev/ttyUSB0)")
    parser.add_argument("--baud", type=int, default=115200, help="Serial baud rate (Default: 115200)")
    parser.add_argument("--ip", type=str, default="127.0.0.1", help="Target Unity IP (127.0.0.1 for PC, Phone Wi-Fi IP for Procus VR)")
    parser.add_argument("--demo", action="store_true", help="Force synthetic demo PPG wave generator")
    args = parser.parse_args()

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    dest_ip = args.ip
    print(f"[Bridge] Target Destination: {dest_ip}:{UDP_PORT_OSC} (OSC) & {dest_ip}:{UDP_PORT_RAW} (Raw UDP)")

    ser = None
    if not args.demo and HAS_SERIAL:
        target_port = args.port or find_esp32_port()
        if target_port:
            try:
                print(f"[Serial] Connecting to ESP32 on {target_port} @ {args.baud} baud...")
                ser = serial.Serial(target_port, args.baud, timeout=0.1)
                time.sleep(1.5) # Allow ESP32 reboot
                print(f"[Serial] Connected successfully to {target_port}!")
            except Exception as e:
                print(f"[Warning] Could not open {target_port}: {e}. Falling back to Demo Mode.")
                ser = None
        else:
            print("[Serial] No ESP32 serial port detected. Running in Demo Simulation Mode.")
    else:
        print("[Demo Mode] Generating real-time bio-accurate PPG waveform...")

    # Simulation state
    sim_bpm = 72.0
    sim_t = 0.0
    stress_phase = 0.0

    print("\n" + "="*60)
    print("      HEART RATE STREAMING ACTIVE (Press Ctrl+C to stop)")
    print("="*60 + "\n")

    try:
        while True:
            bpm_val = None
            raw_ibi = None
            spo2_val = 98.0

            is_hardware_packet = False
            finger_on_sensor = True

            if ser and ser.is_open:
                try:
                    line = ser.readline().decode('utf-8', errors='ignore').strip()
                    if line:
                        # Check for finger status
                        if "no finger" in line.lower() or "finger removed" in line.lower():
                            finger_on_sensor = False
                            sys.stdout.write(f"\r[ESP32 Status] ⚠️  No finger detected on optical sensor. Place your finger firmly on the sensor...   ")
                            sys.stdout.flush()

                        # 1. Regex parse for BPM / Heart Rate / Avg BPM
                        bpm_match = re.search(r'(?:BPM|bpm|HR|Heart\s*Rate|rate|Avg\s*BPM)\s*[:=]?\s*([0-9]+(?:\.[0-9]+)?)', line, re.IGNORECASE)
                        if bpm_match:
                            val = float(bpm_match.group(1))
                            if 35.0 <= val <= 220.0:
                                bpm_val = val
                                is_hardware_packet = True

                        # 2. Regex parse for SpO2
                        spo2_match = re.search(r'(?:SpO2|spo2|O2|oxygen)\s*[:=]?\s*([0-9]+(?:\.[0-9]+)?)', line, re.IGNORECASE)
                        if spo2_match:
                            s_val = float(spo2_match.group(1))
                            if 70.0 <= s_val <= 100.0:
                                spo2_val = s_val

                        # 3. Regex parse for IBI / RR Interval
                        ibi_match = re.search(r'(?:IBI|ibi|RR|rr|interval)\s*[:=]?\s*([0-9]+(?:\.[0-9]+)?)', line, re.IGNORECASE)
                        if ibi_match:
                            raw_ibi = float(ibi_match.group(1))

                        # 4. Pure single float / int on line (e.g. "74" or "74.5")
                        if bpm_val is None:
                            try:
                                pure_f = float(line)
                                if 35.0 <= pure_f <= 220.0:
                                    bpm_val = pure_f
                                    is_hardware_packet = True
                            except ValueError:
                                pass
                except Exception as e:
                    print(f"\n[Serial Error] {e}")

            # Fallback to simulated physiology only if no valid hardware packet this tick
            if bpm_val is None:
                sim_t += 0.05
                rsa = 3.5 * math.sin(sim_t * 1.5)
                drift = 5.0 * math.sin(sim_t * 0.15)
                stress_wave = math.sin(sim_t * 0.04)
                stress_boost = max(0.0, stress_wave) * 18.0
                sim_bpm = 70.0 + rsa + drift + stress_boost + random.uniform(-0.4, 0.4)
                bpm_val = round(sim_bpm, 1)
                raw_ibi = round(60000.0 / bpm_val, 1)
                spo2_val = round(98.5 - (max(0.0, stress_boost) * 0.1), 1)
                source_tag = "[Hardware Wait]" if (ser and ser.is_open and not finger_on_sensor) else "[Synthetic]"
            else:
                if raw_ibi is None:
                    raw_ibi = round(60000.0 / max(30.0, bpm_val), 1)
                source_tag = "[ESP32 Live]"

            # Transmit OSC packet (Port 9000)
            osc_pkt = build_osc_message("/vr/hr", float(bpm_val))
            sock.sendto(osc_pkt, (dest_ip, UDP_PORT_OSC))

            # Transmit Raw UDP packet (Port 5005)
            if ser and ser.is_open and not finger_on_sensor:
                raw_str = "No finger detected\n"
            else:
                raw_str = f"BPM:{bpm_val:.1f},IBI:{raw_ibi:.0f},SPO2:{spo2_val:.1f}\n"
            sock.sendto(raw_str.encode('utf-8'), (dest_ip, UDP_PORT_RAW))

            # Display terminal status
            sys.stdout.write(f"\r{source_tag} Heart Rate: {bpm_val:5.1f} BPM | IBI: {raw_ibi:4.0f} ms | SpO2: {spo2_val:4.1f}% -> {dest_ip}   ")
            sys.stdout.flush()

            time.sleep(0.05) # 20 Hz telemetry streaming

    except KeyboardInterrupt:
        print("\n[Bridge] Stopping transmission. Goodbye!")
    finally:
        if ser and ser.is_open:
            ser.close()
        sock.close()

if __name__ == "__main__":
    main()
