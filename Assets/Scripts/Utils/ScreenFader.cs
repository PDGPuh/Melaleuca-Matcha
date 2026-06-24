using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace RungTramTraSu
{
    public class ScreenFader : MonoBehaviour
    {
        public static ScreenFader Instance { get; private set; }

        [Header("UI Reference")]
        [SerializeField] private Image fadeImage;

        private void Awake()
        {
            if (fadeImage != null)
            {
                Instance = this;
                // Khởi đầu là màn hình đen hoàn toàn
                fadeImage.gameObject.SetActive(true);
                fadeImage.color = Color.black;
            }
            else
            {
                // Nếu không có fadeImage, đây là component thừa/nhầm lẫn trên Managers, tự hủy component để tránh ảnh hưởng đến Managers khác
                Destroy(this);
            }
        }

        private void Start()
        {
            // Tự động Fade-In (Sáng dần) khi bắt đầu game
            StartFadeIn(1.5f);
        }

        public void StartFadeIn(float duration)
        {
            StopAllCoroutines();
            StartCoroutine(FadeRoutine(1, 0, duration, null));
        }

        public void StartFadeOut(float duration, Action onComplete)
        {
            StopAllCoroutines();
            StartCoroutine(FadeRoutine(0, 1, duration, onComplete));
        }

        private IEnumerator FadeRoutine(float startAlpha, float targetAlpha, float duration, Action onComplete)
        {
            if (fadeImage == null)
            {
                Debug.LogWarning("FadeImage chưa được gán trong ScreenFader!");
                onComplete?.Invoke();
                yield break;
            }

            fadeImage.gameObject.SetActive(true);
            fadeImage.color = new Color(0, 0, 0, startAlpha);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float currentAlpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / duration);
                fadeImage.color = new Color(0, 0, 0, currentAlpha);
                yield return null;
            }

            fadeImage.color = new Color(0, 0, 0, targetAlpha);

            // Nếu fade về sáng (alpha = 0) thì ẩn Image đi để không block raycast click chuột vào thế giới
            if (targetAlpha <= 0f)
            {
                fadeImage.gameObject.SetActive(false);
            }

            onComplete?.Invoke();
        }
    }
}
