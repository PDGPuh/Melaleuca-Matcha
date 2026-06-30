using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace RungTramTraSu
{
    public class Phase4Manager : MonoBehaviour
    {
        public static Phase4Manager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private Transform player;
        [SerializeField] private TextMeshProUGUI objectiveText;
        [SerializeField] private List<AnimalAI> animals = new List<AnimalAI>();

        private Camera playerCamera;
        private HashSet<AnimalAI.AnimalType> capturedAnimals = new HashSet<AnimalAI.AnimalType>();
        private bool transitionTriggered = false;
        private float storkWarningCooldown = 0f;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            playerCamera = Camera.main;

            // Auto find player if null
            if (player == null)
            {
                var controllerObj = FindAnyObjectByType<PlayerController>();
                if (controllerObj != null) player = controllerObj.transform;
            }

            // Setup beautiful runtime environment settings (fog and lighting)
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.55f, 0.74f, 0.68f); // Soft green-blue swamp fog
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.02f; // Deeper swamp fog
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.25f;

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

            // Automatically find all AnimalAI objects in the scene
            animals = new List<AnimalAI>(FindObjectsByType<AnimalAI>(FindObjectsSortMode.None));
            Debug.Log($"[Phase4Manager] Start: Found {animals.Count} AnimalAI objects in the scene.");
            foreach (var a in animals)
            {
                Debug.Log($" - Animal: {a.name}, Type: {a.Type}, Position: {a.transform.position}");
            }

            // Unlock player movement
            if (player != null)
            {
                var controller = player.GetComponent<PlayerController>();
                if (controller != null) controller.SetFrozen(false);
            }

            UpdateObjective();
            StartCoroutine(IntroTalk());
        }

        private IEnumerator IntroTalk()
        {
            yield return new WaitForSeconds(2.0f);
            DialogueManager.Instance.ShowDialogue("Ông Ngoại", new string[] {
                "Tới khu đầm lầy bảo tồn rồi nè con.",
                "Vùng này chim chóc với động vật hoang dã cư ngụ nhiều dữ lắm.",
                "Hồi xưa sếu đầu đỏ tụi nó về nghẹt đất luôn, giờ thiên nhiên thay đổi nên hiếm dần rồi.",
                "Con đi nhẹ nhàng thôi nhen, khom khom cúi người xuống đi chậm cho tụi chim không bị giật mình bay mất.",
                "Con thử tìm rồi chụp đủ năm loài động vật hoang dã khác nhau coi được không nha con!"
            });
        }

        private void Update()
        {
            if (transitionTriggered) return;

            if (storkWarningCooldown > 0f) storkWarningCooldown -= Time.deltaTime;

            // Check if player took a photo
            if (Mouse.current.leftButton.wasPressedThisFrame && playerCamera != null)
            {
                var photoCamera = playerCamera.GetComponent<PhotoCamera>();
                if (photoCamera != null && photoCamera.IsZooming)
                {
                    // Check if any animal is in viewport frame
                    CheckAnimalCapture();
                }
            }
        }

        private void CheckAnimalCapture()
        {
            var photoCamera = playerCamera.GetComponent<PhotoCamera>();

            foreach (var animal in animals)
            {
                if (animal == null) continue;
                if (animal.HasFled) continue;
                if (capturedAnimals.Contains(animal.Type)) continue;

                Vector3 viewportPoint = playerCamera.WorldToViewportPoint(animal.transform.position);
                bool inFrame = viewportPoint.z > 0 &&
                               viewportPoint.x >= 0.15f && viewportPoint.x <= 0.85f &&
                               viewportPoint.y >= 0.15f && viewportPoint.y <= 0.85f;

                if (inFrame)
                {
                    // Set photo category dynamically based on the actual captured animal
                    if (photoCamera != null)
                        photoCamera.SetPhotoCategory("Phase4_" + animal.Type.ToString());

                    StartCoroutine(RegisterCapture(animal.Type));
                    return;
                }
            }

            // Reset category to General if no valid animal was captured this frame
            if (photoCamera != null)
                photoCamera.SetPhotoCategory("General");
        }

        private IEnumerator RegisterCapture(AnimalAI.AnimalType type)
        {
            capturedAnimals.Add(type);
            string animalName = GetAnimalVietnameseName(type);
            
            // Close player diary if it is open to avoid UI overlapping with dialogue
            if (DiaryUIController.Instance != null && DiaryUIController.Instance.IsOpen)
            {
                DiaryUIController.Instance.ToggleDiary();
            }

            // Temporary freeze player for dialogue
            var controller = player != null ? player.GetComponent<PlayerController>() : FindAnyObjectByType<PlayerController>();
            if (controller != null) controller.SetFrozen(true);

            string[] comment = GetGrandpaComment(type);
            bool dialogueDone = false;
            DialogueManager.Instance.ShowDialogue("Ông Ngoại", comment, () => {
                dialogueDone = true;
            });

            yield return new WaitUntil(() => dialogueDone);
            if (controller != null) controller.SetFrozen(false);

            UpdateObjective();

            if (capturedAnimals.Count >= 5)
            {
                StartCoroutine(CompletePhaseRoutine());
            }
        }

        private IEnumerator CompletePhaseRoutine()
        {
            yield return new WaitForSeconds(1.5f);
            
            var controller = player.GetComponent<PlayerController>();
            if (controller != null) controller.SetFrozen(true);

            DialogueManager.Instance.ShowDialogue("Ông Ngoại", new string[] {
                "Con chụp khéo quá! Chụp được đủ hết năm loài sinh vật quý giá của rừng mình rồi đó.",
                "Cảnh hoàng hôn sắp buông xuống rồi kìa con ơi, trời chuyển màu nhanh lắm.",
                "Đi, hai ông cháu mình lên đỉnh tháp quan sát đằng kia ngắm toàn cảnh rừng tràm lúc chiều tà nghen."
            }, () => {
                TriggerSceneTransition();
            });
        }

        private void TriggerSceneTransition()
        {
            transitionTriggered = true;
            if (ScreenFader.Instance != null)
            {
                ScreenFader.Instance.StartFadeOut(2f, () => {
                    SceneManager.LoadScene("Phase5_Sunset");
                });
            }
            else
            {
                SceneManager.LoadScene("Phase5_Sunset");
            }
        }

        public void NotifyAnimalScared(AnimalAI.AnimalType type)
        {
            if (storkWarningCooldown <= 0f && !capturedAnimals.Contains(type))
            {
                storkWarningCooldown = 8f; // Cooldown
                StartCoroutine(ShowTemporaryWarning("Ông Ngoại kêu: \"Con đi mạnh chân quá làm tụi nó giật mình trốn mất rồi kìa! Đi rón rén thôi con!\"", 5f));

                // Phát lồng tiếng cảnh báo dậm chân làm động vật biến mất (Câu 30)
                AudioClip clip = Resources.Load<AudioClip>("Audio/Phase4/Câu thoại 30");
                if (clip != null)
                {
                    AudioSource audio = GetComponent<AudioSource>();
                    if (audio == null) audio = gameObject.AddComponent<AudioSource>();
                    audio.PlayOneShot(clip);
                }
            }
        }

        private IEnumerator ShowTemporaryWarning(string warning, float duration)
        {
            string oldObjective = objectiveText.text;
            objectiveText.text = warning;
            objectiveText.color = Color.yellow;
            yield return new WaitForSeconds(duration);
            objectiveText.text = oldObjective;
            objectiveText.color = Color.white;
        }

        private string GetAnimalVietnameseName(AnimalAI.AnimalType type)
        {
            switch (type)
            {
                case AnimalAI.AnimalType.Stork: return "Cò Trắng";
                case AnimalAI.AnimalType.Snake: return "Rắn Nước";
                case AnimalAI.AnimalType.Fish: return "Cá Lóc";
                case AnimalAI.AnimalType.Butterfly: return "Bướm Hoa Súng";
                case AnimalAI.AnimalType.Duck: return "Vịt Trời";
                default: return "Sinh Vật";
            }
        }

        private string[] GetGrandpaComment(AnimalAI.AnimalType type)
        {
            switch (type)
            {
                case AnimalAI.AnimalType.Stork:
                    return new string[] {
                        "Ồ! Tấm hình cò trắng đậu trên cành tràm đẹp quá con ơi. Loài cò này nhát lắm, con phải đi khom người rón rén mới chụp được tụi nó đó."
                    };
                case AnimalAI.AnimalType.Snake:
                    return new string[] {
                        "Rắn nước đó con! Loài này hiền khô hà, tụi nó bơi lội bắt cá nhỏ ăn, không có độc gì đâu con đừng sợ."
                    };
                case AnimalAI.AnimalType.Fish:
                    return new string[] {
                        "Cá lóc quẫy nước nhảy lên kìa! Cá miền Tây mùa nước nổi nhiều vô số kể, ăn không hết luôn đó con."
                    };
                case AnimalAI.AnimalType.Butterfly:
                    return new string[] {
                        "Bướm hoa súng lượn vòng vòng nè. Mấy cụm bông súng bông sen là tụi nó tụ lại hút mật đông vui lắm."
                    };
                case AnimalAI.AnimalType.Duck:
                    return new string[] {
                        "Mấy chú vịt trời đang bơi bập bềnh kiếm mồi kìa con. Con chụp góc này nhìn thanh bình ghê chớ!"
                    };
                default:
                    return new string[] { "Tấm hình sinh vật này đẹp quá con ơi!" };
            }
        }

        private void UpdateObjective()
        {
            if (objectiveText == null) return;
            objectiveText.text = $"Mục tiêu: Chụp ảnh 5 loài động vật ({capturedAnimals.Count}/5).\n" +
                                 "Nhấn C để Crouch tiếp cận chim cò không bay mất.";
        }

        public void OnPhotoQuestCompleted()
        {
            // Handled dynamically in CheckAnimalCapture, but we keep this empty method to avoid breaking callbacks
        }
    }
}
