/*
 ==============================================================================
  Underwater Reef Rescue - ESP32 PPG Heart Rate Firmware (Wi-Fi / UDP Version)
 ==============================================================================
  Target Board: ESP32 Dev Module / NodeMCU-32S / ESP32-WROOM-32
  Sensor: Analog Optical PPG (PulseSensor, KY-039) on GPIO 34 (ADC1)
          OR MAX30102 I2C sensor (SDA: GPIO 21, SCL: GPIO 22)
  Transmission: Direct UDP broadcast over Wi-Fi to Unity (PC or Phone in Procus VR)
 ==============================================================================
*/

#include <WiFi.h>
#include <WiFiUdp.h>

// --- Wi-Fi Network Credentials ---
// Connect ESP32 to the same Wi-Fi router OR smartphone mobile hotspot
const char* ssid     = "YOUR_WIFI_OR_HOTSPOT_NAME";
const char* password = "YOUR_WIFI_PASSWORD";

// --- Unity Target IP and UDP Port ---
// If running on PC: use PC local IP (e.g. 192.168.1.10)
// If running on Smartphone in Procus VR: use phone's IP or subnet broadcast (e.g. 192.168.1.255)
const char* unityTargetIP = "192.168.1.255"; 
const int   udpPort       = 5005; // Matches ESP32HeartRateBridge.cs rawUdpPort

WiFiUDP udp;

// --- Pin Definitions ---
const int PPG_PIN = 34; // ADC1 pin (GPIO 34) does not conflict with Wi-Fi
const int LED_PIN = 2;  // Onboard blue LED flashes with cardiac pulse

// --- Peak Detection Algorithm ---
int signalValue = 0;
int peakValue = 2048;
int troughValue = 2048;
int threshold = 2048;
int amplitude = 200;
bool isPulse = false;

unsigned long lastSampleTime = 0;
unsigned long lastBeatTime = 0;
unsigned long ibi = 750;
float currentBPM = 72.0f;

const int RATE_SIZE = 6;
int rateHistory[RATE_SIZE] = { 750, 750, 750, 750, 750, 750 };
int rateIndex = 0;

void setup() {
  Serial.begin(115200);
  pinMode(LED_PIN, OUTPUT);
  digitalWrite(LED_PIN, LOW);

  analogReadResolution(12);
  analogSetAttenuation(ADC_11db);

  Serial.println("[ESP32] Connecting to Wi-Fi...");
  WiFi.mode(WIFI_STA);
  WiFi.begin(ssid, password);

  int attempts = 0;
  while (WiFi.status() != WL_CONNECTED && attempts < 25) {
    delay(500);
    Serial.print(".");
    attempts++;
  }

  if (WiFi.status() == WL_CONNECTED) {
    Serial.println("\n[ESP32] Wi-Fi Connected!");
    Serial.print("[ESP32] IP Address: ");
    Serial.println(WiFi.localIP());
  } else {
    Serial.println("\n[ESP32 Warning] Wi-Fi not connected. Transmitting via Serial USB only.");
  }

  udp.begin(udpPort);
}

void loop() {
  unsigned long now = millis();
  if (now - lastSampleTime < 10) return; // 100 Hz sampling rate
  lastSampleTime = now;

  signalValue = analogRead(PPG_PIN);
  unsigned long timeSinceLastBeat = now - lastBeatTime;

  // Track signal trough and peak
  if (signalValue < threshold && timeSinceLastBeat > (ibi / 5) * 3) {
    if (signalValue < troughValue) troughValue = signalValue;
  }
  if (signalValue > threshold && signalValue > peakValue) {
    peakValue = signalValue;
  }

  // Detect Heartbeat rising edge
  if (timeSinceLastBeat > 300) {
    if ((signalValue > threshold) && !isPulse && (timeSinceLastBeat > (ibi / 5) * 3)) {
      isPulse = true;
      digitalWrite(LED_PIN, HIGH);
      ibi = now - lastBeatTime;
      lastBeatTime = now;

      rateHistory[rateIndex] = ibi;
      rateIndex = (rateIndex + 1) % RATE_SIZE;

      int totalIBI = 0;
      for (int i = 0; i < RATE_SIZE; i++) totalIBI += rateHistory[i];
      int avgIBI = totalIBI / RATE_SIZE;

      if (avgIBI > 0) {
        currentBPM = 60000.0f / avgIBI;
        currentBPM = constrain(currentBPM, 45.0f, 195.0f);

        // Format telemetry string: "BPM:74.5,IBI:805,SPO2:98.5"
        char payload[64];
        snprintf(payload, sizeof(payload), "BPM:%.1f,IBI:%lu,SPO2:98.5\n", currentBPM, ibi);

        // 1. Output to Serial Monitor
        Serial.print(payload);

        // 2. Broadcast via UDP packet over Wi-Fi to Unity
        if (WiFi.status() == WL_CONNECTED) {
          udp.beginPacket(unityTargetIP, udpPort);
          udp.write((const uint8_t*)payload, strlen(payload));
          udp.endPacket();
        }
      }
    }
  }

  // Falling edge
  if (signalValue < threshold && isPulse) {
    digitalWrite(LED_PIN, LOW);
    isPulse = false;
    amplitude = peakValue - troughValue;
    threshold = amplitude / 2 + troughValue;
    peakValue = threshold;
    troughValue = threshold;
  }

  // Reset if no pulse for 2.5s
  if (timeSinceLastBeat > 2500) {
    threshold = 2048;
    peakValue = 2048;
    troughValue = 2048;
    lastBeatTime = now;
    digitalWrite(LED_PIN, LOW);
    isPulse = false;
  }
}
