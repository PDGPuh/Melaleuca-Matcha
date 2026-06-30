using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

namespace RungTramTraSu
{
    /// <summary>
    /// Hiển thị panel kết quả ảnh chụp: ảnh thực, tên chủ thể, mô tả, badge quý hiếm.
    /// Singleton tự khởi tạo, tồn tại xuyên scene (DontDestroyOnLoad).
    /// Không cần setup tay trong Inspector.
    /// </summary>
    public class PhotoResultUI : MonoBehaviour
    {
        // ─── Singleton lazy-init ───────────────────────────────────────────────
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

        // ─── Settings ─────────────────────────────────────────────────────────
        [Header("Settings")]
        [SerializeField] private float autoCloseDelay = 6f; // Tăng lên 6s để kịp coi hình hiện và đọc chữ

        // ─── Runtime UI references (auto-built) ──────────────────────────────
        private GameObject     panel;
        private RawImage       photoDisplay;
        private TextMeshProUGUI subjectNameText;
        private TextMeshProUGUI descriptionText;
        private TextMeshProUGUI rareBadgeText;
        private RectTransform  countdownBarFill;
        private Button         closeButton;

        private RectTransform  cardRT;
        private RectTransform  shadowRT;

        // ─── State ────────────────────────────────────────────────────────────
        private Action   onCloseCallback;
        private Coroutine autoCloseCoroutine;
        private Coroutine developCoroutine;
        private bool     isShowing = false;

        // ─── Lifecycle ────────────────────────────────────────────────────────
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

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Hiển thị panel kết quả ảnh chụp.
        /// </summary>
        /// <param name="photo">Texture2D ảnh chụp thực tế</param>
        /// <param name="subjectName">Tên chủ thể (ví dụ: "Cò trắng")</param>
        /// <param name="description">Mô tả ngắn về chủ thể</param>
        /// <param name="isRare">Nếu true, hiện badge vàng "LOÀI QUÝ HIẾM"</param>
        /// <param name="onClose">Callback sau khi panel đóng</param>
        public void ShowResult(Texture2D photo, string subjectName, string description,
                               bool isRare = false, Action onClose = null)
        {
            if (panel == null)
            {
                Debug.LogWarning("[PhotoResultUI] Panel chưa được tạo. Gọi callback ngay.");
                onClose?.Invoke();
                return;
            }

            onCloseCallback = onClose;
            isShowing = true;

            // Gán ảnh
            if (photoDisplay != null) photoDisplay.texture = photo;

            // Reset countdown bar
            if (countdownBarFill != null)
            {
                countdownBarFill.anchorMax = new Vector2(1f, 1f);
            }

            panel.SetActive(true);

            // Freeze player + show cursor
            FreezePlayer(true);

            // Chạy animation Polaroid trượt lên + hiện ảnh + viết chữ
            if (developCoroutine != null) StopCoroutine(developCoroutine);
            developCoroutine = StartCoroutine(DevelopPhotoRoutine(subjectName, description, isRare));

            // Bắt đầu đếm ngược tự đóng
            if (autoCloseCoroutine != null) StopCoroutine(autoCloseCoroutine);
            autoCloseCoroutine = StartCoroutine(AutoCloseRoutine());
        }

        /// <summary>Đóng panel thủ công (gọi từ nút hoặc phím).</summary>
        public void CloseResult()
        {
            if (!isShowing) return;
            isShowing = false;

            if (developCoroutine != null)
            {
                StopCoroutine(developCoroutine);
                developCoroutine = null;
            }

            if (autoCloseCoroutine != null)
            {
                StopCoroutine(autoCloseCoroutine);
                autoCloseCoroutine = null;
            }

            if (panel != null) panel.SetActive(false);

            FreezePlayer(false);

            Action cb = onCloseCallback;
            onCloseCallback = null;
            cb?.Invoke();
        }

        // ─── Private Helpers ─────────────────────────────────────────────────

