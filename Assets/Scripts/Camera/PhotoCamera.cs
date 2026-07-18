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

            // Remove legacy "RECText" UI element if present in existing scenes
            GameObject recText = GameObject.Find("RECText");
            if (recText != null)
            {
                Destroy(recText);
            }

            // Runtime swap: thay CameraHandModel Cube cũ bằng model máy ảnh phim cổ thực tế
            SwapCameraHandModel();

            // Sync initial states
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName.Contains("Phase3") || sceneName.Contains("Phase4") || sceneName.Contains("Phase5"))
            {
                UnlockCamera();
            }
        }

        private bool _cameraModelSwapped = false;
        // Rotation đúng cho vintage camera GLB trong first-person view:
        //   -90 trên X: đứng thẳng (Blender Z-up → Unity Y-up)
        //    90 trên Y: ống kính chỉ về phía trước cùng hướng player nhìn
        private static readonly Quaternion CameraModelRotation = Quaternion.Euler(-90f, 90f, 0f);

        /// <summary>
        /// Gắn model máy ảnh cổ thay thế Cube placeholder, hoặc fix rotation nếu GLB đã tồn tại.
        /// KHÔNG Destroy CameraHandModel — giữ reference cho Phase1Manager.
        /// </summary>
        private void SwapCameraHandModel()
        {
            if (_cameraModelSwapped) return;

            Camera mainCam = Camera.main;
            if (mainCam == null) return;

            // Tìm CameraHandModel trong children của Main Camera (kể cả inactive)
            Transform found = mainCam.transform.Find("CameraHandModel");
            if (found == null)
            {
                GameObject go = GameObject.Find("CameraHandModel");
                if (go != null) found = go.transform;
            }
            if (found == null) return;

            // --- CASE 1: Đã có child "VintageCameraModel" → chỉ cần fix rotation ---
            Transform existingVintage = found.Find("VintageCameraModel");
            if (existingVintage != null)
            {
                existingVintage.localRotation = CameraModelRotation;
                _cameraModelSwapped = true;
                return;
            }

            // --- CASE 2: CameraHandModel là GLB model từ setup (có children, không phải Cube) ---
            // Nhận dạng: childCount > 0 nhưng không có Cube MeshFilter
            bool isCubePlaceholder = found.GetComponent<MeshFilter>() != null;
            if (!isCubePlaceholder && found.childCount > 0)
            {
                // Đây là GLB model từ CreateCameraHandModel() — chỉ fix rotation của chính nó
                found.localRotation = CameraModelRotation;
                _cameraModelSwapped = true;
                Debug.Log("[PhotoCamera] Fixed rotation của GLB model từ setup.");
                return;
            }

            // --- CASE 3: Cube placeholder cũ — ẩn mesh + thêm GLB làm child ---
            GameObject cameraPrefab = Resources.Load<GameObject>("Models/VintageCamera");
            if (cameraPrefab == null)
            {
                Debug.LogWarning("[PhotoCamera] Không load được Models/VintageCamera từ Resources.");
                return;
            }

            // Ẩn Cube gốc (giữ nguyên GameObject để Phase1Manager reference vẫn hợp lệ)
            var mr = found.GetComponent<MeshRenderer>();
            if (mr != null) mr.enabled = false;
            var bc = found.GetComponent<BoxCollider>();
            if (bc != null) bc.enabled = false;

            // Thêm GLB làm child — kế thừa scale + position từ parent Cube
            GameObject vintageModel = Instantiate(cameraPrefab, found);
            vintageModel.name = "VintageCameraModel";
            vintageModel.transform.localPosition = Vector3.zero;
            vintageModel.transform.localRotation = CameraModelRotation;
            vintageModel.transform.localScale = Vector3.one;

            foreach (var col in vintageModel.GetComponentsInChildren<Collider>())
                Destroy(col);

            _cameraModelSwapped = true;
            Debug.Log("[PhotoCamera] Đã gắn VintageCameraModel vào CameraHandModel Cube.");
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
            // Phase1Manager sẽ tự gọi cameraHandModel.SetActive(true) sau đó
            // SwapCameraHandModel đã chạy lúc scene start và ẩn MeshRenderer của Cube
            // → model VintageCameraModel (child) sẽ hiển thị cùng với parent
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
