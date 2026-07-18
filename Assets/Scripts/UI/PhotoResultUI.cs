using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using RungTramTraSu.CameraSystem;

namespace RungTramTraSu
{
    /// <summary>
    /// Displays Polaroid photo results with scientific descriptions, 5-star grading, 
    /// mode badges (Auto/Manual), and detailed score breakdowns.
    /// In non-blocking Polaroid HUD popup mode, it slides up from the bottom-right corner,
    /// develops in real-time while the player can continue playing, and auto-closes.
    /// </summary>
    public class PhotoResultUI : MonoBehaviour
    {
        private static PhotoResultUI _instance;
        public static PhotoResultUI Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("[PhotoResultUI]");
                    _instance = go.AddComponent<PhotoResultUI>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("Settings")]
        [SerializeField] private float autoCloseDelay = 4f;

        private GameObject panel;
        private RawImage photoDisplay;
        private TextMeshProUGUI subjectNameText;
        private TextMeshProUGUI descriptionText;
        private TextMeshProUGUI rareBadgeText;
        private TextMeshProUGUI starsText;
        private TextMeshProUGUI scoreBreakdownText;
        private Button closeButton;

        private RectTransform cardRT;
        private RectTransform shadowRT;

        private Action onCloseCallback;
        private Coroutine developCoroutine;
        private Coroutine closeCoroutine;
        private bool isShowing = false;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            BuildUI();
        }

        private void Update()
        {
            if (!isShowing) return;
            if (Keyboard.current != null &&
                (Keyboard.current.spaceKey.wasPressedThisFrame ||
                 Keyboard.current.enterKey.wasPressedThisFrame))
            {
                CloseResult();
            }
        }

        public void ShowResult(Texture2D photo, string subjectName, string description,
                               bool isRare = false, Action onClose = null)
        {
            if (panel == null)
            {
                BuildUI();
            }

            onCloseCallback = onClose;
            isShowing = true;

            if (photoDisplay != null) photoDisplay.texture = photo;

            if (closeCoroutine != null)
            {
                StopCoroutine(closeCoroutine);
                closeCoroutine = null;
            }

            panel.SetActive(true);
            
            // Non-blocking Polaroid HUD: keep player unfrozen, cursor locked
            FreezePlayer(false);

            if (developCoroutine != null) StopCoroutine(developCoroutine);
            developCoroutine = StartCoroutine(DevelopPhotoRoutine(subjectName, description, isRare));
        }

        public void CloseResult()
        {
            if (!isShowing) return;
            isShowing = false;

            if (developCoroutine != null)
            {
                StopCoroutine(developCoroutine);
                developCoroutine = null;
            }

            if (closeCoroutine != null) StopCoroutine(closeCoroutine);
            closeCoroutine = StartCoroutine(SlideOutAndCloseRoutine());
        }

        private IEnumerator SlideOutAndCloseRoutine()
        {
            Vector2 cardStartPos = cardRT != null ? cardRT.anchoredPosition : new Vector2(0f, 0f);
            Vector2 cardTargetPos = new Vector2(0f, -850f);
            Vector2 shadowStartPos = shadowRT != null ? shadowRT.anchoredPosition : new Vector2(8f, -8f);
            Vector2 shadowTargetPos = new Vector2(8f, -858f);

            float duration = 0.5f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                float curve = t * t; // Ease in

                if (cardRT != null)
                    cardRT.anchoredPosition = Vector2.Lerp(cardStartPos, cardTargetPos, curve);
                if (shadowRT != null)
                    shadowRT.anchoredPosition = Vector2.Lerp(shadowStartPos, shadowTargetPos, curve);

                yield return null;
            }

            if (cardRT != null) cardRT.anchoredPosition = cardTargetPos;
            if (shadowRT != null) shadowRT.anchoredPosition = shadowTargetPos;

            if (panel != null) panel.SetActive(false);

            FreezePlayer(false);

            Action cb = onCloseCallback;
            onCloseCallback = null;
            cb?.Invoke();
        }

        private IEnumerator DevelopPhotoRoutine(string subjectName, string description, bool isRare)
        {
            if (photoDisplay != null)
            {
                photoDisplay.color = new Color(0.08f, 0.09f, 0.08f);
            }
            if (subjectNameText != null) subjectNameText.text = "";
            if (descriptionText != null) descriptionText.text = "";
            if (rareBadgeText != null) rareBadgeText.transform.parent.gameObject.SetActive(false);
            if (starsText != null) starsText.text = "";
            if (scoreBreakdownText != null) scoreBreakdownText.text = "";

            Vector2 cardStartPos = new Vector2(0f, -850f);
            Vector2 cardTargetPos = new Vector2(0f, 0f);
            Vector2 shadowStartPos = new Vector2(8f, -858f);
            Vector2 shadowTargetPos = new Vector2(8f, -8f);

            if (cardRT != null) cardRT.anchoredPosition = cardStartPos;
            if (shadowRT != null) shadowRT.anchoredPosition = shadowStartPos;

            // Slide Card Up
            float duration = 0.8f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                float s = 1.15f;
                t = t - 1f;
                float curve = t * t * ((s + 1f) * t + s) + 1f; // Overshoot bounce

                if (cardRT != null)
                    cardRT.anchoredPosition = Vector2.LerpUnclamped(cardStartPos, cardTargetPos, curve);
                if (shadowRT != null)
                    shadowRT.anchoredPosition = Vector2.LerpUnclamped(shadowStartPos, shadowTargetPos, curve);

                yield return null;
            }
            if (cardRT != null) cardRT.anchoredPosition = cardTargetPos;
            if (shadowRT != null) shadowRT.anchoredPosition = shadowTargetPos;

            yield return new WaitForSecondsRealtime(0.2f);

            // Fetch Score details directly from the last calculated score in CameraManager
            PhotoScoring.ScoreResult score = new PhotoScoring.ScoreResult();
            bool hasValidScore = false;
            
            if (CameraManager.Instance != null && !description.Contains("không nằm trong khung ngắm") && !description.Contains("Nhiệm vụ:"))
            {
                score = CameraManager.Instance.LastScore;
                hasValidScore = true;
            }

            // Type Subject Name
            if (subjectNameText != null)
            {
                string textToType = subjectName;
                for (int i = 0; i <= textToType.Length; i++)
                {
                    subjectNameText.text = textToType.Substring(0, i);
                    yield return new WaitForSecondsRealtime(0.03f);
                }
            }

            // Rare badge
            if (isRare && rareBadgeText != null)
            {
                rareBadgeText.transform.parent.gameObject.SetActive(true);
                rareBadgeText.transform.parent.localScale = Vector3.zero;
                float badgeElapsed = 0f;
                while (badgeElapsed < 0.25f)
                {
                    badgeElapsed += Time.unscaledDeltaTime;
                    rareBadgeText.transform.parent.localScale = Vector3.Lerp(Vector3.zero, Vector3.one * 1.1f, badgeElapsed / 0.25f);
                    yield return null;
                }
                rareBadgeText.transform.parent.localScale = Vector3.one;
            }

            // Type Description
            if (descriptionText != null)
            {
                string textToType = description;
                for (int i = 0; i <= textToType.Length; i++)
                {
                    descriptionText.text = textToType.Substring(0, i);
                    yield return new WaitForSecondsRealtime(0.012f);
                }
            }

            // Photo Development Effect
            float devDuration = 1.8f;
            float devElapsed = 0f;
            Color initialColor = new Color(0.08f, 0.09f, 0.08f);
            while (devElapsed < devDuration)
            {
                devElapsed += Time.unscaledDeltaTime;
                float t = devElapsed / devDuration;
                if (photoDisplay != null)
                {
                    photoDisplay.color = Color.Lerp(initialColor, Color.white, t);
                }
                yield return null;
            }
            if (photoDisplay != null) photoDisplay.color = Color.white;

            // Render Stars and Detailed breakdown
            if (hasValidScore)
            {
                string stars = "";
                for (int i = 0; i < 5; i++)
                {
                    stars += i < score.starRating ? "★" : "☆";
                }
                if (starsText != null)
                {
                    starsText.text = stars;
                }

                if (scoreBreakdownText != null)
                {
                    string breakdown = $"Nét: {score.focusScore:F0}/20 | Sáng: {score.exposureScore:F0}/20 | Cỡ: {score.sizeScore:F0}/20\n" +
                                       $"Bố cục: {score.compositionScore:F0}/15 | Hướng: {score.facingScore:F0}/15 | Trập: {score.motionBlurScore:F0}/10\n" +
                                       $"Thủ công: +{score.manualBonus:F0}đ | Tổng điểm: {score.totalScore:F0}/100";
                    scoreBreakdownText.text = breakdown;
                }
            }

            // Auto-close hold period
            float holdTimer = 0f;
            while (holdTimer < autoCloseDelay && isShowing)
            {
                holdTimer += Time.unscaledDeltaTime;
                yield return null;
            }

            if (isShowing)
            {
                CloseResult();
            }
        }

        private void FreezePlayer(bool freeze)
        {
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null) player.SetFrozen(freeze);

            if (freeze)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void BuildUI()
        {
            Canvas canvas = FindOrCreateCanvas();

            // Root Panel (transparent, non-blocking)
            panel = CreateRT("PhotoResultPanel", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image dimBg = panel.AddComponent<Image>();
            dimBg.color = Color.clear;
            dimBg.raycastTarget = false;
            panel.SetActive(false);

            // Polaroid Card (Compact corner UI size - now centered)
            GameObject card = CreateRT("PhotoCard", panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -850f), new Vector2(420f, 520f));
            cardRT = card.GetComponent<RectTransform>();
            cardRT.pivot = new Vector2(0.5f, 0.5f);
            Image cardBg = card.AddComponent<Image>();
            cardBg.color = new Color(0.97f, 0.96f, 0.92f);

            // Shadow
            GameObject shadow = CreateRT("CardShadow", panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(8f, -858f), new Vector2(420f, 520f));
            shadowRT = shadow.GetComponent<RectTransform>();
            shadowRT.pivot = new Vector2(0.5f, 0.5f);
            Image shadowImg = shadow.AddComponent<Image>();
            shadowImg.color = new Color(0f, 0f, 0f, 0.35f);
            shadow.transform.SetSiblingIndex(0);

            // Photo Outline
            Image photoOutline = CreateRT("PhotoOutline", card.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(380f, 290f))
                                 .AddComponent<Image>();
            photoOutline.color = new Color(0.1f, 0.1f, 0.1f, 0.6f);

            // Photo Display
            GameObject photoGo = CreateRT("PhotoDisplay", card.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(376f, 286f));
            photoGo.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);
            photoDisplay = photoGo.AddComponent<RawImage>();
            photoDisplay.color = Color.white;

            // Rare Badge
            GameObject badgeGo = CreateRT("RareBadgeText", card.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-170f, -32f), new Vector2(85f, 22f));
            badgeGo.GetComponent<RectTransform>().pivot = new Vector2(0f, 1f);
            Image badgeBg = badgeGo.AddComponent<Image>();
            badgeBg.color = new Color(0.85f, 0.15f, 0.15f, 0.9f);
            
            GameObject badgeTextGo = CreateRT("BadgeText", badgeGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            rareBadgeText = badgeTextGo.AddComponent<TextMeshProUGUI>();
            rareBadgeText.text = "QUÝ HIẾM";
            rareBadgeText.fontSize = 11f;
            rareBadgeText.fontStyle = FontStyles.Bold;
            rareBadgeText.alignment = TextAlignmentOptions.Center;
            rareBadgeText.color = Color.white;
            badgeGo.SetActive(false);

            // Stars Rating Text
            GameObject starsGo = CreateRT("StarsText", card.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -320f), new Vector2(380f, 26f));
            starsGo.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);
            starsText = starsGo.AddComponent<TextMeshProUGUI>();
            starsText.alignment = TextAlignmentOptions.Center;
            starsText.fontSize = 20f;
            starsText.color = new Color(1f, 0.78f, 0f);

            // Subject Name
            GameObject nameGo = CreateRT("SubjectNameText", card.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -348f), new Vector2(380f, 28f));
            nameGo.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);
            subjectNameText = nameGo.AddComponent<TextMeshProUGUI>();
            subjectNameText.alignment = TextAlignmentOptions.Center;
            subjectNameText.fontSize = 18f;
            subjectNameText.fontStyle = FontStyles.Bold;
            subjectNameText.color = new Color(0.12f, 0.08f, 0.04f);

            // Description
            GameObject descGo = CreateRT("DescriptionText", card.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -378f), new Vector2(380f, 54f));
            descGo.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);
            descriptionText = descGo.AddComponent<TextMeshProUGUI>();
            descriptionText.alignment = TextAlignmentOptions.Center;
            descriptionText.fontSize = 13f;
            descriptionText.color = new Color(0.2f, 0.16f, 0.12f);
            descriptionText.enableWordWrapping = true;

            // Separator Line
            GameObject sep = CreateRT("ResultSeparator", card.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -438f), new Vector2(380f, 2f));
            sep.AddComponent<Image>().color = new Color(0.85f, 0.8f, 0.75f);

            // Score Breakdown text
            GameObject scoreGo = CreateRT("ScoreBreakdownText", card.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -446f), new Vector2(380f, 44f));
            scoreGo.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);
            scoreBreakdownText = scoreGo.AddComponent<TextMeshProUGUI>();
            scoreBreakdownText.alignment = TextAlignmentOptions.Center;
            scoreBreakdownText.fontSize = 11f;
            scoreBreakdownText.color = new Color(0.25f, 0.22f, 0.18f);
            scoreBreakdownText.enableWordWrapping = true;

            // Close button
            GameObject btnGo = CreateRT("CloseButton", card.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-15f, 12f), new Vector2(95f, 26f));
            btnGo.GetComponent<RectTransform>().pivot = new Vector2(1f, 0f);
            Image btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.18f, 0.15f, 0.12f, 1f);
            closeButton = btnGo.AddComponent<Button>();
            closeButton.targetGraphic = btnImg;
            closeButton.onClick.AddListener(CloseResult);

            GameObject btnTextGo = CreateRT("ButtonText", btnGo.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            TextMeshProUGUI btnTmp = btnTextGo.AddComponent<TextMeshProUGUI>();
            btnTmp.text = "Đóng [Space]";
            btnTmp.alignment = TextAlignmentOptions.Center;
            btnTmp.fontSize = 11f;
            btnTmp.color = Color.white;

            // Help Hint
            GameObject hintGo = CreateRT("HintText", card.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(15f, 12f), new Vector2(250f, 26f));
            hintGo.GetComponent<RectTransform>().pivot = Vector2.zero;
            TextMeshProUGUI hintTmp = hintGo.AddComponent<TextMeshProUGUI>();
            hintTmp.text = "Sổ nhật ký khoa học [Tab] để xem";
            hintTmp.fontSize = 11f;
            hintTmp.color = new Color(0.4f, 0.35f, 0.3f);
        }

        private Canvas FindOrCreateCanvas()
        {
            Transform existing = transform.Find("[PhotoResultCanvas]");
            if (existing != null)
            {
                Canvas c = existing.GetComponent<Canvas>();
                if (c != null) return c;
            }

            GameObject canvasGo = new GameObject("[PhotoResultCanvas]");
            canvasGo.transform.SetParent(transform, false);

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject esGo = new GameObject("[EventSystem]");
                esGo.transform.SetParent(transform);
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            return canvas;
        }

        private static GameObject CreateRT(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            return go;
        }
    }
}
