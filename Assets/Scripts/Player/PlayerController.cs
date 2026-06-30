using UnityEngine;
using UnityEngine.InputSystem;

namespace RungTramTraSu
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float walkSpeed = 3.0f;
        [SerializeField] private float crouchSpeed = 1.5f;
        [SerializeField] private float gravity = -9.81f;
        [SerializeField] private float jumpForce = 5.0f;
        [SerializeField] private float groundedStickForce = -4.0f;
        [SerializeField] private float airGravityMultiplier = 1.65f;
        [SerializeField] private float terminalFallSpeed = -28.0f;

        [Header("Grounding")]
        [SerializeField] private bool snapToGroundOnStart = true;
        [SerializeField] private float groundSnapProbeHeight = 3.0f;
        [SerializeField] private float groundSnapProbeDistance = 8.0f;
        [SerializeField] private float groundSnapOffset = 0.05f;
        [SerializeField] private float groundProbeDistance = 0.35f;

        [Header("Camera Settings")]
        [SerializeField] private Transform playerCamera;
        [SerializeField] private float mouseSensitivity = 0.1f;
        [SerializeField] private float upperLookLimit = 80.0f;
        [SerializeField] private float lowerLookLimit = -80.0f;

        [Header("Crouch Settings")]
        [SerializeField] private float standingHeight = 2.0f;
        [SerializeField] private float crouchHeight = 1.2f;
        private float cameraStandingY = 1.3f; // Mắt tầm cao đứng tự nhiên (đã giảm xuống 1.3f để tránh bị cao quá và chạm trần nhà)
        private float cameraCrouchY = 0.8f;    // Mắt tầm thấp cúi tự nhiên (giảm xuống 0.8f để cân đối khi cúi)
        [SerializeField] private float crouchTransitionSpeed = 8.0f;

        private CharacterController characterController;
        private PlayerInput playerInput;

        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction crouchAction;

        private Vector2 currentInput;
        private Vector3 moveDirection;
        private float verticalRotation = 0.0f;
        private float targetCameraY;
        private bool isCrouching = false;
        private bool isFrozen = false;
        private bool isMovementLocked = false;

        private InputAction jumpAction;
        private bool isSwimming = false;
        private float waterSurfaceY = -1.0f;
        private float swimLevelOffset = 1.3f; // Submerges player to shoulders
        private float swimVerticalVelocity = 0f;

        public bool IsCrouching => isCrouching;

        private void Awake()
        {
            // Tối ưu hóa FPS và bật VSync để tránh quá tải CPU/GPU gây lag Editor
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = 60;

            characterController = GetComponent<CharacterController>();
            playerInput = GetComponent<PlayerInput>();

            // Lấy các action từ Input Action Asset đã gán trên PlayerInput
            moveAction = playerInput.actions["Move"];
            lookAction = playerInput.actions["Look"];
            crouchAction = playerInput.actions["Crouch"];
            jumpAction = playerInput.actions["Jump"];
            
            targetCameraY = cameraStandingY;

            // Tự động tìm kiếm camera nếu bị trống do lỗi liên kết ở các Phase sau
            if (playerCamera == null && Camera.main != null)
            {
                playerCamera = Camera.main.transform;
            }
        }

        private void OnEnable()
        {
            if (crouchAction != null)
            {
                crouchAction.started += OnCrouchStarted;
            }
        }

        private void OnDisable()
        {
            if (crouchAction != null)
            {
                crouchAction.started -= OnCrouchStarted;
            }
        }

        private void Start()
        {
            if (snapToGroundOnStart)
            {
                SnapToGroundIfNeeded();
            }

            moveDirection.y = groundedStickForce;
            // Khóa chuột vào tâm màn hình
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            bool wasSwimming = isSwimming;
            UpdateWaterState();

            // Handle splash triggers and crouch resets on entry
            if (isSwimming && !wasSwimming)
            {
                TriggerWaterSplash(transform.position);

                // Reset crouch state immediately when entering water
                if (isCrouching)
                {
                    isCrouching = false;
                    characterController.height = standingHeight;
                    characterController.center = new Vector3(0f, standingHeight / 2f, 0f); // Reset center offset
                    targetCameraY = cameraStandingY;
                    if (playerCamera != null)
                    {
                        Vector3 camPos = playerCamera.localPosition;
                        camPos.y = cameraStandingY;
                        playerCamera.localPosition = camPos;
                    }
                }
            }

            if (isFrozen)
            {
                // Áp dụng trọng lực ngay cả khi đứng im
                if (characterController != null && characterController.enabled && !IsGroundedOrNearGround())
                {
                    moveDirection.y = Mathf.Max(moveDirection.y + gravity * airGravityMultiplier * Time.deltaTime, terminalFallSpeed);
                    characterController.Move(moveDirection * Time.deltaTime);
                }
                return;
            }

            if (!isMovementLocked)
            {
                HandleMovement();
            }
            else
            {
                // Áp dụng trọng lực khi khóa di chuyển (nếu CharacterController bật)
                if (characterController != null && characterController.enabled && !IsGroundedOrNearGround())
                {
                    moveDirection = new Vector3(0, Mathf.Max(moveDirection.y + gravity * airGravityMultiplier * Time.deltaTime, terminalFallSpeed), 0);
                    characterController.Move(moveDirection * Time.deltaTime);
                }
            }

            HandleMouseLook();
            HandleCrouchTransition();
        }

        private void HandleMovement()
        {
            // Đọc di chuyển từ WASD
            Vector2 rawInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;
            currentInput = rawInput;

            if (isSwimming)
            {
                float swimSpeed = crouchSpeed; // Use crouch speed as swim speed

                // Calculate 3D movement direction based on camera angle
                Vector3 right = transform.TransformDirection(Vector3.right);
                Vector3 forward = transform.TransformDirection(Vector3.forward);
                if (playerCamera != null)
                {
                    // Allow vertical swimming directed by looking up/down
                    forward = playerCamera.forward;
                }

                moveDirection = (forward * currentInput.y) + (right * currentInput.x);
                if (moveDirection.sqrMagnitude > 0.0001f)
                {
                    moveDirection.Normalize();
                }
                moveDirection *= swimSpeed;
                float cameraY = moveDirection.y;

                // Vertical controls (Ascend/Descend): Use input system Jump action
                bool ascendPressed = jumpAction != null && jumpAction.IsPressed();
                bool descendPressed = crouchAction != null && crouchAction.IsPressed();

                if (ascendPressed)
                {
                    swimVerticalVelocity = 2.5f;
                }
                else if (descendPressed)
                {
                    swimVerticalVelocity = -2.5f;
                }
                else
                {
                    // Buoyancy spring force pulling towards shoulder level Y
                    float targetSwimY = waterSurfaceY - swimLevelOffset;
                    float yDiff = targetSwimY - transform.position.y;
                    float buoyancyForce = yDiff * 5.0f;

                    // Accumulate damped gravity and buoyancy spring
                    swimVerticalVelocity += (gravity * 0.1f + buoyancyForce) * Time.deltaTime;

                    // Damp vertical velocity towards rest
                    swimVerticalVelocity = Mathf.MoveTowards(swimVerticalVelocity, 0f, 2.0f * Time.deltaTime);
                }

                // Combine camera-directed vertical swimming with vertical controls/buoyancy
                if (currentInput.y != 0f && !ascendPressed && !descendPressed)
                {
                    // Blend camera Y with soft buoyancy
                    moveDirection.y = cameraY + swimVerticalVelocity * 0.2f;
                }
                else
                {
                    moveDirection.y = swimVerticalVelocity;
                }

                // Move the CharacterController
                characterController.Move(moveDirection * Time.deltaTime);

                // Spawn water ripples while moving
                if (currentInput.magnitude > 0.1f && Time.frameCount % 15 == 0)
                {
                    TriggerWaterRipple(transform.position);
                }
            }
            else
            {
                float speed = isCrouching ? crouchSpeed : walkSpeed;
                float movementDirectionY = moveDirection.y;

                // Tính toán hướng di chuyển dựa trên góc nhìn của người chơi
                Vector3 forward = transform.TransformDirection(Vector3.forward);
                Vector3 right = transform.TransformDirection(Vector3.right);

                moveDirection = (forward * currentInput.y) + (right * currentInput.x);
                if (moveDirection.sqrMagnitude > 0.0001f)
                {
                    moveDirection.Normalize();
                }
                moveDirection *= speed;

                // Đọc nút nhảy
                bool jumpPressed = jumpAction != null && jumpAction.triggered;

                // Áp dụng trọng lực và nhảy
                bool isGrounded = IsGroundedOrNearGround();
                if (isGrounded)
                {
                    if (jumpPressed)
                    {
                        moveDirection.y = jumpForce;

                        // Hủy trạng thái cúi nếu đang cúi khi nhảy
                        if (isCrouching)
                        {
                            isCrouching = false;
                            characterController.height = standingHeight;
                            characterController.center = new Vector3(0, standingHeight / 2f, 0);
                            targetCameraY = cameraStandingY;
                        }
                    }
                    else
                    {
                        moveDirection.y = groundedStickForce; // Keep the controller planted on uneven terrain.
                    }
                }
                else
                {
                    moveDirection.y = Mathf.Max(movementDirectionY + (gravity * airGravityMultiplier * Time.deltaTime), terminalFallSpeed);
                }

                // Di chuyển CharacterController
                characterController.Move(moveDirection * Time.deltaTime);
            }
        }

        private void UpdateWaterState()
        {
            if (WaterWaveDeformer.Instance == null)
            {
                waterSurfaceY = -1.0f;
                isSwimming = false;
                swimVerticalVelocity = 0f;
                return;
            }

            waterSurfaceY = WaterWaveDeformer.Instance.GetWaveHeight(transform.position.x, transform.position.z);

            bool nearWalkableGround = IsGroundedOrNearGround();
            float feetY = GetControllerFeetY();
            float waterDepthAtFeet = waterSurfaceY - feetY;

            if (isSwimming)
            {
                if (transform.position.y > waterSurfaceY + 0.25f || (nearWalkableGround && waterDepthAtFeet < 1.0f))
                {
                    isSwimming = false;
                    swimVerticalVelocity = 0f;
                }
            }
            else if (!nearWalkableGround && waterDepthAtFeet > 1.2f)
            {
                isSwimming = true;
            }
        }

        private float GetControllerFeetY()
        {
            if (characterController == null)
            {
                return transform.position.y;
            }

            return transform.position.y + characterController.center.y - characterController.height * 0.5f;
        }

        private bool IsGroundedOrNearGround()
        {
            if (characterController != null && characterController.isGrounded)
            {
                return true;
            }

            RaycastHit hit;
            return TryFindGround(out hit, 0.2f, groundProbeDistance);
        }

        private void SnapToGroundIfNeeded()
        {
            RaycastHit hit;
            if (!TryFindGround(out hit, groundSnapProbeHeight, groundSnapProbeDistance))
            {
                return;
            }

            float feetY = GetControllerFeetY();
            float targetFeetY = hit.point.y + groundSnapOffset;
            float deltaY = targetFeetY - feetY;

            if (Mathf.Abs(deltaY) > 0.05f && Mathf.Abs(deltaY) <= groundSnapProbeDistance)
            {
                transform.position += Vector3.up * deltaY;
            }
        }

        private bool TryFindGround(out RaycastHit bestHit, float probeHeight, float probeDistance)
        {
            bestHit = new RaycastHit();

            Vector3 origin = transform.position;
            origin.y = GetControllerFeetY() + probeHeight;

            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, probeHeight + probeDistance, ~0, QueryTriggerInteraction.Ignore);
            if (hits == null || hits.Length == 0)
            {
                return false;
            }

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider hitCollider = hits[i].collider;
                if (hitCollider == null) continue;
                if (hitCollider.transform == transform || hitCollider.transform.IsChildOf(transform)) continue;
                if (hits[i].normal.y < 0.45f) continue;

                bestHit = hits[i];
                return true;
            }

            return false;
        }

        private void HandleMouseLook()
        {
            // Đọc độ dời chuột (Look)
            Vector2 lookInput = lookAction != null ? lookAction.ReadValue<Vector2>() : Vector2.zero;

            // Xoay ngang (Player Body)
            transform.Rotate(Vector3.up * lookInput.x * mouseSensitivity);

            // Xoay dọc (Camera)
            verticalRotation -= lookInput.y * mouseSensitivity;
            verticalRotation = Mathf.Clamp(verticalRotation, lowerLookLimit, upperLookLimit);

            if (playerCamera != null)
            {
                playerCamera.localRotation = Quaternion.Euler(verticalRotation, 0, 0);
            }
        }

        public void ForceSetLookRotation(Quaternion bodyRotation, float cameraPitch)
        {
            transform.rotation = bodyRotation;
            verticalRotation = cameraPitch;
            if (playerCamera != null)
            {
                playerCamera.localRotation = Quaternion.Euler(verticalRotation, 0, 0);
            }
        }

        private void OnCrouchStarted(InputAction.CallbackContext context)
        {
            if (isFrozen) return;
            if (isSwimming) return; // Ignore crouch toggle while swimming

            // Đảo trạng thái Crouch khi nhấn phím C
            isCrouching = !isCrouching;

            // Thiết lập mục tiêu chiều cao và camera
            characterController.height = isCrouching ? crouchHeight : standingHeight;
            targetCameraY = isCrouching ? cameraCrouchY : cameraStandingY;

            // Điều chỉnh vị trí của CharacterController center để không bị lún đất
            characterController.center = new Vector3(0, characterController.height / 2f, 0);
        }

        private void HandleCrouchTransition()
        {
            if (playerCamera == null) return;

            // Tạo hiệu ứng chuyển đổi mượt mà cho camera khi cúi người
            Vector3 camPos = playerCamera.localPosition;
            camPos.y = Mathf.Lerp(camPos.y, targetCameraY, Time.deltaTime * crouchTransitionSpeed);
            playerCamera.localPosition = camPos;
        }

        /// <summary>
        /// Khóa hoặc mở khóa di chuyển của người chơi (dùng khi thoại, chuyển cảnh)
        /// </summary>
        public void SetFrozen(bool frozen)
        {
            isFrozen = frozen;
            if (frozen)
            {
                moveDirection = Vector3.zero;
                // Mở khóa chuột khi hội thoại để tương tác UI nếu cần
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        /// <summary>
        /// Khóa hoặc mở khóa di chuyển WASD của người chơi nhưng vẫn cho phép xoay chuột nhìn
        /// </summary>
        public void SetMovementLocked(bool locked)
        {
            isMovementLocked = locked;
            if (locked)
            {
                moveDirection = Vector3.zero;
            }
        }

        private void TriggerWaterSplash(Vector3 position)
        {
            GameObject splashObj = new GameObject("PlayerSplashParticles");
            splashObj.transform.position = new Vector3(position.x, waterSurfaceY, position.z);

            ParticleSystem ps = splashObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 1.0f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.25f);
            main.gravityModifier = 0.8f;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 25) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 20f;
            shape.radius = 0.3f;

            var colorOver = ps.colorOverLifetime;
            colorOver.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(new Color(0.7f, 0.85f, 0.95f), 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.8f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            colorOver.color = grad;

            var renderer = splashObj.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                Texture defaultTex = renderer.sharedMaterial != null ? renderer.sharedMaterial.mainTexture : null;

                Material splashMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                splashMat.SetColor("_BaseColor", new Color(1.0f, 1.0f, 1.0f, 0.6f));
                if (defaultTex != null)
                {
                    splashMat.SetTexture("_BaseMap", defaultTex);
                }
                splashMat.SetFloat("_Surface", 1.0f); // Transparent
                splashMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                splashMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                splashMat.SetInt("_ZWrite", 0);
                splashMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                splashMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                renderer.material = splashMat;

                splashObj.AddComponent<MaterialCleanupHelper>().material = splashMat;
            }

            ps.Play();
            Destroy(splashObj, 1.2f);
        }

        private void TriggerWaterRipple(Vector3 position)
        {
            GameObject rippleObj = new GameObject("PlayerSwimRipple");
            rippleObj.transform.position = new Vector3(position.x, waterSurfaceY + 0.01f, position.z);

            ParticleSystem ps = rippleObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = 0.5f;
            main.startSpeed = 0f;
            main.startSize = 0.2f;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.0f, 3) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.1f;

            var sizeOver = ps.sizeOverLifetime;
            sizeOver.enabled = true;
            sizeOver.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1.0f, 1f, 4.0f));

            var colorOver = ps.colorOverLifetime;
            colorOver.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.4f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            colorOver.color = grad;

            var renderer = rippleObj.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                Texture defaultTex = renderer.sharedMaterial != null ? renderer.sharedMaterial.mainTexture : null;

                Material rippleMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                rippleMat.SetColor("_BaseColor", new Color(1.0f, 1.0f, 1.0f, 0.3f));
                if (defaultTex != null)
                {
                    rippleMat.SetTexture("_BaseMap", defaultTex);
                }
                rippleMat.SetFloat("_Surface", 1.0f); // Transparent
                rippleMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                rippleMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                rippleMat.SetInt("_ZWrite", 0);
                rippleMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                rippleMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                renderer.material = rippleMat;

                rippleObj.AddComponent<MaterialCleanupHelper>().material = rippleMat;
            }

            ps.Play();
            Destroy(rippleObj, 0.8f);
        }
    }

    // Helper class to release runtime material memory leaks
    public class MaterialCleanupHelper : MonoBehaviour
    {
        public Material material;
        private void OnDestroy()
        {
            if (material != null)
            {
                Destroy(material);
            }
        }
    }
}
