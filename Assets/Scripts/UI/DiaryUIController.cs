using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;

namespace RungTramTraSu
{
    public class DiaryUIController : MonoBehaviour
    {
        public static DiaryUIController Instance { get; private set; }

        [Header("UI Panels")]
        [SerializeField] private GameObject diaryPanel;          // The main Sổ Nhật Ký UI overlay panel

        [Header("Polaroid Raw Images")]
        [SerializeField] private RawImage imgPhase1Mango;
        [SerializeField] private RawImage imgPhase2Ch1;
        [SerializeField] private RawImage imgPhase2Ch2;
        [SerializeField] private RawImage imgPhase2Ch3;
        [SerializeField] private RawImage imgPhase4Stork;
        [SerializeField] private RawImage imgPhase4Snake;
        [SerializeField] private RawImage imgPhase4Fish;
        [SerializeField] private RawImage imgPhase4Butterfly;
        [SerializeField] private RawImage imgPhase4Duck;
        [SerializeField] private RawImage imgPhase5Sunset;

        [Header("Inventory Item Icon")]
        [SerializeField] private GameObject cameraInventoryIcon; // Visual showing camera in inventory

        private PlayerController playerController;
        private bool isOpen = false;

        public bool IsOpen => isOpen;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            if (diaryPanel != null) diaryPanel.SetActive(false);
            
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                playerController = playerObj.GetComponent<PlayerController>();
            }
        }

        private void Update()
        {
            // Allow opening Diary starting from Phase 1/Phase 2
            if (Keyboard.current != null && (Keyboard.current.tabKey.wasPressedThisFrame || Keyboard.current.iKey.wasPressedThisFrame))
            {
                ToggleDiary();
            }
        }

        public void ToggleDiary()
        {
            if (diaryPanel == null) return;

            isOpen = !isOpen;
            diaryPanel.SetActive(isOpen);

            if (playerController == null)
            {
                GameObject playerObj = GameObject.FindWithTag("Player");
                if (playerObj != null) playerController = playerObj.GetComponent<PlayerController>();
            }

            if (isOpen)
            {
                // Freeze player controls and show cursor
                if (playerController != null)
                {
                    playerController.SetFrozen(true);
                }
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                // Load photos into frames
                PopulatePhotos();
            }
            else
            {
                // Resume player controls and hide cursor
                if (playerController != null)
                {
                    playerController.SetFrozen(false);
                }
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        public void PopulatePhotos()
        {
            if (PersistentGameManager.Instance == null) return;

            // Check if player has obtained camera
            bool hasCamera = false;
            var photoCamera = FindAnyObjectByType<PhotoCamera>();
            if (photoCamera != null) hasCamera = photoCamera.HasCamera;
            // Also enable camera icon in inventory if player obtained it
            if (cameraInventoryIcon != null) cameraInventoryIcon.SetActive(hasCamera);

            AssignPhotoToUI("Phase1_Mango", imgPhase1Mango);
            AssignPhotoToUI("Phase2_Ch1", imgPhase2Ch1);
            AssignPhotoToUI("Phase2_Ch2", imgPhase2Ch2);
            AssignPhotoToUI("Phase2_Ch3", imgPhase2Ch3);
            AssignPhotoToUI("Phase4_Stork", imgPhase4Stork);
            AssignPhotoToUI("Phase4_Snake", imgPhase4Snake);
            AssignPhotoToUI("Phase4_Fish", imgPhase4Fish);
            AssignPhotoToUI("Phase4_Butterfly", imgPhase4Butterfly);
            AssignPhotoToUI("Phase4_Duck", imgPhase4Duck);
            AssignPhotoToUI("Phase5_Sunset", imgPhase5Sunset);
        }

        private void AssignPhotoToUI(string category, RawImage uiImage)
        {
            if (uiImage == null) return;

            Texture2D tex = PersistentGameManager.Instance.GetPhoto(category);
            if (tex != null)
            {
                uiImage.texture = tex;
                uiImage.color = Color.white; // Make visible
            }
            else
            {
                uiImage.texture = null;
                uiImage.color = new Color(0.2f, 0.2f, 0.2f, 0.5f); // Grey placeholder
            }
        }
    }
}
