using System;
using UnityEngine;

namespace UnderwaterReef
{
    /// <summary>
    /// Generates procedural underwater ambient sound, rhythmic heartbeats matching BPM,
    /// bubble regulators, and pickup chimes entirely via software synthesis.
    /// No external audio clips required!
    /// </summary>
    public class UnderwaterAudio : MonoBehaviour
    {
        public static UnderwaterAudio Instance { get; private set; }

        private AudioSource audioSource;
        private AudioSource sfxSource;

        private float sampleRate = 44100f;
        private float heartbeatPhase;
        private float heartbeatTimer;
        private bool playBeatImpulse;
        private float beatVolume;

        // Hydrophone dynamic low-pass state
        private float lowPassFilterState = 0f;
        public bool isSubmerged = true;
        private float regulatorBreathTimer = 0f;
        private DiverController diver;

        // Thread-safe fast PRNG for audio DSP thread (Unity's Random cannot be called off main thread)
        private uint audioRngState = 2463534242;

        private float NextAudioNoise()
        {
            audioRngState ^= audioRngState << 13;
            audioRngState ^= audioRngState >> 17;
            audioRngState ^= audioRngState << 5;
            return ((audioRngState & 0x7FFFFFFF) / (float)0x7FFFFFFF) * 2f - 1f;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = true;
            audioSource.loop = true;
            audioSource.spatialBlend = 0f; // 2D sound

            // Generate 1-second silent looping clip to guarantee OnAudioFilterRead runs
            AudioClip dummyClip = AudioClip.Create("UnderwaterLoop", 44100, 2, (int)sampleRate, false);
            audioSource.clip = dummyClip;
            audioSource.Play();

            // Separate child GameObject for one-shot SFX to prevent OnAudioFilterRead conflict
            GameObject sfxGO = new GameObject("ReefSFXSource");
            sfxGO.transform.SetParent(transform, false);
            sfxSource = sfxGO.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 0f;

            if (HeartRateSystem.Instance != null)
            {
                HeartRateSystem.Instance.OnHeartbeat += HandleHeartbeat;
            }
        }

        private void OnDestroy()
        {
            if (HeartRateSystem.Instance != null)
            {
                HeartRateSystem.Instance.OnHeartbeat -= HandleHeartbeat;
            }
        }

        private void Update()
        {
            if (diver == null) diver = FindAnyObjectByType<DiverController>();
            if (diver != null)
            {
                isSubmerged = diver.transform.position.y < -0.2f;
            }

            // Periodic regulator exhalation bubble breathing sound
            if (isSubmerged)
            {
                regulatorBreathTimer += Time.deltaTime;
                float breathInterval = (HeartRateSystem.Instance != null && HeartRateSystem.Instance.isStressed) ? 2.8f : 4.8f;
                if (regulatorBreathTimer >= breathInterval)
                {
                    regulatorBreathTimer = 0f;
                    PlayRegulatorBubbles();
                }
            }
        }

        private void HandleHeartbeat(float bpm)
        {
            playBeatImpulse = true;
            beatVolume = Mathf.Lerp(0.25f, 0.9f, (bpm - 60f) / 90f);
            heartbeatPhase = 0f;
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            float dt = 1f / sampleRate;
            // Dynamic low-pass alpha: 0.05 when submerged (muffled hydrophone ~350Hz), 0.65 when surfaced (crisp air sound)
            float filterAlpha = isSubmerged ? 0.045f : 0.65f;

            for (int i = 0; i < data.Length; i += channels)
            {
                // 1. Ambient low underwater ocean rumble (pink/brown noise + low pass)
                float noise = NextAudioNoise() * (isSubmerged ? 0.035f : 0.012f);

                // Apply one-pole low pass filter
                lowPassFilterState += filterAlpha * (noise - lowPassFilterState);
                float ambientOutput = lowPassFilterState;

                // 2. Procedural Heartbeat "Lub-Dub" (damped low-frequency sine waves: ~50Hz and ~80Hz)
                float beatSample = 0f;
                if (playBeatImpulse)
                {
                    heartbeatPhase += dt;
                    if (heartbeatPhase < 0.12f)
                    {
                        // "Lub"
                        float env = Mathf.Sin((heartbeatPhase / 0.12f) * Mathf.PI);
                        beatSample = Mathf.Sin(2f * Mathf.PI * 52f * heartbeatPhase) * env * beatVolume * 0.8f;
                    }
                    else if (heartbeatPhase >= 0.16f && heartbeatPhase < 0.28f)
                    {
                        // "Dub"
                        float t2 = heartbeatPhase - 0.16f;
                        float env = Mathf.Sin((t2 / 0.12f) * Mathf.PI);
                        beatSample = Mathf.Sin(2f * Mathf.PI * 68f * t2) * env * beatVolume * 0.6f;
                    }
                    else if (heartbeatPhase >= 0.35f)
                    {
                        playBeatImpulse = false;
                    }
                }

                float mixed = ambientOutput + beatSample;

                for (int c = 0; c < channels; c++)
                {
                    data[i + c] = Mathf.Clamp(data[i + c] + mixed, -1f, 1f);
                }
            }
        }

