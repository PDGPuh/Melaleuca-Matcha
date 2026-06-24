using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Den.Tools;
using Den.Tools.Matrices;
using RungTramTraSu;

namespace RungTramTraSu.Editor
{
    public class Phase2Setup
    {
        private static readonly string[] LegacyContainers = new string[]
        {
            "SpruceTreesContainer", "BeautifiedFoliage", "BeautifiedRocks",
            "TeaTree_Forest", "FloatingLilyPads", "ShorelineReeds", "CajeputTreesContainer",
            "FruitTreesContainer", "WaterfallWaterContainer", "WaterfallRocksContainer", "NatureDecorations"
        };

        private static readonly string[] TreePrefabPaths = new string[]
        {
            "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Trees/PT_Fruit_Tree_01_green.prefab",
            "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Trees/PT_Fruit_Tree_01_apples.prefab",
            "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Trees/PT_Fruit_Tree_01_pears.prefab",
            "Assets/Polytope Studio/Lowpoly_Environments/Prefabs/Trees/PT_Fruit_Tree_01_plums.prefab"
        };

        private static readonly string[] RockCliffPaths = new string[]
        {
            "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Rock Cliffs/Rock Cliff 1.prefab",
            "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Rock Cliffs/Rock Cliff 2.prefab",
            "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Rock Cliffs/Rock Cliff 3.prefab",
            "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Rock Cliffs/Rock Cliff 4.prefab",
            "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Rock Cliffs/Rock Cliff 5.prefab"
        };

        private static readonly string[] StandardRockPaths = new string[]
        {
            "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Standard Rocks/Standard Rock 1.prefab",
            "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Standard Rocks/Standard Rock 2.prefab",
            "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Standard Rocks/Standard Rock 3.prefab",
            "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Standard Rocks/Standard Rock 4.prefab",
            "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Standard Rocks/Standard Rock 5.prefab"
        };

        private static readonly string[] TinyRockPaths = new string[]
        {
            "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Tiny Rocks/Tiny Rock 1.prefab",
            "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Tiny Rocks/Tiny Rock 2.prefab",
            "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Tiny Rocks/Tiny Rock 3.prefab",
            "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Tiny Rocks/Tiny Rock 4.prefab",
            "Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Rocks/Tiny Rocks/Tiny Rock 5.prefab"
        };

        private delegate float HeightQuery(float x, float z);

