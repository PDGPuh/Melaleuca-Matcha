using System.Collections;
using System.Collections.Generic;
using System.Text;
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
        private GrandpaPhase4Animator grandpaAnimator;
        private HashSet<AnimalAI.AnimalType> capturedAnimals = new HashSet<AnimalAI.AnimalType>();
        private readonly AnimalAI.AnimalType[] guidanceOrder = new AnimalAI.AnimalType[]
        {
            AnimalAI.AnimalType.Stork,
            AnimalAI.AnimalType.Duck,
            AnimalAI.AnimalType.Fish,
            AnimalAI.AnimalType.Butterfly,
            AnimalAI.AnimalType.Snake
        };
        private bool transitionTriggered = false;
        private bool showingTemporaryMessage = false;
        private float objectiveRefreshTimer = 0f;
        private float storkWarningCooldown = 0f;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            playerCamera = Camera.main;
            grandpaAnimator = FindAnyObjectByType<GrandpaPhase4Animator>();
            PrepareObjectiveText();

            // Auto find player if null
            if (player == null)
            {
                var controllerObj = FindAnyObjectByType<PlayerController>();
                if (controllerObj != null) player = controllerObj.transform;
            }

            // Setup beautiful runtime environment settings (fog and lighting)
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.56f, 0.66f, 0.59f); // Soft green-grey swamp haze
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0065f; // Keep sun shafts readable without drowning the scene
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.48f, 0.56f, 0.58f);
            RenderSettings.ambientEquatorColor = new Color(0.36f, 0.43f, 0.33f);
            RenderSettings.ambientGroundColor = new Color(0.15f, 0.18f, 0.12f);

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
            if (grandpaAnimator != null)
            {
                grandpaAnimator.PlayCrouchWhisper(99f); // Keep crouching during dialogue
            }

            bool dialogueDone = false;
            DialogueManager.Instance.ShowDialogue("Ông Ngoại", new string[] {
                "Tới khu đầm lầy bảo tồn rồi nè con. Vùng này chim chóc với động vật hoang dã cư ngụ nhiều dữ lắm.",
                "Hồi xưa sếu đầu đỏ tụi nó về nghẹt đất luôn, giờ thiên nhiên thay đổi nên hiếm dần rồi.",
                "Con đi nhẹ nhàng thôi nhen, khom khom cúi người xuống (nhấn phím C để Crouch) đi chậm cho tụi chim không bị giật mình bay mất.",
                "Con thử tìm rồi chụp đủ 5 loài động vật hoang dã khác nhau coi được không nha con!"
            }, () => {
                dialogueDone = true;
            });

            yield return new WaitUntil(() => dialogueDone);

            if (grandpaAnimator != null)
            {
                grandpaAnimator.PlayIdleObserve(4f); // Stand back up
            }
        }

        private void Update()
        {
            if (transitionTriggered) return;

            if (storkWarningCooldown > 0f) storkWarningCooldown -= Time.deltaTime;
            if (!showingTemporaryMessage)
            {
                objectiveRefreshTimer += Time.deltaTime;
                if (objectiveRefreshTimer >= 1.25f)
                {
                    objectiveRefreshTimer = 0f;
                    UpdateObjective();
                }
            }

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

            if (!showingTemporaryMessage)
            {
                StartCoroutine(ShowTemporaryWarning("Trong khung chưa có sinh vật rõ. " + BuildCurrentHintSentence(), 4f));
            }
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
            if (grandpaAnimator != null)
            {
                if (type == AnimalAI.AnimalType.Stork || type == AnimalAI.AnimalType.Duck)
                {
                    grandpaAnimator.PlayPointAtWildlife(5.5f);
                }
                else
                {
                    grandpaAnimator.PlayPhotoComment(5.5f);
                }
            }

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
            if (grandpaAnimator != null)
            {
                grandpaAnimator.PlayWalkGuide(7f);
            }

            DialogueManager.Instance.ShowDialogue("Ông Ngoại", new string[] {
                "Con chụp khéo quá! Chụp được đủ hết 5 loài sinh vật quý giá của rừng mình rồi đó.",
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
                string name = GetAnimalVietnameseName(type);
                StartCoroutine(ShowTemporaryWarning($"Ông Ngoại kêu: \"Con đi mạnh chân quá làm {name} giật mình trốn mất rồi kìa! Nhấn phím C để Crouch (cúi người) đi rón rén thôi con!\"", 5f));
            }
        }

        private IEnumerator ShowTemporaryWarning(string warning, float duration)
        {
            if (objectiveText == null) yield break;
            showingTemporaryMessage = true;
            objectiveText.text = warning;
            objectiveText.color = Color.yellow;
            yield return new WaitForSeconds(duration);
            showingTemporaryMessage = false;
            objectiveText.color = Color.white;
            UpdateObjective();
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
                        "Ồ! Tấm hình cò trắng đậu trên cành tràm đẹp quá con ơi.",
                        "Loài cò này nhát lắm, con phải đi khom người rón rén mới chụp được tụi nó đó."
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

        private void PrepareObjectiveText()
        {
            if (objectiveText == null) return;

            objectiveText.enableWordWrapping = true;
            objectiveText.overflowMode = TextOverflowModes.Overflow;
            objectiveText.fontSize = Mathf.Min(objectiveText.fontSize, 15f);
            objectiveText.lineSpacing = -18f;
            objectiveText.alignment = TextAlignmentOptions.TopLeft;
            objectiveText.color = new Color(1f, 1f, 1f, 0.88f);
            objectiveText.outlineWidth = 0.18f;
            objectiveText.outlineColor = new Color(0f, 0f, 0f, 0.65f);

            RectTransform rect = objectiveText.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(18f, -18f);
            rect.sizeDelta = new Vector2(460f, 52f);
        }

        private string BuildChecklistLine()
        {
            StringBuilder builder = new StringBuilder(128);
            for (int i = 0; i < guidanceOrder.Length; i++)
            {
                AnimalAI.AnimalType type = guidanceOrder[i];
                builder.Append(capturedAnimals.Contains(type) ? "[x] " : "[ ] ");
                builder.Append(GetShortAnimalName(type));
                if (i < guidanceOrder.Length - 1)
                {
                    builder.Append(" | ");
                }
            }

            return builder.ToString();
        }

        private string BuildCurrentHintSentence()
        {
            if (capturedAnimals.Count >= 5)
            {
                return "Đủ ảnh rồi, quay lại gặp ông.";
            }

            AnimalAI.AnimalType targetType = GetNextTargetType();
            AnimalAI target = FindNearestAnimalOfType(targetType);
            string direction = target != null ? GetDirectionHint(target.transform.position) : "ven bờ";
            return $"Gợi ý: {GetHabitatHint(targetType)}, {direction}.";
        }

        private AnimalAI.AnimalType GetNextTargetType()
        {
            for (int i = 0; i < guidanceOrder.Length; i++)
            {
                if (!capturedAnimals.Contains(guidanceOrder[i]))
                {
                    return guidanceOrder[i];
                }
            }

            return guidanceOrder[0];
        }

        private AnimalAI FindNearestAnimalOfType(AnimalAI.AnimalType type)
        {
            AnimalAI best = null;
            float bestDistance = float.MaxValue;
            Vector3 origin = player != null ? player.position : Vector3.zero;

            for (int i = 0; i < animals.Count; i++)
            {
                AnimalAI animal = animals[i];
                if (animal == null || animal.Type != type || animal.HasFled) continue;

                float distance = (animal.transform.position - origin).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = animal;
                }
            }

            return best;
        }

        private string GetDirectionHint(Vector3 worldPosition)
        {
            if (player == null)
            {
                return "ven bờ";
            }

            Vector3 toTarget = worldPosition - player.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            if (distance < 0.1f)
            {
                return "rất gần";
            }

            Vector3 direction = toTarget.normalized;
            Vector3 forward = player.forward;
            forward.y = 0f;
            forward.Normalize();
            Vector3 right = player.right;
            right.y = 0f;
            right.Normalize();

            float forwardDot = Vector3.Dot(forward, direction);
            float rightDot = Vector3.Dot(right, direction);
            string side;

            if (forwardDot > 0.65f)
            {
                side = "trước";
            }
            else if (forwardDot < -0.65f)
            {
                side = "sau";
            }
            else if (rightDot > 0f)
            {
                side = "phải";
            }
            else
            {
                side = "trái";
            }

            return $"{side} {Mathf.RoundToInt(distance)}m";
        }

        private string GetShortAnimalName(AnimalAI.AnimalType type)
        {
            switch (type)
            {
                case AnimalAI.AnimalType.Stork: return "Cò";
                case AnimalAI.AnimalType.Snake: return "Rắn";
                case AnimalAI.AnimalType.Fish: return "Cá";
                case AnimalAI.AnimalType.Butterfly: return "Bướm";
                case AnimalAI.AnimalType.Duck: return "Vịt";
                default: return "Sinh vật";
            }
        }

        private string GetHabitatHint(AnimalAI.AnimalType type)
        {
            switch (type)
            {
                case AnimalAI.AnimalType.Stork:
                    return "cành tràm ven sông";
                case AnimalAI.AnimalType.Duck:
                    return "mặt nước yên";
                case AnimalAI.AnimalType.Fish:
                    return "chỗ có gợn nước";
                case AnimalAI.AnimalType.Butterfly:
                    return "cụm hoa súng";
                case AnimalAI.AnimalType.Snake:
                    return "bờ bùn, khúc cây mục";
                default:
                    return "ven bờ";
            }
        }

        private void UpdateObjective()
        {
            if (objectiveText == null) return;
            AnimalAI.AnimalType targetType = GetNextTargetType();
            objectiveText.text = $"Ảnh {capturedAnimals.Count}/5 | Tìm: {GetAnimalVietnameseName(targetType)}\n" +
                                 BuildCurrentHintSentence();
        }

        public void OnPhotoQuestCompleted()
        {
            // Handled dynamically in CheckAnimalCapture, but we keep this empty method to avoid breaking callbacks
        }
    }
}
