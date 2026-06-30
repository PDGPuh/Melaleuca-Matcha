using UnityEngine;

namespace RungTramTraSu
{
    public class NPCGrandpa : MonoBehaviour, IInteractable
    {
        // Animation
        private Animator animator;
        private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
        private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        // Rowing & Posing
        private Transform hips;
        private Transform leftUpLeg;
        private Transform rightUpLeg;
        private Transform leftLeg;
        private Transform rightLeg;
        private Transform spine;
        private Transform leftArm;
        private Transform rightArm;
        private Transform leftForeArm;
        private Transform rightForeArm;
        private GameObject leftOar;
        private GameObject rightOar;
        private bool wasOnBoat = false;

        [Header("Dialogue Sets")]
        [SerializeField]
        private string[] introDialogue = new string[]
        {
            "Dậy rồi đó hả con? Đêm qua ngủ ngon giấc không?",
            "Sáng nay trời mát mẻ dữ lắm. Ông ngoại chuẩn bị xuồng sẵn rồi, lát hai ông cháu mình đi chơi rừng tràm Trà Sư nghe.",
            "Mà nè, ông ngoại có cái này cho con...",
            "Đây là chiếc máy ảnh phim cũ của ông hồi xưa. Con giữ lấy đi.",
            "Đi chơi rừng đẹp lắm, con cầm máy theo chụp lại làm kỷ niệm.",
            "Nè, con thử cầm lên, bấm chuột phải để ngắm góc rồi chụp thử cây xoài to đằng kia cho ông xem coi có hoạt động tốt không nha!"
        };

        [SerializeField]
        private string[] waitingForPhotoDialogue = new string[]
        {
            "Con cứ từ từ thử xem. Nhấn chuột phải để ngắm và click chuột trái để chụp cây xoài to đằng kia kìa. Thử chụp một tấm đẹp đẹp cho ông coi thử coi."
        };

        [SerializeField]
        private string[] photoTakenDialogue = new string[]
        {
            "Đâu, đưa ông coi tấm hình thử... Ừa! Đẹp lắm con, máy ảnh cũ vậy chứ chụp vẫn bén ngót hà.",
            "Thôi, chuẩn bị đồ đạc rồi hai ông cháu mình xuống xuồng đi con. Đứng đây nắng lên nóng lắm. Bước xuống chiếc xuồng dưới bến nước kia kìa, ông chèo đưa đi."
        };

        [SerializeField]
        private string[] finalWaitingDialogue = new string[]
        {
            "Mau xuống xuồng đi con, ông chèo đưa con đi sâu vào rừng tràm mát lắm."
        };

        public string GetInteractPrompt()
        {
            return "Nói chuyện với Ông Ngoại";
        }

        public void Interact()
        {
            Phase1Manager manager = Phase1Manager.Instance;
            if (manager == null)
            {
                // Check if Phase 5 is active
                var phase5 = Phase5Manager.Instance;
                if (phase5 != null)
                {
                    phase5.StartClimaxDialogue();
                    return;
                }

                Debug.LogWarning("Không tìm thấy Phase1Manager hoặc Phase5Manager. Không thể kích hoạt thoại!");
                return;
            }

            switch (manager.CurrentState)
            {
                case Phase1Manager.Phase1State.Intro:
                    // Bắt đầu hội thoại lần đầu, sau đó kích hoạt nhận máy ảnh
                    // Guard: chỉ trigger nếu chưa bắt đầu (tránh nhấn E nhiều lần)
                    if (!manager.IsIntroDialogueStarted)
                    {
                        manager.IsIntroDialogueStarted = true;
                        DialogueManager.Instance.ShowDialogue("Ông Ngoại", introDialogue, () => {
                            manager.GiveCameraToPlayer();
                        });
                    }
                    break;

                case Phase1Manager.Phase1State.TakingPhoto:
                    // Đang chờ chụp ảnh
                    DialogueManager.Instance.ShowDialogue("Ông Ngoại", waitingForPhotoDialogue);
                    break;

                case Phase1Manager.Phase1State.PhotoTaken:
                    // Đã chụp ảnh xong, nói chuyện để chuyển sang bước lên xuồng
                    DialogueManager.Instance.ShowDialogue("Ông Ngoại", photoTakenDialogue, () => {
                        manager.SetReadyForBoat();
                    });
                    break;

                case Phase1Manager.Phase1State.TalkedAgain:
                case Phase1Manager.Phase1State.OnBoat:
                    // Đã nhắc xuống xuồng
                    DialogueManager.Instance.ShowDialogue("Ông Ngoại", finalWaitingDialogue);
                    break;
            }
        }

