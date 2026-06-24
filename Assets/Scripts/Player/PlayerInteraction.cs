using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RungTramTraSu
{
    [RequireComponent(typeof(PlayerInput))]
    public class PlayerInteraction : MonoBehaviour
    {
        [Header("Raycast Settings")]
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float interactDistance = 3.5f;
        [SerializeField] private LayerMask interactableLayer;

        private PlayerInput playerInput;
        private InputAction interactAction;
        private IInteractable currentInteractable;

        // Sự kiện dùng để cập nhật UI
        public static event Action<string> OnInteractableFound;
        public static event Action OnInteractableLost;

        private void Awake()
        {
            playerInput = GetComponent<PlayerInput>();
            interactAction = playerInput.actions["Interact"];

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
        }

        private void Start()
        {
            if (interactAction == null)
            {
                Debug.LogError("[PlayerInteraction] Interact Action is null! Please check Input Action Asset assignment.");
            }
            else
            {
                Debug.Log($"[PlayerInteraction] Interact Action found: {interactAction.name}, Enabled: {interactAction.enabled}");
            }
        }

        private void OnEnable()
        {
            if (interactAction != null)
            {
                interactAction.started += OnInteractActionStarted;
            }
        }

        private void OnDisable()
        {
            if (interactAction != null)
            {
                interactAction.started -= OnInteractActionStarted;
            }
        }

        private void OnInteractActionStarted(InputAction.CallbackContext context)
        {
            Debug.Log("[PlayerInteraction] Interact action started via callback event!");
            TryInteract();
        }

        private void Update()
        {
            CheckInteractable();
        }

        private void CheckInteractable()
        {
            if (cameraTransform == null) return;

            Ray ray = new Ray(cameraTransform.position, cameraTransform.forward);
            RaycastHit hit;

            // Bắn tia raycast không lọc layer trước để xem nó có đâm trúng gì không
            if (Physics.Raycast(ray, out hit, interactDistance))
            {
                IInteractable interactable = hit.collider.GetComponent<IInteractable>();
                if (interactable != null)
                {
                    // Lọc theo layer mask
                    if (((1 << hit.collider.gameObject.layer) & interactableLayer) != 0)
                    {
                        if (interactable != currentInteractable)
                        {
                            currentInteractable = interactable;
                            Debug.Log($"[PlayerInteraction] Found interactable: {hit.collider.name} on layer {hit.collider.gameObject.layer}");
                            OnInteractableFound?.Invoke(currentInteractable.GetInteractPrompt());
                        }
                        return;
                    }
                    else
                    {
                        Debug.LogWarning($"[PlayerInteraction] Looked at {hit.collider.name} which has IInteractable, but its layer ({hit.collider.gameObject.layer}) is not in mask ({interactableLayer.value})");
                    }
                }
            }

            // Nếu không trúng gì hoặc trúng vật thể không thể tương tác
            if (currentInteractable != null)
            {
                currentInteractable = null;
                Debug.Log("[PlayerInteraction] Lost interactable target");
                OnInteractableLost?.Invoke();
            }
        }

        private void TryInteract()
        {
            if (currentInteractable != null)
            {
                Debug.Log($"[PlayerInteraction] Interacting with: {currentInteractable}");
                currentInteractable.Interact();
            }
            else
            {
                Debug.LogWarning("[PlayerInteraction] TryInteract called but currentInteractable is null!");
            }
        }
    }
}
