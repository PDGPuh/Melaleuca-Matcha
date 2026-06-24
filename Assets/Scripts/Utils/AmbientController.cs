using UnityEngine;

namespace RungTramTraSu
{
    public class AmbientController : MonoBehaviour
    {
        [Header("Audio Sources")]
        public AudioSource windSource;
        public AudioSource foliageSource;

        [Header("Volume Settings")]
        public float normalWindVolume = 0.35f;
        public float normalFoliageVolume = 0.18f;
        [SerializeField] private float indoorVolumeMultiplier = 0.3f;
        [SerializeField] private float transitionSpeed = 2.5f;

        private Transform playerTransform;
        private float targetWindVolume;
        private float targetFoliageVolume;

        private void Start()
        {
            GameObject player = GameObject.Find("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }

            targetWindVolume = normalWindVolume;
            targetFoliageVolume = normalFoliageVolume;

            // Initialize audio source volumes
            if (windSource != null) windSource.volume = normalWindVolume;
            if (foliageSource != null) foliageSource.volume = normalFoliageVolume;
        }

        private void Update()
        {
            if (playerTransform == null) return;

            // Check if player is inside the Grandpa's house
            // House center is approx x = -5.23, y = 0.55, z = 3.85
            Vector3 playerPos = playerTransform.position;
            bool isIndoors = (playerPos.x >= -11.5f && playerPos.x <= -0.5f && 
                              playerPos.z >= -1.0f && playerPos.z <= 9.0f && 
                              playerPos.y >= 0.5f);

            if (isIndoors)
            {
                targetWindVolume = normalWindVolume * indoorVolumeMultiplier;
                targetFoliageVolume = normalFoliageVolume * indoorVolumeMultiplier;
            }
            else
            {
                targetWindVolume = normalWindVolume;
                targetFoliageVolume = normalFoliageVolume;
            }

            // Smoothly transition the volumes
            if (windSource != null)
            {
                windSource.volume = Mathf.Lerp(windSource.volume, targetWindVolume, Time.deltaTime * transitionSpeed);
            }
            if (foliageSource != null)
            {
                foliageSource.volume = Mathf.Lerp(foliageSource.volume, targetFoliageVolume, Time.deltaTime * transitionSpeed);
            }
        }
    }
}
