using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

namespace RungTramTraSu
{
    public class DialogueManager : MonoBehaviour
    {
        public static DialogueManager Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private GameObject dialoguePanel;
        [SerializeField] private TextMeshProUGUI speakerNameText;
        [SerializeField] private TextMeshProUGUI dialogueText;
        [SerializeField] private GameObject continueIndicator;

        [Header("Dialogue Settings")]
        [SerializeField] private float typingSpeed = 0.04f;

        private Queue<string> sentences;
        private bool isTyping = false;
        private string currentSentence = "";
        private Action onDialogueComplete;
        private PlayerController playerController;
        private AudioSource voiceSource;

        private void Awake()
        {
            Instance = this;

            EnsureSentenceQueue();

            if (voiceSource == null)
            {
                voiceSource = GetComponent<AudioSource>();
                if (voiceSource == null)
                {
                    voiceSource = gameObject.AddComponent<AudioSource>();
                }
            }

            // Tự động tìm kiếm và gán các trường UI nếu chưa được liên kết
            if (dialoguePanel == null)
            {
                dialoguePanel = GameObject.Find("DialoguePanel");
            }
            if (dialoguePanel != null)
            {
                if (speakerNameText == null)
                {
                    Transform t = dialoguePanel.transform.Find("SpeakerNameText");
                    if (t != null) speakerNameText = t.GetComponent<TextMeshProUGUI>();
                }
                if (dialogueText == null)
                {
                    Transform t = dialoguePanel.transform.Find("DialogueText");
                    if (t != null) dialogueText = t.GetComponent<TextMeshProUGUI>();
                }
                if (continueIndicator == null)
                {
                    Transform t = dialoguePanel.transform.Find("ContinueIndicator");
                    if (t != null) continueIndicator = t.gameObject;
                }
            }

            if (dialoguePanel != null) dialoguePanel.SetActive(false);
            if (continueIndicator != null) continueIndicator.SetActive(false);
        }

        private void Start()
        {
            playerController = FindAnyObjectByType<PlayerController>();
        }

        private void Update()
        {
            // Kiểm tra click chuột trái hoặc phím Space hoặc phím Enter để tiếp tục hội thoại
            if (dialoguePanel != null && dialoguePanel.activeSelf)
            {
                bool nextInput = (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                                 (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame));
                if (nextInput)
                {
                    if (isTyping)
                    {
                        // Nếu đang chạy chữ, click sẽ hiện toàn bộ câu ngay lập tức
                        StopAllCoroutines();
                        dialogueText.text = currentSentence;
                        isTyping = false;
                        if (continueIndicator != null) continueIndicator.SetActive(true);
                    }
                    else
                    {
                        // Nếu đã chạy chữ xong, click sẽ chuyển sang câu tiếp theo
                        DisplayNextSentence();
                    }
                }
            }
        }

        /// <summary>
        /// Bắt đầu một đoạn hội thoại mới
        /// </summary>
        /// <param name="speaker">Tên người nói</param>
        /// <param name="lines">Danh sách các câu thoại</param>
        /// <param name="onComplete">Callback chạy sau khi thoại xong</param>
        public void ShowDialogue(string speaker, string[] lines, Action onComplete = null)
        {
            EnsureSentenceQueue();

            if (dialoguePanel == null)
            {
                Debug.LogWarning("Dialogue Panel chưa được gán trong Inspector!");
                onComplete?.Invoke();
                return;
            }

            // Khóa di chuyển của người chơi
            if (playerController == null) playerController = FindAnyObjectByType<PlayerController>();
            if (playerController != null) playerController.SetFrozen(true);

            sentences.Clear();
            foreach (string line in lines)
            {
                sentences.Enqueue(line);
            }

            speakerNameText.text = speaker;
            onDialogueComplete = onComplete;
            dialoguePanel.SetActive(true);

            DisplayNextSentence();
        }

        private void EnsureSentenceQueue()
        {
            if (sentences == null)
            {
                sentences = new Queue<string>();
            }
        }

        private void DisplayNextSentence()
        {
            if (voiceSource != null && voiceSource.isPlaying)
            {
                voiceSource.Stop();
            }

            if (sentences.Count == 0)
            {
                EndDialogue();
                return;
            }

            currentSentence = sentences.Dequeue();
            PlayVoiceForSentence(currentSentence);
            StartCoroutine(TypeSentence(currentSentence));
        }

