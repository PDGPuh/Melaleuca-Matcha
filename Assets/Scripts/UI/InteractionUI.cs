using UnityEngine;
using TMPro;

namespace RungTramTraSu
{
    public class InteractionUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject promptPanel;      // Khung chứa UI gợi ý
        [SerializeField] private TextMeshProUGUI promptText;    // Text hiển thị gợi ý (VD: Nhấn E để nói chuyện)

        private void Awake()
        {
            if (promptPanel != null) promptPanel.SetActive(false);
        }

        private void OnEnable()
        {
            // Đăng ký sự kiện tương tác từ PlayerInteraction
            PlayerInteraction.OnInteractableFound += ShowPrompt;
            PlayerInteraction.OnInteractableLost += HidePrompt;
        }

        private void OnDisable()
        {
            // Hủy đăng ký sự kiện khi object bị tắt để tránh rò rỉ bộ nhớ
            PlayerInteraction.OnInteractableFound -= ShowPrompt;
            PlayerInteraction.OnInteractableLost -= HidePrompt;
        }

        private void ShowPrompt(string promptMessage)
        {
            if (promptPanel == null || promptText == null) return;

            promptText.text = "[E] " + promptMessage;
            promptPanel.SetActive(true);
        }

        private void HidePrompt()
        {
            if (promptPanel == null) return;

            promptPanel.SetActive(false);
        }
    }
}
