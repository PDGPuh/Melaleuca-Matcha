using System.Collections;
using UnityEngine;

namespace RungTramTraSu.CameraSystem
{
    public class TutorialManager : MonoBehaviour
    {
        public static TutorialManager Instance { get; private set; }

        public enum LessonStep
        {
            None,
            Welcome,
            OpenCamera,
            ZoomIn,
            ManualFocus,
            ChangeExposure,
            BurstMode,
            Finished
        }

        private LessonStep currentLesson = LessonStep.None;
        private bool isWaitingForAction = false;

        public LessonStep CurrentLesson => currentLesson;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void StartTutorial()
        {
            currentLesson = LessonStep.Welcome;
            StartCoroutine(WelcomeLessonRoutine());
        }

        private IEnumerator WelcomeLessonRoutine()
        {
            yield return new WaitForSeconds(1.5f);
            
            // Lock player movement during lesson briefing
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null) player.SetFrozen(true);

            string[] lines = new string[]
            {
                "Con thấy chiếc máy ảnh cơ này đẹp không? Ngày xưa ba con gởi về cho ông đó.",
                "Hôm nay đi chơi rừng tràm Trà Sư ông chỉ con cách chụp hình chuyên nghiệp nghe.",
                "Đầu tiên, con hãy bấm phím F để giơ máy ảnh lên ngắm thử xem sao!"
            };

            bool dialogueDone = false;
            DialogueManager.Instance.ShowDialogue("Ông Ngoại", lines, () => {
                dialogueDone = true;
            });

            yield return new WaitUntil(() => dialogueDone);

            if (player != null) player.SetFrozen(false);
            
            currentLesson = LessonStep.OpenCamera;
            isWaitingForAction = true;
        }

        private void Update()
        {
            if (!isWaitingForAction) return;

            switch (currentLesson)
            {
                case LessonStep.OpenCamera:
                    // Check if player entered camera mode
                    if (CameraManager.Instance != null && CameraManager.Instance.IsCameraActive)
                    {
                        isWaitingForAction = false;
                        StartCoroutine(ZoomLessonRoutine());
                    }
                    break;

                case LessonStep.ZoomIn:
                    // Check if player scrolled wheel to change focal length
                    if (CameraManager.Instance != null && CameraManager.Instance.LensSys.CurrentFocalLength >= 135f)
                    {
                        isWaitingForAction = false;
                        StartCoroutine(ManualFocusLessonRoutine());
                    }
                    break;

                case LessonStep.ManualFocus:
                    // Check if player adjusted focus or toggled auto focus
                    if (CameraManager.Instance != null && !CameraManager.Instance.FocusSys.IsAutoFocus)
                    {
                        isWaitingForAction = false;
                        StartCoroutine(ExposureLessonRoutine());
                    }
                    break;

                case LessonStep.ChangeExposure:
                    // Check if player changed ISO or Shutter
                    if (CameraManager.Instance != null && CameraManager.Instance.ExpSys.CurrentISO != 200)
                    {
                        isWaitingForAction = false;
                        StartCoroutine(BurstLessonRoutine());
                    }
                    break;

                case LessonStep.BurstMode:
                    // Check if burst was used or just finished
                    if (CameraManager.Instance != null && CameraManager.Instance.IsCameraActive)
                    {
                        // Player practiced exposure and controls, let them complete
                        isWaitingForAction = false;
                        StartCoroutine(FinishLessonRoutine());
                    }
                    break;
            }
        }

        private IEnumerator ZoomLessonRoutine()
        {
            yield return new WaitForSeconds(0.5f);
            
            string[] lines = new string[]
            {
                "À giỏi lắm! Bảng chỉ số ống ngắm DSLR hiện lên rồi đó con.",
                "Giờ con thử lăn bánh xe chuột (Mouse Wheel) để phóng to (Zoom) ống kính lên 135mm hoặc xa hơn xem nào."
            };

            bool dialogueDone = false;
            DialogueManager.Instance.ShowDialogue("Ông Ngoại", lines, () => dialogueDone = true);
            yield return new WaitUntil(() => dialogueDone);

            currentLesson = LessonStep.ZoomIn;
            isWaitingForAction = true;
        }