        private IEnumerator TypeSentence(string sentence)
        {
            dialogueText.text = "";
            isTyping = true;
            if (continueIndicator != null) continueIndicator.SetActive(false);

            foreach (char letter in sentence.ToCharArray())
            {
                dialogueText.text += letter;
                yield return new WaitForSeconds(typingSpeed);
            }

            isTyping = false;
            if (continueIndicator != null) continueIndicator.SetActive(true);
        }

        private void EndDialogue()
        {
            if (voiceSource != null && voiceSource.isPlaying)
            {
                voiceSource.Stop();
            }

            dialoguePanel.SetActive(false);
            if (continueIndicator != null) continueIndicator.SetActive(false);

            // Mở khóa di chuyển cho người chơi
            if (playerController != null) playerController.SetFrozen(false);

            // Kích hoạt callback khi thoại xong (nếu có)
            onDialogueComplete?.Invoke();
        }

        private void PlayVoiceForSentence(string sentence)
        {
            if (voiceSource == null) return;

            string clipPath = null;
            string lower = sentence.ToLower();

            // Phase 1 Voice-overs
            if (lower.Contains("dậy rồi đó hả con"))
            {
                clipPath = "Audio/Phase1/Câu thoại 1";
            }
            else if (lower.Contains("sáng nay trời mát mẻ"))
            {
                clipPath = "Audio/Phase1/Câu thoại 2";
            }
            else if (lower.Contains("mà nè, ông ngoại có cái này"))
            {
                clipPath = "Audio/Phase1/Câu thoại 3";
            }
            else if (lower.Contains("đây là chiếc máy ảnh"))
            {
                clipPath = "Audio/Phase1/Câu thoại 4";
            }
            else if (lower.Contains("đi chơi rừng đẹp lắm"))
            {
                clipPath = "Audio/Phase1/Câu thoại 5";
            }
            else if (lower.Contains("cây xoài to đằng kia"))
            {
                clipPath = "Audio/Phase1/Câu thoại 6";
            }
            else if (lower.Contains("con cứ từ từ thử xem"))
            {
                clipPath = "Audio/Phase1/Câu thoại 7";
            }
            else if (lower.Contains("đâu, đưa ông coi tấm hình"))
            {
                clipPath = "Audio/Phase1/Câu thoại 8";
            }
            else if (lower.Contains("thôi, chuẩn bị đồ đạc"))
            {
                clipPath = "Audio/Phase1/Câu thoại 9";
            }
            else if (lower.Contains("mau xuống xuồng đi con"))
            {
                clipPath = "Audio/Phase1/Câu thoại 10";
            }
            // Phase 2 Voice-overs
            else if (lower.Contains("sông nước miền tây mình rộng lớn"))
            {
                clipPath = "Audio/Phase2/Câu thoại 11";
            }
            else if (lower.Contains("bèo tấm phủ xanh um"))
            {
                clipPath = "Audio/Phase2/Câu thoại 12";
            }
            else if (lower.Contains("tràm mọc san sát nhau, che mát"))
            {
                clipPath = "Audio/Phase2/Câu thoại 13";
            }
            else if (lower.Contains("tới checkpoint rồi nè con"))
            {
                clipPath = "Audio/Phase2/Câu thoại 14";
            }
            else if (lower.Contains("con lấy máy ảnh ra sẵn đi"))
            {
                clipPath = "Audio/Phase2/Câu thoại 15";
            }
            else if (lower.Contains("chụp dính rồi kìa. được mấy tấm hình"))
            {
                clipPath = "Audio/Phase2/Câu thoại 16";
            }
            else if (lower.Contains("chụp dính con sếu đầu đỏ"))
            {
                clipPath = "Audio/Phase2/Câu thoại 17";
            }
            // Phase 3 Voice-overs
            else if (lower.Contains("nước trôi lững lờ mát mẻ"))
            {
                clipPath = "Audio/Phase3/Câu thoại 18";
            }
            else if (lower.Contains("chốt gác kiểm lâm cao nghệu"))
            {
                clipPath = "Audio/Phase3/Câu thoại 19";
            }
            else if (lower.Contains("nhà sàn đầm lầy của mấy chú"))
            {
                clipPath = "Audio/Phase3/Câu thoại 20";
            }
            else if (lower.Contains("mấy vệt nắng chiếu xiên"))
            {
                clipPath = "Audio/Phase3/Câu thoại 21";
            }
            else if (lower.Contains("hồi ngoại còn nhỏ bằng con, vùng đất này sếu đầu đỏ"))
            {
                clipPath = "Audio/Phase3/Câu thoại 22";
            }
            else if (lower.Contains("tiếc là sau này thiên nhiên thay đổi"))
            {
                clipPath = "Audio/Phase3/Câu thoại 23";
            }
            else if (lower.Contains("ngoại mong rừng tràm mình giữ được nét hoang sơ"))
            {
                clipPath = "Audio/Phase3/Câu thoại 24";
            }
            // Phase 4 Voice-overs
            else if (lower.Contains("tới khu đầm lầy bảo tồn rồi nè con"))
            {
                clipPath = "Audio/Phase4/Câu thoại 25";
            }
            else if (lower.Contains("vùng này chim chóc với động vật hoang dã"))
            {
                clipPath = "Audio/Phase4/Câu thoại 26";
            }
            else if (lower.Contains("hồi xưa sếu đầu đỏ tụi nó về nghẹt đất"))
            {
                clipPath = "Audio/Phase4/Câu thoại 27";
            }
            else if (lower.Contains("con đi nhẹ nhàng thôi nhen, khom khom cúi người"))
            {
                clipPath = "Audio/Phase4/Câu thoại 28";
            }
            else if (lower.Contains("chụp đủ năm loài động vật hoang dã"))
            {
                clipPath = "Audio/Phase4/Câu thoại 29";
            }
            else if (lower.Contains("cò trắng đậu trên cành tràm đẹp quá con ơi. loài cò này nhát"))
            {
                clipPath = "Audio/Phase4/Câu thoại 31";
            }
            else if (lower.Contains("rắn nước đó con! loài này hiền khô"))
            {
                clipPath = "Audio/Phase4/Câu thoại 32";
            }
            else if (lower.Contains("cá lóc quẫy nước nhảy lên kìa"))
            {
                clipPath = "Audio/Phase4/Câu thoại 33";
            }
            else if (lower.Contains("bướm hoa súng lượn vòng vòng nè"))
            {
                clipPath = "Audio/Phase4/Câu thoại 34";
            }
            else if (lower.Contains("mấy chú vịt trời đang bơi bập bềnh"))
            {
                clipPath = "Audio/Phase4/Câu thoại 35";
            }
            else if (lower.Contains("con chụp khéo quá! chụp được đủ hết năm loài"))
            {
                clipPath = "Audio/Phase4/Câu thoại 36";
            }
            else if (lower.Contains("cảnh hoàng hôn sắp buông xuống rồi kìa con ơi"))
            {
                clipPath = "Audio/Phase4/Câu thoại 37";
            }
            else if (lower.Contains("lên đỉnh tháp quan sát đằng kia ngắm toàn cảnh"))
            {
                clipPath = "Audio/Phase4/Câu thoại 38";
            }
            // Phase 5 Voice-overs
            else if (lower.Contains("đẹp hông con? ông sống ở đây mấy chục năm"))
            {
                clipPath = "Audio/Phase5/Câu thoại 39";
            }
            else if (lower.Contains("toàn bộ rừng tràm mình chìm trong màu nắng chiều"))
            {
                clipPath = "Audio/Phase5/Câu thoại 40";
            }
            else if (lower.Contains("đi chơi cả ngày rồi, con dùng chiếc máy ảnh chụp"))
            {
                clipPath = "Audio/Phase5/Câu thoại 41";
            }

            if (!string.IsNullOrEmpty(clipPath))
            {
                AudioClip clip = Resources.Load<AudioClip>(clipPath);
                if (clip != null)
                {
                    voiceSource.PlayOneShot(clip);
                }
                else
                {
                    Debug.LogWarning($"[DialogueManager] Không tìm thấy file lồng tiếng tại: Resources/{clipPath}");
                }
            }
        }
    }
}
