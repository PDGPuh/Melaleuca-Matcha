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
            topHeight = 10.75f;

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
            if (player == null) return;

            float currentY = player.position.y;

            // Lerp skybox, fog and lighting color based on player height
            float progress = Mathf.Clamp01((currentY - startHeight) / (topHeight - startHeight));
            ApplySunsetLighting(progress);

            // Trigger climax dialogue when player gets close to the top and grandpa
            if (!dialogueTriggered && currentY >= topHeight - 1.5f && grandpa != null && Vector3.Distance(player.position, grandpa.position) < 5.0f)
            {
                dialogueTriggered = true;
                StartCoroutine(SunsetClimaxRoutine());
            }
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

        private IEnumerator SunsetClimaxRoutine()
        {
            // Close player diary if it is open to avoid UI overlapping with dialogue
            if (DiaryUIController.Instance != null && DiaryUIController.Instance.IsOpen)
            {
                DiaryUIController.Instance.ToggleDiary();
            }

            var controller = player != null ? player.GetComponent<PlayerController>() : FindAnyObjectByType<PlayerController>();
            if (controller != null) controller.SetFrozen(true);

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
            if (controller != null) controller.SetFrozen(false);

            if (photoCamera != null)
            {
                photoCamera.UnlockCamera();
                photoCamera.SetQuestTarget(sunsetTarget);
            }

            UpdateObjectiveText("Mục tiêu: Chụp ảnh Hoàng hôn rực rỡ toàn cảnh Rừng Tràm (chuột phải để ngắm, trái để chụp).");
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

            // Fade to black (wait 2s regardless so the transition feels smooth)
            if (ScreenFader.Instance != null)
            {
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

            if (diaryCanvas != null)
            {
                diaryCanvas.SetActive(true);

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
