using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

namespace RungTramTraSu.CameraSystem
{
    public class CameraGuide : MonoBehaviour
    {
        public static CameraGuide Instance { get; private set; }

        private GameObject guidePanel;
        private TextMeshProUGUI txtGuideTitle;
        private TextMeshProUGUI txtGuideBody;
        private Button btnNext;
        private Button btnPrev;
        private Button btnClose;

        private int currentPageIndex = 0;

        private struct GuidePage
        {
            public string title;
            public string content;
        }

        private static readonly GuidePage[] Pages = new GuidePage[]
        {
            new GuidePage
            {
                title = "1. Tam Giác Phơi Sáng (Exposure Triangle)",
                content = "Bức ảnh đẹp được quyết định bởi lượng ánh sáng đi vào cảm biến, được điều khiển bởi 3 thông số:\n\n" +
                          "• KHẨU ĐỘ (Aperture - F):\n" +
                          "Độ mở của ống kính. F càng nhỏ (như F1.4) nhận nhiều ánh sáng hơn và làm mờ phông nền (xóa phông).\n\n" +
                          "• TỐC ĐỘ MÀN TRẬP (Shutter Speed):\n" +
                          "Thời gian cảm biến thu sáng. Tốc độ nhanh (như 1/1000s) giúp đóng băng các chuyển động nhanh của chim bay.\n\n" +
                          "• ISO (Độ nhạy sáng):\n" +
                          "ISO cao (3200) giúp chụp trong tối dễ hơn nhưng sẽ gây hạt nhiễu (noise) làm giảm điểm chất lượng ảnh."
            },
            new GuidePage
            {
                title = "2. Lấy Nét & Khóa Mục Tiêu (Focusing)",
                content = "• TỰ ĐỘNG LẤY NÉT (Auto Focus - AF):\n" +
                          "Nhấn [TAB] để khóa nét vào con thú gần tâm ngắm nhất. Khung lấy nét sẽ chuyển sang màu xanh lá.\n\n" +
                          "• LẤY NÉT THỦ CÔNG (Manual Focus - MF):\n" +
                          "Nhấn [G] để đổi chế độ. Giữ [SHIFT + Cuộn chuột] hoặc click chuột phải + Cuộn chuột để xoay vòng lấy nét.\n\n" +
                          "• CHỈ SỐ BLUR:\n" +
                          "Nếu lấy nét không chuẩn, chủ thể sẽ bị nhòe và không được tính điểm nhiệm vụ."
            },
            new GuidePage
            {
                title = "3. Thử Thách & Chấm Điểm (Wildlife Photography)",
                content = "Mỗi bức ảnh động vật hoang dã bạn chụp sẽ được hệ thống chấm điểm dựa trên các tiêu chí:\n\n" +
                          "• ĐỘ NÉT: Tiêu cự lấy nét phải chuẩn xác vào con thú.\n" +
                          "• ÁNH SÁNG: Phơi sáng vừa đủ, không quá chói hoặc quá tối.\n" +
                          "• KÍCH THƯỚC: Con thú phải đủ lớn và rõ ràng trong khung hình.\n" +
                          "• BỐ CỤC: Đặt chủ thể gần các đường lưới 1/3 (Rule of Thirds) hoặc tâm ngắm.\n" +
                          "• HƯỚNG NHÌN: Điểm cộng lớn nếu con vật đang hướng mặt về phía ống kính."
            },
            new GuidePage
            {
                title = "4. Chế Độ Chụp Liên Tục (Burst Mode)",
                content = "• CÁCH SỬ DỤNG:\n" +
                          "Trong khi đang ngắm máy ảnh (Giữ chuột phải), nhấn GIỮ CHUỘT TRÁI để máy ảnh chụp liên tiếp 5 bức ảnh mỗi giây (5 FPS).\n\n" +
                          "• ƯU ĐIỂM:\n" +
                          "Rất thích hợp khi chụp các khoảnh khắc chuyển động nhanh như cá lóc nhảy lên mặt nước hoặc đàn chim bất ngờ bay vút lên.\n\n" +
                          "• LỰA CHỌN:\n" +
                          "Hệ thống sẽ tự động lưu lại bức ảnh có điểm số cao nhất trong loạt chụp liên tục."
            }
        };

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                BuildGuideUI();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            if (guidePanel == null || !guidePanel.activeSelf) return;

            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                CloseGuide();
            }
        }

        public void OpenGuide()
        {
            if (guidePanel != null)
            {
                currentPageIndex = 0;
                DisplayPage();
                guidePanel.SetActive(true);

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                PlayerController player = FindAnyObjectByType<PlayerController>();
                if (player != null) player.SetFrozen(true);
            }
        }

        public void CloseGuide()
        {
            if (guidePanel != null)
            {
                guidePanel.SetActive(false);

                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;

                PlayerController player = FindAnyObjectByType<PlayerController>();
                if (player != null) player.SetFrozen(false);
            }
        }

        private void NextPage()
        {
            if (currentPageIndex < Pages.Length - 1)
            {
                currentPageIndex++;
                DisplayPage();
            }
        }

        private void PrevPage()
        {
            if (currentPageIndex > 0)
            {
                currentPageIndex--;
                DisplayPage();
            }
        }

        private void DisplayPage()
        {
            if (currentPageIndex >= 0 && currentPageIndex < Pages.Length)
            {
                txtGuideTitle.text = Pages[currentPageIndex].title;
                txtGuideBody.text = Pages[currentPageIndex].content;

                btnPrev.interactable = (currentPageIndex > 0);
                btnNext.interactable = (currentPageIndex < Pages.Length - 1);
            }
        }

        private void BuildGuideUI()
        {
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null) return;

            guidePanel = CreateRT("CameraGuidePanel", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Image bg = guidePanel.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.85f);
            guidePanel.SetActive(false);

            GameObject box = CreateRT("GuideBookletBox", guidePanel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 650f));
            Image boxImg = box.AddComponent<Image>();
            boxImg.color = new Color(0.96f, 0.95f, 0.90f);

            GameObject titleGo = CreateRT("BookletTitle", box.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(800f, 60f));
            txtGuideTitle = titleGo.AddComponent<TextMeshProUGUI>();
            txtGuideTitle.text = "Sách Hướng Dẫn Nhiếp Ảnh";
            txtGuideTitle.fontSize = 28f;
            txtGuideTitle.fontStyle = FontStyles.Bold;
            txtGuideTitle.color = new Color(0.15f, 0.1f, 0.05f);
            txtGuideTitle.alignment = TextAlignmentOptions.Center;

            GameObject sep = CreateRT("BookletSep", box.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(800f, 4f));
            sep.AddComponent<Image>().color = new Color(0.7f, 0.65f, 0.55f);

            GameObject bodyGo = CreateRT("BookletBody", box.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(800f, 400f));
            txtGuideBody = bodyGo.AddComponent<TextMeshProUGUI>();
            txtGuideBody.text = "Nội dung hướng dẫn...";
            txtGuideBody.fontSize = 20f;
            txtGuideBody.color = new Color(0.2f, 0.15f, 0.1f);
            txtGuideBody.alignment = TextAlignmentOptions.TopLeft;
            txtGuideBody.enableWordWrapping = true;

            GameObject prevBtnGo = CreateButton("BtnPrev", box.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(100f, 50f), new Vector2(160f, 44f), "Trang Trước", PrevPage, out btnPrev);
            GameObject nextBtnGo = CreateButton("BtnNext", box.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-100f, 50f), new Vector2(160f, 44f), "Trang Tiếp", NextPage, out btnNext);
            GameObject closeBtnGo = CreateButton("BtnClose", box.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(160f, 44f), "Đóng Hướng Dẫn", CloseGuide, out btnClose);
        }

        private GameObject CreateButton(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, string text, UnityEngine.Events.UnityAction action, out Button outBtn)
        {
            GameObject btnObj = CreateRT(name, parent, anchorMin, anchorMax, pos, size);
            btnObj.GetComponent<RectTransform>().pivot = anchorMax; 
            
            Image img = btnObj.AddComponent<Image>();
            img.color = new Color(0.18f, 0.15f, 0.12f, 1f);
            
            outBtn = btnObj.AddComponent<Button>();
            outBtn.targetGraphic = img;
            outBtn.onClick.AddListener(action);

            ColorBlock cb = outBtn.colors;
            cb.normalColor = new Color(0.18f, 0.15f, 0.12f, 1f);
            cb.highlightedColor = new Color(0.35f, 0.3f, 0.25f);
            cb.pressedColor = new Color(0.08f, 0.05f, 0.02f);
            outBtn.colors = cb;

            GameObject textGo = CreateRT("BtnText", btnObj.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 18f;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;

            return btnObj;
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
