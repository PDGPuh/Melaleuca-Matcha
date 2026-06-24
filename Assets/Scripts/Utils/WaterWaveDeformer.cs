using UnityEngine;

namespace RungTramTraSu
{
    public class WaterWaveDeformer : MonoBehaviour
    {
        public static WaterWaveDeformer Instance { get; private set; }

        [Header("Wave Parameters")]
        [SerializeField] private float height1 = 0.15f;
        [SerializeField] private float speed1 = 1.6f;
        [SerializeField] private float freq1 = 0.4f;

        [SerializeField] private float height2 = 0.04f;
        [SerializeField] private float speed2 = 2.2f;
        [SerializeField] private float freq2 = 0.8f;

        [Header("Grid Segments")]
        [SerializeField] private int xSegments = 25;
        [SerializeField] private int zSegments = 80;

        [Header("Optimization")]
        [SerializeField] private bool recalculateTangents = false; // Keep false to save CPU performance (no normal maps on water)

        [Header("Texture Tiling")]
        [Tooltip("Meters of world space per texture repeat. Smaller = denser texture. Fixes river UV stretch.")]
        [SerializeField] private float metersPerTile = 4f;

        private Mesh deformingMesh;
        private Vector3[] baseVertices;
        private Vector3[] displacedVertices;
        private float width;
        private float length;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(this);

            InitializeSubdividedMesh();
        }

        private void InitializeSubdividedMesh()
        {
            MeshFilter filter = GetComponent<MeshFilter>();
            if (filter == null) return;

            // Size from local scale (original plane is 10x10)
            width = transform.localScale.x * 10f;
            length = transform.localScale.z * 10f;

            // Reset local scale to prevent double scaling in the renderer
            transform.localScale = Vector3.one;

            // Generate a custom subdivided grid mesh
            deformingMesh = new Mesh();
            deformingMesh.name = "SubdividedWaterMesh";
            deformingMesh.MarkDynamic(); // Optimize vertex buffers for dynamic frame updates

            int xCount = xSegments + 1;
            int zCount = zSegments + 1;
            int numVertices = xCount * zCount;
            Vector3[] vertices = new Vector3[numVertices];
            Vector2[] uv = new Vector2[numVertices];

            float halfW = width * 0.5f;
            float halfL = length * 0.5f;

            // World-space proportional UVs so texels stay square regardless of river size/scale.
            float uvScaleX = (metersPerTile > 0.0001f) ? width / metersPerTile : 1f;
            float uvScaleZ = (metersPerTile > 0.0001f) ? length / metersPerTile : 1f;

            for (int z = 0; z < zCount; z++)
            {
                float zPos = ((float)z / zSegments) * length - halfL;
                for (int x = 0; x < xCount; x++)
                {
                    float xPos = ((float)x / xSegments) * width - halfW;
                    int index = z * xCount + x;
                    vertices[index] = new Vector3(xPos, 0f, zPos);
                    uv[index] = new Vector2(((float)x / xSegments) * uvScaleX, ((float)z / zSegments) * uvScaleZ);
                }
            }

            int numTriangles = xSegments * zSegments * 6;
            int[] triangles = new int[numTriangles];
            int tIndex = 0;
            for (int z = 0; z < zSegments; z++)
            {
                for (int x = 0; x < xSegments; x++)
                {
                    int botLeft = z * xCount + x;
                    int botRight = botLeft + 1;
                    int topLeft = (z + 1) * xCount + x;
                    int topRight = topLeft + 1;

                    triangles[tIndex++] = botLeft;
                    triangles[tIndex++] = topLeft;
                    triangles[tIndex++] = botRight;

                    triangles[tIndex++] = botRight;
                    triangles[tIndex++] = topLeft;
                    triangles[tIndex++] = topRight;
                }
            }

            deformingMesh.vertices = vertices;
            deformingMesh.uv = uv;
            deformingMesh.triangles = triangles;
            deformingMesh.RecalculateNormals();
            deformingMesh.RecalculateTangents();
            deformingMesh.RecalculateBounds();

            filter.mesh = deformingMesh;
            baseVertices = deformingMesh.vertices;
            displacedVertices = new Vector3[baseVertices.Length];
        }

        private void Update()
        {
            if (baseVertices == null) return;

            float t = Time.time;
            UnityEngine.Vector3 pos = transform.position;
            UnityEngine.Vector3 scale = transform.localScale;

            float scaleX = scale.x;
            float scaleZ = scale.z;
            float posX = pos.x;
            float posZ = pos.z;

            for (int i = 0; i < baseVertices.Length; i++)
            {
                Vector3 vertex = baseVertices[i];
                // Fast world pos calculation (since plane rotation is Identity)
                float worldPosX = vertex.x * scaleX + posX;
                float worldPosZ = vertex.z * scaleZ + posZ;
                vertex.y = CalculateWaveY(worldPosX, worldPosZ, t);
                displacedVertices[i] = vertex;
            }

            deformingMesh.vertices = displacedVertices;
            deformingMesh.RecalculateNormals();
            if (recalculateTangents)
            {
                deformingMesh.RecalculateTangents();
            }
            deformingMesh.RecalculateBounds();
        }

        public float GetWaveHeight(float worldX, float worldZ)
        {
            return transform.position.y + CalculateWaveY(worldX, worldZ, Time.time);
        }

        private float CalculateWaveY(float x, float z, float t)
        {
            float w1 = Mathf.Sin(z * freq1 + t * speed1) * height1;
            float w2 = Mathf.Cos(x * freq2 - t * speed2) * height2;
            return w1 + w2;
        }
    }
}