        public void PlayRegulatorBubbles()
        {
            int samples = (int)(sampleRate * 0.45f);
            float[] audioData = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / sampleRate;
                float envelope = Mathf.Sin((t / 0.45f) * Mathf.PI);
                // Multi-tone bubbling gurgle
                float bubble1 = Mathf.Sin(2f * Mathf.PI * (280f + Mathf.Sin(t * 40f) * 80f) * t);
                float bubble2 = Mathf.Sin(2f * Mathf.PI * (420f + Mathf.Cos(t * 60f) * 120f) * t) * 0.5f;
                float hiss = NextAudioNoise() * 0.2f;
                audioData[i] = (bubble1 + bubble2 + hiss) * envelope * 0.18f;
            }

            AudioClip clip = AudioClip.Create("RegulatorBubble", samples, 1, (int)sampleRate, false);
            clip.SetData(audioData, 0);
            sfxSource.PlayOneShot(clip, 0.45f);
        }

        public void PlayFootstepThud(bool isHardRock)
        {
            int samples = (int)(sampleRate * 0.14f);
            float[] audioData = new float[samples];
            float freq = isHardRock ? 85f : 55f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / sampleRate;
                float envelope = Mathf.Exp(-t * 22f);
                float thud = Mathf.Sin(2f * Mathf.PI * freq * t);
                float sandGrit = NextAudioNoise() * 0.25f * Mathf.Exp(-t * 35f);
                audioData[i] = (thud + sandGrit) * envelope * 0.3f;
            }

            AudioClip clip = AudioClip.Create("FootstepThud", samples, 1, (int)sampleRate, false);
            clip.SetData(audioData, 0);
            sfxSource.PlayOneShot(clip, 0.35f);
        }

        public void PlayPearlChime()
        {
            PlayHarmonicTone(880f, 0.35f); // High A
        }

        public void PlayBeaconChime()
        {
            PlayHarmonicTone(440f, 0.8f); // Deep resonant chord
        }

        public void PlayWinFanfare()
        {
            PlayHarmonicTone(523.25f, 1.2f); // C Major harmonic chord
        }

        public void PlayShockBuzz()
        {
            PlayHarmonicTone(220f, 0.4f); // Low shock buzz
        }

        public void PlayDepthMilestonePing()
        {
            // Crisp dual-harmonic acoustic sonar chime for depth milestone
            int samples = (int)(sampleRate * 0.55f);
            float[] audioData = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / sampleRate;
                float envelope = Mathf.Exp(-t * 5.5f);
                float tone1 = Mathf.Sin(2f * Mathf.PI * 1046.5f * t); // High C
                float tone2 = Mathf.Sin(2f * Mathf.PI * 1567.98f * t) * 0.45f; // High G
                audioData[i] = (tone1 + tone2) * envelope * 0.35f;
            }
            AudioClip clip = AudioClip.Create("DepthChime", samples, 1, (int)sampleRate, false);
            clip.SetData(audioData, 0);
            sfxSource.PlayOneShot(clip, 0.4f);
        }

        public void PlayRapidAscentAlarm()
        {
            // Urgent high-frequency electronic double chirp warning
            int samples = (int)(sampleRate * 0.22f);
            float[] audioData = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / sampleRate;
                float freq = t < 0.10f ? 2200f : (t > 0.12f ? 2600f : 0f);
                float envelope = Mathf.Sin((t / 0.22f) * Mathf.PI);
                float tone = Mathf.Sin(2f * Mathf.PI * freq * t);
                audioData[i] = tone * envelope * 0.42f;
            }
            AudioClip clip = AudioClip.Create("AscentAlarm", samples, 1, (int)sampleRate, false);
            clip.SetData(audioData, 0);
            sfxSource.PlayOneShot(clip, 0.5f);
        }

        public void PlayLowOxygenPing()
        {
            // Soft intermittent low-pressure sonar alarm
            int samples = (int)(sampleRate * 0.18f);
            float[] audioData = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / sampleRate;
                float envelope = Mathf.Exp(-t * 12f);
                float tone = Mathf.Sin(2f * Mathf.PI * 659.25f * t); // E5
                audioData[i] = tone * envelope * 0.35f;
            }
            AudioClip clip = AudioClip.Create("LowO2Ping", samples, 1, (int)sampleRate, false);
            clip.SetData(audioData, 0);
            sfxSource.PlayOneShot(clip, 0.4f);
        }

        public void PlayCompassTick()
        {
            // Subtle tactile micro-click for cardinal compass bearings
            int samples = (int)(sampleRate * 0.04f);
            float[] audioData = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / sampleRate;
                float envelope = Mathf.Exp(-t * 85f);
                float click = Mathf.Sin(2f * Mathf.PI * 1800f * t) * envelope * 0.15f;
                audioData[i] = click;
            }
            AudioClip clip = AudioClip.Create("CompassTick", samples, 1, (int)sampleRate, false);
            clip.SetData(audioData, 0);
            sfxSource.PlayOneShot(clip, 0.2f);
        }

        public void PlayHarmonicTone(float baseFreq, float duration)
        {
            int samples = (int)(sampleRate * duration);
            float[] audioData = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = i / sampleRate;
                float envelope = Mathf.Exp(-t * 3.5f);
                float fundamental = Mathf.Sin(2f * Mathf.PI * baseFreq * t);
                float harmonic = Mathf.Sin(2f * Mathf.PI * baseFreq * 1.5f * t) * 0.4f;
                audioData[i] = (fundamental + harmonic) * envelope * 0.5f;
            }

            AudioClip clip = AudioClip.Create("Chime", samples, 1, (int)sampleRate, false);
            clip.SetData(audioData, 0);
            sfxSource.PlayOneShot(clip);
        }
    }
}

