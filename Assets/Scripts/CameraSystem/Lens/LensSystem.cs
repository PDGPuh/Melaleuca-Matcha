using UnityEngine;

namespace RungTramTraSu.CameraSystem
{
    public class LensSystem : MonoBehaviour, ILensSystem
    {
        [System.Serializable]
        public struct LensPreset
        {
            public float focalLength; // in mm
            public float fieldOfView;  // mapped Unity FOV
            public float maxAperture;  // max aperture (e.g. f/1.4)
            public float distortion;    // barrel (-1 to 0) or pincushion (0 to 1)
            public string description;
        }

        [Header("Lens Configurations")]
        [SerializeField] private LensPreset[] lensPresets = new LensPreset[]
        {
            new LensPreset { focalLength = 18f, fieldOfView = 75f, maxAperture = 2.8f, distortion = -0.15f, description = "Ultra Wide - Landscape" },
            new LensPreset { focalLength = 24f, fieldOfView = 61f, maxAperture = 2.8f, distortion = -0.10f, description = "Wide Angle - Architecture" },
            new LensPreset { focalLength = 35f, fieldOfView = 46f, maxAperture = 1.8f, distortion = -0.05f, description = "Wide - Street Photography" },
            new LensPreset { focalLength = 50f, fieldOfView = 34f, maxAperture = 1.4f, distortion = 0.00f, description = "Standard Prime - Human Eye" },
            new LensPreset { focalLength = 85f, fieldOfView = 20f, maxAperture = 1.8f, distortion = 0.02f, description = "Portrait - Subject Isolation" },
            new LensPreset { focalLength = 135f, fieldOfView = 12f, maxAperture = 2.0f, distortion = 0.04f, description = "Medium Telephoto - Wildlife" },
            new LensPreset { focalLength = 200f, fieldOfView = 8f, maxAperture = 2.8f, distortion = 0.06f, description = "Telephoto - Distance Wildlife" },
            new LensPreset { focalLength = 400f, fieldOfView = 4f, maxAperture = 4.0f, distortion = 0.10f, description = "Super Telephoto - Extreme Distance" }
        };

        [SerializeField] private int defaultLensIndex = 3; // 50mm default
        [SerializeField] private float zoomSmoothSpeed = 10f;
        [SerializeField] private float breathingIntensity = 0.05f; // Intensity factor for lens breathing

        private int currentLensIndex;
        private float targetFOV;
        private Camera targetCamera;
        private float currentBreathingOffset = 0f;

        // Interface implementation
        public float CurrentFocalLength => lensPresets[currentLensIndex].focalLength;
        public float CurrentFieldOfView => targetCamera != null ? targetCamera.fieldOfView : targetFOV;
        public float LensBreathingOffset => currentBreathingOffset;
        public float LensDistortionIntensity => lensPresets[currentLensIndex].distortion;

        public string CurrentLensDescription => lensPresets[currentLensIndex].description;
        public float MaxApertureForCurrentLens => lensPresets[currentLensIndex].maxAperture;
        public int CurrentLensIndex => currentLensIndex;
        public int MaxPresets => lensPresets.Length;

        private void Start()
        {
            targetCamera = Camera.main;
            currentLensIndex = defaultLensIndex;
            if (currentLensIndex >= lensPresets.Length) currentLensIndex = 0;
            targetFOV = lensPresets[currentLensIndex].fieldOfView;
            
            if (targetCamera != null)
            {
                targetCamera.fieldOfView = targetFOV;
            }
        }

        public void SetTargetCamera(Camera cam)
        {
            targetCamera = cam;
        }

        public void CycleLens(int direction)
        {
            currentLensIndex += direction;
            if (currentLensIndex >= lensPresets.Length) currentLensIndex = 0;
            else if (currentLensIndex < 0) currentLensIndex = lensPresets.Length - 1;

            targetFOV = lensPresets[currentLensIndex].fieldOfView;
        }

        public void SetFocalLengthPreset(int index)
        {
            if (index >= 0 && index < lensPresets.Length)
            {
                currentLensIndex = index;
                targetFOV = lensPresets[currentLensIndex].fieldOfView;
            }
        }

        private void Update()
        {
            if (targetCamera == null) targetCamera = Camera.main;

            if (targetCamera != null)
            {
                float adjustedFOV = 60f; // Default normal player FOV
                
                if (CameraManager.Instance != null && CameraManager.Instance.IsCameraActive)
                {
                    // Calculate lens breathing: breathing is proportional to closer focus distances.
                    // When focus is very close, the image zooms in slightly (breathing offset subtracted or added).
                    float focusDist = 10f;
                    if (CameraManager.Instance.FocusSys != null)
                    {
                        focusDist = CameraManager.Instance.FocusSys.FocusDistance;
                    }
                    
                    // Focus distance ranges from 0.5m (max breathing) to infinity (0 breathing)
                    currentBreathingOffset = (breathingIntensity / Mathf.Max(0.5f, focusDist)) * targetFOV;
                    adjustedFOV = targetFOV + currentBreathingOffset;
                }
                else
                {
                    currentBreathingOffset = 0f;
                }

                targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, adjustedFOV, Time.deltaTime * zoomSmoothSpeed);
            }
        }

        public float GetMagnification()
        {
            return CurrentFocalLength / 50f;
        }
    }
}
