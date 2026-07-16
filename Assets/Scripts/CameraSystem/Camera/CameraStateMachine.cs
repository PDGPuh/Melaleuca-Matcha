using UnityEngine;

namespace RungTramTraSu.CameraSystem
{
    public enum CameraState
    {
        Inactive,
        ViewfinderAiming,
        StabilizedAiming,
        BurstCapturing
    }

    public class CameraStateMachine : MonoBehaviour
    {
        private CameraState currentState = CameraState.Inactive;

        public CameraState CurrentState => currentState;

        public delegate void StateChanged(CameraState oldState, CameraState newState);
        public event StateChanged OnStateChanged;

        public void ChangeState(CameraState newState)
        {
            if (currentState == newState) return;

            CameraState oldState = currentState;
            currentState = newState;

            OnStateChanged?.Invoke(oldState, newState);
            Debug.Log($"[CameraStateMachine] State changed from {oldState} to {newState}");
        }
    }
}
