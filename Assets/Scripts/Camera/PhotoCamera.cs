using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace RungTramTraSu
{
    public class PhotoCamera : MonoBehaviour
    {
        [Header("Camera Settings")]
        [SerializeField] private Camera playerCamera;
        [SerializeField] private float normalFOV = 60f;
        [SerializeField] private float zoomFOV = 30f;
        [SerializeField] private float zoomSpeed = 8f;

        [Header("UI Canvas References")]
        [SerializeField] private GameObject viewfinderCanvas; // UI Ống ngắm (vạch kẻ)
        [SerializeField] private Image flashImage;             // UI Đèn flash (nháy trắng)
        [SerializeField] private float flashDuration = 0.2f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip shutterSound;

        [Header("Quest Validation")]
        [SerializeField] private Transform questTarget;        // Đối tượng cần chụp (Cây xoài/Con mèo)
        [SerializeField] private LayerMask occlusionLayers;     // Các layer cản tầm nhìn

        private bool hasCamera = false;
        private bool isZooming = false;
        private bool isTakingPhoto = false;
        private string currentPhotoCategory = "General";
        private float targetFOV;

        // Khi false, PhotoCamera sẽ không chụp ảnh (dùng cho Phase2 bird-checkpoint mode)
        private bool captureEnabled = true;
        // Thông tin chủ thể được set bởi Phase Manager trước khi chụp
        private string currentSubjectName = "Phong cảnh";
        private string currentSubjectDescription = "Một khoảnh khắc đẹp của rừng tràm miền Tây.";

        public bool HasCamera => hasCamera;
        public bool IsZooming => isZooming;
        public bool IsTakingPhoto => isTakingPhoto;

        private void Awake()
        {
            if (playerCamera == null) playerCamera = Camera.main;
            targetFOV = normalFOV;
            
            // Auto find or add AudioSource
            if (audioSource == null)
            {
                audioSource = GetComponent<AudioSource>();
                if (audioSource == null)
                {
                    audioSource = gameObject.AddComponent<AudioSource>();
                }
            }

            // Synthesize shutter sound if null
            if (shutterSound == null)
            {
                shutterSound = CreateSyntheticShutterSound();
            }

            // Tự động mở khóa camera ở các Phase sau Phase 2 (Phase 3, 4, 5)
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName.Contains("Phase3") || sceneName.Contains("Phase4") || sceneName.Contains("Phase5"))
            {
                hasCamera = true;
            }
            else
            {
                hasCamera = false;
            }
            
            if (viewfinderCanvas != null) viewfinderCanvas.SetActive(false);
            if (flashImage != null)
            {
                flashImage.gameObject.SetActive(false);
                // Thiết lập màu trắng đục nhưng trong suốt
                flashImage.color = new Color(1, 1, 1, 0);
            }
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
                
                // Click 1 (Mirror up) - starts at t = 0
                float click1 = 0f;
                if (t < 0.04f)
                {
                    float decay1 = Mathf.Exp(-t * 150f); // Fast decay
                    float noise = Random.Range(-1f, 1f) * 0.4f;
                    float tone = Mathf.Sin(2f * Mathf.PI * 1200f * t) * 0.6f;
                    click1 = (noise + tone) * decay1;
                }

                // Click 2 (Mirror down) - starts at t = 0.08s
                float click2 = 0f;
                if (t >= 0.08f && t < 0.15f)
                {
                    float t2 = t - 0.08f;
                    float decay2 = Mathf.Exp(-t2 * 120f); // Decay
                    float noise = Random.Range(-1f, 1f) * 0.3f;
                    float tone = Mathf.Sin(2f * Mathf.PI * 800f * t2) * 0.7f;
                    click2 = (noise + tone) * decay2;
                }

                samples[i] = click1 + click2;
            }

            AudioClip clip = AudioClip.Create("SyntheticShutter", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void Update()
        {
            if (!hasCamera || isTakingPhoto) return;

            HandleZoom();
            UpdateDynamicCategory();
            HandleCapture();
        }

        private void HandleZoom()
        {
            // Kiểm tra giữ chuột phải
            if (Mouse.current.rightButton.isPressed)
            {
                isZooming = true;
                targetFOV = zoomFOV;
                if (viewfinderCanvas != null) viewfinderCanvas.SetActive(true);
            }
            else
            {
                isZooming = false;
                targetFOV = normalFOV;
                if (viewfinderCanvas != null) viewfinderCanvas.SetActive(false);
            }

            // Lerp FOV mượt mà
            if (playerCamera != null)
            {
                playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFOV, Time.deltaTime * zoomSpeed);
            }
        }

        private void HandleCapture()
        {
            // Không chụp nếu capture bị tắt (ví dụ: Phase2 bird mode)
            if (!captureEnabled) return;

            // Không chụp nếu player đang bị frozen (dialogue đang mở)
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null && player.IsFrozen) return;

            // Nhấn chuột trái để chụp ảnh khi đang ngắm (zoom)
            if (isZooming && Mouse.current.leftButton.wasPressedThisFrame)
            {
                StartCoroutine(TakePhotoRoutine());
            }
        }

        public void PlayShutterAndFlash()
        {
            if (audioSource != null && shutterSound != null)
            {
                audioSource.PlayOneShot(shutterSound);
            }
            if (flashImage != null)
            {
                StartCoroutine(FlashRoutine());
            }
        }

        private IEnumerator FlashRoutine()
        {
            flashImage.gameObject.SetActive(true);
            // Fade-in nhanh (trắng xóa)
            float eIn = 0f;
            while (eIn < flashDuration * 0.3f)
            {
                eIn += Time.deltaTime;
                flashImage.color = new Color(1, 1, 1, Mathf.Lerp(0, 1, eIn / (flashDuration * 0.3f)));
                yield return null;
            }
            // Fade-out chậm
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

        private IEnumerator TakePhotoRoutine()
        {
            isTakingPhoto = true;

            // ── Bước 1: Ẩn UI viewfinder và GameUI (HUD/Mục tiêu) trước khi chụp ────────
            if (viewfinderCanvas != null) viewfinderCanvas.SetActive(false);
            GameObject gameUI = GameObject.Find("GameUI");
            if (gameUI != null) gameUI.SetActive(false);

            // ── Bước 2: Chụp screenshot NGAY ĐẦU (trước khi flash bật) ──────────────
            // WaitForEndOfFrame đảm bảo frame hiện tại render xong mới ReadPixels
            yield return new WaitForEndOfFrame();

            int width = Screen.width;
            int height = Screen.height;
            Texture2D capturedTex = new Texture2D(width, height, TextureFormat.RGB24, false);
            capturedTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            capturedTex.Apply();

            // Khôi phục lại GameUI và viewfinder
            if (gameUI != null) gameUI.SetActive(true);
            if (isZooming && viewfinderCanvas != null) viewfinderCanvas.SetActive(true);

            // ── Bước 3: Phát âm thanh shutter + flash (sau khi đã chụp) ─────────────
            PlayShutterAndFlash();

            // ── Bước 4: Lưu ảnh ──────────────────────────────────────────────────────
            if (PersistentGameManager.Instance != null)
            {
                PersistentGameManager.Instance.SavePhoto(currentPhotoCategory, capturedTex);
            }
            if (Phase1Manager.Instance != null)
            {
                Phase1Manager.Instance.SavePhoto(capturedTex);
            }

            // ── Bước 5: Kiểm tra mục tiêu ────────────────────────────────────────────
            bool photoSuccess = ValidatePhotoContent();

            string displayName = photoSuccess ? currentSubjectName : "Không rõ chủ thể";
            string displayDesc = photoSuccess
                ? currentSubjectDescription
                : "Mục tiêu không nằm trong khung ngắm trung tâm. Thử lại nhé!";

            isTakingPhoto = false;

            // ── Bước 6: Hiển thị PhotoResultUI ───────────────────────────────────────
            PhotoResultUI.Instance.ShowResult(capturedTex, displayName, displayDesc);
        }

        private bool ValidatePhotoContent()
        {
            Transform resolvedTarget = questTarget;

            // Phase 4 does not always pre-assign a quest target in time for the same click.
            // Fall back to the animal actually visible in the frame so the shot is judged
            // by what the player photographed instead of a stale target reference.
            if (resolvedTarget == null && IsPhase4Scene())
            {
                resolvedTarget = ResolveVisiblePhase4AnimalTarget();
            }

            if (resolvedTarget == null) return false;

            // Determine the actual visual center of the target (using its Collider bounds center if available)
            Vector3 targetPosition = resolvedTarget.position;
            Collider targetCollider = resolvedTarget.GetComponent<Collider>();
            if (targetCollider == null)
            {
                targetCollider = resolvedTarget.GetComponentInChildren<Collider>();
            }
            if (targetCollider != null)
            {
                targetPosition = targetCollider.bounds.center;
            }

            // Chuyển vị trí mục tiêu từ tọa độ World sang Viewport của Camera
            Vector3 viewportPoint = playerCamera.WorldToViewportPoint(targetPosition);

            // Kiểm tra xem mục tiêu:
            // - Có nằm ở phía trước camera hay không (z > 0)
            // - Có nằm trong phạm vi hiển thị màn hình hay không (x, y từ 0.0 đến 1.0)
            // - Để ảnh đẹp, yêu cầu mục tiêu nằm ở vùng trung tâm (x, y từ 0.2 đến 0.8)
            bool isVisible = viewportPoint.z > 0 && 
                             viewportPoint.x >= 0.2f && viewportPoint.x <= 0.8f && 
                             viewportPoint.y >= 0.2f && viewportPoint.y <= 0.8f;

            if (isVisible)
            {
                // Kiểm tra xem mục tiêu có bị vật cản (như tường, nhà) che mất không (Bỏ qua đối với SunsetQuestTarget)
                if (resolvedTarget.name != "SunsetQuestTarget")
                {
                    RaycastHit hit;
                    Vector3 directionToTarget = targetPosition - playerCamera.transform.position;
                    if (Physics.Raycast(playerCamera.transform.position, directionToTarget, out hit, directionToTarget.magnitude + 1f, occlusionLayers))
                    {
                        // Nếu va chạm trúng vật khác trước mục tiêu
                        if (hit.transform != resolvedTarget && !hit.transform.IsChildOf(resolvedTarget))
                        {
                            Debug.Log("Mục tiêu bị che mất bởi: " + hit.collider.name);
                            return false; // Bị che khuất
                        }
                    }
                }

                // Chụp ảnh thành công! Báo về Phase1Manager
                Debug.Log("Chụp ảnh mục tiêu thành công!");
                if (Phase1Manager.Instance != null) Phase1Manager.Instance.OnPhotoQuestCompleted();
                if (Phase2Manager.Instance != null) Phase2Manager.Instance.OnPhotoQuestCompleted();
                if (Phase3Manager.Instance != null) Phase3Manager.Instance.OnPhotoQuestCompleted();
                if (Phase4Manager.Instance != null) Phase4Manager.Instance.OnPhotoQuestCompleted();
                if (Phase5Manager.Instance != null) Phase5Manager.Instance.OnPhotoQuestCompleted();
                return true;
            }
            else
            {
                Debug.Log("Mục tiêu nằm ngoài tầm ngắm trung tâm. Tọa độ Viewport: " + viewportPoint);
                return false;
            }
        }

        /// <summary>
        /// Kích hoạt máy ảnh khi nhận được từ Ông Ngoại
        /// </summary>
        public void UnlockCamera()
        {
            hasCamera = true;
        }

        /// <summary>
        /// Bật/tắt cơ chế chụp ảnh tự động của PhotoCamera.
        /// Phase2Manager gọi SetCaptureEnabled(false) khi đang ở bird-checkpoint mode
        /// để tránh double-capture.
        /// </summary>
        public void SetCaptureEnabled(bool enabled)
        {
            captureEnabled = enabled;
        }

        /// <summary>
        /// Gán mục tiêu cần chụp cho nhiệm vụ hiện tại
        /// </summary>
        public void SetQuestTarget(Transform target)
        {
            questTarget = target;
        }

        public void SetPhotoCategory(string category)
        {
            currentPhotoCategory = category;
        }

        /// <summary>
        /// Thiết lập thông tin chủ thể được chụp.
        /// Gọi bởi Phase Manager trước khi unlock camera.
        /// </summary>
        public void SetSubjectInfo(string name, string description)
        {
            currentSubjectName = name;
            currentSubjectDescription = description;
        }

        private bool IsPhase4Scene()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            return sceneName.Contains("Phase4");
        }

        private Transform ResolveVisiblePhase4AnimalTarget()
        {
            AnimalAI[] animals = FindObjectsByType<AnimalAI>(FindObjectsSortMode.None);
            Transform bestTarget = null;
            float bestScore = float.MaxValue;

            foreach (AnimalAI animal in animals)
            {
                if (animal == null || animal.HasFled) continue;

                Vector3 targetPosition = animal.transform.position;
                Collider collider = animal.GetComponent<Collider>();
                if (collider == null)
                {
                    collider = animal.GetComponentInChildren<Collider>();
                }

                if (collider != null)
                {
                    targetPosition = collider.bounds.center;
                }

                Vector3 viewportPoint = playerCamera.WorldToViewportPoint(targetPosition);
                bool inFrame = viewportPoint.z > 0 &&
                               viewportPoint.x >= 0.15f && viewportPoint.x <= 0.85f &&
                               viewportPoint.y >= 0.15f && viewportPoint.y <= 0.85f;

                if (!inFrame)
                {
                    continue;
                }

                float centerScore = Mathf.Abs(viewportPoint.x - 0.5f) + Mathf.Abs(viewportPoint.y - 0.5f);
                if (centerScore < bestScore)
                {
                    bestScore = centerScore;
                    bestTarget = animal.transform;
                }
            }

            return bestTarget;
        }

        private void UpdateDynamicCategory()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (sceneName.Contains("Phase5"))
            {
                currentPhotoCategory = "Phase5_Sunset";
            }
            else if (sceneName.Contains("Phase1"))
            {
                currentPhotoCategory = "Phase1_Mango";
            }
        }
    }
}
