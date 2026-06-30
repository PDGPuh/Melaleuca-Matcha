using UnityEngine;

namespace RungTramTraSu
{
    public class WaterScroller : MonoBehaviour
    {
        [SerializeField] private float scrollSpeed = 0.03f; // Flow speed of the river
        private Material waterMat;
        private static readonly int BaseMapProperty = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexProperty = Shader.PropertyToID("_MainTex");
        private int textureProperty;
        private bool hasTextureProperty;

        private void Start()
        {
            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                // Get the instance material to avoid modifying the asset template on disk
                waterMat = renderer.material;
                if (waterMat.HasProperty(BaseMapProperty))
                {
                    textureProperty = BaseMapProperty;
                    hasTextureProperty = true;
                }
                else if (waterMat.HasProperty(MainTexProperty))
                {
                    textureProperty = MainTexProperty;
                    hasTextureProperty = true;
                }
            }
        }

        private void Update()
        {
            if (waterMat != null && hasTextureProperty)
            {
                float offset = Time.time * scrollSpeed;
                waterMat.SetTextureOffset(textureProperty, new Vector2(0f, offset));
            }
        }
    }
}
