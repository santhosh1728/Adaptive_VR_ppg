using System;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;

namespace UnderwaterReef
{
    /// <summary>
    /// Master Hardware Bridge connecting PPG (Photoplethysmography) / ESP32 sensor data to Unity.
    /// Supports:
    /// 1. OSC UDP streaming on Port 9000 (direct compatibility with ppg_to_unity.py /vr/hr and /vr/state)
    /// 2. Raw Text UDP streaming on Port 5005 & 8888 (compatibility with UDPReceiver and direct Wi-Fi microcontrollers)
    /// 3. Direct USB Serial COM Port (COM3/COM4/COM5 auto-reading via reflection)
    /// 4. Seamless auto-fallback to Demo Keyboard Mode when sensor is paused or disconnected.
    /// </summary>
    public class ESP32HeartRateBridge : MonoBehaviour
    {
        public static ESP32HeartRateBridge Instance { get; private set; }

        [Header("OSC & UDP Settings")]
        [Tooltip("Primary port matching ppg_to_unity.py (Default: 9000)")]
        public int oscPort = 9000;
        [Tooltip("Secondary raw UDP port matching UDPReceiver (Default: 5005)")]
        public int rawUdpPort = 5005;
        public bool enableUdp = true;

        [Header("USB Serial Connection")]
        public string serialPortName = "COM3";
        public string portName => serialPortName;
        public int baudRate = 115200;
        public bool enableSerial = true; // Auto-connect directly to COM3 on Start

        [Header("Hardware Live State")]
        public bool isHardwareConnected = false;
        public bool isFingerPlaced = false;
        public float lastReceivedBPM = 72f;
        public string sensorStatusText = "DISCONNECTED";
        public string hardwareLabel = "Demo Mode";
        public float timeSinceLastPacket = 999f;

        [Header("RQ3: End-to-End Transport & Render Latency")]
        [Tooltip("Sub-millisecond delta from UDP/Serial receipt to Unity main-thread consumption")]
        public float lastTransportLatencyMs = 0f;
        public float averageLatencyMs = 0f;
        private long queuedPacketRxTicks = 0;
        private float reconnectTimer = 0f;
        private float fingerHoldTimer = 0f;

        // Threading & sockets
        private UdpClient oscClient;
        private UdpClient rawUdpClient;
        private Thread oscThread;
        private Thread rawUdpThread;
        private Thread serialThread;
        private volatile bool isRunning = false;

        // Serial reflection
        private object serialInstance;
        private MethodInfo openMethod;
        private MethodInfo closeMethod;
        private MethodInfo readLineMethod;
        private PropertyInfo isOpenProp;

        private readonly object lockObj = new object();
        private float queuedBPM = -1f;
        private string queuedStatus = "";
        private string queuedSource = "";
        private bool hasNewPacket = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            isRunning = true;

            if (enableUdp)
            {
                StartOscListener(oscPort);
                StartRawUdpListener(rawUdpPort);
            }

            if (enableSerial)
            {
                StartSerialReader();
            }
        }

        private void Update()
        {
            timeSinceLastPacket += Time.deltaTime;
            fingerHoldTimer += Time.deltaTime;

            // If no data received for 7.0 seconds, mark as disconnected and fallback to demo
            if (timeSinceLastPacket > 7.0f && isHardwareConnected)
            {
                isHardwareConnected = false;
                isFingerPlaced = false;
                hardwareLabel = "Demo Mode";
                sensorStatusText = "DISCONNECTED (Demo Active)";
                if (HeartRateSystem.Instance != null)
                {
                    HeartRateSystem.Instance.useHardwareSensor = false;
                    HeartRateSystem.Instance.hasHardwareBPM = false;
                }
            }

            // Finger Contact Hold: If no contact/pulse signal received for 3.5s, update status to missing finger
            if (fingerHoldTimer > 3.5f && isFingerPlaced)
            {
                isFingerPlaced = false;
                sensorStatusText = "PLACE FINGER ON SENSOR";
            }

            // Automatic Reconnection Watchdog: every 1.5 seconds when disconnected, search for newly plugged COM port
            if (enableSerial && !isHardwareConnected)
            {
                reconnectTimer += Time.deltaTime;
                if (reconnectTimer >= 1.5f)
                {
                    reconnectTimer = 0f;
                    TryAutoReconnectSerial();
                }
            }

            lock (lockObj)
            {
                if (hasNewPacket)
                {
                    hasNewPacket = false;
                    timeSinceLastPacket = 0f;
                    isHardwareConnected = true;
                    if (!string.IsNullOrEmpty(queuedSource)) hardwareLabel = queuedSource;
                    if (!string.IsNullOrEmpty(queuedStatus)) sensorStatusText = queuedStatus;

                    if (queuedPacketRxTicks > 0)
                    {
                        long nowTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                        lastTransportLatencyMs = (float)((nowTicks - queuedPacketRxTicks) * 1000.0 / System.Diagnostics.Stopwatch.Frequency);
                        averageLatencyMs = averageLatencyMs <= 0f ? lastTransportLatencyMs : Mathf.Lerp(averageLatencyMs, lastTransportLatencyMs, 0.2f);
                    }

                    if (queuedBPM >= 35f && queuedBPM <= 220f)
                    {
                        isFingerPlaced = true;
                        fingerHoldTimer = 0f;
                        lastReceivedBPM = queuedBPM;

                        if (HeartRateSystem.Instance != null)
                        {
                            HeartRateSystem.Instance.FeedSensorBPM(lastReceivedBPM);
                        }
                    }
                    else if (queuedBPM == -2f || queuedStatus == "CALCULATING PULSE...")
                    {
                        isFingerPlaced = true;
                        fingerHoldTimer = 0f;
                    }
                    else if (queuedStatus == "PLACE FINGER ON SENSOR")
                    {
                        isFingerPlaced = false;
                    }
                }
            }
        }

