using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

namespace RungTramTraSu.CameraSystem
{
    public class CameraManager : MonoBehaviour, ICameraSystem
    {
        public static CameraManager Instance { get; private set; }

        [Header("Components Mapping")]
        [SerializeField] private Camera mainCamera;
        [SerializeField] private LensSystem lensSys;
        [SerializeField] private FocusSystem focusSys;
        [SerializeField] private ExposureSystem expSys;
        [SerializeField] private PhotoCapture photoCap;
        [SerializeField] private CameraController controller;
        [SerializeField] private CameraStateMachine stateMachine;

        [Header("Camera Attributes")]
        [SerializeField] private float maxBattery = 100f;
        [SerializeField] private float batteryDrainRate = 1.5f; // Per second when camera is active
        [SerializeField] private float batteryRechargeRate = 4.0f; // Per second when camera is inactive
        [SerializeField] private int maxStorageUnits = 50; // Total storage units

        private bool isCameraActive = false;
        private bool isManualMode = false;
        private bool hasCameraUnlocked = false;
        private bool wasActivatedByToggle = false;
        private float currentBattery = 100f;
        private int usedStorageUnits = 0;
        private ImageFormat currentFormat = ImageFormat.JPEG;

        // Current active quest validation details (from external Phase Managers)
        private string activeQuestCategory = "General";
        private Transform activeQuestTarget;

        // Properties
        public bool IsCameraActive => isCameraActive;
        public bool IsManualMode => isManualMode;
        public bool HasCameraUnlocked => hasCameraUnlocked;
        
        public float BatteryPercentage => (currentBattery / maxBattery) * 100f;
        public int AvailableStorage => maxStorageUnits - usedStorageUnits;
        public int MaxStorageUnits => maxStorageUnits;
        public int UsedStorageUnits => usedStorageUnits;
        public ImageFormat CurrentFormat => currentFormat;
        public PhotoScoring.ScoreResult LastScore { get; private set; }


        public LensSystem LensSys => lensSys;
        public FocusSystem FocusSys => focusSys;
        public ExposureSystem ExpSys => expSys;
        public PhotoCapture PhotoCap => photoCap;
        public CameraController Controller => controller;
        public CameraStateMachine StateMachine => stateMachine;

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
            
            // Programmatically resolve subsystems
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

            stateMachine = GetComponentInChildren<CameraStateMachine>();
            if (stateMachine == null) stateMachine = gameObject.AddComponent<CameraStateMachine>();

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

            // Ensure SaveSystem, AlbumManager, AchievementManager, PhotoScoring, and PhotoValidator are instantiated
            var ss = SaveSystem.Instance;
            var am = AlbumManager.Instance;
            var ac = AchievementManager.Instance;
            var ps = PhotoScoring.Instance;
            var pv = PhotoValidator.Instance;

            // Sync unlocked status based on scene
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

        public void SetImageFormat(ImageFormat format)
        {
            currentFormat = format;
        }

        private float lastToggleTime = 0f;
        public void ToggleCameraMode()
        {
            if (!hasCameraUnlocked) return;
            if (Time.time - lastToggleTime < 0.25f) return;
            lastToggleTime = Time.time;

            if (isCameraActive)
            {
                DeactivateCamera();
                wasActivatedByToggle = false;
            }
            else
            {
                if (currentBattery > 5f) // Cannot start with low battery
                {
                    ActivateCamera();
                    wasActivatedByToggle = true;
                }
            }
        }

        private void ActivateCamera()
        {
            isCameraActive = true;
            
            // Adjust player controller movement constraints
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                player.SetMovementLocked(true);
            }

            // Activate/Deactivate viewfinder
            if (CameraUI.Instance != null)
            {
                CameraUI.Instance.SetViewfinderActive(true);
            }

            stateMachine.ChangeState(CameraState.ViewfinderAiming);
        }

        private void DeactivateCamera()
        {
            isCameraActive = false;
            wasActivatedByToggle = false;
            
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                player.SetMovementLocked(false);
            }

            if (CameraUI.Instance != null)
            {
                CameraUI.Instance.SetViewfinderActive(false);
            }

            if (controller != null)
            {
                controller.ResetController();
            }

