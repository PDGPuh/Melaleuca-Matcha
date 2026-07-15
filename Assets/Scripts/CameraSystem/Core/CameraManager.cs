using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

namespace RungTramTraSu.CameraSystem
{
    public class CameraManager : MonoBehaviour
    {
        public static CameraManager Instance { get; private set; }

        [Header("Components Mapping")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private LensSystem lensSys;
        [SerializeField] private FocusSystem focusSys;
        [SerializeField] private ExposureSystem expSys;
        [SerializeField] private PhotoCapture photoCap;
        [SerializeField] private CameraController controller;

        private bool isCameraActive = false;
        private bool isManualMode = false;
        private bool hasCameraUnlocked = false;

        public bool IsCameraActive => isCameraActive;
        public bool IsManualMode => isManualMode;
        public bool HasCameraUnlocked => hasCameraUnlocked;

        public LensSystem LensSys => lensSys;
        public FocusSystem FocusSys => focusSys;
        public ExposureSystem ExpSys => expSys;
        public PhotoCapture PhotoCap => photoCap;
        public CameraController Controller => controller;

        // Current active quest validation details (from external Phase Managers)
        private string activeQuestCategory = "General";
        private Transform activeQuestTarget;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeSubsystems();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitializeSubsystems()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            
            // Generate systems programmatically if missing
            lensSys = GetComponentInChildren<LensSystem>();
            if (lensSys == null) lensSys = gameObject.AddComponent<LensSystem>();

            focusSys = GetComponentInChildren<FocusSystem>();
            if (focusSys == null) focusSys = gameObject.AddComponent<FocusSystem>();

            expSys = GetComponentInChildren<ExposureSystem>();
            if (expSys == null) expSys = gameObject.AddComponent<ExposureSystem>();

            photoCap = GetComponentInChildren<PhotoCapture>();
            if (photoCap == null) photoCap = gameObject.AddComponent<PhotoCapture>();

            controller = GetComponentInChildren<CameraController>();
            if (controller == null) controller = gameObject.AddComponent<CameraController>();

            // Ensure WildlifeDetector exists
            if (FindAnyObjectByType<WildlifeDetector>() == null)
            {
                GameObject det = new GameObject("WildlifeDetector");
                det.AddComponent<WildlifeDetector>();
                det.transform.SetParent(transform);
            }

            // Ensure CameraUI exists programmatically
            if (FindAnyObjectByType<CameraUI>() == null)
            {
                GameObject uiGo = new GameObject("CameraUI");
                uiGo.AddComponent<CameraUI>();
                uiGo.transform.SetParent(transform);
            }

            // Sync unlocked status based on scene index or name on reload
            string name = SceneManager.GetActiveScene().name;
            if (name.Contains("Phase3") || name.Contains("Phase4") || name.Contains("Phase5"))
            {
                hasCameraUnlocked = true;
            }
        }

        public void UnlockCamera()
        {
            hasCameraUnlocked = true;
        }

        public void SetQuestTarget(Transform target)
        {
            activeQuestTarget = target;
            if (WildlifeDetector.Instance != null)
            {
                WildlifeDetector.Instance.RegisterQuestTarget(target);
            }
        }

        public void SetPhotoCategory(string category)
        {
            activeQuestCategory = category;
        }

        public void ToggleCameraMode()
        {
            if (!hasCameraUnlocked) return;

            isCameraActive = !isCameraActive;
            
            // Adjust player controller movement constraints
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                player.SetMovementLocked(isCameraActive);
            }

            // Activate/Deactivate viewfinder
            if (CameraUI.Instance != null)
            {
                CameraUI.Instance.SetViewfinderActive(isCameraActive);
            }

            if (!isCameraActive && controller != null)
            {
                controller.ResetController();
            }
        }

        private void Start()
        {
            // Auto start tutorial on Phase1 garden
            if (SceneManager.GetActiveScene().name.Contains("Phase1") && TutorialManager.Instance != null)
            {
                TutorialManager.Instance.StartTutorial();
            }
        }

        private void Update()
        {
            if (mainCamera == null) mainCamera = Camera.main;

            // Monitor toggling manual booklet guide using New Input System Keyboard check
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame && CameraGuide.Instance != null)
            {
                if (CameraGuide.Instance.gameObject.activeSelf) CameraGuide.Instance.CloseGuide();
                else CameraGuide.Instance.OpenGuide();
            }

            // Check camera toggle key F
            if (CameraInput.Instance != null && CameraInput.Instance.ToggleCameraPressed)
            {
                ToggleCameraMode();
            }

            if (!isCameraActive) return;

