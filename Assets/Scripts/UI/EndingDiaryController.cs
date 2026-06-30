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
        
        private string diaryContent = 
            "Ngày... tháng... năm...\n" +
            "Chuyến đi rừng tràm Trà Sư cùng Ông Ngoại...\n\n" +
            "Mình chưa từng nghĩ quê hương An Giang lại đẹp hoang sơ và kỳ vĩ đến vậy. Màu xanh mướt của bèo tấm, tiếng chim cò líu lo, tia nắng rực rỡ lọc qua tán lá rừng sâu...\n\n" +
            "Lời ông ngoại dặn rất đúng: Thiên nhiên non nước hữu tình của mình, nếu chúng ta không gìn giữ và yêu thương, thì một ngày nào đó tụi nó sẽ biến mất mãi mãi...\n\n" +
            "Tặng ông, và những cánh chim đã xa...";

        private string creditContent =
            "RỪNG TRÀM TRÀ SƯ\n" +
            "Trò chơi trải nghiệm thiên nhiên Việt Nam\n\n\n" +
            "THÀNH VIÊN NHÓM PHÁT TRIỂN\n\n" +
            "Lập trình viên chính\n" +
            "Đặng Quốc Phong - PRU213\n\n" +
            "Hỗ trợ kỹ thuật & Âm thanh\n" +
            "Antigravity AI Agent\n\n" +
            "Giảng viên hướng dẫn\n" +
            "Bộ môn Công nghệ phần mềm\n\n\n" +
            "ÂM THANH & ÂM NHẠC\n\n" +
            "Hòa tấu Cổ Bản Vắn\n" +
            "Nnưt Thành Trí, Huỳnh Tiến,\nHữu Đức, Trung Thiện\n\n" +
            "Lồng tiếng Ông Ngoại\n" +
            "Nghệ sĩ lồng tiếng Phase 1-5\n\n\n" +
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

            StartCoroutine(EndingFlowRoutine());
        }

        private IEnumerator EndingFlowRoutine()
        {
            // 1. Fade out ambient sound
            StartCoroutine(FadeOutAmbientSounds(2.0f));

            // 2. Fade to black
            if (ScreenFader.Instance != null)
            {
                bool fadeDone = false;
                ScreenFader.Instance.StartFadeOut(2.0f, () => fadeDone = true);
                yield return new WaitUntil(() => fadeDone);
            }
            else
            {
                yield return new WaitForSeconds(2.0f);
            }

            // 3. Configure Ending UI
            ConfigureUI();

            // Enable canvas
            if (diaryCanvas != null) diaryCanvas.SetActive(true);

            // 4. Fade in screen back to show the diary
            if (ScreenFader.Instance != null)
            {
                ScreenFader.Instance.StartFadeIn(1.5f);
            }

            // 5. Play music and fade it in
            if (donCaMusic != null)
            {
                musicSource.clip = donCaMusic;
                musicSource.volume = 0f;
                musicSource.Play();
                StartCoroutine(FadeInMusic(3.0f, 0.6f));
            }

            // 6. Typewriter effect
            if (diaryText != null)
            {
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
            }

            // 7. Silent moment
            yield return new WaitForSeconds(3.0f);

            // 8. Fade out diary panel to black
            if (bgPanel != null)
            {
                CanvasGroup cg = bgPanel.gameObject.GetComponent<CanvasGroup>();
                if (cg == null) cg = bgPanel.gameObject.AddComponent<CanvasGroup>();
                
                float elapsed = 0f;
                while (elapsed < 1.5f)
                {
                    elapsed += Time.deltaTime;
                    cg.alpha = Mathf.Lerp(1f, 0f, elapsed / 1.5f);
                    yield return null;
                }
                bgPanel.gameObject.SetActive(false);
            }
            else
            {
                yield return new WaitForSeconds(1.5f);
            }

            // 9. Show Credits Panel
            if (creditPanel != null)
            {
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
            }

            // Style notebook background panel to be centered
            if (bgPanel != null)
            {
                bgPanel.anchorMin = new Vector2(0.5f, 0.5f);
                bgPanel.anchorMax = new Vector2(0.5f, 0.5f);
                bgPanel.pivot = new Vector2(0.5f, 0.5f);
                bgPanel.sizeDelta = new Vector2(900f, 600f);
                bgPanel.anchoredPosition = Vector2.zero;

                var bgImg = bgPanel.GetComponent<Image>();
                if (bgImg != null)
                {
                    bgImg.color = new Color(0.94f, 0.90f, 0.84f, 1f); // Warm paper color
                }

                // Style font if font asset is generated
                TMP_FontAsset customFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/segoepr SDF");
                if (customFont == null) customFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/segoesc SDF");
                if (customFont != null && diaryText != null)
                {
                    diaryText.font = customFont;
                }

                if (diaryText != null)
                {
                    diaryText.color = new Color(0.18f, 0.16f, 0.14f, 1f); // Dark ink color
                    diaryText.lineSpacing = 15f;
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
            AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
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
    }
}