        private IEnumerator DevelopPhotoRoutine(string subjectName, string description, bool isRare)
        {
            // Thiết lập màu tối ban đầu cho ảnh (chưa hiện hình như phôi ảnh Polaroid)
            if (photoDisplay != null)
            {
                photoDisplay.color = new Color(0.08f, 0.09f, 0.08f);
            }
            if (subjectNameText != null) subjectNameText.text = "";
            if (descriptionText != null) descriptionText.text = "";
            if (rareBadgeText != null) rareBadgeText.gameObject.SetActive(false);

            // Đặt vị trí ban đầu của card ở dưới màn hình
            Vector2 cardStartPos = new Vector2(0f, -850f);
            Vector2 cardTargetPos = new Vector2(0f, 0f);
            Vector2 shadowStartPos = new Vector2(8f, -858f);
            Vector2 shadowTargetPos = new Vector2(8f, -8f);

            if (cardRT != null) cardRT.anchoredPosition = cardStartPos;
            if (shadowRT != null) shadowRT.anchoredPosition = shadowStartPos;

            // 1. Hiệu ứng trượt card lên (0.8s) với một chút bounce mượt
            float duration = 0.8f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                
                // Ease out Back
                float s = 1.15f; // độ bounce nhè nhẹ
                t = t - 1f;
                float curve = t * t * ((s + 1f) * t + s) + 1f;

                if (cardRT != null)
                    cardRT.anchoredPosition = Vector2.LerpUnclamped(cardStartPos, cardTargetPos, curve);
                if (shadowRT != null)
                    shadowRT.anchoredPosition = Vector2.LerpUnclamped(shadowStartPos, shadowTargetPos, curve);

                yield return null;
            }
            if (cardRT != null) cardRT.anchoredPosition = cardTargetPos;
            if (shadowRT != null) shadowRT.anchoredPosition = shadowTargetPos;

            // 2. Chờ 0.2s rồi bắt đầu "hiện hình" (hóa chất phản ứng) + viết chữ bút dạ
            yield return new WaitForSecondsRealtime(0.2f);

            // Bắt đầu viết tên chủ thể (typewriter style)
            if (subjectNameText != null)
            {
                string textToType = subjectName;
                for (int i = 0; i <= textToType.Length; i++)
                {
                    subjectNameText.text = textToType.Substring(0, i);
                    yield return new WaitForSecondsRealtime(0.04f);
                }
            }

            // Hiện Badge hiếm nếu có
            if (isRare && rareBadgeText != null)
            {
                rareBadgeText.gameObject.SetActive(true);
                rareBadgeText.transform.localScale = Vector3.zero;
                float badgeElapsed = 0f;
                while (badgeElapsed < 0.25f)
                {
                    badgeElapsed += Time.unscaledDeltaTime;
                    rareBadgeText.transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one * 1.15f, badgeElapsed / 0.25f);
                    yield return null;
                }
                rareBadgeText.transform.localScale = Vector3.one;
            }

            // Bắt đầu gõ mô tả
            if (descriptionText != null)
            {
                string textToType = description;
                for (int i = 0; i <= textToType.Length; i++)
                {
                    descriptionText.text = textToType.Substring(0, i);
                    yield return new WaitForSecondsRealtime(0.015f);
                }
            }

