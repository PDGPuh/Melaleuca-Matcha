using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

namespace RungTramTraSu
{
    public class EndingDiaryController : MonoBehaviour
    {
        public static EndingDiaryController Instance { get; private set; }

        [Header("Diary References")]
        [SerializeField] private GameObject diaryCanvas;
        [SerializeField] private RectTransform bgPanel; // The main notebook panel
        [SerializeField] private TextMeshProUGUI diaryText;
        [SerializeField] private RawImage[] polaroidImages;

        [Header("Credits UI")]
        [SerializeField] private GameObject creditPanel;
        [SerializeField] private TextMeshProUGUI creditText;
        [SerializeField] private Button replayButton;

        [Header("Audio")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;

        private AudioClip donCaMusic;
        private AudioClip penScratchSFX;
        
        [Header("Content Settings")]
        [SerializeField, TextArea(6, 10)]
        private string diaryContent = 
            "Ngày... tháng... năm...\n" +
            "Chuyến đi rừng tràm Trà Sư cùng Ông Ngoại...\n\n" +
            "Mình chưa từng nghĩ quê hương An Giang lại đẹp hoang sơ và kỳ vĩ đến vậy. Màu xanh mướt của bèo tấm, tiếng chim cò líu lo, tia nắng rực rỡ lọc qua tán lá rừng sâu...\n\n" +
            "Lời ông ngoại dặn rất đúng: Thiên nhiên non nước hữu tình của mình, nếu chúng ta không gìn giữ và yêu thương, thì một ngày nào đó tụi nó sẽ biến mất mãi mãi...\n\n" +
            "Tặng ông, và những cánh chim đã xa...";

        [SerializeField, TextArea(10, 20)]
        private string creditContent =
            "RỪNG TRÀM TRÀ SƯ\n" +
            "Trò chơi trải nghiệm thiên nhiên Việt Nam\n\n\n" +
            "THÀNH VIÊN NHÓM PHÁT TRIỂN\n\n" +
            "Lập trình viên\n" +
            "Trương Chí Trung\n" +
            "Phạm Đinh Gia Phú\n" +
            "Nguyễn Phương Khải\n\n" +
            "Hỗ trợ kỹ thuật & Âm thanh\n" +
            "Đỗ Trọng Tín\n\n" +
            "Giảng viên hướng dẫn\n" +
            "Lại Đức Hùng\n\n\n" +
            "ÂM THANH & ÂM NHẠC\n\n" +
            "Hòa tấu Cổ Bản Vắn\n" +
            "Nnưt Thành Trí, Huỳnh Tiến,\nHữu Đức, Trung Thiện\n\n" +
            "Lồng tiếng Ông Ngoại\n" +
            "Thích Thanh\n\n\n" +
            "Cảm ơn bạn đã trải nghiệm trò chơi của chúng tôi!";

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // Setup audio sources
            if (musicSource == null) musicSource = gameObject.AddComponent<AudioSource>();
            if (sfxSource == null) sfxSource = gameObject.AddComponent<AudioSource>();

            musicSource.loop = true;
            musicSource.playOnAwake = false;

            // Load resources
            donCaMusic = Resources.Load<AudioClip>("Audio/Ending/DonCaTaiTu");
            penScratchSFX = CreateSyntheticScratchSound();
        }

        public void StartEndingSequence(GameObject canvas, RectTransform background, TextMeshProUGUI textComp, RawImage[] photos, Button replayBtn)
        {
            diaryCanvas = canvas;
            bgPanel = background;
            diaryText = textComp;
            polaroidImages = photos;
            replayButton = replayBtn;

            if (replayButton != null)
            {
                replayButton.onClick.RemoveAllListeners();
                replayButton.onClick.AddListener(ReplayGame);
            }

            Debug.Log($"[EndingDiary] StartEndingSequence called. canvas: {canvas?.name}, background: {background?.name}, textComp: {textComp?.name}, replayBtn: {replayBtn?.name}");
            StartCoroutine(EndingFlowRoutine());
        }

