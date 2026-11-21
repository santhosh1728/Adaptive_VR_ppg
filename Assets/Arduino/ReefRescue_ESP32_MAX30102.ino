/*
 ==============================================================================
  Underwater Reef Rescue - ESP32 MAX30102 / MAX30105 Heart Rate Firmware
 ==============================================================================
  Target Board: ESP32 Dev Module / NodeMCU-32S / ESP32-WROOM-32
  Sensor: MAX30102 or MAX30105 I2C Optical Sensor
  
  Wiring:
    MAX30102 VIN  -> ESP32 3.3V (or 5V if module has onboard 3.3V LDO)
    MAX30102 GND  -> ESP32 GND
    MAX30102 SDA  -> ESP32 GPIO 21
    MAX30102 SCL  -> ESP32 GPIO 22

  Required Arduino Libraries:
    - "SparkFun MAX3010x Pulse and Proximity Sensor Library"
      (Install via Arduino IDE: Tools -> Manage Libraries -> Search "SparkFun MAX3010x")
 ==============================================================================
  Baud Rate: 115200
  Unity Bridge: Direct USB COM3 or UDP Bridge
 ==============================================================================
*/

#include <Wire.h>
#include "MAX30105.h"
#include "heartRate.h"

MAX30105 particleSensor;

const byte LED_PIN = 2; // ESP32 Onboard Blue LED (GPIO 2)

// --- Sensitive Optical Threshold ---
// Default SparkFun threshold is 50,000 which is often too high for cold/dry skin.
// 12,000 allows immediate, reliable detection on light finger resting!
const long FINGER_THRESHOLD = 12000;

const byte RATE_SIZE = 4; // 4-beat rolling average
byte rates[RATE_SIZE];
byte rateSpot = 0;
long lastBeat = 0;

float beatsPerMinute = 0;
int beatAvg = 0;
unsigned long lastFingerReport = 0;

void setup() {
  Serial.begin(115200);
  pinMode(LED_PIN, OUTPUT);
  digitalWrite(LED_PIN, LOW);

  // Initialize I2C on ESP32 default pins (SDA=21, SCL=22)
  Wire.begin(21, 22);

  // Initialize MAX30102 sensor
  if (!particleSensor.begin(Wire, I2C_SPEED_FAST)) {
    Serial.println("[ESP32 Error] MAX30102 was not found. Please check wiring (SDA=21, SCL=22, VIN=3.3V, GND=GND).");
    while (1) {
      digitalWrite(LED_PIN, HIGH);
      delay(200);
      digitalWrite(LED_PIN, LOW);
      delay(200);
    }
  }

  // Setup MAX30102 optical operating parameters
  // Power level: 0x3F (~12.5mA drive current for deep tissue optical pulse without dropping beats)
  byte ledBrightness = 0x3F; // Boosted from 0x24 so inhalation beats are not dropped
  byte sampleAverage = 4;    // Options: 1, 2, 4, 8, 16, 32
  byte ledMode = 2;          // Options: 1 = Red only, 2 = Red + IR, 3 = Red + IR + Green
  int sampleRate = 400;      // Options: 50, 100, 200, 400, 800, 1000, 1600, 3200
  int pulseWidth = 411;      // Options: 69, 118, 215, 411
  int adcRange = 8192;       // Options: 2048, 4096, 8192, 16384

  particleSensor.setup(ledBrightness, sampleAverage, ledMode, sampleRate, pulseWidth, adcRange);
  particleSensor.setPulseAmplitudeRed(0x0A); // Turn Red LED low to indicate power
  particleSensor.setPulseAmplitudeGreen(0);  // Green off

  // Startup indication: 3 quick blinks
  for (int i = 0; i < 3; i++) {
    digitalWrite(LED_PIN, HIGH);
    delay(100);
    digitalWrite(LED_PIN, LOW);
    delay(100);
  }

  Serial.println("[ESP32] MAX30102 Heart Rate Sensor Online. Place finger lightly on sensor glass.");
}

void loop() {
  long irValue = particleSensor.getIR();
  unsigned long now = millis();

  // 1. Check if finger is placed on the sensor
  if (irValue < FINGER_THRESHOLD) {
    digitalWrite(LED_PIN, LOW);
    beatsPerMinute = 0;
    beatAvg = 0;

    // Report "No finger detected" every 600ms so Unity knows hardware is active but waiting for contact
    if (now - lastFingerReport > 600) {
      Serial.println("No finger detected");
      lastFingerReport = now;
    }
    return;
  }

  // 2. Beat detection algorithm with 320ms refractory lockout (prevents dicrotic notch double-counting)
  if (checkForBeat(irValue) == true) {
    long delta = now - lastBeat;
    if (delta >= 320) {
      lastBeat = now;

      beatsPerMinute = 60.0 / (delta / 1000.0);

      if (beatsPerMinute >= 38.0 && beatsPerMinute <= 200.0) {
        rates[rateSpot++] = (byte)beatsPerMinute;
        rateSpot %= RATE_SIZE;

        int total = 0;
        int count = 0;
        for (byte x = 0; x < RATE_SIZE; x++) {
          if (rates[x] > 0) {
            total += rates[x];
            count++;
          }
        }
        beatAvg = count > 0 ? (total / count) : beatsPerMinute;

        // Pulse onboard LED to verify real-time physical beat
        digitalWrite(LED_PIN, HIGH);

        // Transmit clean, high-precision telemetry line to Unity (with Avg BPM)
        Serial.print("Avg BPM: ");
        Serial.print(beatAvg > 0 ? beatAvg : beatsPerMinute);
        Serial.print(", BPM: ");
        Serial.print(beatsPerMinute, 1);
        Serial.print(", IR: ");
        Serial.println(irValue);

        delay(35); // LED flash duration
        digitalWrite(LED_PIN, LOW);
      }
    }
  } else {
    // If finger is on sensor, but waiting for the next beat or initial pulse lock
    if (beatAvg == 0 && (now - lastFingerReport > 500)) {
      Serial.print("IR: ");
      Serial.print(irValue);
      Serial.println(", BPM: 0.0");
      lastFingerReport = now;
    }
  }
}
