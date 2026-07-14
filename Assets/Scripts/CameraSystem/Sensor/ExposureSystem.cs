using UnityEngine;
using UnityEngine.SceneManagement;

namespace RungTramTraSu.CameraSystem
{
    public class ExposureSystem : MonoBehaviour
    {
        public enum WhiteBalanceMode { Auto, Sunny, Cloudy, Shade, CustomKelvin }

        [Header("Exposure Parameters Presets")]
        private static readonly int[] ISOPresets = { 100, 200, 400, 800, 1600, 3200 };
        private static readonly float[] AperturePresets = { 1.8f, 2.8f, 4.0f, 5.6f, 8.0f, 11.0f, 16.0f };
        private static readonly string[] ShutterPresets = { "1/30", "1/60", "1/125", "1/250", "1/500", "1/1000", "1/2000" };
        private static readonly float[] ShutterValues = { 1f/30f, 1f/60f, 1f/125f, 1f/250f, 1f/500f, 1f/1000f, 1f/2000f };
        private static readonly int[] KelvinPresets = { 4000, 5000, 6500, 7500 };

        private int isoIndex = 1;      // Default ISO 200
        private int apertureIndex = 2; // Default F4.0
        private int shutterIndex = 3;  // Default 1/250
        private int kelvinIndex = 2;   // Default 6500K
        private int evIndex = 2;       // Default EV 0.0 (-2, -1, 0, +1, +2)

        private WhiteBalanceMode wbMode = WhiteBalanceMode.Auto;

        public int CurrentISO => ISOPresets[isoIndex];
        public float CurrentAperture => AperturePresets[apertureIndex];
        public string CurrentShutterString => ShutterPresets[shutterIndex];
        public float CurrentShutterValue => ShutterValues[shutterIndex];
        public float CurrentEVCompensation => evIndex - 2f;
        public WhiteBalanceMode CurrentWBMode => wbMode;
        public int CurrentKelvin => KelvinPresets[kelvinIndex];

        // Cycles through exposure parameters
        public void CycleISO(int dir)
        {
            isoIndex = Mathf.Clamp(isoIndex + dir, 0, ISOPresets.Length - 1);
        }

        public void CycleAperture(int dir)
        {
            apertureIndex = Mathf.Clamp(apertureIndex + dir, 0, AperturePresets.Length - 1);
        }

        public void CycleShutter(int dir)
        {
            shutterIndex = Mathf.Clamp(shutterIndex + dir, 0, ShutterPresets.Length - 1);
        }

        public void CycleEV(int dir)
        {
            evIndex = Mathf.Clamp(evIndex + dir, 0, 4); // 5 elements: -2, -1, 0, +1, +2
        }

        public void CycleWB()
        {
            int maxModes = System.Enum.GetValues(typeof(WhiteBalanceMode)).Length;
            wbMode = (WhiteBalanceMode)(((int)wbMode + 1) % maxModes);
        }

        public void CycleKelvin(int dir)
        {
            kelvinIndex = Mathf.Clamp(kelvinIndex + dir, 0, KelvinPresets.Length - 1);
        }

        // Get ambient light intensity based on active game phase scene
        public float GetAmbientLightLevel()
        {
            string scene = SceneManager.GetActiveScene().name;
            if (scene.Contains("Phase5"))
            {
                return 8.5f; // Dim sunset
            }
            if (scene.Contains("Phase1"))
            {
                return 11.5f; // Daytime garden
            }
            if (scene.Contains("Phase4"))
            {
                return 11.0f; // Soft swamp shadow
            }
            // Phase 2, 3 (canal river)
            return 12.5f; // Bright water reflections
        }

        // Calculate exposure error: difference between camera settings EV and ambient EV + EV compensation
        public float GetExposureError()
        {
            float aperture = CurrentAperture;
            float shutter = CurrentShutterValue;
            float iso = CurrentISO;

            // EV formula: EV = log2(N^2 / t)
            float evSettings = Mathf.Log((aperture * aperture) / shutter, 2.0f);
            
            // Adjust for ISO (ISO 100 is baseline)
            float evISO = evSettings - Mathf.Log(iso / 100f, 2.0f);

            float ambient = GetAmbientLightLevel();
            float targetEV = ambient + CurrentEVCompensation;

            return evISO - targetEV; // 0 is perfectly balanced, positive is underexposed, negative is overexposed
        }

        // Auto adjusts ISO, Shutter, and Aperture to hit target EV
        public void ApplyAutoExposure()
        {
            float ambient = GetAmbientLightLevel();
            float targetEV = ambient + CurrentEVCompensation;

            // Aim for middle ground values: ISO 200, F4.0, Shutter matching targetEV
            // EV = log2(F^2 / t) - log2(ISO/100)
            // Let's set Aperture = F4.0 (index 2), ISO = 200 (index 1)
            apertureIndex = 2; // F4.0
            isoIndex = 1;      // ISO 200
            
            float F = AperturePresets[apertureIndex];
            float ISO = ISOPresets[isoIndex];

            // targetEV = log2(F^2 / t) - log2(ISO/100)
            // targetEV + log2(ISO/100) = log2(F^2 / t)
            // 2^(targetEV + log2(ISO/100)) = F^2 / t
            // t = F^2 / 2^(targetEV + log2(ISO/100))
            float denom = Mathf.Pow(2f, targetEV + Mathf.Log(ISO / 100f, 2f));
            float targetShutter = (F * F) / denom;

            // Find closest shutter preset
            float minDiff = float.MaxValue;
            int bestShutterIndex = 3;
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

            // If shutter is too slow (slow action warning), bump ISO to speed it up
            if (shutterIndex < 2 && isoIndex < ISOPresets.Length - 1)
            {
                isoIndex++; // Bump ISO to 400
                shutterIndex += 2; // Fasten shutter
                shutterIndex = Mathf.Clamp(shutterIndex, 0, ShutterValues.Length - 1);
            }
        }
    }
}
