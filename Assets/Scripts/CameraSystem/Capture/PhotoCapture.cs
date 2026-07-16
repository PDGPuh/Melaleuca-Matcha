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
        [SerializeField] private float burstInterval = 0.15f; 
        [SerializeField] private float flashDuration = 0.18f;
        [SerializeField] private int targetPhotoHeight = 600; // Target resolution height for storage efficiency

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
            if (CameraUI.Instance != null) CameraUI.Instance.SetViewfinderActive(false);
            
            GameObject gameUI = GameObject.Find("GameUI");
            if (gameUI != null) gameUI.SetActive(false);

            yield return new WaitForEndOfFrame();

            // Read screen pixels
            int width = Screen.width;
            int height = Screen.height;
            Texture2D rawScreenTex = new Texture2D(width, height, TextureFormat.RGB24, false);
            rawScreenTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            rawScreenTex.Apply();

            // Restore HUD UI
            if (gameUI != null) gameUI.SetActive(true);
            if (CameraUI.Instance != null && CameraManager.Instance != null && CameraManager.Instance.IsCameraActive)
            {
                CameraUI.Instance.SetViewfinderActive(true);
            }

            // Shutter click sound + screen white flash
            PlayShutterSound();
            TriggerFlash();

            // Use the captured screen texture directly to avoid rendering/blit lag that causes blank/black screenshots.
            Texture2D resizedTex = rawScreenTex;

            // Apply ISO noise
            if (CameraManager.Instance != null && CameraManager.Instance.ExpSys != null)
            {
                ApplyNoiseToTexture(resizedTex, CameraManager.Instance.ExpSys.ISO);
            }

            isCapturing = false;
            callback?.Invoke(resizedTex);
        }

        private IEnumerator BurstCaptureRoutine(Action<List<Texture2D>> callback)
        {
            List<Texture2D> burstList = new List<Texture2D>();
            int maxBurstFrames = 5; // Reduced to 5 for safety and speed
            int capturedFrames = 0;

            while (isBurstModeActive && capturedFrames < maxBurstFrames)
            {
                if (CameraUI.Instance != null) CameraUI.Instance.SetViewfinderActive(false);
                GameObject gameUI = GameObject.Find("GameUI");
                if (gameUI != null) gameUI.SetActive(false);

                yield return new WaitForEndOfFrame();

                int width = Screen.width;
                int height = Screen.height;
                Texture2D rawScreenTex = new Texture2D(width, height, TextureFormat.RGB24, false);
                rawScreenTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                rawScreenTex.Apply();

                if (gameUI != null) gameUI.SetActive(true);
                if (CameraUI.Instance != null && CameraManager.Instance != null && CameraManager.Instance.IsCameraActive)
                {
                    CameraUI.Instance.SetViewfinderActive(true);
                }

                PlayShutterSound();
                TriggerFlash();

                Texture2D resizedTex = rawScreenTex;

                if (CameraManager.Instance != null && CameraManager.Instance.ExpSys != null)
                {
                    ApplyNoiseToTexture(resizedTex, CameraManager.Instance.ExpSys.ISO);
                }

                burstList.Add(resizedTex);
                capturedFrames++;

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

        private Texture2D ResizeTextureGPU(Texture2D source, int targetWidth, int targetHeight)
        {
            RenderTexture rt = RenderTexture.GetTemporary(targetWidth, targetHeight, 0, RenderTextureFormat.ARGB32);
            rt.filterMode = FilterMode.Bilinear;
            RenderTexture.active = rt;
            
            Graphics.Blit(source, rt);
            
            Texture2D result = new Texture2D(targetWidth, targetHeight, TextureFormat.RGB24, false);
            result.ReadPixels(new Rect(0, 0, targetWidth, targetHeight), 0, 0);
            result.Apply();
            
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            return result;
        }

        private void ApplyNoiseToTexture(Texture2D tex, int isoValue)
        {
            if (isoValue <= 200) return; // Silent clean sensor at lower ISOs

            // Scale noise strength based on real-world ISO grain
            // ISO 400: ~0.015, ISO 1600: ~0.04, ISO 12800: ~0.09
            float noiseStrength = (isoValue - 200f) / 12600f * 0.08f + 0.01f;

            // Reduce noise based on Sensor upgrades if the UpgradeManager exists
            if (SaveSystem.Instance != null && SaveSystem.Instance.SensorUpgradeLevel > 1)
            {
                float reduction = (SaveSystem.Instance.SensorUpgradeLevel - 1) * 0.2f; // 20% noise reduction per level
                noiseStrength *= Mathf.Max(0.2f, 1f - reduction);
            }

            Color[] pixels = tex.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                float grain = UnityEngine.Random.Range(-noiseStrength, noiseStrength);
                pixels[i].r = Mathf.Clamp01(pixels[i].r + grain);
                pixels[i].g = Mathf.Clamp01(pixels[i].g + grain);
                pixels[i].b = Mathf.Clamp01(pixels[i].b + grain);
            }
            tex.SetPixels(pixels);
            tex.Apply();
        }
    }
}
