using UnityEngine;

namespace RungTramTraSu.CameraSystem
{
    public class LensSystem : MonoBehaviour
    {
        [System.Serializable]
        public struct LensPreset
        {
            public float focalLength; // in mm
            public float fieldOfView;  // mapped Unity FOV
            public string description;
        }

        [Header("Lens Configurations")]
        [SerializeField] private LensPreset[] lensPresets = new LensPreset[]
        {
            new LensPreset { focalLength = 18f, fieldOfView = 75f, description = "Super Wide - Landscapes" },
            new LensPreset { focalLength = 35f, fieldOfView = 55f, description = "Wide Angle - Street/Environment" },
            new LensPreset { focalLength = 50f, fieldOfView = 40f, description = "Standard - Human perspective" },
            new LensPreset { focalLength = 85f, fieldOfView = 28f, description = "Medium Tele - Close portraits" },
            new LensPreset { focalLength = 135f, fieldOfView = 18f, description = "Telephoto - Short range wildlife" },
            new LensPreset { focalLength = 200f, fieldOfView = 12f, description = "Telephoto - Birding/Medium wildlife" },
            new LensPreset { focalLength = 400f, fieldOfView = 6f, description = "Super Telephoto - Extreme wildlife" }
        };

        [SerializeField] private int defaultLensIndex = 2; // 50mm default
        [SerializeField] private float smoothSpeed = 10f;
        [SerializeField] private float defaultPlayerFOV = 60f; // FOV khi không dùng máy ảnh

        private int currentLensIndex;
        private float targetFOV;
        private Camera targetCamera;
        private bool isActiveForCamera = false; // Chỉ apply FOV khi đang dùng camera

        public float CurrentFocalLength => lensPresets[currentLensIndex].focalLength;
        public string CurrentLensDescription => lensPresets[currentLensIndex].description;
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

        // Cycle through presets (direction: +1 or -1)
        public void CycleLens(int direction)
        {
            currentLensIndex += direction;
            if (currentLensIndex >= lensPresets.Length) currentLensIndex = 0;
            else if (currentLensIndex < 0) currentLensIndex = lensPresets.Length - 1;

            targetFOV = lensPresets[currentLensIndex].fieldOfView;
        }

        public void SetLensIndex(int index)
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
            if (targetCamera == null) return;

            // Chỉ apply lens FOV khi đang ở chế độ camera
            if (isActiveForCamera)
            {
                targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, targetFOV, Time.deltaTime * smoothSpeed);
            }
        }

        /// <summary>Bật lens zoom — gọi khi mở camera viewfinder</summary>
        public void SetCameraViewActive(bool active)
        {
            isActiveForCamera = active;
            if (!active && targetCamera != null)
            {
                // Reset ngay về FOV bình thường khi thoát camera
                targetCamera.fieldOfView = defaultPlayerFOV;
            }
        }

        // Magnification multiplier (e.g. 50mm = 1x, 400mm = 8x zoom)
        public float GetMagnification()
        {
            return CurrentFocalLength / 50f;
        }
    }
}
