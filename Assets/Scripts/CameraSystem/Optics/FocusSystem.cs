using UnityEngine;

namespace RungTramTraSu.CameraSystem
{
    public class FocusSystem : MonoBehaviour
    {
        [Header("Focus Settings")]
        [SerializeField] private float minFocusDistance = 0.5f;
        [SerializeField] private float maxFocusDistance = 80.0f;
        [SerializeField] private float manualFocusSpeed = 15f;
        [SerializeField] private float autoFocusLerpSpeed = 8f;

        private bool isAutoFocus = true;
        private float currentFocusDistance = 10f;
        private float targetFocusDistance = 10f;
        
        private Transform lockedTarget;
        private WildlifeDetector.DetectedTarget lockedTargetData;
        private bool hasTargetLock = false;

        public bool IsAutoFocus => isAutoFocus;
        public float CurrentFocusDistance => currentFocusDistance;
        public Transform LockedTarget => hasTargetLock ? lockedTarget : null;
        public bool HasTargetLock => hasTargetLock;

        private void Start()
        {
            currentFocusDistance = 10f;
            targetFocusDistance = 10f;
        }

        public void SetAutoFocus(bool auto)
        {
            isAutoFocus = auto;
            if (!isAutoFocus)
            {
                targetFocusDistance = currentFocusDistance;
            }
        }

        public void ToggleFocusMode()
        {
            SetAutoFocus(!isAutoFocus);
        }

        public void AdjustManualFocus(float direction)
        {
            if (isAutoFocus)
            {
                // Switch to manual if player attempts to adjust manually
                SetAutoFocus(false);
            }

            targetFocusDistance += direction * manualFocusSpeed * Time.deltaTime;
            targetFocusDistance = Mathf.Clamp(targetFocusDistance, minFocusDistance, maxFocusDistance);
        }

        public void TryLockTarget()
        {
            // Scan for animals in viewfinder
            var visibleAnimals = WildlifeDetector.Instance.ScanForVisibleTargets();
            if (visibleAnimals.Count > 0)
            {
                // Find target closest to screen center
                float bestCenterOffset = float.MaxValue;
                WildlifeDetector.DetectedTarget bestCandidate = new WildlifeDetector.DetectedTarget();
                bool found = false;

                foreach (var animal in visibleAnimals)
                {
                    if (animal.isOccluded) continue;
                    float offset = Mathf.Abs(animal.viewportPos.x - 0.5f) + Mathf.Abs(animal.viewportPos.y - 0.5f);
                    if (offset < bestCenterOffset)
                    {
                        bestCenterOffset = offset;
                        bestCandidate = animal;
                        found = true;
                    }
                }

                if (found)
                {
                    lockedTarget = bestCandidate.go.transform;
                    lockedTargetData = bestCandidate;
                    hasTargetLock = true;
                    // Auto switch to auto focus when locking target
                    isAutoFocus = true;
                    Debug.Log("[FocusSystem] Locked target: " + bestCandidate.displayName);
                }
            }
            else
            {
                // Clear lock
                ClearLock();
            }
        }

        public void ClearLock()
        {
            lockedTarget = null;
            hasTargetLock = false;
        }

        private void Update()
        {
            if (isAutoFocus)
            {
                if (hasTargetLock && lockedTarget != null)
                {
                    // Check if target is still in viewport and not occluded
                    Vector3 viewPos;
                    bool inFrame = WildlifeDetector.Instance.IsInViewfinder(lockedTarget.position, out viewPos, 0.05f);
                    bool occluded = WildlifeDetector.Instance.IsOccluded(lockedTarget.position, lockedTarget);

                    if (!inFrame || occluded)
                    {
                        Debug.Log("[FocusSystem] Lost lock (out of frame or occluded)");
                        ClearLock();
                    }
                    else
                    {
                        // Set focus target to distance to locked target
                        targetFocusDistance = Vector3.Distance(Camera.main.transform.position, lockedTarget.position);
                    }
                }
                else
                {
                    // If no target lock, auto focus defaults to whatever is in the center of the frame
                    RaycastHit hit;
                    Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                    if (Physics.Raycast(ray, out hit, maxFocusDistance))
                    {
                        targetFocusDistance = hit.distance;
                    }
                    else
                    {
                        targetFocusDistance = maxFocusDistance;
                    }
                }
            }

            // Lerp current focus distance
            currentFocusDistance = Mathf.Lerp(currentFocusDistance, targetFocusDistance, Time.deltaTime * autoFocusLerpSpeed);
        }

        // Returns blur factor from 0 (perfect focus) to 1 (extremely blurry)
        public float GetBlurFactor(float subjectDistance, float apertureValue)
        {
            // Depth of field depends on aperture (F-number).
            // A lower F-number (like F1.8) has a very shallow depth of field (blurs easily).
            // A higher F-number (like F16) has a deep depth of field (larger focus range).
            float focusAcceptanceRange = 0.05f * apertureValue; // e.g. at F1.8, range is ~0.09m. At F16, range is ~0.8m.

            float distanceDiff = Mathf.Abs(subjectDistance - currentFocusDistance);
            if (distanceDiff <= focusAcceptanceRange)
            {
                return 0f; // Perfect focus
            }

            // Scale blur relative to how far out of focus the subject is
            float blurScale = (distanceDiff - focusAcceptanceRange) / 5f;
            return Mathf.Clamp01(blurScale);
        }
    }
}