        #region OSC Receiver (Port 9000 - ppg_to_unity.py)
        private void StartOscListener(int port)
        {
            try
            {
                oscClient = new UdpClient(port);
                oscThread = new Thread(() => ListenUdpSocket(oscClient, port, true)) { IsBackground = true };
                oscThread.Start();
                Debug.Log($"<color=#00e5ff>[PPG Bridge]</color> OSC Listener active on UDP port {port}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"<color=#ffaa00>[PPG Bridge]</color> Could not bind OSC port {port}: {ex.Message}");
            }
        }

        private void StartRawUdpListener(int port)
        {
            try
            {
                rawUdpClient = new UdpClient(port);
                rawUdpThread = new Thread(() => ListenUdpSocket(rawUdpClient, port, false)) { IsBackground = true };
                rawUdpThread.Start();
                Debug.Log($"<color=#00e5ff>[PPG Bridge]</color> Raw UDP Listener active on port {port}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"<color=#ffaa00>[PPG Bridge]</color> Could not bind Raw UDP port {port}: {ex.Message}");
            }
        }

        private void ListenUdpSocket(UdpClient client, int port, bool isPrimaryOsc)
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, port);

            while (isRunning && client != null)
            {
                try
                {
                    byte[] data = client.Receive(ref remoteEP);
                    if (data == null || data.Length == 0) continue;

                    // Check if packet is OSC (starts with '/')
                    if (data[0] == '/')
                    {
                        ParseOscPacket(data, port);
                    }
                    else
                    {
                        // Raw UTF-8 text message
                        string text = Encoding.UTF8.GetString(data).Trim();
                        float parsed = ParseSensorText(text, out string status);
                        lock (lockObj)
                        {
                            queuedBPM = parsed;
                            queuedStatus = status;
                            queuedSource = "ESP32 (UDP)";
                            queuedPacketRxTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                            hasNewPacket = true;
                        }
                    }
                }
                catch (SocketException)
                {
                    break;
                }
                catch (Exception)
                {
                    // Keep listening
                }
            }
        }

        private void ParseOscPacket(byte[] data, int port)
        {
            if (data == null || data.Length < 8) return;

            int index = 0;
            string address = ReadOscString(data, ref index);
            if (string.IsNullOrEmpty(address) || index >= data.Length) return;

            string typeTag = ReadOscString(data, ref index);
            if (string.IsNullOrEmpty(typeTag)) return;

            if (address == "/vr/hr")
            {
                float bpm = -1f;

                if (typeTag.StartsWith(",f"))
                {
                    if (index + 4 <= data.Length)
                    {
                        byte[] bytes = new byte[4];
                        Array.Copy(data, index, bytes, 0, 4);
                        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
                        bpm = BitConverter.ToSingle(bytes, 0);
                    }
                }
                else if (typeTag.StartsWith(",i"))
                {
                    if (index + 4 <= data.Length)
                    {
                        bpm = (data[index] << 24) | (data[index + 1] << 16) | (data[index + 2] << 8) | data[index + 3];
                    }
                }

                if (bpm >= 35f && bpm <= 220f)
                {
                    lock (lockObj)
                    {
                        queuedBPM = bpm;
                        queuedSource = $"OSC :{port}";
                        queuedPacketRxTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                        hasNewPacket = true;
                    }
                }
            }
            else if (address == "/vr/state" && typeTag.StartsWith(",s"))
            {
                string state = ReadOscString(data, ref index);
                if (!string.IsNullOrEmpty(state))
                {
                    lock (lockObj)
                    {
                        queuedStatus = state;
                    }
                }
            }
        }

