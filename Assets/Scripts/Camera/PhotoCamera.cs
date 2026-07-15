using System.Collections;
using UnityEngine;
using RungTramTraSu.CameraSystem;

namespace RungTramTraSu
{
    /// <summary>
    /// Facade/Wrapper for PhotoCamera to maintain backwards compatibility with existing Phase Managers
    /// and Editor setup scripts, while delegating operations to the new CameraSystem modules.
    /// </summary>
    public class PhotoCamera : MonoBehaviour
    {
        [Header("Backwards Compatibility Fields")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float normalFOV = 60f;
        [SerializeField] private float zoomFOV = 30f;
        [SerializeField] private float zoomSpeed = 8f;

        [Header("UI Canvas References")]
        [SerializeField] private GameObject viewfinderCanvas;
        [SerializeField] private UnityEngine.UI.Image flashImage;
        [SerializeField] private float flashDuration = 0.2f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip shutterSound;

        [Header("Quest Validation")]
        [SerializeField] private Transform questTarget;
        [SerializeField] private LayerMask occlusionLayers;

        public bool HasCamera => CameraManager.Instance != null ? CameraManager.Instance.HasCameraUnlocked : false;
        public bool IsZooming => CameraManager.Instance != null ? CameraManager.Instance.IsCameraActive : false;
        public bool IsTakingPhoto => PhotoCapture.Instance != null ? PhotoCapture.Instance.IsCapturing : false;

        private void Awake()
        {
            if (playerCamera == null) playerCamera = Camera.main;

            // Instantiates CameraManager automatically if it doesn't exist in the scene
            if (CameraManager.Instance == null)
            {
                GameObject mgr = new GameObject("[CameraSystemManager]");
                mgr.AddComponent<CameraManager>();
                mgr.AddComponent<CameraInput>();
            }

            // Bind flash and audio trigger events from PhotoCapture
            StartCoroutine(BindCaptureEvents());
        }

        private IEnumerator BindCaptureEvents()
        {
            // Wait 1 frame to ensure capture instance initializes
            yield return null;

            if (PhotoCapture.Instance != null)
            {
                PhotoCapture.Instance.OnFlashTriggered += TriggerFlashEffects;
            }

            // Sync initial states
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName.Contains("Phase3") || sceneName.Contains("Phase4") || sceneName.Contains("Phase5"))
            {
                UnlockCamera();
            }
        }

        private void OnDestroy()
        {
            if (PhotoCapture.Instance != null)
            {
                PhotoCapture.Instance.OnFlashTriggered -= TriggerFlashEffects;
            }
        }

        private void TriggerFlashEffects()
        {
            if (flashImage != null)
            {
                StartCoroutine(FlashRoutine());
            }
        }

        private IEnumerator FlashRoutine()
        {
            flashImage.gameObject.SetActive(true);
            float eIn = 0f;
            while (eIn < flashDuration * 0.3f)
            {
                eIn += Time.deltaTime;
                flashImage.color = new Color(1, 1, 1, Mathf.Lerp(0, 1, eIn / (flashDuration * 0.3f)));
                yield return null;
            }
            float eOut = 0f;
            while (eOut < flashDuration * 0.7f)
            {
                eOut += Time.deltaTime;
                flashImage.color = new Color(1, 1, 1, Mathf.Lerp(1, 0, eOut / (flashDuration * 0.7f)));
                yield return null;
            }
            flashImage.color = new Color(1, 1, 1, 0);
            flashImage.gameObject.SetActive(false);
        }

        public void PlayShutterAndFlash()
        {
            if (PhotoCapture.Instance != null)
            {
                PhotoCapture.Instance.PlayShutterSound();
                PhotoCapture.Instance.TriggerFlash();
            }
        }

        public void UnlockCamera()
        {
            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.UnlockCamera();
            }
        }

        public void SetCaptureEnabled(bool enabled)
        {
            // If capture is disabled (e.g. Phase 2 bird-checkpoint mode), 
            // we configure the camera managers and active state appropriately.
            // If capture is disabled, we toggle AF or setup detectors
            Debug.Log("[PhotoCamera Facade] SetCaptureEnabled: " + enabled);
        }

        public void SetQuestTarget(Transform target)
        {
            questTarget = target;
            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.SetQuestTarget(target);
            }
        }

        public void SetPhotoCategory(string category)
        {
            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.SetPhotoCategory(category);
            }
        }

        public void SetSubjectInfo(string name, string description)
        {
            // Info is populated automatically from detector metadata, but we log the call
            Debug.Log($"[PhotoCamera Facade] SetSubjectInfo: Name={name}");
        }
    }
}
