using UnityEngine;
using UnityEditor;
using Den.Tools;
using Den.Tools.Matrices;

namespace RungTramTraSu
{
    public class MeshToTerrainBaker
    {
        [MenuItem("Tools/Bake Mesh to TerrainData")]
        public static void Bake()
        {
            // Find root GameObject (active or inactive) in the active scene
            GameObject meshGo = FindGameObject("OrganicTerrain_Bank");
            if (meshGo == null)
            {
                Debug.LogError("OrganicTerrain_Bank not found in scene!");
                return;
            }

            var col = meshGo.GetComponent<MeshCollider>();
            if (col == null)
            {
                Debug.LogError("OrganicTerrain_Bank must have a MeshCollider component!");
                return;
            }

            // Temporarily activate the mesh GameObject so physics raycasting works
            bool wasActive = meshGo.activeSelf;
            meshGo.SetActive(true);

            try
            {
                int resolution = 1025; // 1024 + 1
                float chunkSize = 150f;
                float halfChunk = chunkSize / 2f;

                // Retrieve bounds and dimensions dynamically
                var bounds = col.bounds;
                float minX = bounds.min.x;
                float maxX = bounds.max.x;
                float minZ = bounds.min.z;
                float maxZ = bounds.max.z;
                float minY = bounds.min.y;
                float heightRange = bounds.max.y - bounds.min.y; // Dynamic range

                string assetPath = "Assets/Scenes/Phase1_GrandpaHouse/Phase1_TerrainData.asset";
                TerrainData terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(assetPath);
                bool isNewAsset = false;

                if (terrainData == null)
                {
                    terrainData = new TerrainData();
                    isNewAsset = true;
                }

                terrainData.heightmapResolution = resolution;
                terrainData.size = new Vector3(chunkSize, heightRange, chunkSize);

                float[,] heights = new float[resolution, resolution];
                Vector3 rayOrigin = Vector3.zero;
                rayOrigin.y = bounds.max.y + 5f; // 5m above max mesh height dynamically
                float rayLength = heightRange + 10f; // Ensure ray covers full height range

                for (int z = 0; z < resolution; z++)
                {
                    float normZ = (float)z / (resolution - 1);
                    float worldZ = -halfChunk + (normZ * chunkSize);

                    for (int x = 0; x < resolution; x++)
                    {
                        float normX = (float)x / (resolution - 1);
                        float worldX = -halfChunk + (normX * chunkSize);

                        rayOrigin.x = worldX;
                        rayOrigin.z = worldZ;
                        Ray ray = new Ray(rayOrigin, Vector3.down);
                        RaycastHit hit;

                        float heightVal = 0f;

                        if (col.Raycast(ray, out hit, rayLength))
                        {
                            // Map Y relative to minimum bound dynamically
                            heightVal = (hit.point.y - minY) / heightRange;
                        }
                        else
                        {
                            // Clamp to border/closest point using dynamic bounds to avoid seams
                            float clampedX = Mathf.Clamp(worldX, minX, maxX);
                            float clampedZ = Mathf.Clamp(worldZ, minZ, maxZ);
                            Ray clampRay = new Ray(new Vector3(clampedX, rayOrigin.y, clampedZ), Vector3.down);
                            if (col.Raycast(clampRay, out hit, rayLength))
                            {
                                heightVal = (hit.point.y - minY) / heightRange;
                            }
                        }

                        // Write to heightmap array
                        heights[z, x] = Mathf.Clamp01(heightVal);
                    }
                }

                terrainData.SetHeights(0, 0, heights);

                if (isNewAsset)
                {
                    AssetDatabase.CreateAsset(terrainData, assetPath);
                }
                else
                {
                    EditorUtility.SetDirty(terrainData);
                }

                // Bake MatrixAsset for MapMagic 2 compatibility (resolution - 1 = 1024)
                string matrixAssetPath = "Assets/Scenes/Phase1_GrandpaHouse/Phase1_MatrixAsset.asset";
                MatrixAsset matrixAsset = AssetDatabase.LoadAssetAtPath<MatrixAsset>(matrixAssetPath);
                bool isNewMatrix = false;

                if (matrixAsset == null)
                {
                    matrixAsset = ScriptableObject.CreateInstance<MatrixAsset>();
                    isNewMatrix = true;
                }

                int matrixRes = resolution - 1; // 1024
                float[,] matrixHeights = new float[matrixRes, matrixRes];
                for (int z = 0; z < matrixRes; z++)
                {
                    for (int x = 0; x < matrixRes; x++)
                    {
                        matrixHeights[z, x] = heights[z, x];
                    }
                }

                Matrix matrix = new Matrix(new CoordRect(0, 0, matrixRes, matrixRes));
                matrix.ImportHeights(matrixHeights);
                matrixAsset.matrix = matrix;
                matrixAsset.RefreshPreview(256);

                if (isNewMatrix)
                {
                    AssetDatabase.CreateAsset(matrixAsset, matrixAssetPath);
                }
                else
                {
                    EditorUtility.SetDirty(matrixAsset);
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log($"Successfully baked Mesh to TerrainData at {assetPath} and MatrixAsset at {matrixAssetPath}");
            }
            finally
            {
                // Restore the original activation state
                meshGo.SetActive(wasActive);
            }
        }

        private static GameObject FindGameObject(string name)
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var rootGo in activeScene.GetRootGameObjects())
            {
                if (rootGo.name == name)
                {
                    return rootGo;
                }
            }
            return null;
        }
    }
}
