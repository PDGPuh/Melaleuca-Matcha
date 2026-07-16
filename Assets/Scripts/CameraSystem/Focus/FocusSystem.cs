using UnityEngine;

namespace RungTramTraSu.CameraSystem
{
    public class FocusSystem : MonoBehaviour, IFocusSystem
    {
        [Header("Focus Settings")]
        [SerializeField] private float minFocusDistance = 0.5f;
        [SerializeField] private float maxFocusDistance = 80.0f;
        [SerializeField] private float manualFocusSpeed = 15f;
        [SerializeField] private float autoFocusLerpSpeed = 8f;
        [SerializeField] private float focusAccuracyThreshold = 0.05f;

        private FocusMode activeFocusMode = FocusMode.SingleAF;
        private float currentFocusDistance = 10f;
        private float targetFocusDistance = 10f;
        
        private Transform lockedTarget;
        private WildlifeDetector.DetectedTarget lockedTargetData;
        private bool hasTargetLock = false;

        // Interface implementation
        public FocusMode ActiveFocusMode => activeFocusMode;
        public float FocusDistance => currentFocusDistance;
        public float CurrentFocusDistance => currentFocusDistance;
        public bool HasTargetLock => hasTargetLock;
        public Transform LockTarget => hasTargetLock ? lockedTarget : null;

        public bool IsAutoFocus => activeFocusMode == FocusMode.SingleAF || activeFocusMode == FocusMode.ContinuousAF;

        private void Start()
        {
            currentFocusDistance = 10f;
            targetFocusDistance = 10f;
        }

        public void SetFocusMode(FocusMode mode)
        {
            activeFocusMode = mode;
            if (activeFocusMode == FocusMode.Manual)
            {
                targetFocusDistance = currentFocusDistance;
            }
        }

        public void AdjustFocusDistance(float delta)
        {
            // Switch to manual focus automatically when manually adjusting
            if (IsAutoFocus)
            {
                SetFocusMode(FocusMode.Manual);
            }

            targetFocusDistance += delta * manualFocusSpeed;
            targetFocusDistance = Mathf.Clamp(targetFocusDistance, minFocusDistance, maxFocusDistance);
        }

        public void LockActiveTarget()
        {
            if (WildlifeDetector.Instance == null) return;

            var visibleAnimals = WildlifeDetector.Instance.ScanForVisibleTargets();
            if (visibleAnimals.Count > 0)
            {
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
                    // Automatically switch to Continuous AF when locking on a target
                    SetFocusMode(FocusMode.ContinuousAF);
                    Debug.Log("[FocusSystem] Locked and tracking target: " + bestCandidate.displayName);
                }
            }
            else
            {
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
            // Handle Auto Focus distance evaluation
            if (IsAutoFocus)
            {
                if (hasTargetLock && lockedTarget != null)
                {
                    // Continuous AF tracking target
                    Vector3 viewPos;
                    bool inFrame = WildlifeDetector.Instance.IsInViewfinder(lockedTarget.position, out viewPos, 0.05f);
                    bool occluded = WildlifeDetector.Instance.IsOccluded(lockedTarget.position, lockedTarget);

                    if (!inFrame || occluded)
                    {
                        Debug.Log("[FocusSystem] Target tracking lost (out of viewfinder bounds or occluded)");
                        ClearLock();
                        // Revert to standard Single AF if target tracking is lost
                        SetFocusMode(FocusMode.SingleAF);
                    }
                    else
                    {
                        targetFocusDistance = Vector3.Distance(Camera.main.transform.position, lockedTarget.position);
                    }
                }
                else if (activeFocusMode == FocusMode.ContinuousAF || activeFocusMode == FocusMode.SingleAF)
                {
                    // Focus on whatever is in the center of the frame (including triggers to detect quest targets like the Mango Tree)
                    RaycastHit hit;
                    Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
                    if (Physics.Raycast(ray, out hit, maxFocusDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                    {
                        targetFocusDistance = hit.distance;
                    }
                    else
                    {
                        targetFocusDistance = maxFocusDistance;
                    }
                }
            }

            // Smoothly move focusing mechanics
            currentFocusDistance = Mathf.Lerp(currentFocusDistance, targetFocusDistance, Time.deltaTime * autoFocusLerpSpeed);
        }

        // Returns blur factor from 0 (perfect sharp focus) to 1 (totally out of focus)
        public float GetBlurFactor(float subjectDistance, float apertureValue)
        {
            // Aperture models depth of field. 
            // e.g. f/1.4 has a shallow depth of field (blur window is narrow)
            // e.g. f/22 has a very deep depth of field (large focus acceptance range)
            float focusAcceptanceRange = focusAccuracyThreshold * apertureValue;

            float distanceDiff = Mathf.Abs(subjectDistance - currentFocusDistance);
            if (distanceDiff <= focusAcceptanceRange)
            {
                return 0f; // Perfect focus
            }

            // Blur maps proportionally to the distance error scaled by the aperture setting
            float blurScale = (distanceDiff - focusAcceptanceRange) / (5.0f * (apertureValue / 2.8f));
            return Mathf.Clamp01(blurScale);
        }
    }
}