        private string ReadOscString(byte[] data, ref int index)
        {
            if (index >= data.Length) return null;
            int start = index;

            while (index < data.Length && data[index] != 0) index++;
            if (index >= data.Length) return null;

            string val = Encoding.UTF8.GetString(data, start, index - start);
            index++; // Skip null terminator
            while (index % 4 != 0 && index < data.Length) index++; // Pad to 4 bytes boundary

            return val;
        }
        #endregion

        #region Serial Reader (USB COM Port)
        private void StartSerialReader()
        {
            try
            {
                Type serialType = FindSerialPortType();
                if (serialType == null) return;

                // Auto-detect available COM ports if present
                MethodInfo getPortNamesMethod = serialType.GetMethod("GetPortNames", BindingFlags.Public | BindingFlags.Static);
                if (getPortNamesMethod != null)
                {
                    string[] ports = (string[])getPortNamesMethod.Invoke(null, null);
                    if (ports != null && ports.Length > 0)
                    {
                        bool foundCurrent = false;
                        foreach (var p in ports) { if (p == serialPortName) foundCurrent = true; }
                        if (!foundCurrent) serialPortName = ports[0];
                    }
                }

                serialInstance = Activator.CreateInstance(serialType, serialPortName, baudRate);
                openMethod = serialType.GetMethod("Open");
                closeMethod = serialType.GetMethod("Close");
                readLineMethod = serialType.GetMethod("ReadLine");
                isOpenProp = serialType.GetProperty("IsOpen");

                PropertyInfo timeoutProp = serialType.GetProperty("ReadTimeout");
                if (timeoutProp != null) timeoutProp.SetValue(serialInstance, 1000);

                openMethod.Invoke(serialInstance, null);
                serialThread = new Thread(ReadSerialLoop) { IsBackground = true };
                serialThread.Start();

                Debug.Log($"<color=#00e5ff>[PPG Bridge]</color> USB Serial connected on {serialPortName}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"<color=#ffaa00>[PPG Bridge]</color> Serial on {serialPortName} not opened ({ex.Message}). UDP listener will receive data from ppg_to_unity.py.");
            }
        }

        private void TryAutoReconnectSerial()
        {
            if (serialThread != null && serialThread.IsAlive) return;

            try
            {
                Type serialType = FindSerialPortType();
                if (serialType == null) return;

                MethodInfo getPortNamesMethod = serialType.GetMethod("GetPortNames", BindingFlags.Public | BindingFlags.Static);
                if (getPortNamesMethod != null)
                {
                    string[] ports = (string[])getPortNamesMethod.Invoke(null, null);
                    if (ports != null && ports.Length > 0)
                    {
                        bool found = false;
                        foreach (var p in ports) { if (p == serialPortName) { found = true; break; } }
                        if (!found) serialPortName = ports[0];
                        StartSerialReader();
                    }
                }
            }
            catch { }
        }

        private Type FindSerialPortType()
        {
            Type t = Type.GetType("System.IO.Ports.SerialPort, System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089");
            if (t != null) return t;
            t = Type.GetType("System.IO.Ports.SerialPort, System.IO.Ports");
            if (t != null) return t;
            return Type.GetType("System.IO.Ports.SerialPort");
        }

        private void ReadSerialLoop()
        {
            while (isRunning && serialInstance != null)
            {
                try
                {
                    bool isOpen = (bool)(isOpenProp?.GetValue(serialInstance) ?? false);
                    if (!isOpen) break;

                    string rawLine = (string)readLineMethod.Invoke(serialInstance, null);
                    if (!string.IsNullOrEmpty(rawLine))
                    {
                        float parsed = ParseSensorText(rawLine, out string status);
                        lock (lockObj)
                        {
                            queuedBPM = parsed;
                            queuedStatus = status;
                            queuedSource = serialPortName;
                            queuedPacketRxTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                            hasNewPacket = true;
                        }
                    }
                }
                catch (Exception)
                {
                    // Port unplugged or communication error — clean up so auto-reconnect can reconnect
                    try { closeMethod?.Invoke(serialInstance, null); } catch { }
                    serialInstance = null;
                    break;
                }
            }
        }
        #endregion

        #region Parsing Utilities
        private float ParseSensorText(string text, out string status)
        {
            status = "OK";
            text = text.Trim();
            if (string.IsNullOrEmpty(text)) return -1f;

            // 1. Explicit No-Finger / Finger Removed
            if (text.IndexOf("no finger", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("finger removed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("no contact", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                status = "PLACE FINGER ON SENSOR";
                return -1f;
            }

            // 2. Optical Infrared (IR) Intensity Check (MAX30102 / Optical)
            long detectedIR = -1;
            var irMatch = System.Text.RegularExpressions.Regex.Match(text, @"(?:IR|irValue)\s*[:=]\s*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (irMatch.Success && long.TryParse(irMatch.Groups[1].Value, out long ir))
            {
                detectedIR = ir;
                // If IR is low (< 8000), finger is completely off the optical window
                if (detectedIR < 8000)
                {
                    status = "PLACE FINGER ON SENSOR";
                    return -1f;
                }
            }

            // 3. Extract BPM or Heart Rate
            // 3a. Prioritize Averaged/Filtered BPM (e.g. "Avg BPM=72" or "Avg BPM: 72") to eliminate noise spikes
            var avgMatch = System.Text.RegularExpressions.Regex.Match(text, @"(?:Avg\s*BPM|Average\s*BPM)\s*[:=]?\s*([0-9]+(?:\.[0-9]+)?)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (avgMatch.Success && float.TryParse(avgMatch.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float avgVal))
            {
                if (avgVal >= 38f && avgVal <= 210f)
                {
                    status = "PULSE LOCKED";
                    return avgVal;
                }
            }

            // 3b. Fallback to standard BPM or Heart Rate
            var bpmMatches = System.Text.RegularExpressions.Regex.Matches(text, @"(?:BPM|Heart\s*Rate|HR)\s*[:=]?\s*([0-9]+(?:\.[0-9]+)?)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            foreach (System.Text.RegularExpressions.Match m in bpmMatches)
            {
                if (float.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float val))
                {
                    if (val >= 38f && val <= 210f)
                    {
                        status = "PULSE LOCKED";
                        return val;
                    }
                }
            }

            // 4. CSV Telemetry Format (e.g. time,IR,RED,beat,IBI,raw_bpm,filtered_bpm,valid,status)
            if (text.Contains(","))
            {
                string[] parts = text.Split(',');
                if (parts.Length >= 9)
                {
                    status = parts[8].Trim().ToUpper();
                    if (float.TryParse(parts[6].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float cbpm))
                    {
                        if (cbpm >= 35f && cbpm <= 220f) return cbpm;
                    }
                }
                else
                {
                    for (int i = 0; i < parts.Length; i++)
                    {
                        float b = ParseNumericBpm(parts[i]);
                        if (b >= 35f && b <= 220f)
                        {
                            status = "PULSE LOCKED";
                            return b;
                        }
                    }
                }
            }

            // 5. Direct standalone numeric float
            float directNum = ParseNumericBpm(text);
            if (directNum >= 35f && directNum <= 220f)
            {
                status = "PULSE LOCKED";
                return directNum;
            }

            // 6. If IR was high (finger firmly on sensor) but BPM is still acquiring or reading 0
            if (detectedIR >= 8000)
            {
                status = "CALCULATING PULSE...";
                return -2f;
            }

            return -1f;
        }

        private float ParseNumericBpm(string text)
        {
            text = text.Trim();
            if (text.Contains(":"))
            {
                string[] parts = text.Split(':');
                text = parts[parts.Length - 1].Trim();
            }
            else if (text.Contains("="))
            {
                string[] parts = text.Split('=');
                text = parts[parts.Length - 1].Trim();
            }

            // Strip any remaining non-numeric chars except digits and dot
            text = System.Text.RegularExpressions.Regex.Replace(text, @"[^\d.]", "");

            if (float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float result))
            {
                return result;
            }

            return -1f;
        }
        #endregion

        private void OnDestroy()
        {
            isRunning = false;

            try { oscClient?.Close(); } catch { }
            try { rawUdpClient?.Close(); } catch { }
            try { if (serialInstance != null) closeMethod?.Invoke(serialInstance, null); } catch { }

            if (oscThread != null && oscThread.IsAlive) try { oscThread.Abort(); } catch { }
            if (rawUdpThread != null && rawUdpThread.IsAlive) try { rawUdpThread.Abort(); } catch { }
            if (serialThread != null && serialThread.IsAlive) try { serialThread.Abort(); } catch { }
        }
    }
}
