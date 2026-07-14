using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RungTramTraSu.CameraSystem
{
    public class AlbumManager : MonoBehaviour
    {
        public static AlbumManager Instance { get; private set; }

        [System.Serializable]
        public class AlbumEntry
        {
            public string animalId; // e.g. "Stork", "Sếu đầu đỏ", "MangoTreeTarget"
            public string vietnameseName;
            public string scientificName;
            public string category;
            public string conservationStatus;
            public float bestScore;
            public int starRating;
            public string captureDate;
            public string captureLocation;
            public byte[] photoJPGBytes; // Serialized photo bytes
            [System.NonSerialized] public Texture2D cachedPhoto;
        }

        private Dictionary<string, AlbumEntry> albumEntries = new Dictionary<string, AlbumEntry>();

        public Dictionary<string, AlbumEntry> Entries => albumEntries;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                LoadAlbum();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public AlbumEntry GetEntry(string id)
        {
            if (albumEntries.TryGetValue(id, out AlbumEntry entry))
            {
                // Ensure texture is reconstructed if missing from deserialization
                if (entry.cachedPhoto == null && entry.photoJPGBytes != null && entry.photoJPGBytes.Length > 0)
                {
                    Texture2D tex = new Texture2D(2, 2);
                    tex.LoadImage(entry.photoJPGBytes);
                    entry.cachedPhoto = tex;
                }
                return entry;
            }
            return null;
        }

        public List<AlbumEntry> GetEntriesByCategory(string category)
        {
            List<AlbumEntry> list = new List<AlbumEntry>();
            foreach (var val in albumEntries.Values)
            {
                if (val.category.ToLower() == category.ToLower())
                {
                    // Reconstruct texture if needed
                    if (val.cachedPhoto == null && val.photoJPGBytes != null && val.photoJPGBytes.Length > 0)
                    {
                        Texture2D tex = new Texture2D(2, 2);
                        tex.LoadImage(val.photoJPGBytes);
                        val.cachedPhoto = tex;
                    }
                    list.Add(val);
                }
            }
            return list;
        }

        public bool AddPhotoToAlbum(
            string id,
            string vnName,
            string scientific,
            string category,
            string status,
            Texture2D photo,
            float score,
            int stars)
        {
            // Clean up id to align keys
            string key = id.Replace("(Clone)", "").Trim();

            // Check if existing high score is higher
            if (albumEntries.TryGetValue(key, out AlbumEntry existing))
            {
                if (existing.bestScore >= score)
                {
                    Debug.Log($"[AlbumManager] Photo of {vnName} rejected. Existing score {existing.bestScore} is higher than {score}.");
                    return false; // Keep old photo
                }
            }

            // Clone photo texture for local retention
            Texture2D savedTex = new Texture2D(photo.width, photo.height, photo.format, false);
            savedTex.SetPixels(photo.GetPixels());
            savedTex.Apply();

            // Encode to JPG for disk storage
            byte[] jpgBytes = savedTex.EncodeToJPG(85);

            AlbumEntry entry = new AlbumEntry
            {
                animalId = key,
                vietnameseName = vnName,
                scientificName = scientific,
                category = category,
                conservationStatus = status,
                bestScore = score,
                starRating = stars,
                captureDate = System.DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                captureLocation = GetCurrentLocationString(),
                photoJPGBytes = jpgBytes,
                cachedPhoto = savedTex
            };

            if (albumEntries.ContainsKey(key))
            {
                albumEntries[key] = entry;
            }
            else
            {
                albumEntries.Add(key, entry);
            }

            Debug.Log($"[AlbumManager] High score photo added for: {vnName} (Score: {score})!");
            SaveAlbum();
            
            // Also notify AchievementManager to check achievements
            if (AchievementManager.Instance != null)
            {
                AchievementManager.Instance.CheckAchievements();
            }

            return true;
        }

        private string GetCurrentLocationString()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (sceneName.Contains("Phase1")) return "Vườn Nhà Ông Ngoại";
            if (sceneName.Contains("Phase2")) return "Kênh Nước Rừng Tràm";
            if (sceneName.Contains("Phase3")) return "Kênh Rừng Tràm Rậm Rạp";
            if (sceneName.Contains("Phase4")) return "Khu Đầm Lầy Bảo Tồn";
            if (sceneName.Contains("Phase5")) return "Tháp Quan Sát Hoàng Hôn";
            return "Rừng Tràm Trà Sư";
        }

        public void SaveAlbum()
        {
            if (SaveSystem.Instance != null)
            {
                SaveSystem.Instance.SaveAlbumData(new List<AlbumEntry>(albumEntries.Values));
            }
        }

        public void LoadAlbum()
        {
            if (SaveSystem.Instance != null)
            {
                List<AlbumEntry> loaded = SaveSystem.Instance.LoadAlbumData();
                albumEntries.Clear();
                foreach (var entry in loaded)
                {
                    albumEntries.Add(entry.animalId, entry);
                }
                Debug.Log($"[AlbumManager] Loaded {albumEntries.Count} album entries from disk.");
            }
        }

        public void ClearAlbum()
        {
            albumEntries.Clear();
            SaveAlbum();
            Debug.Log("[AlbumManager] Album cleared.");
        }
    }
}