            // Đồng thời chạy hiệu ứng phai màu ảnh (Polaroid development) sang sáng rõ màu sắc
            float devDuration = 2.2f;
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
        }

        private IEnumerator AutoCloseRoutine()
        {
            float elapsed = 0f;
            while (elapsed < autoCloseDelay)
            {
                elapsed += Time.unscaledDeltaTime;
                float progress = 1f - (elapsed / autoCloseDelay);
                if (countdownBarFill != null)
                {
                    countdownBarFill.anchorMax = new Vector2(progress, 1f);
                }
                yield return null;
            }
            CloseResult();
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

        // ─── Runtime UI Builder ───────────────────────────────────────────────

        private void BuildUI()
        {
            // Tìm Canvas phù hợp hoặc tạo mới
            Canvas canvas = FindOrCreateCanvas();

            // ── Root Panel (dim overlay) ──
            panel = CreateRT("PhotoResultPanel", canvas.transform,
                              Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image dimBg = panel.AddComponent<Image>();
            dimBg.color = new Color(0f, 0f, 0f, 0.78f);
            panel.SetActive(false);

            // ── Photo Card (polaroid frame) ──
            GameObject card = CreateRT("PhotoCard", panel.transform,
                                       new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                       new Vector2(0f, 0f), new Vector2(640f, 540f));
            cardRT = card.GetComponent<RectTransform>();
            Image cardBg = card.AddComponent<Image>();
            cardBg.color = new Color(0.97f, 0.96f, 0.90f);

            // Bóng đổ card (fake bằng một image đen lệch phía sau)
            GameObject shadow = CreateRT("CardShadow", panel.transform,
                                         new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                         new Vector2(8f, -8f), new Vector2(640f, 540f));
            shadowRT = shadow.GetComponent<RectTransform>();
            Image shadowImg = shadow.AddComponent<Image>();
            shadowImg.color = new Color(0f, 0f, 0f, 0.35f);
            shadow.transform.SetSiblingIndex(0); // Đặt sau card

            // Viền đen ảnh (outline effect) - tạo trước để nằm dưới ảnh
            Image photoOutline = CreateRT("PhotoOutline", card.transform,
                                          new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                          new Vector2(0f, -26f), new Vector2(594f, 337f))
                                 .AddComponent<Image>();
            photoOutline.color = new Color(0.1f, 0.1f, 0.1f, 0.6f);

            // ── Photo Display ──
            GameObject photoGo = CreateRT("PhotoDisplay", card.transform,
                                          new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                          new Vector2(0f, -28f), new Vector2(588f, 331f));
            photoGo.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);
            photoDisplay = photoGo.AddComponent<RawImage>();
            photoDisplay.color = Color.white;

            // ── Rare Badge ──
            GameObject badgeGo = CreateRT("RareBadgeText", card.transform,
                                          new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                          new Vector2(0f, -372f), new Vector2(560f, 34f));
            badgeGo.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);
            rareBadgeText = badgeGo.AddComponent<TextMeshProUGUI>();
            rareBadgeText.text = "⭐  LOÀI QUÝ HIẾM!  ⭐";
            rareBadgeText.alignment = TextAlignmentOptions.Center;
            rareBadgeText.fontSize = 20f;
            rareBadgeText.fontStyle = FontStyles.Bold;
            rareBadgeText.color = new Color(1f, 0.84f, 0f); // Vàng gold
            badgeGo.SetActive(false);

            // ── Subject Name ──
            GameObject nameGo = CreateRT("SubjectNameText", card.transform,
                                         new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                         new Vector2(0f, -370f), new Vector2(560f, 48f));
            nameGo.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);
            subjectNameText = nameGo.AddComponent<TextMeshProUGUI>();
            subjectNameText.alignment = TextAlignmentOptions.Center;
            subjectNameText.fontSize = 30f;
            subjectNameText.fontStyle = FontStyles.Bold;
            subjectNameText.color = new Color(0.12f, 0.08f, 0.04f);

            // ── Description ──
            GameObject descGo = CreateRT("DescriptionText", card.transform,
                                         new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                                         new Vector2(0f, -422f), new Vector2(560f, 72f));
            descGo.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 1f);
            descriptionText = descGo.AddComponent<TextMeshProUGUI>();
            descriptionText.alignment = TextAlignmentOptions.Center;
            descriptionText.fontSize = 15f;
            descriptionText.fontStyle = FontStyles.Italic;
            descriptionText.color = new Color(0.3f, 0.22f, 0.14f);
            descriptionText.enableWordWrapping = true;

            // ── Countdown Bar (background) ──
            GameObject barBgGo = CreateRT("CountdownBarBg", card.transform,
                                          new Vector2(0f, 0f), new Vector2(1f, 0f),
                                          new Vector2(0f, 8f), new Vector2(-40f, 7f));
            barBgGo.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);
            Image barBgImg = barBgGo.AddComponent<Image>();
            barBgImg.color = new Color(0.55f, 0.55f, 0.55f, 0.4f);

            // ── Countdown Bar (fill) ──
            GameObject barFillGo = CreateRT("CountdownBarFill", barBgGo.transform,
                                            Vector2.zero, Vector2.one,
                                            Vector2.zero, Vector2.zero);
            countdownBarFill = barFillGo.GetComponent<RectTransform>();
            Image barFillImg = barFillGo.AddComponent<Image>();
            barFillImg.color = new Color(0.18f, 0.75f, 0.38f);

            // ── Close Button ──
            GameObject btnGo = CreateRT("CloseButton", card.transform,
                                        new Vector2(1f, 0f), new Vector2(1f, 0f),
                                        new Vector2(-20f, 16f), new Vector2(148f, 40f));
            btnGo.GetComponent<RectTransform>().pivot = new Vector2(1f, 0f);
            Image btnImg = btnGo.AddComponent<Image>();
            btnImg.color = new Color(0.15f, 0.15f, 0.15f, 0.88f);
            closeButton = btnGo.AddComponent<Button>();
            closeButton.targetGraphic = btnImg;

            // Hiệu ứng hover
            ColorBlock cb = closeButton.colors;
            cb.normalColor   = new Color(0.15f, 0.15f, 0.15f, 0.88f);
            cb.highlightedColor = new Color(0.3f, 0.3f, 0.3f);
            cb.pressedColor  = new Color(0.05f, 0.05f, 0.05f);
            closeButton.colors = cb;
            closeButton.onClick.AddListener(CloseResult);

            GameObject btnTextGo = CreateRT("ButtonText", btnGo.transform,
                                            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            TextMeshProUGUI btnTmp = btnTextGo.AddComponent<TextMeshProUGUI>();
            btnTmp.text = "Đóng  [Space]";
            btnTmp.alignment = TextAlignmentOptions.Center;
            btnTmp.fontSize = 14f;
            btnTmp.color = new Color(0.9f, 0.9f, 0.9f);

            // Photo hint text bottom-left
            GameObject hintGo = CreateRT("HintText", card.transform,
                                         new Vector2(0f, 0f), new Vector2(0f, 0f),
                                         new Vector2(20f, 16f), new Vector2(260f, 40f));
            hintGo.GetComponent<RectTransform>().pivot = Vector2.zero;
            TextMeshProUGUI hintTmp = hintGo.AddComponent<TextMeshProUGUI>();
            hintTmp.text = "Nhật ký ảnh [Tab] để xem lại";
            hintTmp.fontSize = 12f;
            hintTmp.color = new Color(0.5f, 0.4f, 0.3f, 0.8f);
        }

        private Canvas FindOrCreateCanvas()
        {
            // Ưu tiên dùng Canvas có sẵn trong scene với sort order cao
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            Canvas best = null;
            foreach (var c in canvases)
            {
                if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    if (best == null || c.sortingOrder > best.sortingOrder)
                        best = c;
                }
            }
            if (best != null) return best;

            // Tạo Canvas mới nếu không tìm thấy
            GameObject canvasGo = new GameObject("[PhotoResultCanvas]");
            canvasGo.transform.SetParent(transform);
            DontDestroyOnLoad(canvasGo);

            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200; // Trên tất cả UI khác

            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            // Thêm EventSystem nếu chưa có
            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject esGo = new GameObject("[EventSystem]");
                esGo.transform.SetParent(transform);
                DontDestroyOnLoad(esGo);
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            return canvas;
        }

        /// <summary>Tạo RectTransform với anchor/offset cho trước.</summary>
        private static GameObject CreateRT(string name, Transform parent,
                                           Vector2 anchorMin, Vector2 anchorMax,
                                           Vector2 anchoredPos, Vector2 sizeDelta)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin       = anchorMin;
            rt.anchorMax       = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta       = sizeDelta;
            return go;
        }
    }
}
