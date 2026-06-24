using UnityEngine;

namespace RungTramTraSu
{
    public class WaterScroller : MonoBehaviour
    {
        [SerializeField] private float scrollSpeed = 0.03f; // Flow speed of the river
        private Material waterMat;
        private static readonly int BaseMapProperty = Shader.PropertyToID("_BaseMap");

        private void Start()
        {
            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                // Get the instance material to avoid modifying the asset template on disk
                waterMat = renderer.material;
            }
        }

        private void Update()
        {
            if (waterMat != null)
            {
                float offset = Time.time * scrollSpeed;
                waterMat.SetTextureOffset(BaseMapProperty, new Vector2(0f, offset));
            }
        }
    }
}
