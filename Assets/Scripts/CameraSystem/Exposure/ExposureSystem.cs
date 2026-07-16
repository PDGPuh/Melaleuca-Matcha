using UnityEngine;
using System.Collections.Generic;

namespace RungTramTraSu.CameraSystem
{
    public class ExposureSystem : MonoBehaviour, IExposureSystem
    {
        public enum WhiteBalanceMode { Auto, Sunny, Cloudy, Shade, CustomKelvin }

        [Header("Exposure Parameters Presets")]
        private static readonly int[] ISOPresets = { 100, 200, 400, 800, 1600, 3200, 6400, 12800 };
        private static readonly float[] AperturePresets = { 1.2f, 1.4f, 1.8f, 2.0f, 2.8f, 4.0f, 5.6f, 8.0f, 11.0f, 16.0f, 22.0f };
        private static readonly string[] ShutterPresets = { "1/8000", "1/4000", "1/2000", "1/1000", "1/500", "1/250", "1/125", "1/60", "1/30", "1/15", "1/8", "1/4", "1/2", "1s" };
        private static readonly float[] ShutterValues = { 1f/8000f, 1f/4000f, 1f/2000f, 1f/1000f, 1f/500f, 1f/250f, 1f/125f, 1f/60f, 1f/30f, 1f/15f, 1f/8f, 1f/4f, 1f/2f, 1.0f };
        private static readonly int[] KelvinPresets = { 3200, 4000, 5200, 6000, 6500, 7500, 9000 };

        private int isoIndex = 1;      // Default ISO 200
        private int apertureIndex = 5; // Default F4.0
        private int shutterIndex = 5;  // Default 1/250
        private int kelvinIndex = 4;   // Default 6500K
        private int evIndex = 2;       // Default EV 0.0 (-2, -1, 0, +1, +2)
        private MeteringMode currentMetering = MeteringMode.Matrix;
        private WhiteBalanceMode wbMode = WhiteBalanceMode.Auto;

        // Interface implementation properties
        public int ISO => ISOPresets[isoIndex];
        public float Aperture => AperturePresets[apertureIndex];
        public float ShutterSpeed => ShutterValues[shutterIndex];
        public float EVValue => evIndex - 2f;
        public MeteringMode CurrentMetering => currentMetering;

        // Backwards compatibility wrappers
        public int CurrentISO => ISO;
        public float CurrentAperture => Aperture;
        public float CurrentShutterValue => ShutterSpeed;
        public float CurrentEVCompensation => EVValue;

        // Visual mapping properties for UI
        public string CurrentShutterString => ShutterPresets[shutterIndex];
        public WhiteBalanceMode CurrentWBMode => wbMode;
        public int CurrentKelvin => KelvinPresets[kelvinIndex];

        private void Start()
        {
            // Auto add MeteringSystem if missing
            if (MeteringSystem.Instance == null)
            {
                gameObject.AddComponent<MeteringSystem>();
            }
        }

        public void CycleISO(int direction)
        {
            isoIndex = Mathf.Clamp(isoIndex + direction, 0, ISOPresets.Length - 1);
        }

        public void CycleAperture(int direction)
        {
            apertureIndex = Mathf.Clamp(apertureIndex + direction, 0, AperturePresets.Length - 1);
            
            // Constrain by current lens max aperture
            if (CameraManager.Instance != null && CameraManager.Instance.LensSys != null)
            {
                float maxAp = CameraManager.Instance.LensSys.MaxApertureForCurrentLens;
                while (Aperture < maxAp && apertureIndex < AperturePresets.Length - 1)
                {
                    apertureIndex++;
                }
            }
        }

        public void CycleShutter(int direction)
        {
            shutterIndex = Mathf.Clamp(shutterIndex + direction, 0, ShutterPresets.Length - 1);
        }

        public void AdjustEV(float step)
        {
            int dir = step > 0f ? 1 : -1;
            evIndex = Mathf.Clamp(evIndex + dir, 0, 4); // -2, -1, 0, +1, +2
        }

        public void CycleEV(int direction)
        {
            evIndex = Mathf.Clamp(evIndex + direction, 0, 4);
        }

        public void SetMeteringMode(MeteringMode mode)
        {
            currentMetering = mode;
        }

