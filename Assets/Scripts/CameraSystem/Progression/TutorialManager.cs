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
            yield return new WaitForSeconds(0.5f);
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
            currentLesson = LessonStep.ZoomIn;
            isWaitingForAction = true;
        }

        private IEnumerator ManualFocusLessonRoutine()
        {
            yield return new WaitForSeconds(0.5f);
            currentLesson = LessonStep.ManualFocus;
            isWaitingForAction = true;
        }

        private IEnumerator ExposureLessonRoutine()
        {
            yield return new WaitForSeconds(0.5f);
            currentLesson = LessonStep.ChangeExposure;
            isWaitingForAction = true;
        }

        private IEnumerator BurstLessonRoutine()
        {
            yield return new WaitForSeconds(0.5f);
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
                case LessonStep.Welcome: return "Hướng dẫn: Giữ Chuột Phải để ngắm máy ảnh.";
                case LessonStep.OpenCamera: return "Hướng dẫn: Giữ Chuột Phải để ngắm máy ảnh.";
                case LessonStep.ZoomIn: return "Hướng dẫn: Lăn bánh xe chuột (Mouse Wheel) để zoom ống kính lên >= 135mm.";
                case LessonStep.ManualFocus: return "Hướng dẫn: Nhấn G để đổi Focus Mode sang Thủ Công (Manual).";
                case LessonStep.ChangeExposure: return "Hướng dẫn: Nhấn I để thay đổi giá trị ISO.";
                case LessonStep.BurstMode: return "Hướng dẫn: Giữ Chuột Trái để chụp thử hoặc đi chụp Cây Xoài.";
                default: return "";
            }
        }
    }
}
