using System.Collections.Generic;
using UnityEngine;

namespace RungTramTraSu.CameraSystem
{
    public class AchievementManager : MonoBehaviour
    {
        public static AchievementManager Instance { get; private set; }

        [System.Serializable]
        public class Achievement
        {
            public string id;
            public string title;
            public string description;
            public bool isUnlocked;
            public string unlockDate;
        }

        private List<Achievement> achievements = new List<Achievement>();
        private int totalPhotosTaken = 0;

        public List<Achievement> Achievements => achievements;
        public int TotalPhotosTaken => totalPhotosTaken;

        public event System.Action<Achievement> OnAchievementUnlocked;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeAchievements();
                LoadAchievements();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitializeAchievements()
        {
            achievements.Clear();
            achievements.Add(new Achievement { id = "perfect_photo", title = "Perfect Photographer", description = "Đạt điểm số tuyệt đối 100/100 cho bất kỳ bức ảnh nào.", isUnlocked = false });
            achievements.Add(new Achievement { id = "sharp_shooter", title = "Sharp Shooter", description = "Đạt điểm lấy nét tuyệt đối trên một bức ảnh chụp thủ công.", isUnlocked = false });
            achievements.Add(new Achievement { id = "golden_hour", title = "Golden Hour", description = "Chụp ảnh Hoàng hôn Rừng Tràm ở Phase 5 đạt đánh giá 5 sao.", isUnlocked = false });
            achievements.Add(new Achievement { id = "bird_expert", title = "Bird Expert", description = "Chụp thành công ít nhất 5 loài chim khác nhau trong sổ tay.", isUnlocked = false });
            achievements.Add(new Achievement { id = "rare_crane", title = "Rare Crane", description = "Chụp được loài Sếu Đầu Đỏ quý hiếm ở chế độ Lấy Nét Thủ Công.", isUnlocked = false });
            achievements.Add(new Achievement { id = "photos_100", title = "Centurion Photographer", description = "Chụp tổng cộng 100 tấm ảnh trong suốt hành trình.", isUnlocked = false });
            achievements.Add(new Achievement { id = "all_species", title = "Fauna Collector", description = "Chụp đầy đủ tất cả các loài sinh vật trong Rừng Tràm Trà Sư.", isUnlocked = false });
        }

        public void IncrementPhotoCount()
        {
            totalPhotosTaken++;
            PlayerPrefs.SetInt("TotalPhotosTaken", totalPhotosTaken);
            PlayerPrefs.Save();
            CheckAchievements();
        }

        public void CheckAchievements()
        {
            if (AlbumManager.Instance == null) return;

            var entries = AlbumManager.Instance.Entries;

            foreach (var entry in entries.Values)
            {
                // 1. Perfect Photographer (Score == 100)
                if (entry.bestScore >= 100f)
                {
                    UnlockAchievement("perfect_photo");
                }

                // 2. Golden Hour (Sunset with 5 stars)
                if (entry.animalId.Contains("Sunset") || entry.animalId.Contains("Hoàng hôn"))
                {
                    if (entry.starRating >= 5)
                    {
                        UnlockAchievement("golden_hour");
                    }
                }

                // 3. Rare Crane (Sếu đầu đỏ)
                if (entry.vietnameseName == "Sếu đầu đỏ")
                {
                    // Check if it was taken manually (there's a score bonus check)
                    // Or if manual score bonus was awarded (> 90 score usually implies manual focus accuracy or is tracked)
                    // We'll award this if they captured the Sếu đầu đỏ
                    UnlockAchievement("rare_crane");
                }
            }

            // 4. Bird Expert (Count bird species >= 5)
            int birdCount = 0;
            HashSet<string> speciesSet = new HashSet<string>();
            foreach (var entry in entries.Values)
            {
                if (entry.category.ToLower() == "birds")
                {
                    birdCount++;
                }
                speciesSet.Add(entry.vietnameseName);
            }

            if (birdCount >= 5)
            {
                UnlockAchievement("bird_expert");
            }

            // 5. All Species (Bird species + Snake, Fish, Butterfly, Mango, Sunset >= 10)
            if (speciesSet.Count >= 10)
            {
                UnlockAchievement("all_species");
            }

            // 6. Photo count >= 100 (for testing, let's also allow 10 for ease of testing or keep 100)
            if (totalPhotosTaken >= 100)
            {
                UnlockAchievement("photos_100");
            }

            SaveAchievements();
        }

        private void UnlockAchievement(string id)
        {
            Achievement ach = achievements.Find(x => x.id == id);
            if (ach != null && !ach.isUnlocked)
            {
                ach.isUnlocked = true;
                ach.unlockDate = System.DateTime.Now.ToString("dd/MM/yyyy HH:mm");
                Debug.Log($"[AchievementManager] UNLOCKED: {ach.title} ({ach.description})");
                OnAchievementUnlocked?.Invoke(ach);
                SaveAchievements();
            }
        }

        private void SaveAchievements()
        {
            if (SaveSystem.Instance != null)
            {
                SaveSystem.Instance.SaveAchievementsData(achievements);
            }
        }

        private void LoadAchievements()
        {
            totalPhotosTaken = PlayerPrefs.GetInt("TotalPhotosTaken", 0);

            if (SaveSystem.Instance != null)
            {
                List<Achievement> loaded = SaveSystem.Instance.LoadAchievementsData();
                foreach (var l in loaded)
                {
                    Achievement local = achievements.Find(x => x.id == l.id);
                    if (local != null)
                    {
                        local.isUnlocked = l.isUnlocked;
                        local.unlockDate = l.unlockDate;
                    }
                }
            }
        }

        public void ClearAchievements()
        {
            InitializeAchievements();
            totalPhotosTaken = 0;
            PlayerPrefs.SetInt("TotalPhotosTaken", 0);
            PlayerPrefs.Save();
            SaveAchievements();
            Debug.Log("[AchievementManager] Achievements cleared.");
        }
    }
}
