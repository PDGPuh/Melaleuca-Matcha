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

            // Automatically find all AnimalAI objects in the scene
            animals = new List<AnimalAI>(FindObjectsByType<AnimalAI>(FindObjectsSortMode.None));

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
                "Tới khu đầm lầy bảo tồn rồi nè con. Vùng này chim chóc với động vật hoang dã cư ngụ nhiều dữ lắm.",
                "Hồi xưa sếu đầu đỏ tụi nó về nghẹt đất luôn, giờ thiên nhiên thay đổi nên hiếm dần rồi.",
                "Con đi nhẹ nhàng thôi nhen, khom khom cúi người xuống (nhấn phím C để Crouch) đi chậm cho tụi chim không bị giật mình bay mất.",
                "Con thử tìm rồi chụp đủ 5 loài động vật hoang dã khác nhau coi được không nha con!"
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
                if (animal.HasFled) continue; // Bỏ qua nếu con vật đã bỏ chạy trốn mất
                if (capturedAnimals.Contains(animal.Type)) continue;

                Vector3 viewportPoint = playerCamera.WorldToViewportPoint(animal.transform.position);
                bool inFrame = viewportPoint.z > 0 && 
                              viewportPoint.x >= 0.22f && viewportPoint.x <= 0.78f && 
                              viewportPoint.y >= 0.22f && viewportPoint.y <= 0.78f;

                if (inFrame)
                {
                    // Check occlusion
                    RaycastHit hit;
                    Vector3 dir = animal.transform.position - playerCamera.transform.position;
                    if (Physics.Raycast(playerCamera.transform.position, dir, out hit, dir.magnitude + 0.5f))
                    {
                        if (hit.transform != animal.transform && !hit.transform.IsChildOf(animal.transform))
                        {
                            Debug.Log("Animal is occluded by: " + hit.collider.name);
                            continue;
                        }
                    }

                    // Set photo category dynamically based on the actual captured animal
                    if (photoCamera != null)
                    {
                        photoCamera.SetPhotoCategory("Phase4_" + animal.Type.ToString());
                    }

                    // Photo captured successfully!
                    StartCoroutine(RegisterCapture(animal.Type));
                    return;
                }
            }

            // Reset category to General if no valid animal was captured this frame
            if (photoCamera != null)
            {
                photoCamera.SetPhotoCategory("General");
            }
        }

        private IEnumerator RegisterCapture(AnimalAI.AnimalType type)
        {
            capturedAnimals.Add(type);
            string animalName = GetAnimalVietnameseName(type);
            
            // Temporary freeze player for dialogue
            var controller = player.GetComponent<PlayerController>();
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

        private void UpdateObjective()
        {
            if (objectiveText != null)
            {
                objectiveText.text = $"Mục tiêu: Tìm và chụp ảnh 5 loài động vật ({capturedAnimals.Count}/5).\n" +
                                     $"Nhấn C để Crouch (cúi người) để tiếp cận chim cò không bay mất.";
            }
        }

        public void OnPhotoQuestCompleted()
        {
            // Handled dynamically in CheckAnimalCapture, but we keep this empty method to avoid breaking callbacks
        }
    }
}
