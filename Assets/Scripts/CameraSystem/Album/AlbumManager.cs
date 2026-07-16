using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RungTramTraSu.CameraSystem
{
    public class AlbumManager : MonoBehaviour, IAlbumManager
    {
        private static AlbumManager instance;
        public static AlbumManager Instance
        {
            get
            {
                if (instance == null)
                {
                    // Touch SaveSystem.Instance to ensure it is created before loading the album
                    if (SaveSystem.Instance == null) { }

                    GameObject go = new GameObject("AlbumManager");
                    instance = go.AddComponent<AlbumManager>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        [System.Serializable]
        public class AlbumEntry
        {
            public string animalId; 
            public string vietnameseName;
            public string scientificName;
            public string category;
            public string conservationStatus;
            public float bestScore;
            public int starRating;
            public string captureDate;
            public string captureLocation;
            
            // Advanced Photography Settings Metadata
            public int iso;
            public float aperture;
            public float shutterSpeed;
            public string whiteBalance;
            public bool isFavorite;
            public bool isBestShot;

            public byte[] photoJPGBytes; // Serialized photo bytes
            [System.NonSerialized] public Texture2D cachedPhoto;
        }

        private Dictionary<string, AlbumEntry> albumEntries = new Dictionary<string, AlbumEntry>();

        public Dictionary<string, AlbumEntry> Entries => albumEntries;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                LoadAlbum();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        // Interface implementation methods
        public void SavePhoto(Texture2D photo, PhotoMetadata meta, ScoreResult score)
        {
            // Redirects to full AddPhotoToAlbum using metadata wrapper
            AddPhotoToAlbum(
                meta.targetId,
                meta.vietnameseName,
                meta.scientificName,
                meta.category,
                meta.conservationStatus,
                photo,
                score.totalScore,
                score.starRating,
                meta.iso,
                meta.aperture,
                meta.shutterSpeed,
                meta.whiteBalance
            );
        }

        public List<PhotoLogEntry> ListPhotos()
        {
            List<PhotoLogEntry> logs = new List<PhotoLogEntry>();
            foreach (var entry in albumEntries.Values)
            {
                logs.Add(new PhotoLogEntry
                {
                    photoId = entry.animalId,
                    vietnameseName = entry.vietnameseName,
                    score = entry.bestScore,
                    stars = entry.starRating
                });
            }
            return logs;
        }

        public AlbumEntry GetEntry(string id)
        {
            if (albumEntries.TryGetValue(id, out AlbumEntry entry))
            {
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
            int stars,
            int isoSetting = 100,
            float apertureSetting = 4.0f,
            float shutterSpeedSetting = 0.004f,
            string wbSetting = "Auto")
        {
            string key = id.Replace("(Clone)", "").Trim();

            if (albumEntries.TryGetValue(key, out AlbumEntry existing))
            {
                if (existing.bestScore >= score)
                {
                    Debug.Log($"[AlbumManager] Photo of {vnName} rejected. Existing score {existing.bestScore} is higher than {score}.");
                    return false; 
                }
            }

            // Clone photo texture for local retention
            Texture2D savedTex = new Texture2D(photo.width, photo.height, photo.format, false);
            savedTex.SetPixels(photo.GetPixels());
            savedTex.Apply();

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
                
                iso = isoSetting,
                aperture = apertureSetting,
                shutterSpeed = shutterSpeedSetting,
                whiteBalance = wbSetting,
                isFavorite = false,
                isBestShot = (score >= 90f), // Mark as best shot if score is >= 90 (5 stars)

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
            
            if (AchievementManager.Instance != null)
            {
                AchievementManager.Instance.CheckAchievements();
            }

            return true;
        }

        public void SetFavoriteStatus(string id, bool favorite)
        {
            if (albumEntries.TryGetValue(id, out AlbumEntry entry))
            {
                entry.isFavorite = favorite;
                SaveAlbum();
                Debug.Log($"[AlbumManager] Set favorite status for {id} to: {favorite}");
            }
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
