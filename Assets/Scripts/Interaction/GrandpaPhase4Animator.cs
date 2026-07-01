using System.Collections.Generic;
using UnityEngine;

namespace RungTramTraSu
{
    public class GrandpaPhase4Animator : MonoBehaviour
    {
        public enum Phase4Pose
        {
            IdleObserve,
            PointAtWildlife,
            CrouchWhisper,
            PhotoComment,
            WalkGuide
        }

        [Header("Phase 4 Animation")]
        [SerializeField] private Phase4Pose currentPose = Phase4Pose.IdleObserve;
        [SerializeField] private bool autoCycle = false;
        [SerializeField] private float stateMinSeconds = 4.5f;
        [SerializeField] private float stateMaxSeconds = 8.0f;
        [SerializeField] private float blendSpeed = 5.5f;

        [Header("Motion Strength")]
        [SerializeField] private float breathHeight = 0.015f;
        [SerializeField] private float bodySwayDegrees = 2.2f;
        [SerializeField] private float headLookDegrees = 12f;
        [SerializeField] private float armGestureDegrees = 44f;
        [SerializeField] private float walkStepDegrees = 10f;

        private readonly Dictionary<Transform, Quaternion> baseRotations = new Dictionary<Transform, Quaternion>();
        private Animator animator;
        private Transform hips;
        private Transform spine;
        private Transform spine01;
        private Transform spine02;
        private Transform neck;
        private Transform head;
        private Transform leftShoulder;
        private Transform rightShoulder;
        private Transform leftArm;
        private Transform rightArm;
        private Transform leftForeArm;
        private Transform rightForeArm;
        private Transform leftHand;
        private Transform rightHand;
        private Transform leftUpLeg;
        private Transform rightUpLeg;
        private Transform leftLeg;
        private Transform rightLeg;

        private Vector3 baseLocalPosition;
        private Vector3 baseHipsLocalPosition;
        private Phase4Pose targetPose;
        private float targetHoldUntil;
        private float nextAutoSwitchAt;
        private float blend;
        private bool cached;
        private Phase4Pose activeProceduralPose = Phase4Pose.IdleObserve;

        public Phase4Pose CurrentPose => currentPose;

        private void Awake()
        {
            // Do not cache in Awake, wait until first LateUpdate so Animator has evaluated first frame
        }

        private void OnEnable()
        {
            cached = false;
            targetPose = currentPose;
            ScheduleAutoSwitch();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            if (!cached)
            {
                CacheRig();
            }

            if (autoCycle && Time.time >= nextAutoSwitchAt && Time.time >= targetHoldUntil)
            {
                SetMode(PickAmbientPose(), Random.Range(stateMinSeconds, stateMaxSeconds));
            }

            float targetBlend = (currentPose == Phase4Pose.IdleObserve || currentPose == Phase4Pose.WalkGuide) ? 0f : 1f;
            blend = Mathf.MoveTowards(blend, targetBlend, blendSpeed * Time.deltaTime);

            if (blend <= 0f && (currentPose == Phase4Pose.IdleObserve || currentPose == Phase4Pose.WalkGuide))
            {
                activeProceduralPose = currentPose;
            }

            ApplyPose(Time.time);
        }

        public void PlayIdleObserve(float holdSeconds = 4f)
        {
            SetMode(Phase4Pose.IdleObserve, holdSeconds);
        }

        public void PlayPointAtWildlife(float holdSeconds = 5f)
        {
            SetMode(Phase4Pose.PointAtWildlife, holdSeconds);
        }

        public void PlayCrouchWhisper(float holdSeconds = 6f)
        {
            SetMode(Phase4Pose.CrouchWhisper, holdSeconds);
        }

        public void PlayPhotoComment(float holdSeconds = 5f)
        {
            SetMode(Phase4Pose.PhotoComment, holdSeconds);
        }

        public void PlayWalkGuide(float holdSeconds = 5f)
        {
            SetMode(Phase4Pose.WalkGuide, holdSeconds);
        }