        private bool isWalkingToBoat = false;
        private float walkSpeed = 1.8f;
        private int currentWaypointIndex = 0;
        private Vector3[] walkWaypoints = new Vector3[0];

        private void Start()
        {
            animator = GetComponent<Animator>();
            // Start in idle state
            SetAnimation(false, false);

            // Tự động đưa Ông Ngoại lên thuyền nếu đang ở cảnh Phase 2 hoặc Phase 3
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName == "Phase2_Canal" || sceneName == "Phase3_BambooBridge")
            {
                GameObject boatObj = GameObject.Find("Sampan Boat");
                if (boatObj != null)
                {
                    transform.SetParent(boatObj.transform, false);
                    transform.localScale = new Vector3(0.85f / 5f, 0.85f / 5f, 0.85f / 5f);
                    transform.localPosition = new Vector3(0.3f, 0.06f, 0f);
                    transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                }
            }
        }

        public void WalkToBoat()
        {
            isWalkingToBoat = true;
            currentWaypointIndex = 0;

            // Hand-crafted waypoints that follow the actual ground surface:
            // House floor (Y≈1.84) → Door (Y≈1.84, Z=1.62) → Porch (Y≈1.62) → Stepping stones (from Stone 4) → Pier (Y≈1.03)
            var pointsList = new System.Collections.Generic.List<Vector3>();

            // Phase 1: Walk across the house floor towards the door opening
            pointsList.Add(new Vector3(-5.5f, 1.84f, 3.12f));   // Inside house, start walking forward
            pointsList.Add(new Vector3(-3.5f, 1.84f, 1.62f));   // Turn towards the door opening (Z=1.62 is the door center)
            pointsList.Add(new Vector3(-1.15f, 1.84f, 1.62f));  // Directly in the middle of the door opening

            // Phase 2: Step down onto the porch
            pointsList.Add(new Vector3(-0.5f, 1.62f, 1.62f));   // Porch just outside the door
            pointsList.Add(new Vector3(0.0f,  1.58f, 2.08f));   // Align with the first stepping stone

            // Phase 3: Down to terrain level, following the stepping stones path
            GameObject stonesContainer = GameObject.Find("SteppingStonesPath");
            if (stonesContainer != null)
            {
                var stonePoints = new System.Collections.Generic.List<Vector3>();
                foreach (Transform child in stonesContainer.transform)
                {
                    stonePoints.Add(child.position);
                }
                // Sort by X so we walk in order from house toward pier
                stonePoints.Sort((a, b) => a.x.CompareTo(b.x));

                // We start from index 4 (Stone 4, X ≈ 0.5) to avoid backtracking to Stones 0-3 which are behind/under the house
                for (int i = 4; i < stonePoints.Count; i += 2)
                {
                    var p = stonePoints[i];
                    pointsList.Add(new Vector3(p.x, p.y + 0.05f, p.z));
                }

                // Always include the last stone
                if (stonePoints.Count > 0)
                {
                    var last = stonePoints[stonePoints.Count - 1];
                    var lastAdded = pointsList[pointsList.Count - 1];
                    if (Vector3.Distance(last, lastAdded) > 0.5f)
                    {
                        pointsList.Add(new Vector3(last.x, last.y + 0.05f, last.z));
                    }
                }
            }

            // Phase 4: Step onto the wooden pier/deck (Y = 1.03)
            pointsList.Add(new Vector3(13.0f, 1.03f, 8.0f));

            // Phase 5: Walk along the pier to near the boat
            pointsList.Add(new Vector3(17.5f, 1.03f, 8.0f));

            walkWaypoints = pointsList.ToArray();
        }