        [MenuItem("Tools/Beautify Phase 2")]
        public static void Beautify()
        {
            // Active scene validation
            if (SceneManager.GetActiveScene().name != "Phase2_Canal")
            {
                EditorUtility.DisplayDialog("Error", "Please open the Phase2_Canal scene before running this tool!", "OK");
                return;
            }

            Debug.Log("Starting Phase 2 Beautification...");
            Random.InitState(42); // Deterministic placement seed

            // 0. Backup Grass Detail Maps from Main Terrain
            var terrain = GameObject.Find("Main Terrain")?.GetComponent<Terrain>();
            System.Collections.Generic.List<int[,]> detailMaps = new System.Collections.Generic.List<int[,]>();
            if (terrain != null && terrain.terrainData != null)
            {
                int layers = terrain.terrainData.detailPrototypes.Length;
                int w = terrain.terrainData.detailWidth;
                int h = terrain.terrainData.detailHeight;
                for (int i = 0; i < layers; i++)
                {
                    detailMaps.Add(terrain.terrainData.GetDetailLayer(0, 0, w, h, i));
                }
                Debug.Log($"Backed up {layers} detail layers. Total grass sum will be restored.");
            }
            
            CleanupExisting();

            // 1. Modify Heights
            string matrixPath = "Assets/Scenes/Phase2_Canal/Phase2_Canal_MatrixAsset.asset";
            var matrixAsset = AssetDatabase.LoadAssetAtPath<MatrixAsset>(matrixPath);
            if (matrixAsset == null)
            {
                Debug.LogError("MatrixAsset not found at: " + matrixPath);
                return;
            }
            Matrix matrix = matrixAsset.matrix;
            if (matrix == null || matrix.rect.size.x != 1024 || matrix.rect.size.z != 1024)
            {
                Debug.LogError("MatrixAsset matrix is invalid or not 1024x1024!");
                return;
            }

            Debug.Log("Updating heightmap matrix...");
            for (int z = 0; z < 1024; z++)
            {
                float normZ = (float)z / 1023f;
                float worldZ = -75f + (normZ * 150f);
                for (int x = 0; x < 1024; x++)
                {
                    float normX = (float)x / 1023f;
                    float worldX = -75f + (normX * 150f);

                    float absoluteY = GetHeightAt(worldX, worldZ);
                    // Use scale = 20.0f to avoid mountain height clipping
                    float normalizedHeight = (absoluteY + 2.2f) / 20.0f;
                    matrix[x, z] = Mathf.Clamp01(normalizedHeight);
                }
            }

            // Update MapMagic Object parameters
            var mapMagic = Object.FindAnyObjectByType<MapMagic.Core.MapMagicObject>();
            if (mapMagic != null)
            {
                Undo.RecordObject(mapMagic, "Update MapMagic Settings");
                mapMagic.globals.height = 20.0f;
                mapMagic.terrainSettings.pixelError = 1;
                EditorUtility.SetDirty(mapMagic);
            }

            matrixAsset.RefreshPreview(256);
            EditorUtility.SetDirty(matrixAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Heightmap matrix updated and saved.");

            // 2. Setup Environment Settings (Skybox, Lighting, Fog)
            SetupEnvironment();

            // 3. Spawn Trees (Polytope Studio Fruit Trees)
            SpawnTrees(matrixAsset);

            // 4. Spawn Water Planes (Lake, Stream, Waterfall)
            SpawnWaterfallWater();

            // 5. Spawn Rocks (Waterfall Crescent & Bed Rocks)
            SpawnRocks(matrixAsset);

            // 6. Spawn Winding Canal Details (Shoreline Rocks, Foliage, Reeds, Lily Pads & Lotus Flowers)
            SpawnWindingCanalDetails();

            // 7. Force MapMagic Refresh and Restore Grass Details asynchronously
            if (mapMagic != null)
            {
                mapMagic.Refresh(clearAll: true);
                Debug.Log("MapMagic Refresh triggered.");

                if (detailMaps.Count > 0)
                {
                    Debug.Log("Waiting for MapMagic generation to restore grass details...");
                    int framesToWait = 60; // wait 60 editor updates (~1 second) to let MapMagic start, then wait for IsGenerating to end
                    EditorApplication.CallbackFunction checkProgress = null;
                    checkProgress = () =>
                    {
                        if (framesToWait > 0)
                        {
                            framesToWait--;
                            return;
                        }

                        if (mapMagic.IsGenerating())
                        {
                            return; // still generating
                        }

                        // Done generating! Restore grass detail maps
                        EditorApplication.update -= checkProgress;
                        if (terrain != null && terrain.terrainData != null)
                        {
                            int w = terrain.terrainData.detailWidth;
                            int h = terrain.terrainData.detailHeight;
                            for (int i = 0; i < detailMaps.Count; i++)
                            {
                                terrain.terrainData.SetDetailLayer(0, 0, i, detailMaps[i]);
                            }
                            Debug.Log("✅ Grass detail maps restored successfully!");
                            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                        }
                    };
                    EditorApplication.update += checkProgress;
                }
            }
            
            // Scene dirtiness registration
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("Beautification completed successfully.");
        }

        [MenuItem("Tools/Reset Phase 2 Terrain")]
        public static void ResetToFlat()
        {
            if (SceneManager.GetActiveScene().name != "Phase2_Canal")
            {
                EditorUtility.DisplayDialog("Error", "Please open the Phase2_Canal scene before running this tool!", "OK");
                return;
            }

            Debug.Log("Resetting Phase 2 Terrain to Flat Banks...");
            
            // Backup grass detail maps
            var terrain = GameObject.Find("Main Terrain")?.GetComponent<Terrain>();
            System.Collections.Generic.List<int[,]> detailMaps = new System.Collections.Generic.List<int[,]>();
            if (terrain != null && terrain.terrainData != null)
            {
                int layers = terrain.terrainData.detailPrototypes.Length;
                int w = terrain.terrainData.detailWidth;
                int h = terrain.terrainData.detailHeight;
                for (int i = 0; i < layers; i++)
                {
                    detailMaps.Add(terrain.terrainData.GetDetailLayer(0, 0, w, h, i));
                }
            }

            // Cleanup waterfall objects
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var r in roots)
            {
                if (r == null) continue;
                string name = r.name;
                if (name == "WaterfallWaterContainer" || name == "WaterfallRocksContainer")
                {
                    Undo.DestroyObjectImmediate(r);
                }
            }

            string matrixPath = "Assets/Scenes/Phase2_Canal/Phase2_Canal_MatrixAsset.asset";
            var matrixAsset = AssetDatabase.LoadAssetAtPath<MatrixAsset>(matrixPath);
            if (matrixAsset == null) return;
            Matrix matrix = matrixAsset.matrix;

            for (int z = 0; z < 1024; z++)
            {
                float normZ = (float)z / 1023f;
                float worldZ = -75f + (normZ * 150f);
                float canalCenterX = GetCanalCenterX(worldZ);

                for (int x = 0; x < 1024; x++)
                {
                    float normX = (float)x / 1023f;
                    float worldX = -75f + (normX * 150f);
                    float distFromCenter = worldX - canalCenterX;

                    float absoluteY;
                    if (Mathf.Abs(distFromCenter) <= 10.0f)
                    {
                        absoluteY = -2.2f; // Canal bed
                    }
                    else if (Mathf.Abs(distFromCenter) < 14.0f)
                    {
                        // Slope from -2.2m to 0.0m
                        float t = (Mathf.Abs(distFromCenter) - 10.0f) / 4.0f;
                        absoluteY = Mathf.Lerp(-2.2f, 0.0f, t);
                    }
                    else
                    {
                        absoluteY = 0.0f; // Flat bank
                    }

                    float normalizedHeight = (absoluteY + 2.2f) / 20.0f;
                    matrix[x, z] = Mathf.Clamp01(normalizedHeight);
                }
            }

            var mapMagic = Object.FindAnyObjectByType<MapMagic.Core.MapMagicObject>();
            if (mapMagic != null)
            {
                Undo.RecordObject(mapMagic, "Reset MapMagic Settings");
                mapMagic.globals.height = 20.0f;
                mapMagic.terrainSettings.pixelError = 1;
                EditorUtility.SetDirty(mapMagic);
            }

            matrixAsset.RefreshPreview(256);
            EditorUtility.SetDirty(matrixAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (mapMagic != null)
            {
                mapMagic.Refresh(clearAll: true);
                
                if (detailMaps.Count > 0)
                {
                    int framesToWait = 60;
                    EditorApplication.CallbackFunction checkProgress = null;
                    checkProgress = () =>
                    {
                        if (framesToWait > 0)
                        {
                            framesToWait--;
                            return;
                        }
                        if (mapMagic.IsGenerating()) return;
                        EditorApplication.update -= checkProgress;
                        if (terrain != null && terrain.terrainData != null)
                        {
                            int w = terrain.terrainData.detailWidth;
                            int h = terrain.terrainData.detailHeight;
                            for (int i = 0; i < detailMaps.Count; i++)
                            {
                                terrain.terrainData.SetDetailLayer(0, 0, i, detailMaps[i]);
                            }
                            Debug.Log("✅ Grass detail maps restored successfully after reset!");
                            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                        }
                    };
                    EditorApplication.update += checkProgress;
                }
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("✅ Phase 2 Terrain reset to flat bank successfully!");
        }

        [MenuItem("Tools/Clear Phase 2 Textures")]
        public static void ClearTextures()
        {
            if (SceneManager.GetActiveScene().name != "Phase2_Canal")
            {
                EditorUtility.DisplayDialog("Error", "Please open the Phase2_Canal scene before running this tool!", "OK");
                return;
            }

            Debug.Log("Clearing Terrain Textures from MapMagic graph and TerrainData...");

            // 1. Get MapMagic Graph
            var mapMagic = Object.FindAnyObjectByType<MapMagic.Core.MapMagicObject>();
            if (mapMagic != null && mapMagic.graph != null)
            {
                Undo.RecordObject(mapMagic.graph, "Modify MapMagic Graph");
                var graph = mapMagic.graph;

                // Find TexturesOutput200 node
                MapMagic.Nodes.Generator texturesOutputNode = null;
                foreach (var gen in graph.generators)
                {
                    if (gen is MapMagic.Nodes.MatrixGenerators.TexturesOutput200)
                    {
                        texturesOutputNode = gen;
                        break;
                    }
                }

                if (texturesOutputNode != null)
                {
                    graph.Remove(texturesOutputNode);
                    EditorUtility.SetDirty(graph);
                    Debug.Log("Removed TexturesOutput node from MapMagic graph.");
                }
            }

            // 2. Clear terrain layers from Unity Terrain
            var terrain = GameObject.Find("Main Terrain")?.GetComponent<Terrain>();
            if (terrain != null && terrain.terrainData != null)
            {
                Undo.RecordObject(terrain.terrainData, "Clear Terrain Layers");
                terrain.terrainData.terrainLayers = new TerrainLayer[0];
                EditorUtility.SetDirty(terrain.terrainData);
                Debug.Log("Cleared TerrainLayers from Unity Terrain.");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (mapMagic != null)
            {
                mapMagic.Refresh(clearAll: true);
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("✅ Phase 2 Terrain textures cleared successfully!");
        }

        private static void CleanupExisting()
        {
            var roots = SceneManager.GetActiveScene().GetRootGameObjects();
            foreach (var r in roots)
            {
                if (r == null) continue;
                string name = r.name;
                if (System.Array.IndexOf(LegacyContainers, name) >= 0)
                {
                    Undo.DestroyObjectImmediate(r);
                }
            }
        }

        private static float GetCanalCenterX(float worldZ)
        {
            if (worldZ < -55f) return 25f;
            if (worldZ > 55f)
            {
                // Curve to the left (X decreases) to make the river look like it winds out of sight
                float t = (worldZ - 55f);
                return 25f - t * 0.7f;
            }
            return 25f + Mathf.Sin((worldZ + 55f) * (Mathf.PI / 110f) * 3f) * 6f;
        }

        private static float GetHeightAt(float worldX, float worldZ)
        {
            float canalCenterX = GetCanalCenterX(worldZ);
            float distFromCenter = worldX - canalCenterX;

            // 1. Canal Zone (Riverbed)
            if (Mathf.Abs(distFromCenter) <= 10.0f)
            {
                return -2.2f;
            }

            // 2. Right Bank (East Side)
            if (distFromCenter > 10.0f)
            {
                if (distFromCenter < 14.0f)
                {
                    float t = (distFromCenter - 10.0f) / 4.0f;
                    return Mathf.Lerp(-2.2f, 0.0f, t);
                }
                else if (distFromCenter < 25.0f)
                {
                    return 0.0f;
                }
                else
                {
                    float t = (distFromCenter - 25.0f) / 25.0f;
                    float baseHeight = Mathf.Lerp(0.0f, 8.0f, Mathf.Clamp01(t));
                    float noise = Mathf.PerlinNoise(worldX * 0.08f + 200f, worldZ * 0.08f + 200f) * 1.5f;
                    return baseHeight + noise;
                }
            }

            // 3. Left Bank (West Side)
            float normalL = 0f;
            float leftBankEdgeX = canalCenterX - 14.0f;
            if (distFromCenter > -14.0f)
            {
                float t = (distFromCenter - (-10.0f)) / -4.0f;
                normalL = Mathf.Lerp(-2.2f, 2.5f, t);
            }
            else
            {
                float t = (leftBankEdgeX - worldX) / (leftBankEdgeX - (-75f));
                float baseHeight = Mathf.Lerp(2.5f, 13.0f, Mathf.Clamp01(t));
                float noiseFade = Mathf.Clamp01((leftBankEdgeX - worldX) / 10.0f);
                float noise = Mathf.PerlinNoise(worldX * 0.05f + 100f, worldZ * 0.05f + 100f) * 4.0f * noiseFade;
                normalL = baseHeight + noise;
            }

            // Carving Waterfall, Stream, and Lake centered at Z = -20
            float distToLake = Mathf.Sqrt((worldX - (-45f)) * (worldX - (-45f)) + (worldZ - (-20f)) * (worldZ - (-20f)));
            if (distToLake < 12.0f)
            {
                float t = distToLake / 12.0f;
                float lakeBedY = Mathf.Lerp(4.5f, 5.5f, t);
                normalL = Mathf.Lerp(lakeBedY, normalL, t * t);
            }

            float distToStreamCenterZ = Mathf.Abs(worldZ - (-20f));
            if (distToStreamCenterZ < 6.0f)
            {
                float blendT = distToStreamCenterZ / 6.0f;

                if (worldX >= -45f && worldX <= leftBankEdgeX)
                {
                    float progressX = (worldX - (-45f)) / (leftBankEdgeX - (-45f));
                    float streamBedY = Mathf.Lerp(5.0f, 4.5f, progressX);
                    normalL = Mathf.Lerp(streamBedY, normalL, blendT * blendT);
                }
                else if (distFromCenter > -12.0f && distFromCenter < -10.0f)
                {
                    // Waterfall slope drop from Y = 4.5m to Y = -2.2m (2.0m run matching water plane)
                    float t = (distFromCenter - (-10.0f)) / -2.0f;
                    float waterfallSlopeY = Mathf.Lerp(-2.2f, 4.5f, t);
                    normalL = Mathf.Lerp(waterfallSlopeY, normalL, blendT * blendT);
                }
            }

            return normalL;
        }

        private static bool IsInExclusionZone(Vector3 pos)
        {
            float canalCenterX = GetCanalCenterX(pos.z);
            // Start Pier: (25, -56) — radius 12m
            if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(25f, -56f)) < 12f) return true;
            
            // End Pier: (25, 55) — radius 8m
            if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(25f, 55f)) < 8f) return true;

            // Waterfall, Stream, and Lake: Z between -27 and -13, X < canalCenterX - 12
            if (pos.z >= -27f && pos.z <= -13f && pos.x < canalCenterX - 12.0f) return true;
            
            return false;
        }