        public void SetMode(Phase4Pose pose, float holdSeconds = 4f)
        {
            currentPose = pose;
            targetHoldUntil = Time.time + Mathf.Max(0.5f, holdSeconds);
            nextAutoSwitchAt = targetHoldUntil;

            if (pose != Phase4Pose.IdleObserve && pose != Phase4Pose.WalkGuide)
            {
                activeProceduralPose = pose;
                blend = 0f;
            }

            if (animator != null)
            {
                bool isWalking = pose == Phase4Pose.WalkGuide;
                animator.SetBool("IsWalking", isWalking);
                animator.SetBool("IsRunning", false);
                animator.SetFloat("Speed", isWalking ? 0.5f : 0f);
            }
        }

        private void CacheRig()
        {
            animator = GetComponent<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Update(0f); // Force evaluation of the first frame!
            }

            baseLocalPosition = transform.localPosition;
            hips = FindRigPart("Hips");
            if (hips != null) baseHipsLocalPosition = hips.localPosition;
            spine = FindRigPart("Spine");
            spine01 = FindRigPart("Spine01");
            spine02 = FindRigPart("Spine02");
            neck = FindRigPart("neck", "Neck");
            head = FindRigPart("Head");
            leftShoulder = FindRigPart("LeftShoulder");
            rightShoulder = FindRigPart("RightShoulder");
            leftArm = FindRigPart("LeftArm");
            rightArm = FindRigPart("RightArm");
            leftForeArm = FindRigPart("LeftForeArm");
            rightForeArm = FindRigPart("RightForeArm");
            leftHand = FindRigPart("LeftHand");
            rightHand = FindRigPart("RightHand");
            leftUpLeg = FindRigPart("LeftUpLeg");
            rightUpLeg = FindRigPart("RightUpLeg");
            leftLeg = FindRigPart("LeftLeg");
            rightLeg = FindRigPart("RightLeg");

            baseRotations.Clear();
            Track(hips);
            Track(spine);
            Track(spine01);
            Track(spine02);
            Track(neck);
            Track(head);
            Track(leftShoulder);
            Track(rightShoulder);
            Track(leftArm);
            Track(rightArm);
            Track(leftForeArm);
            Track(rightForeArm);
            Track(leftHand);
            Track(rightHand);
            Track(leftUpLeg);
            Track(rightUpLeg);
            Track(leftLeg);
            Track(rightLeg);
            cached = true;
        }

        private Transform FindRigPart(params string[] names)
        {
            var transforms = GetComponentsInChildren<Transform>(true);
            foreach (var wanted in names)
            {
                foreach (var item in transforms)
                {
                    if (item.name == wanted)
                    {
                        return item;
                    }
                }
            }

            foreach (var wanted in names)
            {
                string lower = wanted.ToLowerInvariant();
                foreach (var item in transforms)
                {
                    if (item.name.ToLowerInvariant().Contains(lower))
                    {
                        return item;
                    }
                }
            }

            return null;
        }

        private void Track(Transform target)
        {
            if (target == null || baseRotations.ContainsKey(target))
            {
                return;
            }

            baseRotations.Add(target, target.localRotation);
        }

        private Quaternion GetBaseRot(Transform t)
        {
            if (t != null && baseRotations.TryGetValue(t, out Quaternion r)) return r;
            return t != null ? t.localRotation : Quaternion.identity;
        }

