using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using RungTramTraSu.CameraSystem;

namespace RungTramTraSu
{
    public class DiaryUIController : MonoBehaviour
    {
        public static DiaryUIController Instance { get; private set; }

        [Header("UI Panels")]
        [SerializeField] private GameObject diaryPanel;

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
        [SerializeField] private GameObject cameraInventoryIcon;

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
            if (Keyboard.current != null && (Keyboard.current.tabKey.wasPressedThisFrame || Keyboard.current.iKey.wasPressedThisFrame))
            {
                // Don't open diary if camera is currently active (aiming/viewfinder is active) to prevent key conflicts with Focus Lock and ISO
                if (!isOpen && CameraManager.Instance != null && CameraManager.Instance.IsCameraActive) return;

                // Don't open diary if camera manual guide is active to avoid overlay clashes
                if (CameraGuide.Instance != null && CameraGuide.Instance.gameObject.activeSelf) return;
                
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
                if (playerController != null)
                {
                    playerController.SetFrozen(true);
                }
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                PopulatePhotos();
            }
            else
            {
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
            // Sync inventory item indicator
            bool hasCamera = false;
            var photoCamera = FindAnyObjectByType<PhotoCamera>();
            if (photoCamera != null) hasCamera = photoCamera.HasCamera;
            if (cameraInventoryIcon != null) cameraInventoryIcon.SetActive(hasCamera);

            // Populate with mapped Album keys
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

            Texture2D tex = null;
            AlbumManager.AlbumEntry entry = null;

            // 1. Try to load from AlbumManager if available
            if (AlbumManager.Instance != null)
            {
                string albumKey = GetAlbumKeyFromCategory(category);
                if (!string.IsNullOrEmpty(albumKey))
                {
                    entry = AlbumManager.Instance.GetEntry(albumKey);
                    if (entry != null)
                    {
                        tex = entry.cachedPhoto;
                    }
                }

                // If specific key not found, check wildcards for birds in Phase 2
                if (tex == null && category.StartsWith("Phase2_"))
                {
                    // Look for any bird entry in the album
                    var birds = AlbumManager.Instance.GetEntriesByCategory("Birds");
                    int birdIndex = 0;
                    if (category == "Phase2_Ch2") birdIndex = 1;
                    if (category == "Phase2_Ch3") birdIndex = 2;

                    if (birds.Count > birdIndex)
                    {
                        entry = birds[birdIndex];
                        tex = entry.cachedPhoto;
                    }
                }
            }

            // 2. Fall back to PersistentGameManager
            if (tex == null && PersistentGameManager.Instance != null)
            {
                tex = PersistentGameManager.Instance.GetPhoto(category);
            }

            // Bind values
            if (tex != null)
            {
                uiImage.texture = tex;
                uiImage.color = Color.white;

                // Find sibling TextMeshProUGUI to write score overlay
                TextMeshProUGUI textMesh = uiImage.transform.parent != null ? uiImage.transform.parent.GetComponentInChildren<TextMeshProUGUI>() : null;
                if (textMesh != null && entry != null)
                {
                    string stars = "";
                    for (int s = 0; s < entry.starRating; s++) stars += "★";
                    textMesh.text = $"{entry.vietnameseName}\nScore: {entry.bestScore:F0} {stars}";
                    textMesh.fontSize = 11f;
                }
            }
            else
            {
                uiImage.texture = null;
                uiImage.color = new Color(0.18f, 0.18f, 0.18f, 0.65f);

                TextMeshProUGUI textMesh = uiImage.transform.parent != null ? uiImage.transform.parent.GetComponentInChildren<TextMeshProUGUI>() : null;
                if (textMesh != null)
                {
                    textMesh.text = "Chưa Chụp";
                    textMesh.fontSize = 13f;
                }
            }
        }

        private string GetAlbumKeyFromCategory(string category)
        {
            switch (category)
            {
                case "Phase1_Mango": return "Cây Xoài Cổ Thụ";
                case "Phase4_Stork": return "Cò Trắng";
                case "Phase4_Snake": return "Rắn Nước";
                case "Phase4_Fish": return "Cá Lóc";
                case "Phase4_Butterfly": return "Bướm Hoa Súng";
                case "Phase4_Duck": return "Vịt Trời";
                case "Phase5_Sunset": return "Hoàng Hôn Rừng Tràm";
                default: return "";
            }
        }
    }
}
