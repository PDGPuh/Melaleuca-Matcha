using UnityEngine;

namespace RungTramTraSu.CameraSystem
{
    public class CameraController : MonoBehaviour
    {
        [Header("Breathing Sway Settings")]
        [SerializeField] private float normalSwayFreq = 1.5f;
        [SerializeField] private float normalSwayAmp = 0.05f;
        [SerializeField] private float zoomSwayMultiplier = 0.25f; // Zoom dampens sway amplitude
        [SerializeField] private float stabilizeSwayMult = 0.15f;  // Shift reduces sway

        private float timeCount = 0f;
        private bool isStabilized = false;
        private bool isTripodMode = false;
        private Vector3 startLocalPosition;
        private Quaternion startLocalRotation;

        public bool IsTripodMode => isTripodMode;

        private void Start()
        {
            startLocalPosition = transform.localPosition;
            startLocalRotation = transform.localRotation;
        }

        public void SetStabilized(bool stabilized)
        {
            isStabilized = stabilized;
        }

        public void ToggleTripodMode()
        {
            isTripodMode = !isTripodMode;
            
            // Toggle player movement block if tripod is active
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                player.SetMovementLocked(isTripodMode);
            }
            
            if (isTripodMode)
            {
                // Reset local pos and rot immediately
                transform.localPosition = startLocalPosition;
                transform.localRotation = startLocalRotation;
            }
        }

        public void ResetController()
        {
            isTripodMode = false;
            isStabilized = false;
            transform.localPosition = startLocalPosition;
            transform.localRotation = startLocalRotation;
            
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                player.SetMovementLocked(false);
            }
        }

        private void Update()
        {
            if (isTripodMode) return;

            // Update timer
            float speed = isStabilized ? normalSwayFreq * 0.5f : normalSwayFreq;
            timeCount += Time.deltaTime * speed;

            // Determine amplitude based on zooming and stabilization
            float amp = normalSwayAmp;
            
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                // If zooming (FOV is narrow), reduce sway angle or scale it appropriately
                float zoomRatio = mainCam.fieldOfView / 60f;
                amp *= Mathf.Lerp(zoomSwayMultiplier, 1f, zoomRatio);
            }

            if (isStabilized)
            {
                amp *= stabilizeSwayMult;
            }

            // Procedural Lissajous sway for rotation and translation
            float swayX = Mathf.Sin(timeCount) * amp * 0.1f;
            float swayY = Mathf.Cos(timeCount * 2f) * amp * 0.15f;
            float rotSwayX = Mathf.Sin(timeCount * 1.3f) * amp * 8f;
            float rotSwayY = Mathf.Cos(timeCount * 0.8f) * amp * 8f;

            transform.localPosition = startLocalPosition + new Vector3(swayX, swayY, 0f);
            transform.localRotation = startLocalRotation * Quaternion.Euler(rotSwayX, rotSwayY, 0f);
        }
    }
}