        private void ApplyPose(float time)
        {
            float slow = Mathf.Sin(time * 1.15f);
            float slower = Mathf.Sin(time * 0.45f);
            float breath = Mathf.Sin(time * 1.65f);

            transform.localPosition = baseLocalPosition + Vector3.up * (breath * breathHeight);

            // Fetch base rotations (cached A-pose)
            Quaternion baseHips = GetBaseRot(hips);
            Quaternion baseSpine = GetBaseRot(spine);
            Quaternion baseSpine01 = GetBaseRot(spine01);
            Quaternion baseSpine02 = GetBaseRot(spine02);
            Quaternion baseNeck = GetBaseRot(neck);
            Quaternion baseHead = GetBaseRot(head);
            Quaternion baseLShoulder = GetBaseRot(leftShoulder);
            Quaternion baseRShoulder = GetBaseRot(rightShoulder);
            Quaternion baseLArm = GetBaseRot(leftArm);
            Quaternion baseRArm = GetBaseRot(rightArm);
            Quaternion baseLForeArm = GetBaseRot(leftForeArm);
            Quaternion baseRForeArm = GetBaseRot(rightForeArm);
            Quaternion baseLHand = GetBaseRot(leftHand);
            Quaternion baseRHand = GetBaseRot(rightHand);
            Quaternion baseLUpLeg = GetBaseRot(leftUpLeg);
            Quaternion baseRUpLeg = GetBaseRot(rightUpLeg);
            Quaternion baseLLeg = GetBaseRot(leftLeg);
            Quaternion baseRLeg = GetBaseRot(rightLeg);

            // Read Animator's current frame rotations
            Quaternion animHips = hips != null ? hips.localRotation : Quaternion.identity;
            Quaternion animSpine = spine != null ? spine.localRotation : Quaternion.identity;
            Quaternion animSpine01 = spine01 != null ? spine01.localRotation : Quaternion.identity;
            Quaternion animSpine02 = spine02 != null ? spine02.localRotation : Quaternion.identity;
            Quaternion animNeck = neck != null ? neck.localRotation : Quaternion.identity;
            Quaternion animHead = head != null ? head.localRotation : Quaternion.identity;
            Quaternion animLShoulder = leftShoulder != null ? leftShoulder.localRotation : Quaternion.identity;
            Quaternion animRShoulder = rightShoulder != null ? rightShoulder.localRotation : Quaternion.identity;
            Quaternion animLArm = leftArm != null ? leftArm.localRotation : Quaternion.identity;
            Quaternion animRArm = rightArm != null ? rightArm.localRotation : Quaternion.identity;
            Quaternion animLForeArm = leftForeArm != null ? leftForeArm.localRotation : Quaternion.identity;
            Quaternion animRForeArm = rightForeArm != null ? rightForeArm.localRotation : Quaternion.identity;
            Quaternion animLHand = leftHand != null ? leftHand.localRotation : Quaternion.identity;
            Quaternion animRHand = rightHand != null ? rightHand.localRotation : Quaternion.identity;
            Quaternion animLUpLeg = leftUpLeg != null ? leftUpLeg.localRotation : Quaternion.identity;
            Quaternion animRUpLeg = rightUpLeg != null ? rightUpLeg.localRotation : Quaternion.identity;
            Quaternion animLLeg = leftLeg != null ? leftLeg.localRotation : Quaternion.identity;
            Quaternion animRLeg = rightLeg != null ? rightLeg.localRotation : Quaternion.identity;

            // Default: if not overridden, use the Animator's current rotation!
            Quaternion desHips = animHips;
            Quaternion desSpine = animSpine;
            Quaternion desSpine01 = animSpine01;
            Quaternion desSpine02 = animSpine02;
            Quaternion desNeck = animNeck;
            Quaternion desHead = animHead;
            Quaternion desLShoulder = animLShoulder;
            Quaternion desRShoulder = animRShoulder;
            Quaternion desLArm = animLArm;
            Quaternion desRArm = animRArm;
            Quaternion desLForeArm = animLForeArm;
            Quaternion desRForeArm = animRForeArm;
            Quaternion desLHand = animLHand;
            Quaternion desRHand = animRHand;
            Quaternion desLUpLeg = animLUpLeg;
            Quaternion desRUpLeg = animRUpLeg;
            Quaternion desLLeg = animLLeg;
            Quaternion desRLeg = animRLeg;

            Vector3 hipsPosOffset = Vector3.zero;

            switch (activeProceduralPose)
            {
                case Phase4Pose.PointAtWildlife:
                    // Spine/neck/head look slightly to the right/forward
                    desSpine = baseSpine * Quaternion.Euler(-2f, 8f, -2f);
                    desSpine01 = baseSpine01 * Quaternion.Euler(-1.3f, 5.2f, -1.3f);
                    desSpine02 = baseSpine02 * Quaternion.Euler(1f, 10f, -3f);
                    desNeck = baseNeck * Quaternion.Euler(-3f, 15f, 0f);
                    desHead = baseHead * Quaternion.Euler(-2f, 12f, 0f);
                    
                    // Point right arm forward relative to the base A-pose!
                    desRArm = baseRArm * Quaternion.Euler(-90f, 50f, 10f);
                    desRForeArm = baseRForeArm * Quaternion.Euler(-10f, 0f, 0f);
                    desRShoulder = baseRShoulder * Quaternion.Euler(0f, 0f, 8f);
                    break;

                case Phase4Pose.CrouchWhisper:
                    hipsPosOffset = new Vector3(0f, -25f, -6f);
                    
                    // Override hips, spine, neck, head, and legs
                    desHips = baseHips * Quaternion.Euler(5f, 0f, 0f);
                    desSpine = baseSpine * Quaternion.Euler(8f, -2f, 1f);
                    desSpine01 = baseSpine01 * Quaternion.Euler(5.2f, -1.3f, 0.65f);
                    desSpine02 = baseSpine02 * Quaternion.Euler(5f, -2f, 0f);
                    desNeck = baseNeck * Quaternion.Euler(-5f, -5f, 0f);
                    desHead = baseHead * Quaternion.Euler(-3f, -4f, 0f);
                    
                    desLUpLeg = baseLUpLeg * Quaternion.Euler(-35f, 0f, 2f);
                    desRUpLeg = baseRUpLeg * Quaternion.Euler(-35f, 0f, -2f);
                    desLLeg = baseLLeg * Quaternion.Euler(45f, 0f, 0f);
                    desRLeg = baseRLeg * Quaternion.Euler(45f, 0f, 0f);

                    // Arms are NOT overridden, so they stay at animLArm / animRArm (Animator control)
                    break;

                case Phase4Pose.PhotoComment:
                    desSpine = baseSpine * Quaternion.Euler(6f, -4f, 1f);
                    desSpine01 = baseSpine01 * Quaternion.Euler(3.9f, -2.6f, 0.65f);
                    desSpine02 = baseSpine02 * Quaternion.Euler(4f, -4f, 1f);
                    desNeck = baseNeck * Quaternion.Euler(8f + Mathf.Sin(time * 3.2f) * 1.5f, -8f, 0f);
                    desHead = baseHead * Quaternion.Euler(6f + Mathf.Sin(time * 3.2f) * 1.8f, -6f, 0f);
                    
                    // Override arms to raise camera
                    desLArm = baseLArm * Quaternion.Euler(-40f, 20f, -10f);
                    desRArm = baseRArm * Quaternion.Euler(-40f, -20f, 10f);
                    desLForeArm = baseLForeArm * Quaternion.Euler(40f, 0f, 0f);
                    desRForeArm = baseRForeArm * Quaternion.Euler(40f, 0f, 0f);
                    break;

                case Phase4Pose.WalkGuide:
                    float step = Mathf.Sin(time * 4.0f);
                    desSpine = baseSpine * Quaternion.Euler(-1f, 0f, step * 1.4f);
                    desSpine02 = baseSpine02 * Quaternion.Euler(1f, 0f, -step * 1.2f);
                    desNeck = baseNeck * Quaternion.Euler(-2f, 10f, 0f);
                    desHead = baseHead * Quaternion.Euler(-1f, 9f, 0f);
                    desLArm = baseLArm * Quaternion.Euler(step * -12f, 0f, -5f);
                    desRArm = baseRArm * Quaternion.Euler(-step * -12f, 0f, 5f);
                    desLForeArm = baseLForeArm * Quaternion.Euler(Mathf.Max(0f, step) * 10f, 0f, 0f);
                    desRForeArm = baseRForeArm * Quaternion.Euler(Mathf.Max(0f, -step) * 10f, 0f, 0f);
                    desLUpLeg = baseLUpLeg * Quaternion.Euler(step * walkStepDegrees, 0f, 0f);
                    desRUpLeg = baseRUpLeg * Quaternion.Euler(-step * walkStepDegrees, 0f, 0f);
                    desLLeg = baseLLeg * Quaternion.Euler(Mathf.Max(0f, -step) * 15f, 0f, 0f);
                    desRLeg = baseRLeg * Quaternion.Euler(Mathf.Max(0f, step) * 15f, 0f, 0f);
                    break;
            }

            // Apply slerp to bones
            float b = Mathf.Clamp01(blend);
            if (hips != null)
            {
                Vector3 desiredHipsPos = baseHipsLocalPosition + hipsPosOffset;
                hips.localPosition = Vector3.Lerp(hips.localPosition, desiredHipsPos, b);
                hips.localRotation = Quaternion.Slerp(animHips, desHips, b);
            }
            if (spine != null) spine.localRotation = Quaternion.Slerp(animSpine, desSpine, b);
            if (spine01 != null) spine01.localRotation = Quaternion.Slerp(animSpine01, desSpine01, b);
            if (spine02 != null) spine02.localRotation = Quaternion.Slerp(animSpine02, desSpine02, b);
            if (neck != null) neck.localRotation = Quaternion.Slerp(animNeck, desNeck, b);
            if (head != null) head.localRotation = Quaternion.Slerp(animHead, desHead, b);
            if (leftShoulder != null) leftShoulder.localRotation = Quaternion.Slerp(animLShoulder, desLShoulder, b);
            if (rightShoulder != null) rightShoulder.localRotation = Quaternion.Slerp(animRShoulder, desRShoulder, b);
            if (leftArm != null) leftArm.localRotation = Quaternion.Slerp(animLArm, desLArm, b);
            if (rightArm != null) rightArm.localRotation = Quaternion.Slerp(animRArm, desRArm, b);
            if (leftForeArm != null) leftForeArm.localRotation = Quaternion.Slerp(animLForeArm, desLForeArm, b);
            if (rightForeArm != null) rightForeArm.localRotation = Quaternion.Slerp(animRForeArm, desRForeArm, b);
            if (leftHand != null) leftHand.localRotation = Quaternion.Slerp(animLHand, desLHand, b);
            if (rightHand != null) rightHand.localRotation = Quaternion.Slerp(animRHand, desRHand, b);
            if (leftUpLeg != null) leftUpLeg.localRotation = Quaternion.Slerp(animLUpLeg, desLUpLeg, b);
            if (rightUpLeg != null) rightUpLeg.localRotation = Quaternion.Slerp(animRUpLeg, desRUpLeg, b);
            if (leftLeg != null) leftLeg.localRotation = Quaternion.Slerp(animLLeg, desLLeg, b);
            if (rightLeg != null) rightLeg.localRotation = Quaternion.Slerp(animRLeg, desRLeg, b);
        }

        private void ApplyLocalRotation(Transform target, Vector3 eulerOffset)
        {
            if (target == null || !baseRotations.TryGetValue(target, out Quaternion baseRotation))
            {
                return;
            }

            Quaternion desired = baseRotation * Quaternion.Euler(eulerOffset);
            target.localRotation = Quaternion.Slerp(target.localRotation, desired, Mathf.Clamp01(blend));
        }

        private Phase4Pose PickAmbientPose()
        {
            int pick = Random.Range(0, 100);
            if (pick < 50) return Phase4Pose.IdleObserve;
            if (pick < 72) return Phase4Pose.CrouchWhisper;
            if (pick < 88) return Phase4Pose.PointAtWildlife;
            return Phase4Pose.PhotoComment;
        }

        private void ScheduleAutoSwitch()
        {
            nextAutoSwitchAt = Time.time + Random.Range(stateMinSeconds, stateMaxSeconds);
        }
    }
}
