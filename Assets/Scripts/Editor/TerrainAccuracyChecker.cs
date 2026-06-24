using UnityEngine;
using UnityEditor;

namespace RungTramTraSu
{
    public class TerrainAccuracyChecker
    {
        [MenuItem("Tools/Check Terrain Accuracy")]
        public static void CheckAccuracy()
        {
            // Find root GameObject (active or inactive) in the active scene
            GameObject meshGo = FindGameObject("OrganicTerrain_Bank");

            // Find Terrain component using non-deprecated API
            var terrainGo = Object.FindAnyObjectByType<Terrain>();

            if (meshGo == null || terrainGo == null)
            {
                Debug.LogError($"Error: Targets not found! MeshGo exists: {meshGo != null}, TerrainGo exists: {terrainGo != null}");
                return;
            }

            var col = meshGo.GetComponent<MeshCollider>();
            if (col == null)
            {
                Debug.LogError("Error: OrganicTerrain_Bank has no MeshCollider component!");
                return;
            }

            // Temporarily activate the mesh GameObject so physics raycasting works
            bool wasActive = meshGo.activeSelf;
            meshGo.SetActive(true);

            float totalError = 0f;
            float maxError = 0f;
            int totalSamples = 1000;
            int validCount = 0;

            // Seed random to make verification deterministic and repeatable
            Random.InitState(42);

            var bounds = col.bounds;
            // Use 90% of the bounds size to avoid raycasting at the very edge where precision might vary
            float sampleRadiusX = (bounds.size.x / 2f) * 0.9f;
            float sampleRadiusZ = (bounds.size.z / 2f) * 0.9f;
            float centerX = bounds.center.x;
            float centerZ = bounds.center.z;

            float rayOriginY = bounds.max.y + 5f;
            float rayLength = bounds.size.y + 10f;

            try
            {
                for (int i = 0; i < totalSamples; i++)
                {
                    float x = Random.Range(centerX - sampleRadiusX, centerX + sampleRadiusX);
                    float z = Random.Range(centerZ - sampleRadiusZ, centerZ + sampleRadiusZ);

                    RaycastHit hit;
                    Ray ray = new Ray(new Vector3(x, rayOriginY, z), Vector3.down);
                    float meshY = 0f;

                    if (col.Raycast(ray, out hit, rayLength))
                    {
                        meshY = hit.point.y;
                    }
                    else
                    {
                        continue;
                    }

                    // Sample the terrain height at the same coordinates
                    float terrainY = terrainGo.SampleHeight(new Vector3(x, 0, z)) + terrainGo.transform.position.y;
                    float error = Mathf.Abs(meshY - terrainY);

                    totalError += error;
                    if (error > maxError)
                    {
                        maxError = error;
                    }
                    validCount++;
                }
            }
            finally
            {
                // Restore the original activation state
                meshGo.SetActive(wasActive);
            }

            if (validCount == 0)
            {
                Debug.LogError("Error: Failed to perform any valid height comparisons!");
                return;
            }

            float mae = totalError / validCount;
            Debug.Log($"Terrain Accuracy Results: MAE = {mae:F6}m, Max Error = {maxError:F6}m (Compared {validCount}/{totalSamples} points)");
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
