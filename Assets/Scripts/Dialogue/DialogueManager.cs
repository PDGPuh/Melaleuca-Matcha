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

        private void Awake()
        {
            Instance = this;

            sentences = new Queue<string>();

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
                if (Mouse.current.leftButton.wasPressedThisFrame || 
                    Keyboard.current.spaceKey.wasPressedThisFrame || 
                    Keyboard.current.enterKey.wasPressedThisFrame)
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

        private void DisplayNextSentence()
        {
            if (sentences.Count == 0)
            {
                EndDialogue();
                return;
            }

            currentSentence = sentences.Dequeue();
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
            dialoguePanel.SetActive(false);
            if (continueIndicator != null) continueIndicator.SetActive(false);

            // Mở khóa di chuyển cho người chơi
            if (playerController != null) playerController.SetFrozen(false);

            // Kích hoạt callback khi thoại xong (nếu có)
            onDialogueComplete?.Invoke();
        }
    }
}
