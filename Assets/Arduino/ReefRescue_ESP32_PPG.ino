/*
 ==============================================================================
  Underwater Reef Rescue - ESP32 PPG Heart Rate Firmware
  Target Board: ESP32 Dev Module / NodeMCU-32S / ESP32-WROOM-32
  Sensor: Analog Optical PPG Sensor (PulseSensor, KY-039, or Photodiode)
          connected to ADC1 Pin (e.g., GPIO 34)
 ==============================================================================
  Baud Rate: 115200
  Unity Bridge: ESP32HeartRateBridge.cs
 ==============================================================================
*/

#include <Arduino.h>

// --- Pin Definitions ---
const int PPG_PIN = 34;          // Analog pin (ADC1 GPIO 34, does not interfere with WiFi)
const int LED_PIN = 2;           // Onboard status LED (blinks with heartbeat)

// --- Algorithm Parameters ---
const int SAMPLE_INTERVAL_MS = 10;   // 100 Hz sampling rate
const int MIN_IBI_MS = 300;          // Max heart rate cap (~200 BPM)
const int MAX_IBI_MS = 1500;         // Min heart rate cap (~40 BPM)

// --- Dynamic Threshold Variables ---
int signalValue = 0;
int peakValue = 2048;
int troughValue = 2048;
int threshold = 2048;
int amplitude = 250;
bool isPulse = false;

unsigned long lastSampleTime = 0;
unsigned long lastBeatTime = 0;
unsigned long lastReportTime = 0;
unsigned long ibi = 750;             // Inter-Beat Interval in ms (750ms = 80 BPM)
float currentBPM = 72.0f;

// Running average for smoothing
const int RATE_SIZE = 6;
int rateHistory[RATE_SIZE] = { 750, 750, 750, 750, 750, 750 };
int rateIndex = 0;

void setup() {
  Serial.begin(115200);
  pinMode(LED_PIN, OUTPUT);
  digitalWrite(LED_PIN, LOW);

  // Configure ADC for ESP32 (12-bit resolution: 0 - 4095)
  analogReadResolution(12);
  analogSetAttenuation(ADC_11db); // Full range 0 - 3.3V

  // Startup flash
  for (int i = 0; i < 3; i++) {
    digitalWrite(LED_PIN, HIGH);
    delay(100);
    digitalWrite(LED_PIN, LOW);
    delay(100);
  }

  Serial.println("[ESP32] Underwater Reef Rescue PPG Monitor Initialized.");
}

void loop() {
  unsigned long currentTime = millis();

  if (currentTime - lastSampleTime >= SAMPLE_INTERVAL_MS) {
    lastSampleTime = currentTime;
    processPPG();
  }
}

void processPPG() {
  signalValue = analogRead(PPG_PIN);
  unsigned long now = millis();
  unsigned long timeSinceLastBeat = now - lastBeatTime;

  // Track signal peaks and troughs to adapt to changing ambient light
  if (signalValue < threshold && timeSinceLastBeat > (ibi / 5) * 3) {
    if (signalValue < troughValue) {
      troughValue = signalValue;
    }
  }

  if (signalValue > threshold && signalValue > peakValue) {
    peakValue = signalValue;
  }

  // Heartbeat Peak Detection
  if (timeSinceLastBeat > MIN_IBI_MS) {
    if ((signalValue > threshold) && !isPulse && (timeSinceLastBeat < MAX_IBI_MS)) {
      isPulse = true;
      digitalWrite(LED_PIN, HIGH);

      ibi = now - lastBeatTime;
      lastBeatTime = now;

      // Add to running average
      rateHistory[rateIndex] = ibi;
      rateIndex = (rateIndex + 1) % RATE_SIZE;

      int totalIBI = 0;
      for (int i = 0; i < RATE_SIZE; i++) {
        totalIBI += rateHistory[i];
      }
      int avgIBI = totalIBI / RATE_SIZE;

      if (avgIBI > 0) {
        currentBPM = 60000.0f / avgIBI;
        currentBPM = constrain(currentBPM, 45.0f, 195.0f);

        // Transmit formatted line to Unity (with Avg BPM)
        Serial.print("Avg BPM: ");
        Serial.println(currentBPM, 1);
      }
    }
  }

  // Falling edge / end of pulse
  if (signalValue < threshold && isPulse) {
    digitalWrite(LED_PIN, LOW);
    isPulse = false;
    amplitude = peakValue - troughValue;
    threshold = amplitude / 2 + troughValue;
    peakValue = threshold;
    troughValue = threshold;
  }

  // Reset if no pulse detected for 2.5 seconds (finger removed)
  if (timeSinceLastBeat > 2500) {
    threshold = 2048;
    peakValue = 2048;
    troughValue = 2048;
    lastBeatTime = now;
    digitalWrite(LED_PIN, LOW);
    isPulse = false;
    Serial.println("No finger detected");
  }
}

