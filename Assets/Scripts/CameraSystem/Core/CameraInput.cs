using UnityEngine;
using UnityEngine.InputSystem;

namespace RungTramTraSu.CameraSystem
{
    public class CameraInput : MonoBehaviour
    {
        public static CameraInput Instance { get; private set; }

        public bool ToggleCameraPressed => Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame;
        public bool ToggleModePressed => Keyboard.current != null && Keyboard.current.gKey.wasPressedThisFrame;
        public bool ToggleTripodPressed => Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame;
        public bool FocusLockPressed => Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame;
        public bool StabilizeHeld => Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
        public bool CycleISOPressed => Keyboard.current != null && Keyboard.current.iKey.wasPressedThisFrame;
        public bool CycleShutterPressed => Keyboard.current != null && Keyboard.current.kKey.wasPressedThisFrame;
        public bool CycleAperturePressed => Keyboard.current != null && Keyboard.current.oKey.wasPressedThisFrame;
        public bool CycleExposureCompensationPressed => Keyboard.current != null && Keyboard.current.uKey.wasPressedThisFrame;
        public bool CycleWBPressed => Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame;

        public bool ShutterPressed => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        public bool ShutterHeld => Mouse.current != null && Mouse.current.leftButton.isPressed;
        public bool ShutterReleased => Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
        public bool AimHeld => Mouse.current != null && Mouse.current.rightButton.isPressed;

        public float ScrollDelta => Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;

        public float FocusManualDirection
        {
            get
            {
                float dir = 0f;
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.qKey.isPressed) dir = -1f;
                    if (Keyboard.current.eKey.isPressed) dir = 1f;
                }
                return dir;
            }
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
