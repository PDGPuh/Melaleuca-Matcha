using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace RungTramTraSu
{
    public class Phase5Manager : MonoBehaviour
    {
        public static void SetInstance(Phase5Manager manager)
        {
            Instance = manager;
        }

        public static Phase5Manager Instance { get; private set; }

        public bool IsEndingActive => dialogueTriggered;

        [Header("References")]
        [SerializeField] private Transform player;
        [SerializeField] private Transform grandpa;
        [SerializeField] private Light dirLight;
        [SerializeField] private TextMeshProUGUI objectiveText;
        [SerializeField] private PhotoCamera photoCamera;
        [SerializeField] private Transform sunsetTarget;
        [SerializeField] private AudioClip climaxMusic;
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private UnityEngine.Video.VideoClip outroVideo;
        [SerializeField] private UnityEngine.Video.VideoPlayer videoPlayer;

        [Header("Diary UI References")]
        [SerializeField] private GameObject diaryCanvas;
        [SerializeField] private RawImage[] polaroidImages;
        [SerializeField] private TextMeshProUGUI diaryText;
        [SerializeField] private Button replayButton;

        [Header("Sunset Colors")]
        [SerializeField] private Color dayFogColor = new Color(0.6f, 0.78f, 0.72f);
        [SerializeField] private Color sunsetFogColor = new Color(0.78f, 0.38f, 0.35f);
        [SerializeField] private Color dayLightColor = new Color(1.0f, 0.96f, 0.86f);
        [SerializeField] private Color sunsetLightColor = new Color(0.98f, 0.45f, 0.22f);

        private bool dialogueTriggered = false;
        private bool sunsetCaptured = false;
        private float startHeight = 1.0f;
        private float topHeight = 10.75f;
        private Material skyboxInstance;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            if (diaryCanvas != null) diaryCanvas.SetActive(false);

            // Auto find player if null
            if (player == null)
            {
                var controllerObj = FindAnyObjectByType<PlayerController>();
                if (controllerObj != null) player = controllerObj.transform;
            }

            // Auto find grandpa if null
            if (grandpa == null)
            {
                var grandpaObj = FindAnyObjectByType<NPCGrandpa>();
                if (grandpaObj != null) grandpa = grandpaObj.transform;
            }

            if (player != null)
            {
                startHeight = player.position.y;
                var controller = player.GetComponent<PlayerController>();
                if (controller != null) controller.SetFrozen(false);
            }

            // Set topHeight matching the new deck surface height
            topHeight = 18.20f;

            // Setup beautiful runtime fog settings
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;

            // Snap flying foliage and rocks to the organic terrain at runtime using raycasting
            int groundLayers = LayerMask.GetMask("Default", "Terrain");
            GameObject foliage = GameObject.Find("BeautifiedFoliage");
            if (foliage != null)
            {
                foreach (Transform child in foliage.transform)
                {
                    Vector3 pos = child.position;
                    RaycastHit hit;
                    if (Physics.Raycast(new Vector3(pos.x, 20f, pos.z), Vector3.down, out hit, 40f, groundLayers))
                    {
                        pos.y = hit.point.y - 0.05f;
                    }
                    child.position = pos;
                }
            }

            GameObject rocks = GameObject.Find("BeautifiedRocks");
            if (rocks != null)
            {
                foreach (Transform child in rocks.transform)
                {
                    Vector3 pos = child.position;
                    RaycastHit hit;
                    if (Physics.Raycast(new Vector3(pos.x, 20f, pos.z), Vector3.down, out hit, 40f, groundLayers))
                    {
                        if (child.name.ToLower().Contains("cliff"))
                        {
                            pos.y = hit.point.y - 2.5f;
                        }
                        else
                        {
                            pos.y = hit.point.y - 0.3f;
                        }
                    }
                    child.position = pos;
                }
            }

            // Instantiate a copy of the skybox material so we don't modify the asset on disk
            if (RenderSettings.skybox != null)
            {
                skyboxInstance = Instantiate(RenderSettings.skybox);
                RenderSettings.skybox = skyboxInstance;
            }

            UpdateObjectiveText("Mục tiêu: Leo lên đỉnh tháp quan sát để ngắm hoàng hôn cùng Ông Ngoại.");
            ApplySunsetLighting(0f);
        }

        private void OnDestroy()
        {
            if (skyboxInstance != null)
            {
                Destroy(skyboxInstance);
            }
        }

        private void Update()
        {
            // Lighting and dialogue are triggered via player interaction with Grandpa
        }

        private void ApplySunsetLighting(float progress)
        {
            RenderSettings.fogColor = Color.Lerp(dayFogColor, sunsetFogColor, progress);
            RenderSettings.fogDensity = Mathf.Lerp(0.015f, 0.026f, progress);

            if (dirLight != null)
            {
                dirLight.color = Color.Lerp(dayLightColor, sunsetLightColor, progress);
                dirLight.intensity = Mathf.Lerp(1.35f, 0.75f, progress);
                // Slowly tilt sun down
                dirLight.transform.rotation = Quaternion.Euler(Mathf.Lerp(20f, 6f, progress), -65f, 0f);
            }

            if (skyboxInstance != null)
            {
                // Lerp skybox tint from standard light grey/white to warm orange-red sunset
                Color targetTint = Color.Lerp(new Color(0.5f, 0.5f, 0.5f, 0.5f), new Color(0.85f, 0.42f, 0.28f, 0.5f), progress);
                skyboxInstance.SetColor("_Tint", targetTint);
                // Gradually dim the skybox exposure
                skyboxInstance.SetFloat("_Exposure", Mathf.Lerp(1.1f, 0.45f, progress));
            }
        }

        public void StartClimaxDialogue()
        {
            if (dialogueTriggered) return;
            dialogueTriggered = true;
            StartCoroutine(SunsetClimaxRoutine());
        }

        private IEnumerator SmoothTransitionToSunset(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                ApplySunsetLighting(progress);
                yield return null;
            }
            ApplySunsetLighting(1f);
        }

        private IEnumerator SunsetClimaxRoutine()
        {
            // Close player diary if it is open to avoid UI overlapping with dialogue
            if (DiaryUIController.Instance != null && DiaryUIController.Instance.IsOpen)
            {
                DiaryUIController.Instance.ToggleDiary();
            }

            var controller = player != null ? player.GetComponent<PlayerController>() : FindAnyObjectByType<PlayerController>();
            if (controller != null) controller.SetFrozen(true);

            // Smoothly rotate player and tilt camera up to face sunset
            if (controller != null && sunsetTarget != null)
            {
                StartCoroutine(SmoothRotatePlayerToSunset(controller, sunsetTarget.position, 2.5f));
            }

            // Smoothly transition lighting from day to sunset during the dialogue (over 12 seconds)
            StartCoroutine(SmoothTransitionToSunset(12.0f));

            // Play climax background music (Hoang_Hon_Toc_Bac.mp3)
            if (musicSource != null && climaxMusic != null)
            {
                musicSource.clip = climaxMusic;
                musicSource.loop = true;
                musicSource.volume = 0.5f;
                musicSource.Play();
                Debug.Log("[SunsetClimaxRoutine] Started playing climax background music.");
            }

            Debug.Log($"[SunsetClimaxRoutine] Started. grandpa={grandpa != null}");
            // Trigger pointing animation towards sunset
            var grandpaAnim = grandpa != null ? grandpa.GetComponent<GrandpaPhase4Animator>() : null;
            Debug.Log($"[SunsetClimaxRoutine] grandpaAnim={grandpaAnim != null}");
            if (grandpaAnim != null)
            {
                Debug.Log("[SunsetClimaxRoutine] Calling PlayPointAtWildlife(99f)");
                grandpaAnim.PlayPointAtWildlife(99f);
            }

            string[] climax = new string[] {
                "Đẹp hông con? Ông sống ở đây mấy chục năm, chiều chiều leo lên đây dòm vẫn thấy nó đẹp y chang lần đầu.",
                "Toàn bộ rừng tràm mình chìm trong màu nắng chiều óng ả đỏ vàng, thật sự thanh bình đúng không con?",
                "Đi chơi cả ngày rồi, con dùng chiếc máy ảnh chụp bức ảnh hoàng hôn toàn cảnh Rừng Tràm này làm kỷ niệm sau cuối nha."
            };

            bool dialogueDone = false;
            DialogueManager.Instance.ShowDialogue("Ông Ngoại", climax, () => {
                dialogueDone = true;
            });

            yield return new WaitUntil(() => dialogueDone);

            // Revert back to standing idle observe
            if (grandpaAnim != null)
            {
                Debug.Log("[SunsetClimaxRoutine] Dialogue done, reverting to IdleObserve");
                grandpaAnim.PlayIdleObserve(99f);
            }

            if (controller != null) controller.SetFrozen(false);

            if (photoCamera != null)
            {
                photoCamera.UnlockCamera();
                photoCamera.SetQuestTarget(sunsetTarget);
            }

            UpdateObjectiveText("Mục tiêu: Chụp ảnh Hoàng hôn rực rỡ toàn cảnh Rừng Tràm (chuột phải để ngắm, trái để chụp).");
        }

        private IEnumerator SmoothRotatePlayerToSunset(PlayerController controller, Vector3 sunsetPos, float duration)
        {
            float elapsed = 0f;
            Quaternion startBodyRot = controller.transform.rotation;

            Vector3 lookDir = (sunsetPos - controller.transform.position);
            lookDir.y = 0;
            Quaternion targetBodyRot = Quaternion.LookRotation(lookDir.normalized);

            float startPitch = Camera.main != null ? Camera.main.transform.localEulerAngles.x : 0f;
            if (startPitch > 180f) startPitch -= 360f;

            // Look slightly up towards the sky (-12f) to frame the transition beautiful
            float targetPitch = -12.0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

                Quaternion currentBodyRot = Quaternion.Slerp(startBodyRot, targetBodyRot, t);
                float currentPitch = Mathf.Lerp(startPitch, targetPitch, t);

                controller.ForceSetLookRotation(currentBodyRot, currentPitch);
                yield return null;
            }

            controller.ForceSetLookRotation(targetBodyRot, targetPitch);
        }

        public void OnPhotoQuestCompleted()
        {
            if (sunsetCaptured) return;
            sunsetCaptured = true;

            UpdateObjectiveText("Đang lưu bức ảnh hoàng hôn...");
            StartCoroutine(EndingSequenceRoutine());
        }

        private IEnumerator EndingSequenceRoutine()
        {
            yield return new WaitForSeconds(1.5f);

            // Freeze player if present
            if (player != null)
            {
                var controller = player.GetComponent<PlayerController>();
                if (controller != null) controller.SetFrozen(true);
            }

            // Stop other ambient sounds
            var ambientWind = GameObject.Find("Ambient_Wind")?.GetComponent<AudioSource>();
            if (ambientWind != null) ambientWind.Stop();
            var ambientRiver = GameObject.Find("Ambient_River")?.GetComponent<AudioSource>();
            if (ambientRiver != null) ambientRiver.Stop();

            // Stop the climax music
            if (musicSource != null)
            {
                musicSource.Stop();
            }

            // Fade to black (wait 2s regardless so the transition feels smooth)
            if (ScreenFader.Instance != null)
            {
                ScreenFader.Instance.StartFadeOut(2.0f, null);
                float elapsed = 0f;
                while (elapsed < 2.0f)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSeconds(2.0f);
            }

            // Play the outro video clip
            if (videoPlayer == null)
            {
                videoPlayer = gameObject.GetComponent<UnityEngine.Video.VideoPlayer>();
                if (videoPlayer == null)
                {
                    videoPlayer = gameObject.AddComponent<UnityEngine.Video.VideoPlayer>();
                }
            }

            if (videoPlayer != null && outroVideo != null)
            {
                videoPlayer.source = UnityEngine.Video.VideoSource.VideoClip;
                videoPlayer.clip = outroVideo;
                videoPlayer.renderMode = UnityEngine.Video.VideoRenderMode.CameraNearPlane;
                videoPlayer.targetCamera = Camera.main;
                videoPlayer.aspectRatio = UnityEngine.Video.VideoAspectRatio.FitInside;
                
                // Set playOnAwake and loop to false
                videoPlayer.playOnAwake = false;
                videoPlayer.isLooping = false;

                // Prepare
                videoPlayer.Prepare();
                Debug.Log("[EndingSequenceRoutine] Preparing outro video...");
                while (!videoPlayer.isPrepared)
                {
                    yield return null;
                }

                // Play
                videoPlayer.Play();
                Debug.Log("[EndingSequenceRoutine] Started playing outro video.");

                // Fade back in quickly so the player can actually see the video on screen (since we faded out to black before)
                if (ScreenFader.Instance != null)
                {
                    ScreenFader.Instance.StartFadeIn(0.5f);
                }

                // Wait a bit to ensure isPlaying becomes true
                yield return new WaitForSeconds(0.5f);

                // Wait until the video is done playing
                while (videoPlayer.isPlaying)
                {
                    yield return null;
                }

                // Fade out to black again as the video completes
                if (ScreenFader.Instance != null)
                {
                    ScreenFader.Instance.StartFadeOut(1.0f, null);
                    yield return new WaitForSeconds(1.0f);
                }

                // Stop and detach
                videoPlayer.Stop();
                videoPlayer.targetCamera = null;
                Debug.Log("[EndingSequenceRoutine] Outro video finished.");
            }

            // Open Diary UI
            OpenDiaryUI();

            // --- Fallback: if diaryCanvas is not assigned in Inspector, show a simple message ---
            if (diaryCanvas == null)
            {
                UpdateObjectiveText("✓ Hoàn thành! Cảm ơn bạn đã chơi Rừng Tràm Trà Sư!\nChuyển về màn hình chính sau 5 giây...");
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                yield return new WaitForSeconds(5f);
                SceneManager.LoadScene("Phase1_GrandpaHouse");
            }
        }

        private void OpenDiaryUI()
        {
            // Unlock mouse cursor for UI interaction
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Stop other ambient sounds
            var ambientWind = GameObject.Find("Ambient_Wind")?.GetComponent<AudioSource>();
            if (ambientWind != null) ambientWind.Stop();
            var ambientRiver = GameObject.Find("Ambient_River")?.GetComponent<AudioSource>();
            if (ambientRiver != null) ambientRiver.Stop();

            // Play the outro background music
            if (musicSource != null && climaxMusic != null)
            {
                musicSource.clip = climaxMusic;
                musicSource.loop = true;
                musicSource.volume = 0.55f;
                musicSource.Play();
                Debug.Log("[OpenDiaryUI] Started playing outro background music: Hoang_Hon_Toc_Bac");
            }

            if (diaryCanvas != null)
            {
                diaryCanvas.SetActive(true);

                // Fade back in to reveal the diary UI after the post-video fade-out
                if (ScreenFader.Instance != null)
                {
                    ScreenFader.Instance.StartFadeIn(1.0f);
                }

                // Populate photos
                if (PersistentGameManager.Instance != null && polaroidImages != null)
                {
                    for (int i = 0; i < polaroidImages.Length; i++)
                    {
                        Texture2D tex = null;
                        if (i == 0)
                        {
                            tex = PersistentGameManager.Instance.GetPhoto("Phase1_Mango");
                        }
                        else if (i == 1)
                        {
                            tex = PersistentGameManager.Instance.GetPhoto("Phase2_Ch1");
                            if (tex == null) tex = PersistentGameManager.Instance.GetPhoto("Phase2_Ch2");
                            if (tex == null) tex = PersistentGameManager.Instance.GetPhoto("Phase2_Ch3");
                        }
                        else if (i == 2)
                        {
                            tex = PersistentGameManager.Instance.GetPhoto("Phase2_Ch3");
                            if (tex == null) tex = PersistentGameManager.Instance.GetPhoto("Phase2_Ch2");
                            if (tex == null) tex = PersistentGameManager.Instance.GetPhoto("Phase2_Ch1");
                        }
                        else if (i == 3)
                        {
                            // Tìm bất kỳ ảnh động vật nào đã chụp ở Phase 4
                            tex = PersistentGameManager.Instance.GetPhoto("Phase4_Duck");
                            if (tex == null) tex = PersistentGameManager.Instance.GetPhoto("Phase4_Stork");
                            if (tex == null) tex = PersistentGameManager.Instance.GetPhoto("Phase4_Snake");
                            if (tex == null) tex = PersistentGameManager.Instance.GetPhoto("Phase4_Fish");
                            if (tex == null) tex = PersistentGameManager.Instance.GetPhoto("Phase4_Butterfly");
                        }
                        else if (i == 4)
                        {
                            tex = PersistentGameManager.Instance.GetPhoto("Phase5_Sunset");
                        }

                        if (polaroidImages[i] != null)
                        {
                            if (tex != null)
                            {
                                polaroidImages[i].texture = tex;
                                polaroidImages[i].color = Color.white;
                            }
                            else
                            {
                                polaroidImages[i].texture = null;
                                polaroidImages[i].color = Color.gray;
                            }
                        }
                    }
                }

                // Typewriter diary text
                StartCoroutine(TypewriterDiaryText());

                // Set up restart button
                if (replayButton != null)
                {
                    replayButton.onClick.RemoveAllListeners();
                    replayButton.onClick.AddListener(ReplayGame);
                }
            }
        }

        private IEnumerator TypewriterDiaryText()
        {
            if (diaryText == null) yield break;

            string fullText = "Cuốn sổ lưu niệm: Chuyến đi rừng tràm Trà Sư cùng Ông Ngoại...\n\n" +
                              "\"Mình chưa từng nghĩ quê hương An Giang lại đẹp hoang sơ và kỳ vĩ đến vậy.\n" +
                              "Màu xanh mướt của bèo tấm, tiếng chim cò líu lo, tia nắng rực rỡ lọc qua tán lá rừng sâu...\n" +
                              "Lời ông ngoại dặn rất đúng: Thiên nhiên non nước hữu tình của mình, nếu chúng ta không gìn giữ và yêu thương, thì một ngày nào đó tụi nó sẽ biến mất mãi mãi...\"\n\n" +
                              "Cảm ơn bạn đã trải nghiệm trò chơi Rừng Tràm Trà Sư!\n" +
                              "Nhóm phát triển PRU213 - Unity 6000.4.7f1";

            diaryText.text = "";
            foreach (char c in fullText.ToCharArray())
            {
                diaryText.text += c;
                yield return new WaitForSeconds(0.015f);
            }
        }

        private void ReplayGame()
        {
            if (PersistentGameManager.Instance != null)
            {
                PersistentGameManager.Instance.ClearPhotos();
            }
            SceneManager.LoadScene("Phase1_GrandpaHouse");
        }

        private void UpdateObjectiveText(string text)
        {
            if (objectiveText != null) objectiveText.text = text;
        }
    }
}