        private IEnumerator EndingFlowRoutine()
        {
            // 1. Fade out ambient sound
            StartCoroutine(FadeOutAmbientSounds(2.0f));

            // 2. Fade to black
            if (ScreenFader.Instance != null)
            {
                Debug.Log("[EndingDiary] Fading out screen to black...");
                bool fadeDone = false;
                ScreenFader.Instance.StartFadeOut(2.0f, () => fadeDone = true);
                yield return new WaitUntil(() => fadeDone);
            }
            else
            {
                Debug.LogWarning("[EndingDiary] ScreenFader.Instance is null, waiting 2s instead.");
                yield return new WaitForSeconds(2.0f);
            }

            // 3. Configure Ending UI
            ConfigureUI();

            // Enable canvas
            if (diaryCanvas != null)
            {
                // Detach from parent to fix Unity nested CanvasScaler scaling bug
                diaryCanvas.transform.SetParent(null, false);
                diaryCanvas.transform.localScale = Vector3.one;
                
                diaryCanvas.SetActive(true);
                
                // Force Canvas component and GraphicRaycaster to be enabled
                Canvas c = diaryCanvas.GetComponent<Canvas>();
                if (c != null)
                {
                    c.enabled = true;
                    c.renderMode = RenderMode.ScreenSpaceOverlay;
                    c.sortingOrder = 998; // Render on top of gameUI (0) but below FaderCanvas (999)
                    Debug.Log($"[EndingDiary] Canvas component enabled: {c.enabled}, renderMode: {c.renderMode}, sortingOrder: {c.sortingOrder}");
                }
                
                CanvasGroup cg = diaryCanvas.GetComponent<CanvasGroup>();
                if (cg == null) cg = diaryCanvas.AddComponent<CanvasGroup>();
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;

                // Set parent hierarchy scaling to 1 to avoid UI being scaled to zero
                Transform curr = diaryCanvas.transform;
                while (curr != null)
                {
                    curr.localScale = Vector3.one;
                    Debug.Log($"[EndingDiary] Forced localScale of {curr.name} to Vector3.one. ActiveSelf: {curr.gameObject.activeSelf}");
                    curr = curr.parent;
                }
            }
            else
            {
                Debug.LogError("[EndingDiary] diaryCanvas is null!");
            }

            // 4. Fade in screen back to show the diary
            if (ScreenFader.Instance != null)
            {
                Debug.Log("[EndingDiary] Fading in screen...");
                ScreenFader.Instance.StartFadeIn(1.5f);
            }

            // 5. Play music and fade it in
            if (donCaMusic != null)
            {
                Debug.Log("[EndingDiary] Playing đờn ca tài tử music...");
                musicSource.clip = donCaMusic;
                musicSource.volume = 0f;
                musicSource.Play();
                StartCoroutine(FadeInMusic(3.0f, 0.6f));
            }
            else
            {
                Debug.LogWarning("[EndingDiary] DonCaMusic audio clip not loaded!");
            }

            // 6. Typewriter effect
            if (diaryText != null)
            {
                Debug.Log("[EndingDiary] Starting typewriter diary text...");
                diaryText.text = "";
                yield return new WaitForSeconds(0.5f); // Pause before writing

                int charCount = 0;
                foreach (char c in diaryContent.ToCharArray())
                {
                    diaryText.text += c;
                    
                    // Play scratch sound periodically on printing letters (exclude spaces/newlines)
                    if (c != ' ' && c != '\n')
                    {
                        charCount++;
                        if (charCount % 2 == 0)
                        {
                            PlayScratchSound();
                        }
                    }

                    yield return new WaitForSeconds(UnityEngine.Random.Range(0.04f, 0.06f));
                }
                
                // Stop any lingering writing sounds immediately
                if (sfxSource != null)
                {
                    sfxSource.Stop();
                }
            }
            else
            {
                Debug.LogError("[EndingDiary] diaryText TextMeshPro component is null!");
            }

            // 7. Silent moment
            yield return new WaitForSeconds(3.0f);

            // 8. Fade out diary panel to black
            if (bgPanel != null)
            {
                Debug.Log("[EndingDiary] Fading out diary panel...");
                CanvasGroup cg = bgPanel.gameObject.GetComponent<CanvasGroup>();
                if (cg == null) cg = bgPanel.gameObject.AddComponent<CanvasGroup>();
                
                CanvasGroup shadowCg = null;
                Transform shadowTrans = diaryCanvas.transform.Find("BookShadow");
                if (shadowTrans != null)
                {
                    shadowCg = shadowTrans.gameObject.GetComponent<CanvasGroup>();
                    if (shadowCg == null) shadowCg = shadowTrans.gameObject.AddComponent<CanvasGroup>();
                }

                float elapsed = 0f;
                while (elapsed < 1.5f)
                {
                    elapsed += Time.deltaTime;
                    float alpha = Mathf.Lerp(1f, 0f, elapsed / 1.5f);
                    cg.alpha = alpha;
                    if (shadowCg != null) shadowCg.alpha = alpha;
                    yield return null;
                }
                bgPanel.gameObject.SetActive(false);
                if (shadowTrans != null) shadowTrans.gameObject.SetActive(false);
            }
            else
            {
                yield return new WaitForSeconds(1.5f);
            }

            // 9. Show Credits Panel
            if (creditPanel != null)
            {
                Debug.Log("[EndingDiary] Showing Credit Panel...");
                creditPanel.SetActive(true);
                CanvasGroup ccg = creditPanel.GetComponent<CanvasGroup>();
                if (ccg == null) ccg = creditPanel.AddComponent<CanvasGroup>();
                ccg.alpha = 0f;

                // Fade in credits panel
                float elapsed = 0f;
                while (elapsed < 1.5f)
                {
                    elapsed += Time.deltaTime;
                    ccg.alpha = Mathf.Lerp(0f, 1f, elapsed / 1.5f);
                    yield return null;
                }

                // 10. Scroll credits text
                if (creditText != null)
                {
                    creditText.text = creditContent;
                    RectTransform textRect = creditText.GetComponent<RectTransform>();
                    textRect.anchoredPosition = new Vector2(0, -500f); // Start from bottom

                    float scrollDuration = 18f;
                    float scrollElapsed = 0f;
                    while (scrollElapsed < scrollDuration)
                    {
                        scrollElapsed += Time.deltaTime;
                        float posY = Mathf.Lerp(-500f, 650f, scrollElapsed / scrollDuration);
                        textRect.anchoredPosition = new Vector2(0, posY);
                        yield return null;
                    }
                }
            }

            // 11. Show Replay Button
            if (replayButton != null)
            {
                Debug.Log("[EndingDiary] Showing Replay Button...");
                replayButton.gameObject.SetActive(true);
                CanvasGroup rcg = replayButton.gameObject.GetComponent<CanvasGroup>();
                if (rcg == null) rcg = replayButton.gameObject.AddComponent<CanvasGroup>();
                rcg.alpha = 0f;

                float relapsed = 0f;
                while (relapsed < 1.0f)
                {
                    relapsed += Time.deltaTime;
                    rcg.alpha = Mathf.Lerp(0f, 1f, relapsed / 1.0f);
                    yield return null;
                }

                // Lock cursor state to allow clicking the replay button
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void ConfigureUI()
        {
            if (diaryCanvas == null) return;

            Debug.Log("[EndingDiary] Configuring Ending UI...");
            
            // Auto configure CanvasScaler to match screen size beautifully
            var scaler = diaryCanvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = diaryCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // Make sure canvas scale is correct
            diaryCanvas.transform.localScale = Vector3.one;

            // Faded Overlay Panel background
            Transform overlayTrans = diaryCanvas.transform.Find("FadedOverlay");
            GameObject overlayObj;
            if (overlayTrans == null)
            {
                overlayObj = new GameObject("FadedOverlay");
                overlayObj.transform.SetParent(diaryCanvas.transform, false);
                overlayObj.transform.SetAsFirstSibling();
                
                var overlayRect = overlayObj.AddComponent<RectTransform>();
                overlayRect.anchorMin = Vector2.zero;
                overlayRect.anchorMax = Vector2.one;
                overlayRect.offsetMin = Vector2.zero;
                overlayRect.offsetMax = Vector2.zero;
                
                var overlayImg = overlayObj.AddComponent<Image>();
                overlayImg.color = new Color(0.06f, 0.04f, 0.02f, 0.9f);
                Debug.Log("[EndingDiary] Created FadedOverlay object.");
            }
            else
            {
                overlayObj = overlayTrans.gameObject;
                overlayObj.SetActive(true);
            }

            // Style notebook background panel to be centered
            if (bgPanel != null)
            {
                bgPanel.gameObject.SetActive(true);
                bgPanel.transform.localScale = Vector3.one;

                // Reset CanvasGroup if present
                CanvasGroup cg = bgPanel.GetComponent<CanvasGroup>();
                if (cg == null) cg = bgPanel.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 1f;

                // Adjust size to look like a premium open journal
                bgPanel.anchorMin = new Vector2(0.5f, 0.5f);
                bgPanel.anchorMax = new Vector2(0.5f, 0.5f);
                bgPanel.pivot = new Vector2(0.5f, 0.5f);
                bgPanel.sizeDelta = new Vector2(960f, 600f);
                bgPanel.anchoredPosition = Vector2.zero;

                var bgImg = bgPanel.GetComponent<Image>();
                if (bgImg != null)
                {
                    bgImg.color = new Color(0.95f, 0.92f, 0.88f, 1f); // Cozy paper warm color
                }

                // 1. Create Drop Shadow for the entire book panel
                Transform shadowTrans = diaryCanvas.transform.Find("BookShadow");
                GameObject shadowObj;
                if (shadowTrans == null)
                {
                    shadowObj = new GameObject("BookShadow");
                    shadowObj.transform.SetParent(diaryCanvas.transform, false);
                    shadowObj.transform.SetSiblingIndex(bgPanel.transform.GetSiblingIndex()); // right behind bgPanel
                    
                    var shadowRect = shadowObj.AddComponent<RectTransform>();
                    shadowRect.anchorMin = bgPanel.anchorMin;
                    shadowRect.anchorMax = bgPanel.anchorMax;
                    shadowRect.pivot = bgPanel.pivot;
                    shadowRect.sizeDelta = new Vector2(970f, 610f); // slightly larger
                    shadowRect.anchoredPosition = new Vector2(12f, -12f); // offset down/right
                    
                    var shadowImg = shadowObj.AddComponent<Image>();
                    shadowImg.color = new Color(0f, 0f, 0f, 0.4f); // transparent black shadow
                }
                else
                {
                    shadowObj = shadowTrans.gameObject;
                    shadowObj.SetActive(true);
                }

                // 2. Create Book Spine (center fold crease) in the middle
                Transform spineTrans = bgPanel.transform.Find("BookSpine");
                if (spineTrans == null)
                {
                    GameObject spineObj = new GameObject("BookSpine");
                    spineObj.transform.SetParent(bgPanel.transform, false);
                    
                    var spineRect = spineObj.AddComponent<RectTransform>();
                    spineRect.anchorMin = new Vector2(0.5f, 0.5f);
                    spineRect.anchorMax = new Vector2(0.5f, 0.5f);
                    spineRect.pivot = new Vector2(0.5f, 0.5f);
                    spineRect.sizeDelta = new Vector2(8f, 560f);
                    spineRect.anchoredPosition = Vector2.zero;
                    
                    var spineImg = spineObj.AddComponent<Image>();
                    spineImg.color = new Color(0.15f, 0.12f, 0.1f, 0.22f); // soft brown spine line
                }

                // 3. Create Ruled Page Lines on the Left Page
                Transform linesContainerTrans = bgPanel.transform.Find("LeftPageLines");
                if (linesContainerTrans == null)
                {
                    GameObject container = new GameObject("LeftPageLines");
                    container.transform.SetParent(bgPanel.transform, false);
                    var containerRect = container.AddComponent<RectTransform>();
                    containerRect.anchorMin = Vector2.zero;
                    containerRect.anchorMax = Vector2.one;
                    containerRect.offsetMin = Vector2.zero;
                    containerRect.offsetMax = Vector2.zero;
                    
                    // Spawn horizontal ruled lines
                    float startY = 190f;
                    float spacingY = 36f;
                    int numLines = 11;
                    for (int i = 0; i < numLines; i++)
                    {
                        GameObject lineObj = new GameObject("Line_" + i);
                        lineObj.transform.SetParent(container.transform, false);
                        var lineRect = lineObj.AddComponent<RectTransform>();
                        lineRect.anchorMin = new Vector2(0.5f, 0.5f);
                        lineRect.anchorMax = new Vector2(0.5f, 0.5f);
                        lineRect.pivot = new Vector2(0.5f, 0.5f);
                        lineRect.sizeDelta = new Vector2(400f, 1f); // line width matching text area
                        lineRect.anchoredPosition = new Vector2(-240f, startY - (i * spacingY));
                        
                        var lineImg = lineObj.AddComponent<Image>();
                        lineImg.color = new Color(0.18f, 0.15f, 0.12f, 0.1f); // very light brown line
                    }
                }
                else
                {
                    linesContainerTrans.gameObject.SetActive(true);
                }

                // Style font if font asset is generated
                TMP_FontAsset customFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/segoepr SDF");
                if (customFont == null) customFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/segoesc SDF");
                if (customFont != null && diaryText != null)
                {
                    diaryText.font = customFont;
                    Debug.Log($"[EndingDiary] Applied custom font: {customFont.name}");
                }
                else
                {
                    Debug.LogWarning("[EndingDiary] Custom font segoepr/segoesc not found in Resources. Using default TMPro font.");
                }

                if (diaryText != null)
                {
                    // Reposition DiaryText to fit perfectly inside Left Page
                    var dtRect = diaryText.GetComponent<RectTransform>();
                    dtRect.anchorMin = new Vector2(0.5f, 0.5f);
                    dtRect.anchorMax = new Vector2(0.5f, 0.5f);
                    dtRect.pivot = new Vector2(0.5f, 0.5f);
                    dtRect.sizeDelta = new Vector2(400f, 440f);
                    dtRect.anchoredPosition = new Vector2(-240f, -10f); // Center of left page
                    
                    diaryText.color = new Color(0.18f, 0.16f, 0.14f, 1f); // Dark handwriting ink
                    diaryText.lineSpacing = 16f;
                    diaryText.fontSize = 20f;
                    diaryText.alignment = TextAlignmentOptions.TopLeft;
                }
            }

            // 4. Hide all existing polaroid shadows on bgPanel first to avoid duplicates/orphans
            if (bgPanel != null)
            {
                for (int i = 0; i < bgPanel.transform.childCount; i++)
                {
                    Transform child = bgPanel.transform.GetChild(i);
                    if (child.name.EndsWith("_Shadow"))
                    {
                        child.gameObject.SetActive(false);
                    }
                }
            }

            // 5. Dynamically position and stylize polaroids on the right page
            if (polaroidImages != null)
            {
                List<GameObject> activeFrames = new List<GameObject>();
                foreach (var rImg in polaroidImages)
                {
                    if (rImg != null && rImg.transform.parent != null && rImg.transform.parent.gameObject.activeSelf)
                    {
                        activeFrames.Add(rImg.transform.parent.gameObject);
                    }
                }

                int count = activeFrames.Count;
                Debug.Log($"[EndingDiary] Laying out {count} visible polaroids on the right page.");

                Vector2[] positions = new Vector2[count];
                float[] rotations = new float[count];

                if (count == 1)
                {
                    positions[0] = new Vector2(240f, 10f);
                    rotations[0] = -3f;
                }
                else if (count == 2)
                {
                    positions[0] = new Vector2(170f, 100f);
                    rotations[0] = -5f;
                    positions[1] = new Vector2(310f, -100f);
                    rotations[1] = 4f;
                }
                else if (count == 3)
                {
                    positions[0] = new Vector2(170f, 140f);
                    rotations[0] = -6f;
                    positions[1] = new Vector2(310f, 60f);
                    rotations[1] = 5f;
                    positions[2] = new Vector2(240f, -120f);
                    rotations[2] = -2f;
                }
                else if (count == 4)
                {
                    positions[0] = new Vector2(170f, 150f);
                    rotations[0] = -5f;
                    positions[1] = new Vector2(310f, 130f);
                    rotations[1] = 4f;
                    positions[2] = new Vector2(160f, -110f);
                    rotations[2] = -3f;
                    positions[3] = new Vector2(300f, -130f);
                    rotations[3] = 6f;
                }
                else if (count >= 5)
                {
                    positions[0] = new Vector2(160f, 160f);
                    rotations[0] = -6f;
                    positions[1] = new Vector2(320f, 140f);
                    rotations[1] = 4f;
                    positions[2] = new Vector2(150f, -20f);
                    rotations[2] = -3f;
                    positions[3] = new Vector2(330f, -40f);
                    rotations[3] = 5f;
                    positions[4] = new Vector2(240f, -180f);
                    rotations[4] = -2f;
                }

                for (int i = 0; i < count; i++)
                {
                    GameObject frameObj = activeFrames[i];
                    RectTransform fRect = frameObj.GetComponent<RectTransform>();
                    if (fRect != null)
                    {
                        fRect.anchorMin = new Vector2(0.5f, 0.5f);
                        fRect.anchorMax = new Vector2(0.5f, 0.5f);
                        fRect.pivot = new Vector2(0.5f, 0.5f);
                        fRect.sizeDelta = new Vector2(150f, 175f);
                        fRect.anchoredPosition = positions[i];
                        fRect.localRotation = Quaternion.Euler(0f, 0f, rotations[i]);
                    }

                    Image fImg = frameObj.GetComponent<Image>();
                    if (fImg != null)
                    {
                        fImg.color = new Color(0.97f, 0.96f, 0.93f, 1f); // Polaroid frame color
                    }

                    CreatePolaroidShadow(frameObj, positions[i], rotations[i]);
                    CreatePolaroidTape(frameObj, rotations[i]);
                }
            }

            // Reposition Replay Button inside bgPanel so it hides/shows correctly
            if (replayButton != null)
            {
                replayButton.gameObject.SetActive(false); // Hide initially
                // Move button outside bgPanel directly onto canvas so it stays visible when bgPanel fades out!
                replayButton.transform.SetParent(diaryCanvas.transform, false);
                var btnRect = replayButton.GetComponent<RectTransform>();
                btnRect.anchorMin = new Vector2(0.5f, 0.15f);
                btnRect.anchorMax = new Vector2(0.5f, 0.15f);
                btnRect.pivot = new Vector2(0.5f, 0.5f);
                btnRect.anchoredPosition = Vector2.zero;
                btnRect.sizeDelta = new Vector2(200f, 50f);

                var btnText = replayButton.GetComponentInChildren<TextMeshProUGUI>();
                if (btnText != null)
                {
                    btnText.text = "Chơi Lại Từ Đầu";
                    btnText.color = Color.white;
                }
            }

            // Create Credits Panel dynamically
            Transform creditTrans = diaryCanvas.transform.Find("CreditPanel");
            if (creditTrans == null)
            {
                GameObject cp = new GameObject("CreditPanel");
                cp.transform.SetParent(diaryCanvas.transform, false);
                // Position behind replay button but in front of overlay
                if (replayButton != null) cp.transform.SetSiblingIndex(replayButton.transform.GetSiblingIndex() - 1);
                
                var cpRect = cp.AddComponent<RectTransform>();
                cpRect.anchorMin = Vector2.zero;
                cpRect.anchorMax = Vector2.one;
                cpRect.offsetMin = Vector2.zero;
                cpRect.offsetMax = Vector2.zero;

                // Transparent black panel
                var cpImg = cp.AddComponent<Image>();
                cpImg.color = Color.clear; // Let overlay handle background

                // Scroll container mask to hide overflowing text
                GameObject maskObj = new GameObject("MaskContainer");
                maskObj.transform.SetParent(cp.transform, false);
                var mRect = maskObj.AddComponent<RectTransform>();
                mRect.anchorMin = new Vector2(0.2f, 0.1f);
                mRect.anchorMax = new Vector2(0.8f, 0.9f);
                mRect.offsetMin = Vector2.zero;
                mRect.offsetMax = Vector2.zero;
                var mask = maskObj.AddComponent<Mask>();
                mask.showMaskGraphic = false;
                var maskImg = maskObj.AddComponent<Image>();
                maskImg.color = Color.black;

                // Scrollable text
                GameObject ctObj = new GameObject("CreditText");
                ctObj.transform.SetParent(maskObj.transform, false);
                var ctRect = ctObj.AddComponent<RectTransform>();
                ctRect.anchorMin = new Vector2(0.5f, 0f);
                ctRect.anchorMax = new Vector2(0.5f, 0f);
                ctRect.pivot = new Vector2(0.5f, 0.5f);
                ctRect.sizeDelta = new Vector2(700f, 900f);
                
                creditText = ctObj.AddComponent<TextMeshProUGUI>();
                creditText.fontSize = 20;
                creditText.color = new Color(0.95f, 0.92f, 0.85f);
                creditText.alignment = TextAlignmentOptions.Center;
                creditText.lineSpacing = 10f;

                creditPanel = cp;
            }
            else
            {
                creditPanel = creditTrans.gameObject;
                creditText = creditPanel.GetComponentInChildren<TextMeshProUGUI>();
            }

            creditPanel.SetActive(false); // Hide initially
        }

        private IEnumerator FadeOutAmbientSounds(float duration)
        {
            AudioSource[] sources = FindObjectsByType<AudioSource>();
            float elapsed = 0f;

            // Store initial volumes
            var initialVolumes = new Dictionary<AudioSource, float>();
            foreach (var src in sources)
            {
                if (src != musicSource && src != sfxSource && src.isPlaying)
                {
                    initialVolumes[src] = src.volume;
                }
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                foreach (var kvp in initialVolumes)
                {
                    if (kvp.Key != null)
                    {
                        kvp.Key.volume = Mathf.Lerp(kvp.Value, 0f, t);
                    }
                }
                yield return null;
            }

            foreach (var kvp in initialVolumes)
            {
                if (kvp.Key != null)
                {
                    kvp.Key.Stop();
                }
            }
        }

        private IEnumerator FadeInMusic(float duration, float targetVolume)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                musicSource.volume = Mathf.Lerp(0f, targetVolume, elapsed / duration);
                yield return null;
            }
            musicSource.volume = targetVolume;
        }