        private static bool IsInFoliageExclusionZone(Vector3 pos)
        {
            float canalCenterX = GetCanalCenterX(pos.z);
            // Start Pier: (25, -56) — radius 8m
            if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(25f, -56f)) < 8f) return true;
            
            // End Pier: (25, 55) — radius 5m
            if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(25f, 55f)) < 5f) return true;

            // Waterfall, Stream, and Lake: Z between -27 and -13, X < canalCenterX - 12
            if (pos.z >= -27f && pos.z <= -13f && pos.x < canalCenterX - 12.0f) return true;
            
            return false;
        }

        private static void SetupEnvironment()
        {
            // 1. Skybox
            Material skyboxMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/EmaceArt/Slavic World Free/Skybox/Epic_BigCloudsSoft_V2/EA03_LowPolyBigClouds.mat");
            if (skyboxMat != null)
            {
                RenderSettings.skybox = skyboxMat;
                Debug.Log("Skybox material applied successfully!");
            }

            // 2. Directional Light Setup
            GameObject dirLight = GameObject.Find("Directional Light");
            if (dirLight != null)
            {
                Undo.RecordObject(dirLight.transform, "Adjust Directional Light Rotation");
                dirLight.transform.rotation = Quaternion.Euler(32f, -45f, 0f);
                
                Light lightComp = dirLight.GetComponent<Light>();
                if (lightComp != null)
                {
                    Undo.RecordObject(lightComp, "Adjust Light Parameters");
                    lightComp.color = new Color(1.0f, 0.96f, 0.86f);
                    lightComp.intensity = 1.35f;
                    lightComp.shadows = LightShadows.Soft;
                    lightComp.shadowStrength = 0.8f;
                    EditorUtility.SetDirty(lightComp);
                }
                EditorUtility.SetDirty(dirLight.transform);
                Debug.Log("Directional light rotation and intensity adjusted.");
            }

            // 3. Ambient fill light
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.25f;
            DynamicGI.UpdateEnvironment();
            Debug.Log("Ambient fill light configured.");

            // 4. Fog
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.6f, 0.78f, 0.72f);
            RenderSettings.fogDensity = 0.015f;
            Debug.Log("Atmospheric fog setup completed.");
        }

        private static void SpawnTrees(MatrixAsset matrixAsset)
        {
            GameObject container = new GameObject("FruitTreesContainer");
            Undo.RegisterCreatedObjectUndo(container, "Create FruitTreesContainer");

            GameObject[] prefabs = new GameObject[TreePrefabPaths.Length];
            for (int i = 0; i < TreePrefabPaths.Length; i++)
            {
                prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(TreePrefabPaths[i]);
                if (prefabs[i] == null)
                {
                    Debug.LogError("Tree prefab not found: " + TreePrefabPaths[i]);
                    return;
                }
            }

            var spawnedPositions = new System.Collections.Generic.List<Vector3>();

            // Left Bank Forest Backdrop (80 trees)
            SpawnTreeGroup(prefabs, -75f, -15f, -70f, 70f, 80, spawnedPositions, container.transform, matrixAsset, "Left Bank Forest Backdrop");
            // Left Bank Proximity (40 trees)
            SpawnTreeGroup(prefabs, -15f, 10f, -70f, 70f, 40, spawnedPositions, container.transform, matrixAsset, "Left Bank Proximity");
            // Right Bank Forest Backdrop (50 trees)
            SpawnTreeGroup(prefabs, 42f, 75f, -70f, 70f, 50, spawnedPositions, container.transform, matrixAsset, "Right Bank Forest Backdrop");
        }

        private static void SpawnTreeGroup(GameObject[] prefabs, float minX, float maxX, float minZ, float maxZ, int count, System.Collections.Generic.List<Vector3> spawned, Transform parent, MatrixAsset matrixAsset, string zoneName)
        {
            int placed = 0;
            for (int i = 0; i < count; i++)
            {
                bool success = false;
                for (int attempts = 0; attempts < 50; attempts++)
                {
                    float rx = Random.Range(minX, maxX);
                    float rz = Random.Range(minZ, maxZ);

                    float canalX = GetCanalCenterX(rz);
                    if (Mathf.Abs(rx - canalX) < 14.5f) continue;
                    if (Vector2.Distance(new Vector2(rx, rz), new Vector2(25f, -56f)) < 12f) continue;
                    if (Vector2.Distance(new Vector2(rx, rz), new Vector2(25f, 55f)) < 8f) continue;
                    if (rz >= -27f && rz <= -13f && rx < canalX - 12.0f) continue;

                    bool tooClose = false;
                    foreach (var pos in spawned)
                    {
                        if (Vector2.Distance(new Vector2(rx, rz), new Vector2(pos.x, pos.z)) < 3.0f)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    if (tooClose) continue;

                    float normX = (rx - (-75f)) / 150f;
                    float normZ = (rz - (-75f)) / 150f;
                    int col = Mathf.Clamp((int)(normX * 1024f), 0, 1023);
                    int row = Mathf.Clamp((int)(normZ * 1024f), 0, 1023);
                    float ry = (matrixAsset.matrix[col, row] * 20.0f) - 2.2f;

                    GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
                    GameObject treeGo = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                    treeGo.transform.position = new Vector3(rx, ry, rz);
                    treeGo.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    treeGo.transform.localScale = Vector3.one * Random.Range(0.7f, 1.4f);
                    treeGo.transform.SetParent(parent);
                    treeGo.isStatic = true;

                    Undo.RegisterCreatedObjectUndo(treeGo, "Spawn Fruit Tree");
                    spawned.Add(treeGo.transform.position);
                    success = true;
                    placed++;
                    break;
                }
            }
        }

        private static void SpawnWaterfallWater()
        {
            GameObject container = new GameObject("WaterfallWaterContainer");
            Undo.RegisterCreatedObjectUndo(container, "Create WaterfallWaterContainer");

            string matPath = "Assets/Bitgem/StylisedWater/URP/Materials/example-water-02.mat";
            Material waterMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (waterMat == null)
            {
                Debug.LogError("Water material not found: " + matPath);
                return;
            }

            // 1. Widen & Position Canal Water Plane (Scale down to 4.2 & 15.39 to prevent 10x size overflow)
            var riverWater = GameObject.Find("RiverWater_Canal");
            if (riverWater != null)
            {
                Undo.RecordObject(riverWater.transform, "Adjust River Water Width");
                riverWater.transform.position = new Vector3(25.0f, -1.0f, 0.0f);
                riverWater.transform.localScale = new Vector3(4.2f, 1.0f, 15.39f);
                
                if (riverWater.GetComponent<WaterWaveDeformer>() == null)
                {
                    Undo.AddComponent<WaterWaveDeformer>(riverWater);
                }
                EditorUtility.SetDirty(riverWater);
            }

            // 2. Lake Water (Y = 6.0m, Z = -20)
            GameObject lake = GameObject.CreatePrimitive(PrimitiveType.Plane);
            lake.name = "Lake_Water";
            lake.transform.position = new Vector3(-45.0f, 6.0f, -20.0f);
            lake.transform.localScale = new Vector3(2.4f, 1.0f, 2.4f);
            lake.transform.rotation = Quaternion.identity;
            CleanWaterColliderAndAssignMaterial(lake, waterMat, container.transform);
            
            // Add custom material with proper tiling for lake
            Material lakeMat = new Material(waterMat);
            lakeMat.name = "LakeWaterMaterial";
            if (lakeMat.HasProperty("_BaseMap")) lakeMat.SetTextureScale("_BaseMap", new Vector2(2.4f, 2.4f));
            lake.GetComponent<Renderer>().sharedMaterial = lakeMat;
            Undo.RegisterCreatedObjectUndo(lake, "Spawn Lake Water");

            float canalCenterX_Z = GetCanalCenterX(-20f);

            // 3. Stream Water (12m wide, slopes 6.0m to 5.5m)
            float streamEndX = canalCenterX_Z - 12.0f;
            float centerX = (-45.0f + streamEndX) / 2f;
            float lengthX = streamEndX - (-45.0f);
            GameObject stream = GameObject.CreatePrimitive(PrimitiveType.Plane);
            stream.name = "Stream_Water";
            stream.transform.position = new Vector3(centerX, 5.75f, -20.0f);
            stream.transform.localScale = new Vector3(lengthX / 10f, 1.0f, 1.2f);
            
            Vector3 streamFlowDir = new Vector3(lengthX, -0.5f, 0.0f).normalized;
            Vector3 widthDir = new Vector3(0f, 0f, 1f);
            Vector3 streamNormal = Vector3.Cross(widthDir, streamFlowDir).normalized;
            stream.transform.rotation = Quaternion.LookRotation(streamFlowDir, streamNormal);
            CleanWaterColliderAndAssignMaterial(stream, waterMat, container.transform);

            // Apply custom non-stretching material instance
            Material streamMat = new Material(waterMat);
            streamMat.name = "StreamWaterMaterial";
            if (streamMat.HasProperty("_BaseMap")) streamMat.SetTextureScale("_BaseMap", new Vector2(lengthX / 4f, 1.2f));
            stream.GetComponent<Renderer>().sharedMaterial = streamMat;
            Undo.RegisterCreatedObjectUndo(stream, "Spawn Stream Water");

            // 4. Waterfall Water (12m wide, drops 5.5m to -1.0m over 2.0m run)
            float startX = canalCenterX_Z - 12.0f;
            float endX = canalCenterX_Z - 10.0f;
            float fallCenterX = (startX + endX) / 2f;
            GameObject waterfall = GameObject.CreatePrimitive(PrimitiveType.Plane);
            waterfall.name = "Waterfall_Water";
            waterfall.transform.position = new Vector3(fallCenterX, 2.25f, -20.0f);
            
            float diagonalLength = Mathf.Sqrt(2.0f * 2.0f + 6.5f * 6.5f);
            waterfall.transform.localScale = new Vector3(diagonalLength / 10f, 1.0f, 1.2f);
            
            Vector3 fallFlowDir = new Vector3(2.0f, -6.5f, 0.0f).normalized;
            Vector3 fallNormal = Vector3.Cross(widthDir, fallFlowDir).normalized;
            waterfall.transform.rotation = Quaternion.LookRotation(fallFlowDir, fallNormal);
            CleanWaterColliderAndAssignMaterial(waterfall, waterMat, container.transform);

            // Apply custom non-stretching material instance
            Material fallMat = new Material(waterMat);
            fallMat.name = "WaterfallWaterMaterial";
            if (fallMat.HasProperty("_BaseMap")) fallMat.SetTextureScale("_BaseMap", new Vector2(diagonalLength / 4f, 1.2f));
            waterfall.GetComponent<Renderer>().sharedMaterial = fallMat;
            Undo.RegisterCreatedObjectUndo(waterfall, "Spawn Waterfall Water");
        }

        private static void CleanWaterColliderAndAssignMaterial(GameObject go, Material mat, Transform parent)
        {
            var collider = go.GetComponent<Collider>();
            if (collider != null) Object.DestroyImmediate(collider);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.transform.SetParent(parent);
            go.isStatic = true;
        }

        private static void SpawnRocks(MatrixAsset matrixAsset)
        {
            GameObject container = new GameObject("WaterfallRocksContainer");
            Undo.RegisterCreatedObjectUndo(container, "Create WaterfallRocksContainer");

            GameObject[] cliffs = LoadPrefabs(RockCliffPaths);
            GameObject[] stdRocks = LoadPrefabs(StandardRockPaths);
            if (cliffs.Length == 0 || stdRocks.Length == 0) return;

            float canalCenterX_Z = GetCanalCenterX(-20f);
            float leftBankEdgeX = canalCenterX_Z - 12.0f; // End of stream carving

            // 1. Horseshoe Rock Amphitheater wrapping around the drop (Z = -26 to -14) at leftBankEdgeX
            SpawnRockAt(cliffs[Random.Range(0, cliffs.Length)], new Vector3(leftBankEdgeX - 1.5f, 0.2f, -25.5f), Quaternion.Euler(0f, 45f, 0f), new Vector3(4f, 5f, 4f), container.transform, "Spawn Cliff Rock");
            SpawnRockAt(cliffs[Random.Range(0, cliffs.Length)], new Vector3(leftBankEdgeX - 0.5f, 0.2f, -24.0f), Quaternion.Euler(0f, 75f, 0f), new Vector3(4f, 5f, 4f), container.transform, "Spawn Cliff Rock");
            SpawnRockAt(cliffs[Random.Range(0, cliffs.Length)], new Vector3(leftBankEdgeX - 1.5f, 0.2f, -14.5f), Quaternion.Euler(0f, 315f, 0f), new Vector3(4f, 5f, 4f), container.transform, "Spawn Cliff Rock");
            SpawnRockAt(cliffs[Random.Range(0, cliffs.Length)], new Vector3(leftBankEdgeX - 0.5f, 0.2f, -16.0f), Quaternion.Euler(0f, 285f, 0f), new Vector3(4f, 5f, 4f), container.transform, "Spawn Cliff Rock");
            SpawnRockAt(cliffs[Random.Range(0, cliffs.Length)], new Vector3(leftBankEdgeX - 3.0f, 1.2f, -22.5f), Quaternion.Euler(0f, 90f, 0f), new Vector3(3.5f, 5f, 3.5f), container.transform, "Spawn Cliff Rock");
            SpawnRockAt(cliffs[Random.Range(0, cliffs.Length)], new Vector3(leftBankEdgeX - 3.0f, 1.2f, -17.5f), Quaternion.Euler(0f, 270f, 0f), new Vector3(3.5f, 5f, 3.5f), container.transform, "Spawn Cliff Rock");

            // 2. Spawn Standard Rocks along the stream bed
            for (int i = 0; i < 15; i++)
            {
                float rx = Random.Range(-40f, leftBankEdgeX - 1.0f);
                float rz = Random.Range(-24.5f, -15.5f);
                float normX = (rx - (-75f)) / 150f;
                float normZ = (rz - (-75f)) / 150f;
                int col = Mathf.Clamp((int)(normX * 1024f), 0, 1023);
                int row = Mathf.Clamp((int)(normZ * 1024f), 0, 1023);
                float ry = (matrixAsset.matrix[col, row] * 20.0f) - 2.2f;

                SpawnRockAt(stdRocks[Random.Range(0, stdRocks.Length)], new Vector3(rx, ry - 0.2f, rz), Quaternion.Euler(Random.Range(0f, 360f), Random.Range(0f, 360f), Random.Range(0f, 360f)), Vector3.one * Random.Range(0.8f, 1.8f), container.transform, "Spawn Bed Rock");
            }

            // 3. Spawn Lake Border Rocks
            for (int i = 0; i < 24; i++)
            {
                float angle = i * (360f / 24f) * Mathf.Deg2Rad;
                float rx = -45.0f + Mathf.Cos(angle) * 11.5f;
                float rz = -20.0f + Mathf.Sin(angle) * 11.5f;

                float normX = (rx - (-75f)) / 150f;
                float normZ = (rz - (-75f)) / 150f;
                int col = Mathf.Clamp((int)(normX * 1024f), 0, 1023);
                int row = Mathf.Clamp((int)(normZ * 1024f), 0, 1023);
                float ry = (matrixAsset.matrix[col, row] * 20.0f) - 2.2f;

                float rockY = Mathf.Max(ry, 5.8f) - 0.2f;
                SpawnRockAt(stdRocks[Random.Range(0, stdRocks.Length)], new Vector3(rx, rockY, rz), Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-15f, 15f)), Vector3.one * Random.Range(1.2f, 2.5f), container.transform, "Spawn Lake Border Rock");
            }

            // 4. Spawn Waterfall Base Splash Rocks
            for (int i = 0; i < 8; i++)
            {
                float rx = Random.Range(canalCenterX_Z - 10.0f, canalCenterX_Z - 7.5f);
                float rz = Random.Range(-25.0f, -15.0f);
                SpawnRockAt(stdRocks[Random.Range(0, stdRocks.Length)], new Vector3(rx, -1.3f, rz), Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-15f, 15f)), Vector3.one * Random.Range(1.5f, 2.5f), container.transform, "Spawn Splash Rock");
            }
        }

        private static void SpawnWindingCanalDetails()
        {
            GameObject natureContainer = new GameObject("NatureDecorations");
            Undo.RegisterCreatedObjectUndo(natureContainer, "Create NatureDecorations");

            GameObject lilyContainer = new GameObject("FloatingLilyPads");
            Undo.RegisterCreatedObjectUndo(lilyContainer, "Create FloatingLilyPads");

            GameObject reedContainer = new GameObject("ShorelineReeds");
            Undo.RegisterCreatedObjectUndo(reedContainer, "Create ShorelineReeds");

            // Load prefabs
            GameObject grassPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Foliage/Grass/Grass.prefab");
            GameObject flowerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Foliage/Flower/Flower.prefab");
            GameObject bushPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Foliage/Bush/Bush.prefab");
            GameObject mushroomPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Foliage/Mushroom/Mushrooms Patch.prefab");
            GameObject branchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Foliage/Branch/Branch.prefab");
            GameObject logPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Foliage/Log/Log.prefab");
            GameObject stumpPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Proxy Games/Stylized Nature Kit Lite/Prefabs/Foliage/Stump/Stump.prefab");

            GameObject[] stdRocks = LoadPrefabs(StandardRockPaths);
            GameObject[] tinyRocks = LoadPrefabs(TinyRockPaths);
            GameObject[] cliffs = LoadPrefabs(RockCliffPaths);

            if (stdRocks.Length == 0 || tinyRocks.Length == 0 || cliffs.Length == 0) return;

            string matrixPath = "Assets/Scenes/Phase2_Canal/Phase2_Canal_MatrixAsset.asset";
            var matrixAsset = AssetDatabase.LoadAssetAtPath<MatrixAsset>(matrixPath);
            if (matrixAsset == null) return;
            Matrix matrix = matrixAsset.matrix;

            HeightQuery GetHeight = (wx, wz) =>
            {
                float normX = (wx - (-75f)) / 150f;
                float normZ = (wz - (-75f)) / 150f;
                int col = Mathf.Clamp((int)(normX * 1024f), 0, 1023);
                int row = Mathf.Clamp((int)(normZ * 1024f), 0, 1023);
                return (matrix[col, row] * 20.0f) - 2.2f;
            };

            // 1. Shoreline rocks along winding banks
            for (float z = -65f; z <= 65f; z += 3.5f)
            {
                float canalCenter = GetCanalCenterX(z);
                
                // Left bank shoreline rock
                if (Random.value < 0.65f)
                {
                    float x = canalCenter - Random.Range(9.5f, 11.2f);
                    float y = GetHeight(x, z) - 0.2f;
                    Vector3 pos = new Vector3(x, y, z);
                    if (!IsInExclusionZone(pos))
                    {
                        SpawnPrefab(stdRocks[Random.Range(0, stdRocks.Length)], pos, Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-15f, 15f)), Vector3.one * Random.Range(0.8f, 1.6f), natureContainer.transform);
                    }
                }
                // Right bank shoreline rock
                if (Random.value < 0.65f)
                {
                    float x = canalCenter + Random.Range(9.5f, 11.2f);
                    float y = GetHeight(x, z) - 0.2f;
                    Vector3 pos = new Vector3(x, y, z);
                    if (!IsInExclusionZone(pos))
                    {
                        SpawnPrefab(stdRocks[Random.Range(0, stdRocks.Length)], pos, Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-15f, 15f)), Vector3.one * Random.Range(0.8f, 1.6f), natureContainer.transform);
                    }
                }
            }

            // 2. Tiny rocks along shores
            for (float z = -65f; z <= 65f; z += 1.8f)
            {
                float canalCenter = GetCanalCenterX(z);
                if (Random.value < 0.5f)
                {
                    float side = Random.value < 0.5f ? -1f : 1f;
                    float x = canalCenter + (Random.Range(9.3f, 10.8f) * side);
                    float y = GetHeight(x, z) - 0.05f;
                    Vector3 pos = new Vector3(x, y, z);
                    if (!IsInExclusionZone(pos))
                    {
                        SpawnPrefab(tinyRocks[Random.Range(0, tinyRocks.Length)], pos, Quaternion.Euler(Random.Range(-45f, 45f), Random.Range(0f, 360f), Random.Range(-45f, 45f)), Vector3.one * Random.Range(0.5f, 1.3f), natureContainer.transform);
                    }
                }
            }

            // 3. Cliff backdrops (far boundaries)
            for (float z = -60f; z <= 60f; z += 15f)
            {
                // Left border backdrop
                {
                    float x = -68f;
                    float y = GetHeight(x, z) - 3.5f;
                    Vector3 pos = new Vector3(x, y, z);
                    if (!IsInExclusionZone(pos))
                    {
                        SpawnPrefab(cliffs[Random.Range(0, cliffs.Length)], pos, Quaternion.Euler(0f, Random.Range(70f, 110f), 0f), new Vector3(3f, Random.Range(3.5f, 5.5f), 3f), natureContainer.transform);
                    }
                }
                // Right border backdrop
                {
                    float x = 68f;
                    float y = GetHeight(x, z) - 3.5f;
                    Vector3 pos = new Vector3(x, y, z);
                    if (!IsInExclusionZone(pos))
                    {
                        SpawnPrefab(cliffs[Random.Range(0, cliffs.Length)], pos, Quaternion.Euler(0f, Random.Range(-110f, -70f), 0f), new Vector3(3f, Random.Range(3.5f, 5.5f), 3f), natureContainer.transform);
                    }
                }
            }

            // 4. Dense foliage clusters
            // Left bank floor clusters
            for (int i = 0; i < 220; i++)
            {
                float z = Random.Range(-65f, 65f);
                float canalCenter = GetCanalCenterX(z);
                float x = Random.Range(-65f, canalCenter - 13.0f);
                float y = GetHeight(x, z);
                Vector3 pos = new Vector3(x, y, z);

                if (pos.y >= -0.3f && !IsInFoliageExclusionZone(pos))
                {
                    float rand = Random.value;
                    if (rand < 0.55f) SpawnCluster(grassPrefab, pos, Random.Range(3, 7), 1.2f, natureContainer.transform, 0.2f, 0.5f, GetHeight);
                    else if (rand < 0.70f) SpawnCluster(flowerPrefab, pos, Random.Range(3, 7), 1.2f, natureContainer.transform, 1.0f, 2.2f, GetHeight);
                    else if (rand < 0.80f) SpawnCluster(mushroomPrefab, pos, Random.Range(3, 7), 1.0f, natureContainer.transform, 1.0f, 2.2f, GetHeight);
                    else if (rand < 0.90f) SpawnCluster(bushPrefab, pos, Random.Range(3, 7), 1.2f, natureContainer.transform, 2.0f, 3.8f, GetHeight);
                    else
                    {
                        float debrisRand = Random.value;
                        if (debrisRand < 0.40f) SpawnPrefab(branchPrefab, pos, Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 360f), Random.Range(-10f, 10f)), Vector3.one * Random.Range(1.2f, 2.2f), natureContainer.transform);
                        else if (debrisRand < 0.70f) SpawnPrefab(logPrefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), Vector3.one * Random.Range(1.2f, 2.2f), natureContainer.transform);
                        else SpawnPrefab(stumpPrefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), Vector3.one * Random.Range(1.2f, 2.2f), natureContainer.transform);
                    }
                }
            }

            // Right bank floor clusters
            for (int i = 0; i < 110; i++)
            {
                float z = Random.Range(-65f, 65f);
                float canalCenter = GetCanalCenterX(z);
                float x = Random.Range(canalCenter + 13.0f, 65f);
                float y = GetHeight(x, z);
                Vector3 pos = new Vector3(x, y, z);

                if (pos.y >= -0.3f && !IsInFoliageExclusionZone(pos))
                {
                    float rand = Random.value;
                    if (rand < 0.55f) SpawnCluster(grassPrefab, pos, Random.Range(3, 6), 1.2f, natureContainer.transform, 0.2f, 0.5f, GetHeight);
                    else if (rand < 0.75f) SpawnCluster(flowerPrefab, pos, Random.Range(3, 6), 1.2f, natureContainer.transform, 1.0f, 2.2f, GetHeight);
                    else SpawnCluster(bushPrefab, pos, Random.Range(3, 6), 1.2f, natureContainer.transform, 2.0f, 3.8f, GetHeight);
                }
            }

            // Shoreline foliage clusters
            for (float z = -65f; z <= 65f; z += 1.5f)
            {
                float canalCenter = GetCanalCenterX(z);

                // Left shore
                if (Random.value < 0.95f)
                {
                    float x = canalCenter - Random.Range(10.5f, 13.0f);
                    float y = GetHeight(x, z);
                    Vector3 pos = new Vector3(x, y, z);
                    if (pos.y >= -0.3f && !IsInFoliageExclusionZone(pos))
                    {
                        float typeVal = Random.value;
                        if (typeVal < 0.60f) SpawnCluster(grassPrefab, pos, Random.Range(3, 6), 1.0f, natureContainer.transform, 0.2f, 0.5f, GetHeight);
                        else if (typeVal < 0.85f) SpawnCluster(flowerPrefab, pos, Random.Range(3, 6), 1.0f, natureContainer.transform, 1.0f, 2.2f, GetHeight);
                        else SpawnCluster(bushPrefab, pos, Random.Range(3, 6), 1.0f, natureContainer.transform, 2.0f, 3.8f, GetHeight);
                    }
                }

                // Right shore
                if (Random.value < 0.95f)
                {
                    float x = canalCenter + Random.Range(10.5f, 13.0f);
                    float y = GetHeight(x, z);
                    Vector3 pos = new Vector3(x, y, z);
                    if (pos.y >= -0.3f && !IsInFoliageExclusionZone(pos))
                    {
                        float typeVal = Random.value;
                        if (typeVal < 0.60f) SpawnCluster(grassPrefab, pos, Random.Range(3, 6), 1.0f, natureContainer.transform, 0.2f, 0.5f, GetHeight);
                        else if (typeVal < 0.85f) SpawnCluster(flowerPrefab, pos, Random.Range(3, 6), 1.0f, natureContainer.transform, 1.0f, 2.2f, GetHeight);
                        else SpawnCluster(bushPrefab, pos, Random.Range(3, 6), 1.0f, natureContainer.transform, 2.0f, 3.8f, GetHeight);
                    }
                }
            }

            // 5. Floating Lily Pads and Lotus Flowers
            Material lilyMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            lilyMat.color = new Color(0.12f, 0.45f, 0.22f);
            if (lilyMat.HasProperty("_Smoothness")) lilyMat.SetFloat("_Smoothness", 0.1f);

            for (int i = 0; i < 50; i++)
            {
                float z = Random.Range(-45f, 45f);
                float canalCenter = GetCanalCenterX(z);
                float x = canalCenter + Random.Range(-7.0f, 7.0f);
                float y = -0.98f;
                Vector3 pos = new Vector3(x, y, z);

                if (!IsInFoliageExclusionZone(pos))
                {
                    GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    pad.name = "LilyPad_" + i;
                    pad.transform.SetParent(lilyContainer.transform);
                    pad.transform.position = pos;
                    pad.transform.localScale = new Vector3(Random.Range(0.6f, 1.4f), 0.01f, Random.Range(0.6f, 1.4f));
                    Object.DestroyImmediate(pad.GetComponent<Collider>());
                    pad.GetComponent<Renderer>().sharedMaterial = lilyMat;
                    pad.isStatic = true;
                    Undo.RegisterCreatedObjectUndo(pad, "Spawn LilyPad");

                    if (Random.value < 0.35f)
                    {
                        CreateLotusFlower(pos, pad.transform);
                    }
                }
            }

            // 6. Shoreline Reeds
            Material reedMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            reedMat.color = new Color(0.24f, 0.38f, 0.14f);
            if (reedMat.HasProperty("_Smoothness")) reedMat.SetFloat("_Smoothness", 0.05f);

            for (int i = 0; i < 120; i++)
            {
                float z = Random.Range(-50f, 50f);
                float canalCenter = GetCanalCenterX(z);
                float x = (Random.value > 0.5f) ? canalCenter - Random.Range(9.2f, 10.8f) : canalCenter + Random.Range(9.2f, 10.8f);
                float y = GetHeight(x, z);
                Vector3 pos = new Vector3(x, y, z);

                if (!IsInFoliageExclusionZone(pos))
                {
                    GameObject reed = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    reed.name = "Reed_" + i;
                    reed.transform.SetParent(reedContainer.transform);
                    float reedHeight = Random.Range(1.2f, 2.3f);
                    reed.transform.position = new Vector3(x, y + reedHeight * 0.5f - 0.2f, z);
                    reed.transform.localScale = new Vector3(0.06f, reedHeight, 0.06f);
                    Object.DestroyImmediate(reed.GetComponent<Collider>());
                    reed.GetComponent<Renderer>().sharedMaterial = reedMat;
                    reed.isStatic = true;
                    Undo.RegisterCreatedObjectUndo(reed, "Spawn Reed");
                }
            }
        }

        private static void SpawnCluster(GameObject prefab, Vector3 centerPos, int count, float radius, Transform parent, float minScale, float maxScale, HeightQuery heightQuery)
        {
            if (prefab == null) return;
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                Vector3 spawnPos = new Vector3(centerPos.x + offset.x, 0f, centerPos.z + offset.y);
                spawnPos.y = heightQuery(spawnPos.x, spawnPos.z);

                if (spawnPos.y >= -0.3f && !IsInFoliageExclusionZone(spawnPos))
                {
                    SpawnPrefab(prefab, spawnPos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), Vector3.one * Random.Range(minScale, maxScale), parent);
                }
            }
        }

        private static GameObject SpawnPrefab(GameObject prefab, Vector3 position, Quaternion rotation, Vector3 scale, Transform parent)
        {
            if (prefab == null) return null;
            GameObject go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (go != null)
            {
                go.transform.position = position;
                go.transform.rotation = rotation;
                go.transform.localScale = scale;
                go.transform.SetParent(parent);

                SetStaticRecursively(go);

                // Strip colliders from small decorative foliage to prevent player floating
                string nameLower = go.name.ToLower();
                if (nameLower.Contains("grass") || nameLower.Contains("flower") || nameLower.Contains("mushroom") || nameLower.Contains("bush"))
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(go))
                    {
                        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    }
                    Collider[] colliders = go.GetComponentsInChildren<Collider>();
                    foreach (var c in colliders)
                    {
                        if (c != null)
                        {
                            Undo.DestroyObjectImmediate(c);
                        }
                    }
                }
                Undo.RegisterCreatedObjectUndo(go, "Spawn " + go.name);
            }
            return go;
        }

        private static void SetStaticRecursively(GameObject go)
        {
            if (go == null) return;
            go.isStatic = true;
            foreach (Transform child in go.transform)
            {
                if (child != null)
                {
                    SetStaticRecursively(child.gameObject);
                }
            }
        }

        private static void CreateLotusFlower(Vector3 position, Transform parent)
        {
            GameObject flower = new GameObject("LotusFlower");
            flower.transform.SetParent(parent, false);
            flower.transform.position = position + new Vector3(0f, 0.02f, 0f);

            Material petalMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            petalMat.color = new Color(0.96f, 0.52f, 0.74f);
            if (petalMat.HasProperty("_Smoothness")) petalMat.SetFloat("_Smoothness", 0.1f);

            Material centerMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            centerMat.color = new Color(1.0f, 0.84f, 0f);

            GameObject center = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            center.name = "Pistil";
            center.transform.SetParent(flower.transform, false);
            center.transform.localScale = new Vector3(0.18f, 0.08f, 0.18f);
            center.transform.localPosition = new Vector3(0, 0.02f, 0);
            center.GetComponent<Renderer>().sharedMaterial = centerMat;
            Object.DestroyImmediate(center.GetComponent<Collider>());
            center.isStatic = true;

            for (int i = 0; i < 6; i++)
            {
                float angle = i * 60f * Mathf.Deg2Rad;
                GameObject petal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                petal.name = "Petal_" + i;
                petal.transform.SetParent(flower.transform, false);
                petal.transform.localScale = new Vector3(0.06f, 0.01f, 0.22f);

                float x = Mathf.Sin(angle) * 0.1f;
                float z = Mathf.Cos(angle) * 0.1f;
                petal.transform.localPosition = new Vector3(x, 0.01f, z);
                petal.transform.localRotation = Quaternion.Euler(15f, i * 60f, 0f);

                petal.GetComponent<Renderer>().sharedMaterial = petalMat;
                Object.DestroyImmediate(petal.GetComponent<Collider>());
                petal.isStatic = true;
            }
            flower.isStatic = true;
            Undo.RegisterCreatedObjectUndo(flower, "Spawn Lotus Flower");
        }

        private static GameObject[] LoadPrefabs(string[] paths)
        {
            var list = new System.Collections.Generic.List<GameObject>();
            foreach (string p in paths)
            {
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go != null) list.Add(go);
                else Debug.LogWarning("Failed to load rock prefab at: " + p);
            }
            return list.ToArray();
        }

        private static void SpawnRockAt(GameObject prefab, Vector3 pos, Quaternion rot, Vector3 scale, Transform parent, string undoName)
        {
            GameObject go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            go.transform.position = pos;
            go.transform.rotation = rot;
            go.transform.localScale = scale;
            go.transform.SetParent(parent);
            go.isStatic = true;
            Undo.RegisterCreatedObjectUndo(go, undoName);
        }

        [MenuItem("Tools/Audit Phase 2")]
        public static void Audit()
        {
            // Active scene validation
            if (SceneManager.GetActiveScene().name != "Phase2_Canal")
            {
                EditorUtility.DisplayDialog("Error", "Please open the Phase2_Canal scene before running this audit!", "OK");
                return;
            }

            Debug.Log("Starting Phase 2 Scenery Audit...");
            var matrixPath = "Assets/Scenes/Phase2_Canal/Phase2_Canal_MatrixAsset.asset";
            var matrixAsset = AssetDatabase.LoadAssetAtPath<MatrixAsset>(matrixPath);
            if (matrixAsset == null)
            {
                Debug.LogError("MatrixAsset not found at: " + matrixPath);
                return;
            }
            Matrix matrix = matrixAsset.matrix;

            // 1. Verify Canal heights (Y = -2.2m flat relative to winding center)
            bool canalOk = true;
            for (float z = -50f; z < 50f; z += 5f)
            {
                float cx = GetCanalCenterX(z);
                for (float offset = -8f; offset <= 8f; offset += 2f)
                {
                    float x = cx + offset;
                    float normX = (x - (-75f)) / 150f;
                    float normZ = (z - (-75f)) / 150f;
                    int col = Mathf.Clamp((int)(normX * 1024f), 0, 1023);
                    int row = Mathf.Clamp((int)(normZ * 1024f), 0, 1023);
                    float normY = matrix[col, row];
                    // Using scale = 20.0f
                    float y = (normY * 20.0f) - 2.2f;
                    if (Mathf.Abs(y - (-2.2f)) > 0.05f)
                    {
                        Debug.LogError($"Canal height mismatch at ({x}, {z}): Y = {y}");
                        canalOk = false;
                        break;
                    }
                }
                if (!canalOk) break;
            }
            if (canalOk) Debug.Log("✅ Canal riverbed heights verified successfully (winding at Y = -2.2m).");

            // 2. Verify tree heights and exclusion zones
            GameObject container = GameObject.Find("FruitTreesContainer");
            if (container == null)
            {
                Debug.LogError("FruitTreesContainer root GameObject not found! Run 'Tools/Beautify Phase 2' first.");
                return;
            }

            int treeCount = container.transform.childCount;
            int invalidTrees = 0;
            foreach (Transform tree in container.transform)
            {
                Vector3 pos = tree.position;

                float normX = (pos.x - (-75f)) / 150f;
                float normZ = (pos.z - (-75f)) / 150f;
                int col = Mathf.Clamp((int)(normX * 1024f), 0, 1023);
                int row = Mathf.Clamp((int)(normZ * 1024f), 0, 1023);
                float normY = matrix[col, row];
                // Using scale = 20.0f
                float expectedY = (normY * 20.0f) - 2.2f;

                if (Mathf.Abs(pos.y - expectedY) > 0.05f)
                {
                    Debug.LogError($"Tree at {pos} height mismatch! Expected Y: {expectedY}, actual Y: {pos.y}");
                    invalidTrees++;
                }

                // Verify exclusion zones
                float canalX = GetCanalCenterX(pos.z);
                if (Mathf.Abs(pos.x - canalX) < 14.5f)
                {
                    Debug.LogError($"Tree at {pos} is inside the canal water!");
                    invalidTrees++;
                }
                if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(25f, -56f)) < 12f)
                {
                    Debug.LogError($"Tree at {pos} is inside Start Pier exclusion zone!");
                    invalidTrees++;
                }
                if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(25f, 55f)) < 8f)
                {
                    Debug.LogError($"Tree at {pos} is inside End Pier exclusion zone!");
                    invalidTrees++;
                }
                if (pos.z >= -27f && pos.z <= -13f && pos.x < canalX - 12.0f)
                {
                    Debug.LogError($"Tree at {pos} is inside waterfall stream zone!");
                    invalidTrees++;
                }
            }

            if (invalidTrees == 0)
            {
                Debug.Log($"✅ Trees verified successfully: all {treeCount} fruit trees sit exactly on the terrain and respect exclusion zones.");
            }
            else
            {
                Debug.LogError($"❌ Tree audit failed: {invalidTrees} issues found.");
            }
        }
    }
}
