using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace RungTramTraSu.CameraSystem
{
    public class PhotoCapture : MonoBehaviour
    {
        public static PhotoCapture Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private float burstInterval = 0.1f; // 10 FPS Max
        [SerializeField] private float flashDuration = 0.18f;

        private AudioSource audioSource;
        private AudioClip syntheticShutterSound;
        private bool isCapturing = false;
        private bool isBurstModeActive = false;

        public bool IsCapturing => isCapturing;
        public bool IsBurstModeActive => isBurstModeActive;

        public event Action<Texture2D> OnPhotoCaptured;
        public event Action OnFlashTriggered;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

            syntheticShutterSound = CreateSyntheticShutterSound();
        }

        // Helper to synthesize DSLR shutter mirror click
        private AudioClip CreateSyntheticShutterSound()
        {
            int sampleRate = 44100;
            float duration = 0.18f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float click1 = 0f;
                if (t < 0.04f)
                {
                    float decay1 = Mathf.Exp(-t * 150f);
                    float noise = UnityEngine.Random.Range(-1f, 1f) * 0.4f;
                    float tone = Mathf.Sin(2f * Mathf.PI * 1200f * t) * 0.6f;
                    click1 = (noise + tone) * decay1;
                }

                float click2 = 0f;
                if (t >= 0.08f && t < 0.15f)
                {
                    float t2 = t - 0.08f;
                    float decay2 = Mathf.Exp(-t2 * 120f);
                    float noise = UnityEngine.Random.Range(-1f, 1f) * 0.3f;
                    float tone = Mathf.Sin(2f * Mathf.PI * 800f * t2) * 0.7f;
                    click2 = (noise + tone) * decay2;
                }

                samples[i] = click1 + click2;
            }

            AudioClip clip = AudioClip.Create("SyntheticShutter", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        public void PlayShutterSound()
        {
            if (audioSource != null && syntheticShutterSound != null)
            {
                audioSource.PlayOneShot(syntheticShutterSound);
            }
        }

        public void TriggerFlash()
        {
            OnFlashTriggered?.Invoke();
        }

        public void CaptureSingleShot(Action<Texture2D> callback)
        {
            if (isCapturing) return;
            StartCoroutine(CaptureRoutine(callback));
        }

        public void StartBurstCapture(Action<List<Texture2D>> callback)
        {
            if (isCapturing || isBurstModeActive) return;
            isBurstModeActive = true;
            StartCoroutine(BurstCaptureRoutine(callback));
        }

        public void StopBurstCapture()
        {
            isBurstModeActive = false;
        }

        private IEnumerator CaptureRoutine(Action<Texture2D> callback)
        {
            isCapturing = true;

            // 1. Hide viewfinder canvas and UI HUD
            CameraUI.Instance.SetViewfinderActive(false);
            GameObject gameUI = GameObject.Find("GameUI");
            if (gameUI != null) gameUI.SetActive(false);

            // Wait for end of frame to read pixels
            yield return new WaitForEndOfFrame();

            int width = Screen.width;
            int height = Screen.height;
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            // Restore HUD UI
            if (gameUI != null) gameUI.SetActive(true);
            CameraUI.Instance.SetViewfinderActive(true);

            // Shutter click sound + screen white flash
            PlayShutterSound();
            TriggerFlash();

            isCapturing = false;
            callback?.Invoke(tex);
        }

        private IEnumerator BurstCaptureRoutine(Action<List<Texture2D>> callback)
        {
            List<Texture2D> burstList = new List<Texture2D>();
            
            // Limit burst to 10 frames maximum to avoid memory crash
            int maxBurstFrames = 10;
            int capturedFrames = 0;

            while (isBurstModeActive && capturedFrames < maxBurstFrames)
            {
                // Disable UI temporarily
                CameraUI.Instance.SetViewfinderActive(false);
                GameObject gameUI = GameObject.Find("GameUI");
                if (gameUI != null) gameUI.SetActive(false);

                yield return new WaitForEndOfFrame();

                int width = Screen.width;
                int height = Screen.height;
                Texture2D tex = new Texture2D(width, height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();

                if (gameUI != null) gameUI.SetActive(true);
                CameraUI.Instance.SetViewfinderActive(true);

                PlayShutterSound();
                TriggerFlash();

                burstList.Add(tex);
                capturedFrames++;

                // Wait burst interval
                float elapsed = 0f;
                while (elapsed < burstInterval && isBurstModeActive)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }

            isBurstModeActive = false;
            callback?.Invoke(burstList);
        }
    }
}