        private void Update()
        {
            if (isWalkingToBoat && walkWaypoints.Length > 0)
            {
                Vector3 targetPos = walkWaypoints[currentWaypointIndex];
                float step = walkSpeed * Time.deltaTime;
                Vector3 targetDir = targetPos - transform.position;
                targetDir.y = 0; // Rotate horizontal only

                if (Vector3.Distance(new Vector3(transform.position.x, 0f, transform.position.z), new Vector3(targetPos.x, 0f, targetPos.z)) > 0.2f)
                {
                    // Play walking animation while moving
                    SetAnimation(true, false);

                    if (targetDir.magnitude > 0.05f)
                    {
                        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(targetDir), 8.0f * Time.deltaTime);
                    }
                    transform.position = Vector3.MoveTowards(transform.position, targetPos, step);
                }
                else
                {
                    // Move to next waypoint or finish
                    if (currentWaypointIndex < walkWaypoints.Length - 1)
                    {
                        currentWaypointIndex++;
                    }
                    else
                    {
                        isWalkingToBoat = false;
                        // Stop walking animation
                        SetAnimation(false, false);
                        // Parent to boat and sit down
                        GameObject boatObj = GameObject.Find("Sampan Boat");
                        if (boatObj != null)
                        {
                            transform.SetParent(boatObj.transform, true);
                            transform.localPosition = new Vector3(0f, 0.06f, 0.3f); // puts him at world (21.0, -0.7, 8.0)
                            transform.localRotation = Quaternion.identity;
                        }
                    }
                }
            }
        }

        private void OnDisable()
        {
            if (leftOar != null) Destroy(leftOar);
            if (rightOar != null) Destroy(rightOar);
        }

        private void OnDestroy()
        {
            if (leftOar != null) Destroy(leftOar);
            if (rightOar != null) Destroy(rightOar);
        }

        private void InitializeBones()
        {
            hips = FindBoneRecursive(transform, "Hips");
            leftUpLeg = FindBoneRecursive(transform, "LeftUpLeg");
            rightUpLeg = FindBoneRecursive(transform, "RightUpLeg");
            leftLeg = FindBoneRecursive(transform, "LeftLeg");
            rightLeg = FindBoneRecursive(transform, "RightLeg");
            spine = FindBoneRecursive(transform, "Spine");
            leftArm = FindBoneRecursive(transform, "LeftArm");
            rightArm = FindBoneRecursive(transform, "RightArm");
            leftForeArm = FindBoneRecursive(transform, "LeftForeArm");
            rightForeArm = FindBoneRecursive(transform, "RightForeArm");
        }

        private Transform FindBoneRecursive(Transform parent, string name)
        {
            if (parent.name == name) return parent;
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform found = FindBoneRecursive(parent.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private void SpawnOars()
        {
            Transform leftHand = FindBoneRecursive(transform, "LeftHand");
            Transform rightHand = FindBoneRecursive(transform, "RightHand");

            if (leftHand == null || rightHand == null) return;

            Material woodMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            woodMat.color = new Color(0.35f, 0.22f, 0.12f); // Gỗ nâu mộc mạc

            // Left Oar
            leftOar = new GameObject("ProceduralOar_L");
            leftOar.transform.SetParent(leftHand, false);
            leftOar.transform.localPosition = Vector3.zero;
            // Left oar points to the left (-X) and down
            leftOar.transform.localRotation = Quaternion.Euler(0f, -90f, -30f);

            GameObject shaftL = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaftL.name = "Shaft";
            shaftL.transform.SetParent(leftOar.transform, false);
            shaftL.transform.localScale = new Vector3(0.06f, 1.5f, 0.06f); // 3m oar shaft
            shaftL.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            shaftL.GetComponent<Renderer>().sharedMaterial = woodMat;
            Destroy(shaftL.GetComponent<Collider>());

            GameObject bladeL = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bladeL.name = "Blade";
            bladeL.transform.SetParent(leftOar.transform, false);
            bladeL.transform.localScale = new Vector3(0.25f, 0.02f, 0.7f);
            bladeL.transform.localPosition = new Vector3(0f, 2.7f, 0f);
            bladeL.GetComponent<Renderer>().sharedMaterial = woodMat;
            Destroy(bladeL.GetComponent<Collider>());

            // Right Oar
            rightOar = new GameObject("ProceduralOar_R");
            rightOar.transform.SetParent(rightHand, false);
            rightOar.transform.localPosition = Vector3.zero;
            // Right oar points to the right (+X) and down
            rightOar.transform.localRotation = Quaternion.Euler(0f, 90f, 30f);

            GameObject shaftR = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shaftR.name = "Shaft";
            shaftR.transform.SetParent(rightOar.transform, false);
            shaftR.transform.localScale = new Vector3(0.06f, 1.5f, 0.06f);
            shaftR.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            shaftR.GetComponent<Renderer>().sharedMaterial = woodMat;
            Destroy(shaftR.GetComponent<Collider>());

            GameObject bladeR = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bladeR.name = "Blade";
            bladeR.transform.SetParent(rightOar.transform, false);
            bladeR.transform.localScale = new Vector3(0.25f, 0.02f, 0.7f);
            bladeR.transform.localPosition = new Vector3(0f, 2.7f, 0f);
            bladeR.GetComponent<Renderer>().sharedMaterial = woodMat;
            Destroy(bladeR.GetComponent<Collider>());
        }

        private void LateUpdate()
        {
            bool isOnBoat = transform.parent != null && transform.parent.name == "Sampan Boat";
            
            if (isOnBoat)
            {
                if (hips == null)
                {
                    InitializeBones();
                }
                if (leftOar == null || rightOar == null)
                {
                    SpawnOars();
                }

                if (hips == null) return;

                wasOnBoat = true;

                // 1. Lower hips to make him sit on the bench of the boat
                hips.localPosition = new Vector3(-0.06f, 38.0f, 1.31f);

                // 2. Rotate upper legs forward/up
                leftUpLeg.localRotation = Quaternion.Euler(-75f, 0f, 0f);
                rightUpLeg.localRotation = Quaternion.Euler(-75f, 0f, 0f);

                // 3. Rotate lower legs backward/down
                leftLeg.localRotation = Quaternion.Euler(80f, 0f, 0f);
                rightLeg.localRotation = Quaternion.Euler(80f, 0f, 0f);

                // 4. Animate rowing motion using a sine wave
                float time = Time.time * 2.0f; // Speed of rowing
                float wave = Mathf.Sin(time); // Ranges from -1 to 1

                // Spine leans forward and backward
                float spineAngle = 20f + wave * 10f;
                if (spine != null) spine.localRotation = Quaternion.Euler(spineAngle, 0f, 0f);

                // Arms reach forward
                float leftArmY = -60f - wave * 20f;
                float rightArmY = 60f + wave * 20f;
                if (leftArm != null) leftArm.localRotation = Quaternion.Euler(0f, leftArmY, 25f + wave * 10f);
                if (rightArm != null) rightArm.localRotation = Quaternion.Euler(0f, rightArmY, -25f - wave * 10f);

                // Elbows (Forearms) bend/extend slightly in sync
                float leftForeArmY = -40f + wave * 15f;
                float rightForeArmY = 40f - wave * 15f;
                if (leftForeArm != null) leftForeArm.localRotation = Quaternion.Euler(0f, leftForeArmY, 0f);
                if (rightForeArm != null) rightForeArm.localRotation = Quaternion.Euler(0f, rightForeArmY, 0f);
            }
            else
            {
                if (wasOnBoat)
                {
                    wasOnBoat = false;
                    if (leftOar != null) Destroy(leftOar);
                    if (rightOar != null) Destroy(rightOar);
                    if (animator != null) animator.Rebind();
                }
            }
        }

        /// <summary>
        /// Updates the Animator parameters to play the correct animation state.
        /// </summary>
        private void SetAnimation(bool walking, bool running)
        {
            if (animator == null) return;
            animator.SetBool(IsWalkingHash, walking);
            animator.SetBool(IsRunningHash, running);
            float speed = running ? 1.0f : (walking ? 0.5f : 0.0f);
            animator.SetFloat(SpeedHash, speed);
        }
    }
}
