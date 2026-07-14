using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RungTramTraSu.CameraSystem
{
    public class SaveSystem : MonoBehaviour
    {
        public static SaveSystem Instance { get; private set; }

        private string albumPath;
        private string achievementPath;

        [System.Serializable]
        private class Wrapper<T>
        {
            public List<T> list;
        }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                albumPath = Path.Combine(Application.persistentDataPath, "album_data.json");
                achievementPath = Path.Combine(Application.persistentDataPath, "achievements_data.json");
            }
            else
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
    }
}