            stateMachine.ChangeState(CameraState.Inactive);
        }

        public void ConsumeBattery(float amount)
        {
            currentBattery = Mathf.Max(0f, currentBattery - amount);
            if (currentBattery <= 0f && isCameraActive)
            {
                DeactivateCamera();
            }
        }

        public void ClearStorage()
        {
            usedStorageUnits = 0;
        }

        public void UpgradeStorageCapacity(int additionalUnits)
        {
            maxStorageUnits += additionalUnits;
        }

        public void UpgradeBatteryCap(float additionalBattery)
        {
            maxBattery += additionalBattery;
            currentBattery = Mathf.Clamp(currentBattery + additionalBattery, 0f, maxBattery);
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

            // Handle battery charging / draining
            if (isCameraActive)
            {
                ConsumeBattery(batteryDrainRate * Time.deltaTime);
            }
            else
            {
                currentBattery = Mathf.Min(maxBattery, currentBattery + batteryRechargeRate * Time.deltaTime);
            }

            // Monitor toggling manual guidebook using New Input System Keyboard check
            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame && CameraGuide.Instance != null)
            {
                if (CameraGuide.Instance.gameObject.activeSelf) CameraGuide.Instance.CloseGuide();
                else CameraGuide.Instance.OpenGuide();
            }

            // Check camera toggle key F
            // Check camera toggle key F or Right Mouse click
            if (CameraInput.Instance != null && (CameraInput.Instance.ToggleCameraPressed || CameraInput.Instance.AimPressed))
            {
                PlayerController player = FindAnyObjectByType<PlayerController>();
                bool isFrozen = player != null && player.IsFrozen;
                if (!isFrozen)
                {
                    ToggleCameraMode();
                }
            }

            if (!isCameraActive) return;

            HandleInputUpdates();
        }

        private void HandleInputUpdates()
        {
            CameraInput input = CameraInput.Instance;
            if (input == null) return;

            // 1. Zoom Adjustment (Cycle lens focal lengths)
            float scroll = input.ScrollDelta;
            if (scroll != 0f && !input.StabilizeHeld) // Scroll adjusts focal length unless Shift is held
            {
                int dir = scroll > 0f ? 1 : -1;
                lensSys.CycleLens(dir);
            }

            // 2. Focus Adjustments
            if (input.FocusManualDirection != 0f)
            {
                focusSys.AdjustFocusDistance(input.FocusManualDirection * Time.deltaTime);
            }
            else if (scroll != 0f && input.AimHeld) // Hold right mouse + Scroll adjusts focus manually
            {
                float dir = scroll > 0f ? 0.05f : -0.05f;
                focusSys.AdjustFocusDistance(dir);
            }
            else if (scroll != 0f && input.StabilizeHeld) // Shift + Scroll adjusts focus manually
            {
                float dir = scroll > 0f ? 0.05f : -0.05f;
                focusSys.AdjustFocusDistance(dir);
            }

            if (input.FocusLockPressed)
            {
                focusSys.LockActiveTarget();
            }

            // 3. Stabilization (holding breath)
            if (controller != null)
            {
                controller.SetStabilized(input.StabilizeHeld);
                if (input.StabilizeHeld)
                {
                    stateMachine.ChangeState(CameraState.StabilizedAiming);
                }
                else
                {
                    stateMachine.ChangeState(CameraState.ViewfinderAiming);
                }
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
                focusSys.SetFocusMode(isManualMode ? FocusMode.Manual : FocusMode.SingleAF);
                Debug.Log("[CameraManager] Focus Mode: " + focusSys.ActiveFocusMode);
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
                expSys.ApplyAutoExposure();
            }

            // 7. Shutter capture trigger
            if (input.ShutterPressed && !photoCap.IsCapturing)
            {
                // Check storage requirements
                int requiredUnits = currentFormat == ImageFormat.RAW ? 2 : 1;
                if (AvailableStorage < requiredUnits)
                {
                    Debug.LogWarning("[CameraManager] Cannot capture photo. Storage is full!");
                    return;
                }

                if (isManualMode)
                {
                    // Hold left click in manual mode triggers Burst Mode
                    StartCoroutine(CheckBurstTrigger());
                }
                else
                {
                    // Single shot
                    ConsumeBattery(5f); // Capture drains battery
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
                    stateMachine.ChangeState(CameraState.BurstCapturing);
                    ConsumeBattery(10f); // Burst consumes more battery
                    photoCap.StartBurstCapture(ProcessBurstPhotos);
                    break;
                }
                yield return null;
            }

            if (!burstStarted)
            {
                ConsumeBattery(5f);
                photoCap.CaptureSingleShot(ProcessCapturedPhoto);
            }
        }

        public void ProcessCapturedPhoto(Texture2D photo)
        {
            if (WildlifeDetector.Instance == null || PhotoScoring.Instance == null || PhotoValidator.Instance == null) return;

            // Register storage cost
            int cost = currentFormat == ImageFormat.RAW ? 2 : 1;
            usedStorageUnits += cost;

            // Scan for targets inside viewfinder bounds
            var targets = WildlifeDetector.Instance.ScanForVisibleTargets();
            WildlifeDetector.DetectedTarget bestTarget = new WildlifeDetector.DetectedTarget();
            bool targetFound = false;

            if (targets.Count > 0)
            {
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
            float blur = targetFound ? focusSys.GetBlurFactor(bestTarget.distance, expSys.Aperture) : 0f;
            float expErr = expSys.CalculateLuminanceDeviation();
            
            PhotoScoring.ScoreResult score = PhotoScoring.Instance.CalculateScore(
                targetFound ? bestTarget : new WildlifeDetector.DetectedTarget { displayName = "Phong Cảnh Rừng Tràm", screenCoverage = 10f, viewportPos = new Vector3(0.5f, 0.5f, 1f) },
                focusSys.FocusDistance,
                blur,
                expErr,
                expSys.ShutterSpeed,
                expSys.Aperture,
                expSys.ISO,
                isManualMode
            );
            LastScore = score;


            // Check validation for quests
            string failReason = "";
            bool isValidated = false;
            
            if (targetFound)
            {
                isValidated = PhotoValidator.Instance.ValidatePhoto(bestTarget, blur, activeQuestCategory, activeQuestTarget, out failReason);
            }

            string subject = targetFound ? bestTarget.displayName : "Phong Cảnh Rừng Tràm";
            string desc = targetFound ? GetDescBySubject(bestTarget.displayName) : "Một khoảnh khắc yên ả, hữu tình sâu trong lòng Rừng Tràm Trà Sư.";
            bool rare = targetFound && bestTarget.isRare;

            if (targetFound && isValidated)
            {
                // Save to Phase1Manager specifically if in Phase 1
                if (Phase1Manager.Instance != null)
                {
                    Phase1Manager.Instance.SavePhoto(photo);
                }

                // Save to PersistentGameManager
                if (PersistentGameManager.Instance != null)
                {
                    PersistentGameManager.Instance.SavePhoto(activeQuestCategory, photo);
                }

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

                // Call Phase completion hooks
                TriggerPhaseValidationCallbacks(bestTarget);
            }

            // Deactivate camera first to reset FOV, lock states, and hide the viewfinder UI cleanly before showing result card
            DeactivateCamera();

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
                float score = EvaluatePhotoScoreLocally(tex);
                if (score > highestScore)
                {
                    highestScore = score;
                    bestPhoto = tex;
                }
            }

            // Clean up unused textures
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
            
            float blur = focusSys.GetBlurFactor(targets[0].distance, expSys.Aperture);
            float expErr = expSys.CalculateLuminanceDeviation();
            
            var res = PhotoScoring.Instance.CalculateScore(
                targets[0],
                focusSys.FocusDistance,
                blur,
                expErr,
                expSys.ShutterSpeed,
                expSys.Aperture,
                expSys.ISO,
                isManualMode
            );
            return res.totalScore;
        }

        private void TriggerPhaseValidationCallbacks(WildlifeDetector.DetectedTarget target)
        {
            if (Phase1Manager.Instance != null) Phase1Manager.Instance.OnPhotoQuestCompleted();
            if (Phase2Manager.Instance != null) Phase2Manager.Instance.OnPhotoQuestCompleted(target);
            if (Phase3Manager.Instance != null) Phase3Manager.Instance.OnPhotoQuestCompleted();
            if (Phase4Manager.Instance != null) Phase4Manager.Instance.OnPhotoQuestCompleted(target);
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