        private void CreatePolaroidShadow(GameObject frameObj, Vector2 position, float rotation)
        {
            if (bgPanel == null || frameObj == null) return;

            string shadowName = frameObj.name + "_Shadow";
            Transform existingShadow = bgPanel.transform.Find(shadowName);
            if (existingShadow != null)
            {
                existingShadow.gameObject.SetActive(true);
                return;
            }

            GameObject shadowObj = new GameObject(shadowName);
            shadowObj.transform.SetParent(bgPanel.transform, false);
            shadowObj.transform.SetSiblingIndex(frameObj.transform.GetSiblingIndex()); // placed right behind frameObj

            var shadowRect = shadowObj.AddComponent<RectTransform>();
            shadowRect.anchorMin = new Vector2(0.5f, 0.5f);
            shadowRect.anchorMax = new Vector2(0.5f, 0.5f);
            shadowRect.pivot = new Vector2(0.5f, 0.5f);
            shadowRect.sizeDelta = new Vector2(150f, 175f);
            shadowRect.anchoredPosition = position + new Vector2(4f, -4f); // offset down/right
            shadowRect.localRotation = Quaternion.Euler(0f, 0f, rotation);

            var shadowImg = shadowObj.AddComponent<Image>();
            shadowImg.color = new Color(0.18f, 0.15f, 0.12f, 0.22f); // soft brown-black shadow
        }