        public void CycleWB()
        {
            int maxModes = System.Enum.GetValues(typeof(WhiteBalanceMode)).Length;
            wbMode = (WhiteBalanceMode)(((int)wbMode + 1) % maxModes);
        }

        public void CycleKelvin(int direction)
        {
            kelvinIndex = Mathf.Clamp(kelvinIndex + direction, 0, KelvinPresets.Length - 1);
        }

        public float CalculateLuminanceDeviation()
        {
            float N = Aperture;
            float t = ShutterSpeed;
            float S = ISO;

            // EV at standard ISO 100: EV = log2(N^2 / t)
            float evSettings = Mathf.Log((N * N) / t, 2.0f);
            
            // Offset EV matching current ISO
            float evAdjusted = evSettings - Mathf.Log(S / 100f, 2.0f);

            // Fetch ambient EV from MeteringSystem
            float ambientEV = 12f;
            if (MeteringSystem.Instance != null)
            {
                List<WildlifeDetector.DetectedTarget> targets = null;
                if (WildlifeDetector.Instance != null)
                {
                    targets = WildlifeDetector.Instance.ScanForVisibleTargets();
                }
                ambientEV = MeteringSystem.Instance.EvaluateAmbientEV(currentMetering, targets);
            }

            float targetEV = ambientEV + EVValue;
            
            // Returns exposure error (0 is perfectly exposed, positive is underexposed/dark, negative is overexposed/bright)
            // Note: Standard convention: evAdjusted > targetEV means camera is expecting MORE light (smaller aperture/faster shutter)
            // than target. So if settings EV is higher than target EV, it lets in LESS light (underexposed).
            return evAdjusted - targetEV;
        }

        // Keep compatibility wrapper for old code
        public float GetExposureError()
        {
            return CalculateLuminanceDeviation();
        }

        public void ApplyAutoExposure()
        {
            float ambientEV = 12f;
            if (MeteringSystem.Instance != null)
            {
                List<WildlifeDetector.DetectedTarget> targets = null;
                if (WildlifeDetector.Instance != null)
                {
                    targets = WildlifeDetector.Instance.ScanForVisibleTargets();
                }
                ambientEV = MeteringSystem.Instance.EvaluateAmbientEV(currentMetering, targets);
            }
            float targetEV = ambientEV + EVValue;

            // Choose average Aperture & ISO
            apertureIndex = 5; // F4.0
            isoIndex = 1;      // ISO 200

            // Apply lens limitations if any
            if (CameraManager.Instance != null && CameraManager.Instance.LensSys != null)
            {
                float maxAp = CameraManager.Instance.LensSys.MaxApertureForCurrentLens;
                while (Aperture < maxAp && apertureIndex < AperturePresets.Length - 1)
                {
                    apertureIndex++;
                }
            }

            float N = Aperture;
            float S = ISO;

            // targetEV = log2(N^2 / t) - log2(S/100)
            // targetEV + log2(S/100) = log2(N^2 / t)
            // 2^(targetEV + log2(S/100)) = N^2 / t
            // t = N^2 / 2^(targetEV + log2(S/100))
            float denom = Mathf.Pow(2f, targetEV + Mathf.Log(S / 100f, 2f));
            float targetShutter = (N * N) / denom;

            // Find closest Shutter Preset
            float minDiff = float.MaxValue;
            int bestShutterIndex = 5;
            for (int i = 0; i < ShutterValues.Length; i++)
            {
                float diff = Mathf.Abs(ShutterValues[i] - targetShutter);
                if (diff < minDiff)
                {
                    minDiff = diff;
                    bestShutterIndex = i;
                }
            }
            shutterIndex = bestShutterIndex;

            // If shutter is dangerously slow (e.g. slow action / blur), raise ISO to enable faster shutter speed
            if (shutterIndex > 8 && isoIndex < ISOPresets.Length - 1) // 1/30s or slower
            {
                isoIndex += 2; // Bump ISO (e.g. to 800)
                shutterIndex -= 2; // Fasten shutter speed
                shutterIndex = Mathf.Clamp(shutterIndex, 0, ShutterValues.Length - 1);
            }
        }
    }
}