            HandleInputUpdates();
        }

        private void HandleInputUpdates()
        {
            CameraInput input = CameraInput.Instance;
            if (input == null) return;

            // 1. Zoom Adjustment
            float scroll = input.ScrollDelta;
            if (scroll != 0f && !input.StabilizeHeld) // Scroll adjusts zoom unless Shift is held
            {
                int dir = scroll > 0f ? 1 : -1;
                lensSys.CycleLens(dir);
            }

            // 2. Focus Adjustments
            if (input.FocusManualDirection != 0f)
            {
                focusSys.AdjustManualFocus(input.FocusManualDirection);
            }
            else if (scroll != 0f && input.StabilizeHeld) // Scroll + Shift adjusts focus distance
            {
                float dir = scroll > 0f ? 0.08f : -0.08f;
                focusSys.AdjustManualFocus(dir * Time.deltaTime * 60f);
            }

            if (input.FocusLockPressed)
            {
                focusSys.TryLockTarget();
            }

            // 3. Stabilization (holding breath)
            if (controller != null)
            {
                controller.SetStabilized(input.StabilizeHeld);
            }

            // 4. Tripod Mode
            if (input.ToggleTripodPressed && controller != null)
            {
                controller.ToggleTripodMode();
            }

            // 5. Mode Selection (Auto vs Manual)
            if (input.ToggleModePressed)
            {
                isManualMode = !isManualMode;
                focusSys.SetAutoFocus(!isManualMode);
                Debug.Log("[CameraManager] Camera Mode toggled. Manual: " + isManualMode);
            }

            // 6. Manual parameter tuning (ISO, Shutter, Aperture, EV, White Balance)
            if (isManualMode)
            {
                if (input.CycleISOPressed) expSys.CycleISO(1);
                if (input.CycleShutterPressed) expSys.CycleShutter(1);
                if (input.CycleAperturePressed) expSys.CycleAperture(1);
                if (input.CycleExposureCompensationPressed) expSys.CycleEV(1);
                if (input.CycleWBPressed) expSys.CycleWB();
            }
            else
            {
                // Auto exposure handles setting exposure triangle matching ambient light
                expSys.ApplyAutoExposure();
            }

            // 7. Shutter capture trigger
            if (input.ShutterPressed && !photoCap.IsCapturing)
            {
                // Continuous burst loop or single frame capture
                if (isManualMode)
                {
                    // Hold left click in manual mode triggers Burst Mode
                    StartCoroutine(CheckBurstTrigger());
                }
                else
                {
                    // Single shot
                    photoCap.CaptureSingleShot(ProcessCapturedPhoto);
                }
            }
        }

        private System.Collections.IEnumerator CheckBurstTrigger()
        {
            float holdTime = 0f;
            bool burstStarted = false;

            while (CameraInput.Instance.ShutterHeld)
            {
                holdTime += Time.deltaTime;
                if (holdTime >= 0.28f && !burstStarted)
                {
                    burstStarted = true;
                    photoCap.StartBurstCapture(ProcessBurstPhotos);
                    break;
                }
                yield return null;
            }

            if (!burstStarted)
            {
                photoCap.CaptureSingleShot(ProcessCapturedPhoto);
            }
        }

        private void ProcessCapturedPhoto(Texture2D photo)
        {
            if (WildlifeDetector.Instance == null || PhotoScoring.Instance == null || PhotoValidator.Instance == null) return;

            // Scan for targets inside viewfinder bounds
            var targets = WildlifeDetector.Instance.ScanForVisibleTargets();
            WildlifeDetector.DetectedTarget bestTarget = new WildlifeDetector.DetectedTarget();
            bool targetFound = false;

            if (targets.Count > 0)
            {
                // Select the target nearest to center focus point
                float bestOffset = float.MaxValue;
                foreach (var t in targets)
                {
                    if (t.isOccluded) continue;
                    float offset = Mathf.Abs(t.viewportPos.x - 0.5f) + Mathf.Abs(t.viewportPos.y - 0.5f);
                    if (offset < bestOffset)
                    {
                        bestOffset = offset;
                        bestTarget = t;
                        targetFound = true;
                    }
                }
            }

            // Generate score metrics
            float blur = targetFound ? focusSys.GetBlurFactor(bestTarget.distance, expSys.CurrentAperture) : 0f;
            float expErr = expSys.GetExposureError();
            
            PhotoScoring.ScoreResult score = PhotoScoring.Instance.CalculateScore(
                targetFound ? bestTarget : new WildlifeDetector.DetectedTarget { displayName = "Phong Cảnh Rừng Tràm", screenCoverage = 10f, viewportPos = new Vector3(0.5f, 0.5f, 1f) },
                focusSys.CurrentFocusDistance,
                blur,
                expErr,
                expSys.CurrentShutterValue,
                expSys.CurrentAperture,
                expSys.CurrentISO,
                isManualMode
            );

            // Check validation for quests
            string failReason = "";
            bool isValidated = false;
            
            if (targetFound)
            {
                isValidated = PhotoValidator.Instance.ValidatePhoto(bestTarget, blur, activeQuestCategory, activeQuestTarget, out failReason);
            }

            // Show Polaroid results UI
            string subject = targetFound ? bestTarget.displayName : "Phong Cảnh Rừng Tràm";
            string desc = targetFound ? GetDescBySubject(bestTarget.displayName) : "Một khoảnh khắc yên ả, hữu tình sâu trong lòng Rừng Tràm Trà Sư.";
            bool rare = targetFound && bestTarget.isRare;

            if (targetFound && isValidated)
            {
                // Save to album and persistence
                if (AlbumManager.Instance != null)
                {
                    AlbumManager.Instance.AddPhotoToAlbum(
                        bestTarget.displayName,
                        bestTarget.displayName,
                        bestTarget.scientificName,
                        bestTarget.category,
                        bestTarget.conservationStatus,
                        photo,
                        score.totalScore,
                        score.starRating
                    );
                }

                // Increment photo counter for achievements
                if (AchievementManager.Instance != null)
                {
                    AchievementManager.Instance.IncrementPhotoCount();
                }

                // Call Phase completion hooks
                TriggerPhaseValidationCallbacks();
            }

            if (PhotoResultUI.Instance != null)
            {
                PhotoResultUI.Instance.ShowResult(photo, subject, isValidated ? desc : failReason, rare);
            }
        }

        private void ProcessBurstPhotos(List<Texture2D> list)
        {
            if (list == null || list.Count == 0) return;

            // Automatically select the single highest scoring photo from the burst set
            Texture2D bestPhoto = list[0];
            float highestScore = -1f;

            foreach (var tex in list)
            {
                // Score locally
                float score = EvaluatePhotoScoreLocally(tex);
                if (score > highestScore)
                {
                    highestScore = score;
                    bestPhoto = tex;
                }
            }

            // Clean up unused textures to avoid memory leaks
            foreach (var tex in list)
            {
                if (tex != bestPhoto)
                {
                    Destroy(tex);
                }
            }

            // Process the best photo as normal capture
            ProcessCapturedPhoto(bestPhoto);
        }

        private float EvaluatePhotoScoreLocally(Texture2D tex)
        {
            var targets = WildlifeDetector.Instance.ScanForVisibleTargets();
            if (targets.Count == 0) return 10f;
            
            float blur = focusSys.GetBlurFactor(targets[0].distance, expSys.CurrentAperture);
            float expErr = expSys.GetExposureError();
            
            var res = PhotoScoring.Instance.CalculateScore(
                targets[0],
                focusSys.CurrentFocusDistance,
                blur,
                expErr,
                expSys.CurrentShutterValue,
                expSys.CurrentAperture,
                expSys.CurrentISO,
                isManualMode
            );
            return res.totalScore;
        }

        private void TriggerPhaseValidationCallbacks()
        {
            if (Phase1Manager.Instance != null) Phase1Manager.Instance.OnPhotoQuestCompleted();
            if (Phase2Manager.Instance != null) Phase2Manager.Instance.OnPhotoQuestCompleted();
            if (Phase3Manager.Instance != null) Phase3Manager.Instance.OnPhotoQuestCompleted();
            if (Phase4Manager.Instance != null) Phase4Manager.Instance.OnPhotoQuestCompleted();
            if (Phase5Manager.Instance != null) Phase5Manager.Instance.OnPhotoQuestCompleted();
        }

        private string GetDescBySubject(string name)
        {
            if (name.Contains("Mango") || name.Contains("Xoài"))
            {
                return "Cây xoài cổ thụ trồng trong vườn nhà Ông Ngoại có tuổi thọ hàng chục năm tuổi.";
            }
            if (name.Contains("Sunset") || name.Contains("Hoàng hôn"))
            {
                return "Cảnh hoàng hôn chiều tà rực đỏ buông xuống bao trùm toàn cảnh thảm thực vật Rừng Tràm Trà Sư.";
            }
            if (name.Contains("Cò Trắng") || name.Contains("Cò trắng"))
            {
                return "Loài cò lông trắng muốt. Đi rón rén để không làm chúng giật mình bay đi.";
            }
            if (name.Contains("Sếu đầu đỏ"))
            {
                return "Loài sếu đầu đỏ cực kỳ quý hiếm có tên trong Sách Đỏ, biểu tượng của khu đất ngập nước miền Tây.";
            }
            return "Loài động thực vật bản địa sinh trưởng tại vùng nước ngập rừng tràm miền Tây Nam Bộ.";
        }
    }
}