        private void CreatePolaroidTape(GameObject frameObj, float rotation)
        {
            if (frameObj == null) return;

            string tapeName = "Tape";
            Transform existingTape = frameObj.transform.Find(tapeName);
            if (existingTape != null)
            {
                existingTape.gameObject.SetActive(true);
                return;
            }

            GameObject tapeObj = new GameObject(tapeName);
            tapeObj.transform.SetParent(frameObj.transform, false);

            var tapeRect = tapeObj.AddComponent<RectTransform>();
            tapeRect.anchorMin = new Vector2(0.5f, 1f); // anchor top-center
            tapeRect.anchorMax = new Vector2(0.5f, 1f);
            tapeRect.pivot = new Vector2(0.5f, 0.5f);
            tapeRect.sizeDelta = new Vector2(50f, 20f); // tape dimensions
            tapeRect.anchoredPosition = new Vector2(0f, 5f); // offset slightly above the top edge
            tapeRect.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-15f, 15f)); // slight random tape rotation

            var tapeImg = tapeObj.AddComponent<Image>();
            tapeImg.color = new Color(0.92f, 0.90f, 0.70f, 0.35f); // semi-transparent yellow/tan scotch tape
        }

        private void PlayScratchSound()
        {
            if (sfxSource == null || penScratchSFX == null) return;
            sfxSource.pitch = UnityEngine.Random.Range(0.85f, 1.2f);
            sfxSource.volume = UnityEngine.Random.Range(0.12f, 0.22f);
            sfxSource.PlayOneShot(penScratchSFX);
        }

        private AudioClip CreateSyntheticScratchSound()
        {
            int sampleRate = 44100;
            float duration = 0.08f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float prevValue = 0f;
            for (int i = 0; i < sampleCount; i++)
            {
                float rawNoise = UnityEngine.Random.Range(-1f, 1f);
                float filtered = rawNoise - prevValue;
                prevValue = rawNoise;

                float t = (float)i / sampleCount;
                float envelope = Mathf.Sin(t * Mathf.PI);

                samples[i] = filtered * envelope * 0.12f;
            }

            AudioClip clip = AudioClip.Create("SyntheticScratch", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void ReplayGame()
        {
            if (PersistentGameManager.Instance != null)
            {
                PersistentGameManager.Instance.ClearPhotos();
            }
            SceneManager.LoadScene("Phase1_GrandpaHouse");
        }
    }
}
