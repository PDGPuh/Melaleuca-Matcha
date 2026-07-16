using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RungTramTraSu.CameraSystem
{
    public class SaveSystem : MonoBehaviour
    {
        private static SaveSystem instance;
        public static SaveSystem Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("SaveSystem");
                    instance = go.AddComponent<SaveSystem>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        private string albumPath;
        private string achievementPath;
        private string upgradesPath;

        // Upgrade Levels (Persistent Properties)
        public int SensorUpgradeLevel { get; set; } = 1;
        public int AutofocusUpgradeLevel { get; set; } = 1;
        public int StorageUpgradeLevel { get; set; } = 1;
        public int BatteryUpgradeLevel { get; set; } = 1;

        // Statistics Tracker
        public int TotalPhotosTaken { get; set; } = 0;
        public int ManualPhotosTaken { get; set; } = 0;
        public float HighestPhotoScore { get; set; } = 0f;

        [System.Serializable]
        private class Wrapper<T>
        {
            public List<T> list;
        }

        [System.Serializable]
        private class UpgradesSaveData
        {
            public int sensorLevel;
            public int autofocusLevel;
            public int storageLevel;
            public int batteryLevel;
            public int totalPhotos;
            public int manualPhotos;
            public float highestScore;
        }

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
                albumPath = Path.Combine(Application.persistentDataPath, "album_data.json");
                achievementPath = Path.Combine(Application.persistentDataPath, "achievements_data.json");
                upgradesPath = Path.Combine(Application.persistentDataPath, "camera_upgrades.json");
                LoadUpgradesAndStats();
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        public void SaveAlbumData(List<AlbumManager.AlbumEntry> entries)
        {
            try
            {
                Wrapper<AlbumManager.AlbumEntry> wrapper = new Wrapper<AlbumManager.AlbumEntry> { list = entries };
                string json = JsonUtility.ToJson(wrapper, true);
                File.WriteAllText(albumPath, json);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[SaveSystem] Failed to save album data: " + e.Message);
            }
        }

        public List<AlbumManager.AlbumEntry> LoadAlbumData()
        {
            if (!File.Exists(albumPath))
            {
                return new List<AlbumManager.AlbumEntry>();
            }

            try
            {
                string json = File.ReadAllText(albumPath);
                Wrapper<AlbumManager.AlbumEntry> wrapper = JsonUtility.FromJson<Wrapper<AlbumManager.AlbumEntry>>(json);
                return wrapper != null && wrapper.list != null ? wrapper.list : new List<AlbumManager.AlbumEntry>();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[SaveSystem] Failed to load album data: " + e.Message);
                return new List<AlbumManager.AlbumEntry>();
            }
        }

        public void SaveAchievementsData(List<AchievementManager.Achievement> list)
        {
            try
            {
                Wrapper<AchievementManager.Achievement> wrapper = new Wrapper<AchievementManager.Achievement> { list = list };
                string json = JsonUtility.ToJson(wrapper, true);
                File.WriteAllText(achievementPath, json);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[SaveSystem] Failed to save achievements: " + e.Message);
            }
        }

        public List<AchievementManager.Achievement> LoadAchievementsData()
        {
            if (!File.Exists(achievementPath))
            {
                return new List<AchievementManager.Achievement>();
            }

            try
            {
                string json = File.ReadAllText(achievementPath);
                Wrapper<AchievementManager.Achievement> wrapper = JsonUtility.FromJson<Wrapper<AchievementManager.Achievement>>(json);
                return wrapper != null && wrapper.list != null ? wrapper.list : new List<AchievementManager.Achievement>();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[SaveSystem] Failed to load achievements: " + e.Message);
                return new List<AchievementManager.Achievement>();
            }
        }

        public void SaveUpgradesAndStats()
        {
            try
            {
                UpgradesSaveData data = new UpgradesSaveData
                {
                    sensorLevel = SensorUpgradeLevel,
                    autofocusLevel = AutofocusUpgradeLevel,
                    storageLevel = StorageUpgradeLevel,
                    batteryLevel = BatteryUpgradeLevel,
                    totalPhotos = TotalPhotosTaken,
                    manualPhotos = ManualPhotosTaken,
                    highestScore = HighestPhotoScore
                };
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(upgradesPath, json);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[SaveSystem] Failed to save upgrades and stats: " + e.Message);
            }
        }

        public void LoadUpgradesAndStats()
        {
            if (!File.Exists(upgradesPath)) return;

            try
            {
                string json = File.ReadAllText(upgradesPath);
                UpgradesSaveData data = JsonUtility.FromJson<UpgradesSaveData>(json);
                if (data != null)
                {
                    SensorUpgradeLevel = data.sensorLevel == 0 ? 1 : data.sensorLevel;
                    AutofocusUpgradeLevel = data.autofocusLevel == 0 ? 1 : data.autofocusLevel;
                    StorageUpgradeLevel = data.storageLevel == 0 ? 1 : data.storageLevel;
                    BatteryUpgradeLevel = data.batteryLevel == 0 ? 1 : data.batteryLevel;
                    TotalPhotosTaken = data.totalPhotos;
                    ManualPhotosTaken = data.manualPhotos;
                    HighestPhotoScore = data.highestScore;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("[SaveSystem] Failed to load upgrades and stats: " + e.Message);
            }
        }
    }
}