        private IEnumerator ManualFocusLessonRoutine()
        {
            yield return new WaitForSeconds(0.5f);

            string[] lines = new string[]
            {
                "Kính zoom nhìn gần rõ ràng quá hả con! Thấy cả tổ chim đằng xa luôn.",
                "Mặc định máy ảnh tự động lấy nét (Auto Focus). Con có thể tắt nó đi bằng cách bấm phím G để tự chỉnh.",
                "Bấm phím G chuyển sang chế độ Lấy Nét Thủ Công (Manual Focus) cho ông coi thử!"
            };

            bool dialogueDone = false;
            DialogueManager.Instance.ShowDialogue("Ông Ngoại", lines, () => dialogueDone = true);
            yield return new WaitUntil(() => dialogueDone);

            currentLesson = LessonStep.ManualFocus;
            isWaitingForAction = true;
        }

        private IEnumerator ExposureLessonRoutine()
        {
            yield return new WaitForSeconds(0.5f);

            string[] lines = new string[]
            {
                "Hay quá! Khi ở chế độ Thủ Công, con có thể đè Shift + Lăn chuột hoặc bấm Q/E để vặn nét xa gần tùy ý.",
                "Kế tiếp là thông số ISO. Bấm phím I để thay đổi ISO. ISO càng cao ảnh càng sáng nhưng sẽ bị hạt noise đó con.",
                "Con thử bấm phím I để đổi chỉ số ISO đi nào!"
            };

            bool dialogueDone = false;
            DialogueManager.Instance.ShowDialogue("Ông Ngoại", lines, () => dialogueDone = true);
            yield return new WaitUntil(() => dialogueDone);

            currentLesson = LessonStep.ChangeExposure;
            isWaitingForAction = true;
        }

        private IEnumerator BurstLessonRoutine()
        {
            yield return new WaitForSeconds(0.5f);

            string[] lines = new string[]
            {
                "Đúng rồi đó! Ngoài ra con có thể bấm phím K để đổi Tốc độ màn trập (Shutter Speed) giúp chụp chim bay nhanh không bị nhòe.",
                "Bấm phím O để chỉnh Khẩu độ (Aperture) giúp mờ hậu cảnh (xóa phông).",
                "Đặc biệt con có thể nhấn Giữ Chuột Trái để chụp liên tục (Burst Mode 10 FPS) bắt trọn mọi khoảnh khắc chuyển động của thú rừng.",
                "Con đã nắm hết cách sử dụng rồi đó. Giờ hãy đi chụp cây Xoài cổ thụ đằng kia làm bài thực hành đầu tiên nghen!"
            };

            bool dialogueDone = false;
            DialogueManager.Instance.ShowDialogue("Ông Ngoại", lines, () => dialogueDone = true);
            yield return new WaitUntil(() => dialogueDone);

            currentLesson = LessonStep.BurstMode;
            isWaitingForAction = true;
        }

        private IEnumerator FinishLessonRoutine()
        {
            currentLesson = LessonStep.Finished;
            isWaitingForAction = false;
            yield return null;
        }

        public string GetLessonHint()
        {
            switch (currentLesson)
            {
                case LessonStep.Welcome: return "Nghe Ông Ngoại hướng dẫn...";
                case LessonStep.OpenCamera: return "Hướng dẫn: Nhấn F để mở máy ảnh DSLR.";
                case LessonStep.ZoomIn: return "Hướng dẫn: Lăn bánh xe chuột (Mouse Wheel) để zoom ống kính lên >= 135mm.";
                case LessonStep.ManualFocus: return "Hướng dẫn: Nhấn G để đổi Focus Mode sang Thủ Công (Manual).";
                case LessonStep.ChangeExposure: return "Hướng dẫn: Nhấn I để thay đổi giá trị ISO.";
                case LessonStep.BurstMode: return "Hướng dẫn: Giữ Chuột Trái để chụp thử hoặc đi chụp Cây Xoài.";
                default: return "";
            }
        }
    }
}
