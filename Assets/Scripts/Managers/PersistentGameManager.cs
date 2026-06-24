using System.Collections.Generic;
using UnityEngine;

namespace RungTramTraSu
{
    public class PersistentGameManager : MonoBehaviour
    {
        private static PersistentGameManager instance;

        public static PersistentGameManager Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new GameObject("PersistentGameManager");
                    instance = go.AddComponent<PersistentGameManager>();
                    DontDestroyOnLoad(go);
                }
                return instance;
            }
        }

        public Dictionary<string, Texture2D> savedPhotosDict = new Dictionary<string, Texture2D>();

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (instance != this)
            {
                Destroy(gameObject);
            }
        }

        public void SavePhoto(Texture2D photo)
        {
            SavePhoto("General_" + System.DateTime.Now.Ticks, photo);
        }

        public void SavePhoto(string category, Texture2D photo)
        {
            // Clone texture to prevent memory release on scene changes
            Texture2D newTex = new Texture2D(photo.width, photo.height, photo.format, false);
            newTex.SetPixels(photo.GetPixels());
            newTex.Apply();

            if (savedPhotosDict.ContainsKey(category))
            {
                if (savedPhotosDict[category] != null) Destroy(savedPhotosDict[category]);
                savedPhotosDict[category] = newTex;
            }
            else
            {
                savedPhotosDict.Add(category, newTex);
            }
            Debug.Log("[PersistentGameManager] Photo saved for category: " + category + "! Total: " + savedPhotosDict.Count);
        }

        public Texture2D GetPhoto(string category)
        {
            if (savedPhotosDict.TryGetValue(category, out Texture2D tex))
            {
                return tex;
            }
            return null;
        }

        public void ClearPhotos()
        {
            foreach (var kvp in savedPhotosDict)
            {
                if (kvp.Value != null) Destroy(kvp.Value);
            }
            savedPhotosDict.Clear();
        }
    }
}
