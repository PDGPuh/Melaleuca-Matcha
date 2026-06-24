using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace RungTramTraSu.Editor
{
    public class SceneBeautifierV5 : EditorWindow
    {
        private const string ScenePath = "Assets/Scenes/Phase1_GrandpaHouse.unity";
        private const string KitPath = "Assets/Proxy Games/Stylized Nature Kit Lite/";

        // Prefab Paths
        private const string WaterPrefabPath = KitPath + "Prefabs/Water/Detailed Water.prefab";
        private const string GrassPrefabPath = KitPath + "Prefabs/Foliage/Grass/Grass.prefab";
        private const string FlowerPrefabPath = KitPath + "Prefabs/Foliage/Flower/Flower.prefab";
        private const string BushPrefabPath = KitPath + "Prefabs/Foliage/Bush/Bush.prefab";
        private const string MushroomPrefabPath = KitPath + "Prefabs/Foliage/Mushroom/Mushrooms Patch.prefab";
        private const string BranchPrefabPath = KitPath + "Prefabs/Foliage/Branch/Branch.prefab";
        private const string LogPrefabPath = KitPath + "Prefabs/Foliage/Log/Log.prefab";
        private const string StumpPrefabPath = KitPath + "Prefabs/Foliage/Stump/Stump.prefab";
        
        private static readonly string[] SprucePrefabPaths = new string[]
        {
            KitPath + "Prefabs/Foliage/Trees/Spruce 1.prefab",
            KitPath + "Prefabs/Foliage/Trees/Spruce 2.prefab"
        };

        private static readonly string[] StandardRockPaths = new string[]
        {
            KitPath + "Prefabs/Rocks/Standard Rocks/Standard Rock 1.prefab",
            KitPath + "Prefabs/Rocks/Standard Rocks/Standard Rock 2.prefab",
            KitPath + "Prefabs/Rocks/Standard Rocks/Standard Rock 3.prefab",
            KitPath + "Prefabs/Rocks/Standard Rocks/Standard Rock 4.prefab",
            KitPath + "Prefabs/Rocks/Standard Rocks/Standard Rock 5.prefab"
        };

        private static readonly string[] TinyRockPaths = new string[]
        {
            KitPath + "Prefabs/Rocks/Tiny Rocks/Tiny Rock 1.prefab",
            KitPath + "Prefabs/Rocks/Tiny Rocks/Tiny Rock 2.prefab",
            KitPath + "Prefabs/Rocks/Tiny Rocks/Tiny Rock 3.prefab",
            KitPath + "Prefabs/Rocks/Tiny Rocks/Tiny Rock 4.prefab",
            KitPath + "Prefabs/Rocks/Tiny Rocks/Tiny Rock 5.prefab"
        };

        private static readonly string[] RockCliffPaths = new string[]
        {
            KitPath + "Prefabs/Rocks/Rock Cliffs/Rock Cliff 1.prefab",
            KitPath + "Prefabs/Rocks/Rock Cliffs/Rock Cliff 2.prefab",
            KitPath + "Prefabs/Rocks/Rock Cliffs/Rock Cliff 3.prefab",
            KitPath + "Prefabs/Rocks/Rock Cliffs/Rock Cliff 4.prefab",
            KitPath + "Prefabs/Rocks/Rock Cliffs/Rock Cliff 5.prefab"
        };

        // Material Paths
        private const string TerrainGrassTexturePath = KitPath + "Textures/Terrain Grass.png";
        private const string SkyboxMaterialPath = "Assets/EmaceArt/Slavic World Free/Skybox/Epic_BigCloudsSoft_V2/EA03_LowPolyBigClouds.mat";

        private static float GetHeightAt(float x, float z)
        {
            float canalCenter = 25f + Mathf.Sin(z * 0.08f) * 5f;
            float dist = Mathf.Abs(x - canalCenter);
            
            float t = (dist - 6f) / 8f;
            t = Mathf.Clamp01(t);
            float smoothT = t * t * (3f - 2f * t);
            float baseHeight = Mathf.Lerp(-2.2f, 0.0f, smoothT);
            
            float noise = 0f;
            if (dist > 5f)
            {
                float n1 = Mathf.PerlinNoise(x * 0.08f + 100f, z * 0.08f + 100f) * 1.5f;
                float n2 = Mathf.PerlinNoise(x * 0.25f + 200f, z * 0.25f + 200f) * 0.3f;
                noise = (n1 + n2) * smoothT;
            }
            
            float leftBoost = 0f;
            if (x < -10f)
            {
                leftBoost = Mathf.Lerp(0f, 2.5f, (-10f - x) / 45f);
            }
            
            return baseHeight + noise + leftBoost;
        }

        private static bool IsInExclusionZone(Vector3 pos)
        {
            // Stilt House: (-5, *, 0)  — radius 12m
            if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(-5f, 0f)) < 12f) return true;
            
            // Grandpa NPC: (-1, *, 0)  — radius 3.8m
            if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(-1f, 0f)) < 3.8f) return true;
            
            // Wooden Pier: (15, *, 8)  — radius 5m
            if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(15f, 8f)) < 5f) return true;
            
            // Sampan Boat: (19.5, *, 8) — radius 3m
            if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(19.5f, 8f)) < 3f) return true;
            
            // Mango Tree: (-3, *, 14)  — radius 4m
            if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(-3f, 14f)) < 4f) return true;
            
            // Wooden Sign: (12.5, *, 6.5) — radius 3m
            if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(12.5f, 6.5f)) < 3f) return true;
            
            return false;
        }

        private static bool IsInFoliageExclusionZone(Vector3 pos)
        {
            // Stilt House: (-5, 0) — radius 5m (allows grass near stilts, but not inside center)
            if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(-5f, 0f)) < 5.0f) return true;
            
            // Grandpa NPC: (-1, 0) — radius 1.5m
            if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(-1f, 0f)) < 1.5f) return true;
            
            // Wooden Pier: (15, 8) — radius 1.5m (allows grass right up to bridge edges)
            if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(15f, 8f)) < 1.5f) return true;
            
            // Sampan Boat: (19.5, 8) — radius 3.0m
            if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(19.5f, 8f)) < 3.0f) return true;
            
            // Mango Tree: (-3, 14) — radius 1.0m
            if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(-3f, 14f)) < 1.0f) return true;
            
            // Wooden Sign: (12.5, 6.5) — radius 0.8m
            if (Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(12.5f, 6.5f)) < 0.8f) return true;
            
            return false;
        }

        private static void EnsureSceneLoaded()
        {
            Scene currentScene = SceneManager.GetActiveScene();
            if (currentScene.path != ScenePath)
            {
                Debug.Log($"Loading scene: {ScenePath}");
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
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

        private static GameObject GetOrCreateContainer(string name)
        {
            GameObject container = GameObject.Find(name);
            if (container != null)
            {
                Undo.DestroyObjectImmediate(container);
            }
            container = new GameObject(name);
            return container;
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

                // Mark static recursively for batching
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
                            DestroyImmediate(c);
                        }
                    }
                }
            }
            return go;
        }

//         [MenuItem("Rung Tram Tra Su/Beautify Phase 1/Step 1 - Upgrade Terrain")]
        public static void UpgradeTerrain()
        {
            EnsureSceneLoaded();
            Debug.Log("Upgrading Terrain material...");

            GameObject terrainObj = GameObject.Find("OrganicTerrain_Bank");
            if (terrainObj == null)
            {
                Debug.LogError("OrganicTerrain_Bank not found in scene!");
                return;
            }

            MeshRenderer renderer = terrainObj.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                Debug.LogError("OrganicTerrain_Bank has no MeshRenderer!");
                return;
            }

            Texture2D grassTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TerrainGrassTexturePath);
            if (grassTex == null)
            {
                Debug.LogError($"Could not load terrain texture at: {TerrainGrassTexturePath}");
                return;
            }

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("Universal Render Pipeline/Lit shader not found!");
                return;
            }

            Material terrainMat = new Material(urpLit);
            terrainMat.name = "StylizedTerrainMaterial";
            terrainMat.SetTexture("_BaseMap", grassTex);
            terrainMat.SetTextureScale("_BaseMap", new Vector2(8f, 10f));
            terrainMat.SetFloat("_Smoothness", 0.1f);

            // Save material to the project so it persists
            string matPath = "Assets/StylizedTerrainMaterial.mat";
            AssetDatabase.CreateAsset(terrainMat, matPath);
            AssetDatabase.SaveAssets();

            Undo.RecordObject(renderer, "Upgrade Terrain Material");
            renderer.sharedMaterial = terrainMat;

            Debug.Log("Terrain material upgraded successfully!");
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

//         [MenuItem("Rung Tram Tra Su/Beautify Phase 1/Step 2 - Upgrade Water")]
        public static void UpgradeWater()
        {
            EnsureSceneLoaded();
            Debug.Log("Upgrading canal water...");

            // Destroy the tiled square water container if it exists
            GameObject container = GameObject.Find("StylizedWater");
            if (container != null)
            {
                Undo.DestroyObjectImmediate(container);
                Debug.Log("Removed tiled StylizedWater container.");
            }

            // Find and enable old water plane
            GameObject riverObj = GameObject.Find("RiverWater_Canal");
            if (riverObj == null)
            {
                Debug.Log("RiverWater_Canal not found in scene. Recreating it...");
                riverObj = GameObject.CreatePrimitive(PrimitiveType.Plane);
                riverObj.name = "RiverWater_Canal";
                
                // Remove MeshCollider as it's a visual-only water surface
                MeshCollider mc = riverObj.GetComponent<MeshCollider>();
                if (mc != null)
                {
                    DestroyImmediate(mc);
                }
                Undo.RegisterCreatedObjectUndo(riverObj, "Create RiverWater_Canal");
            }

            Undo.RecordObject(riverObj, "Enable and Upgrade RiverWater_Canal");
            riverObj.SetActive(true);
            
            // Position at original Y = -1.0f
            riverObj.transform.position = new Vector3(25f, -1.0f, 0f);
            riverObj.transform.localScale = new Vector3(4.5f, 1f, 15f);

            // Attach WaterWaveDeformer script for 3D waves
            if (riverObj.GetComponent<WaterWaveDeformer>() == null)
            {
                Undo.AddComponent<WaterWaveDeformer>(riverObj);
                Debug.Log("Attached WaterWaveDeformer script to RiverWater_Canal.");
            }
            // Destroy old WaterScroller if present
            WaterScroller oldScroller = riverObj.GetComponent<WaterScroller>();
            if (oldScroller != null)
            {
                Undo.DestroyObjectImmediate(oldScroller);
            }

            // Load original duckweed texture for natural swamp canal look
            Texture2D duckweedTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/duckweed_water_texture.png");

            MeshRenderer mr = riverObj.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                // Find or load the Water.mat from the kit
                Material waterMat = AssetDatabase.LoadAssetAtPath<Material>(KitPath + "Materials/Water.mat");
                if (waterMat == null)
                {
                    Debug.LogWarning("Water.mat not found in kit. Creating custom transparent water material...");
                    
                    Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
                    waterMat = new Material(urpLit);
                    waterMat.name = "CustomStylizedWater";
                    waterMat.SetFloat("_Surface", 1.0f); // Transparent
                    waterMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    waterMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    waterMat.SetInt("_ZWrite", 0);
                    waterMat.DisableKeyword("_ALPHATEST_ON");
                    waterMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    waterMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    waterMat.SetColor("_BaseColor", new Color(0.12f, 0.45f, 0.4f, 0.7f));
                    waterMat.SetFloat("_Smoothness", 0.95f);
                    waterMat.SetFloat("_Metallic", 0.1f);
                    if (duckweedTex != null)
                    {
                        waterMat.SetTexture("_BaseMap", duckweedTex);
                        waterMat.SetTextureScale("_BaseMap", new Vector2(4f, 40f));
                    }
                    
                    AssetDatabase.CreateAsset(waterMat, "Assets/CustomStylizedWater.mat");
                    AssetDatabase.SaveAssets();
                }
                else
                {
                    // Configure it to be transparent teal URP with duckweed texture
                    Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
                    if (urpLit != null)
                    {
                        waterMat.shader = urpLit;
                        waterMat.SetFloat("_Surface", 1.0f); // Transparent
                        waterMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                        waterMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                        waterMat.SetInt("_ZWrite", 0);
                        waterMat.DisableKeyword("_ALPHATEST_ON");
                        waterMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                        waterMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                        waterMat.SetColor("_BaseColor", new Color(0.12f, 0.45f, 0.4f, 0.7f));
                        waterMat.SetFloat("_Smoothness", 0.95f);
                        waterMat.SetFloat("_Metallic", 0.1f);
                        if (duckweedTex != null)
                        {
                            waterMat.SetTexture("_BaseMap", duckweedTex);
                            waterMat.SetTextureScale("_BaseMap", new Vector2(4f, 40f));
                        }
                        EditorUtility.SetDirty(waterMat);
                    }
                }

                mr.sharedMaterial = waterMat;
                Debug.Log("Applied transparent teal URP water material to RiverWater_Canal.");
            }

            Debug.Log("Water upgraded successfully!");
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

//         [MenuItem("Rung Tram Tra Su/Beautify Phase 1/Step 3 - Populate Nature")]
        public static void PopulateNature()
        {
            EnsureSceneLoaded();
            Debug.Log("Populating nature decorations with dense clusters...");

            // Delete old primitive decoration containers
            string[] oldDecorations = new string[] { "ShorelineReeds", "FloatingLilyPads", "PathReedsCorridor" };
            foreach (string name in oldDecorations)
            {
                GameObject obj = GameObject.Find(name);
                if (obj != null)
                {
                    Undo.DestroyObjectImmediate(obj);
                }
            }

            GameObject container = GetOrCreateContainer("NatureDecorations");

            // Load Prefabs
            GameObject grassPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GrassPrefabPath);
            GameObject flowerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FlowerPrefabPath);
            GameObject bushPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BushPrefabPath);
            GameObject mushroomPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MushroomPrefabPath);
            GameObject branchPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BranchPrefabPath);
            GameObject logPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LogPrefabPath);
            GameObject stumpPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StumpPrefabPath);

            Random.InitState(42);

            // --- ZONE A: Canal Banks (Left and Right shorelines) ---
            // Tighter step of z (1.2f) for moderate shorelines without causing lag
            for (float z = -65f; z <= 65f; z += 1.2f)
            {
                float canalCenter = 25f + Mathf.Sin(z * 0.08f) * 5f;

                // Left shoreline cluster
                if (Random.value < 0.98f)
                {
                    float x = canalCenter - Random.Range(11.5f, 15.0f);
                    float y = GetHeightAt(x, z);
                    Vector3 pos = new Vector3(x, y, z);
                    if (pos.y >= -0.3f && !IsInFoliageExclusionZone(pos))
                    {
                        // Spawn cluster composed of a single random prefab type (composition logic)
                        float typeVal = Random.value;
                        if (typeVal < 0.60f)
                            SpawnCluster(grassPrefab, pos, Random.Range(3, 6), 1.0f, container.transform, 0.2f, 0.45f);
                        else if (typeVal < 0.85f)
                            SpawnCluster(flowerPrefab, pos, Random.Range(3, 6), 1.0f, container.transform, 1.0f, 2.2f);
                        else
                            SpawnCluster(bushPrefab, pos, Random.Range(3, 6), 1.0f, container.transform, 2.0f, 3.8f);
                    }
                }

                // Right shoreline cluster
                if (Random.value < 0.98f)
                {
                    float x = canalCenter + Random.Range(11.5f, 15.0f);
                    float y = GetHeightAt(x, z);
                    Vector3 pos = new Vector3(x, y, z);
                    if (pos.y >= -0.3f && !IsInFoliageExclusionZone(pos))
                    {
                        float typeVal = Random.value;
                        if (typeVal < 0.60f)
                            SpawnCluster(grassPrefab, pos, Random.Range(3, 6), 1.0f, container.transform, 0.2f, 0.45f);
                        else if (typeVal < 0.85f)
                            SpawnCluster(flowerPrefab, pos, Random.Range(3, 6), 1.0f, container.transform, 1.0f, 2.2f);
                        else
                            SpawnCluster(bushPrefab, pos, Random.Range(3, 6), 1.0f, container.transform, 2.0f, 3.8f);
                    }
                }
            }

            // --- ZONE B: Open Land (Left bank, X < 10) ---
            for (int i = 0; i < 250; i++)
            {
                float x = Random.Range(-52f, 8f);
                float z = Random.Range(-62f, 62f);
                float y = GetHeightAt(x, z);
                Vector3 pos = new Vector3(x, y, z);

                if (pos.y >= -0.3f && !IsInFoliageExclusionZone(pos))
                {
                    float rand = Random.value;
                    if (rand < 0.55f)
                    {
                        SpawnCluster(grassPrefab, pos, Random.Range(3, 7), 1.2f, container.transform, 0.2f, 0.45f);
                    }
                    else if (rand < 0.70f)
                    {
                        SpawnCluster(flowerPrefab, pos, Random.Range(3, 7), 1.2f, container.transform, 1.0f, 2.2f);
                    }
                    else if (rand < 0.80f)
                    {
                        SpawnCluster(mushroomPrefab, pos, Random.Range(3, 7), 1.0f, container.transform, 1.0f, 2.2f);
                    }
                    else if (rand < 0.90f)
                    {
                        SpawnCluster(bushPrefab, pos, Random.Range(3, 7), 1.2f, container.transform, 2.0f, 3.8f);
                    }
                    else
                    {
                        float debrisRand = Random.value;
                        if (debrisRand < 0.40f)
                            SpawnPrefab(branchPrefab, pos, Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 360f), Random.Range(-10f, 10f)), Vector3.one * Random.Range(1.2f, 2.2f), container.transform);
                        else if (debrisRand < 0.70f)
                            SpawnPrefab(logPrefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), Vector3.one * Random.Range(1.2f, 2.2f), container.transform);
                        else
                            SpawnPrefab(stumpPrefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), Vector3.one * Random.Range(1.2f, 2.2f), container.transform);
                    }
                }
            }

            // --- ZONE C: Far Right Bank ---
            for (int i = 0; i < 120; i++)
            {
                float z = Random.Range(-62f, 62f);
                float canalCenter = 25f + Mathf.Sin(z * 0.08f) * 5f;
                float x = Random.Range(canalCenter + 14f, 52f);
                float y = GetHeightAt(x, z);
                Vector3 pos = new Vector3(x, y, z);

                if (pos.y >= -0.3f && !IsInFoliageExclusionZone(pos))
                {
                    float rand = Random.value;
                    if (rand < 0.55f)
                    {
                        SpawnCluster(grassPrefab, pos, Random.Range(3, 6), 1.2f, container.transform, 0.2f, 0.45f);
                    }
                    else if (rand < 0.75f)
                    {
                        SpawnCluster(flowerPrefab, pos, Random.Range(3, 6), 1.2f, container.transform, 1.0f, 2.2f);
                    }
                    else
                    {
                        SpawnCluster(bushPrefab, pos, Random.Range(3, 6), 1.2f, container.transform, 2.0f, 3.8f);
                    }
                }
            }

            // Register fully populated container with Undo
            Undo.RegisterCreatedObjectUndo(container, "Create NatureDecorations");

            Debug.Log("Nature decorations populated successfully with dense clusters!");
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private static void SpawnCluster(GameObject prefab, Vector3 centerPos, int count, float radius, Transform parent, float minScale, float maxScale)
        {
            if (prefab == null) return;
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                Vector3 spawnPos = new Vector3(centerPos.x + offset.x, 0f, centerPos.z + offset.y);
                spawnPos.y = GetHeightAt(spawnPos.x, spawnPos.z);

                // Check exclusion zone per item
                if (spawnPos.y >= -0.3f && !IsInFoliageExclusionZone(spawnPos))
                {
                    SpawnPrefab(prefab, spawnPos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), Vector3.one * Random.Range(minScale, maxScale), parent);
                }
            }
        }

//         [MenuItem("Rung Tram Tra Su/Beautify Phase 1/Step 4 - Add Rocks")]
        public static void AddRocks()
        {
            EnsureSceneLoaded();
            Debug.Log("Placing rocks...");

            GameObject container = GetOrCreateContainer("RockFormations");

            // Load rock prefabs
            List<GameObject> standardRocks = LoadPrefabs(StandardRockPaths);
            List<GameObject> tinyRocks = LoadPrefabs(TinyRockPaths);
            List<GameObject> rockCliffs = LoadPrefabs(RockCliffPaths);

            if (standardRocks.Count == 0 || tinyRocks.Count == 0 || rockCliffs.Count == 0)
            {
                Debug.LogError("One or more rock prefab arrays failed to load!");
                return;
            }

            Random.InitState(101);

            // --- Shoreline Canal Rocks ---
            for (float z = -65f; z <= 65f; z += 4f)
            {
                float canalCenter = 25f + Mathf.Sin(z * 0.08f) * 5f;

                // Left Bank Rock
                if (Random.value < 0.6f)
                {
                    float x = canalCenter - Random.Range(5.8f, 7.2f);
                    float terrainY = GetHeightAt(x, z);
                    float y = terrainY - 0.2f; // slightly submerge
                    Vector3 pos = new Vector3(x, y, z);
                    if (terrainY >= -1.3f && !IsInExclusionZone(pos))
                    {
                        GameObject rockPrefab = standardRocks[Random.Range(0, standardRocks.Count)];
                        SpawnPrefab(rockPrefab, pos, Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-15f, 15f)), Vector3.one * Random.Range(0.7f, 1.4f), container.transform);
                    }
                }

                // Right Bank Rock
                if (Random.value < 0.6f)
                {
                    float x = canalCenter + Random.Range(5.8f, 7.2f);
                    float terrainY = GetHeightAt(x, z);
                    float y = terrainY - 0.2f; // slightly submerge
                    Vector3 pos = new Vector3(x, y, z);
                    if (terrainY >= -1.3f && !IsInExclusionZone(pos))
                    {
                        GameObject rockPrefab = standardRocks[Random.Range(0, standardRocks.Count)];
                        SpawnPrefab(rockPrefab, pos, Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-15f, 15f)), Vector3.one * Random.Range(0.7f, 1.4f), container.transform);
                    }
                }
            }

            // --- Tiny Rocks Scattered at Shoreline ---
            for (float z = -65f; z <= 65f; z += 2f)
            {
                float canalCenter = 25f + Mathf.Sin(z * 0.08f) * 5f;
                if (Random.value < 0.5f)
                {
                    float side = Random.value < 0.5f ? -1f : 1f;
                    float x = canalCenter + (Random.Range(5.5f, 6.8f) * side);
                    float terrainY = GetHeightAt(x, z);
                    float y = terrainY - 0.05f;
                    Vector3 pos = new Vector3(x, y, z);
                    if (terrainY >= -1.2f && !IsInExclusionZone(pos))
                    {
                        GameObject tinyRock = tinyRocks[Random.Range(0, tinyRocks.Count)];
                        SpawnPrefab(tinyRock, pos, Quaternion.Euler(Random.Range(-45f, 45f), Random.Range(0f, 360f), Random.Range(-45f, 45f)), Vector3.one * Random.Range(0.5f, 1.2f), container.transform);
                    }
                }
            }

            // --- Cliff backdrops (far edges of terrain) ---
            // Left Edge Backdrop (X = -14.5f to stay on terrain bank)
            for (float z = -60f; z <= 60f; z += 18f)
            {
                float x = -14.5f;
                float y = GetHeightAt(x, z) - 3.8f;
                Vector3 pos = new Vector3(x, y, z);
                GameObject cliff = rockCliffs[Random.Range(0, rockCliffs.Count)];
                SpawnPrefab(cliff, pos, Quaternion.Euler(0f, Random.Range(80f, 100f), 0f), new Vector3(2.5f, Random.Range(2.5f, 4f), 2.5f), container.transform);
            }

            // Right Edge Backdrop (X = 52)
            for (float z = -60f; z <= 60f; z += 18f)
            {
                float x = 52f;
                float y = GetHeightAt(x, z) - 3.8f;
                Vector3 pos = new Vector3(x, y, z);
                GameObject cliff = rockCliffs[Random.Range(0, rockCliffs.Count)];
                SpawnPrefab(cliff, pos, Quaternion.Euler(0f, Random.Range(-100f, -80f), 0f), new Vector3(2.5f, Random.Range(2.5f, 4f), 2.5f), container.transform);
            }

            // Register fully populated container with Undo
            Undo.RegisterCreatedObjectUndo(container, "Create RockFormations");

            Debug.Log("Rocks placed successfully!");
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        private static List<GameObject> LoadPrefabs(string[] paths)
        {
            List<GameObject> list = new List<GameObject>();
            foreach (string p in paths)
            {
                GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (go != null) list.Add(go);
                else Debug.LogWarning($"Could not load prefab at: {p}");
            }
            return list;
        }

//         [MenuItem("Rung Tram Tra Su/Beautify Phase 1/Step 5 - Add Spruce Trees and Skybox")]
        public static void AddSpruceAndSkybox()
        {
            EnsureSceneLoaded();
            Debug.Log("Adding Spruce Trees for variety and applying Skybox...");

            GameObject container = GetOrCreateContainer("SpruceTreesContainer");

            // Load Spruce Prefabs
            List<GameObject> spruceTrees = LoadPrefabs(SprucePrefabPaths);
            if (spruceTrees.Count == 0)
            {
                Debug.LogError("Spruce tree prefabs failed to load!");
                return;
            }

            Random.InitState(202);

            // Place Spruce Trees at far edges as majestic backdrop trees
            // Left Bank Forest Backdrop (X between -50 and -15) - 80 trees
            for (int i = 0; i < 80; i++)
            {
                float x = Random.Range(-50f, -15f);
                float z = Random.Range(-62f, 62f);
                float y = GetHeightAt(x, z);
                Vector3 pos = new Vector3(x, y, z);

                if (!IsInExclusionZone(pos))
                {
                    GameObject spruce = spruceTrees[Random.Range(0, spruceTrees.Count)];
                    // Clipping prevention scale capping near house
                    float scaleMultiplier = (x > -25f) ? Random.Range(2.0f, 3.5f) : Random.Range(3.5f, 6.5f);
                    SpawnPrefab(spruce, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), Vector3.one * scaleMultiplier, container.transform);
                }
            }

            // Right Bank Forest Backdrop (X between 38 and 50) - 40 trees
            for (int i = 0; i < 40; i++)
            {
                float z = Random.Range(-62f, 62f);
                float canalCenter = 25f + Mathf.Sin(z * 0.08f) * 5f;
                float x = Random.Range(canalCenter + 15f, 50f);
                float y = GetHeightAt(x, z);
                Vector3 pos = new Vector3(x, y, z);

                if (!IsInExclusionZone(pos))
                {
                    GameObject spruce = spruceTrees[Random.Range(0, spruceTrees.Count)];
                    SpawnPrefab(spruce, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), Vector3.one * Random.Range(3.5f, 6.5f), container.transform);
                }
            }

            // Apply Skybox
            Material skyboxMat = AssetDatabase.LoadAssetAtPath<Material>(SkyboxMaterialPath);
            if (skyboxMat != null)
            {
                RenderSettings.skybox = skyboxMat;
                Debug.Log("Skybox material applied successfully!");
            }
            else
            {
                Debug.LogWarning($"Could not load skybox material at: {SkyboxMaterialPath}");
            }

            // Adjust Directional Light to a better daytime angle to eliminate long dark shadow faces
            GameObject dirLight = GameObject.Find("Directional Light");
            if (dirLight != null)
            {
                Undo.RecordObject(dirLight.transform, "Adjust Directional Light Rotation");
                dirLight.transform.rotation = Quaternion.Euler(32f, -45f, 0f); // Higher angle (32 degrees)
                
                Light lightComp = dirLight.GetComponent<Light>();
                if (lightComp != null)
                {
                    Undo.RecordObject(lightComp, "Adjust Light Intensity");
                    lightComp.color = new Color(1.0f, 0.96f, 0.86f);
                    lightComp.intensity = 1.35f;
                    lightComp.shadows = LightShadows.Soft;
                    lightComp.shadowStrength = 0.8f;
                }
                Debug.Log("Directional light rotation and intensity adjusted.");
            }

            // Configure URP Ambient fill light to resolve pitch-black shadows in non-lit areas (use Skybox mode)
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.25f;
            DynamicGI.UpdateEnvironment();
            Debug.Log("Ambient fill light configured successfully!");

            // Atmospheric tea-green Fog
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.6f, 0.78f, 0.72f);
            RenderSettings.fogDensity = 0.015f;

            // Register fully populated container with Undo
            Undo.RegisterCreatedObjectUndo(container, "Create SpruceTreesContainer");

            Debug.Log("Spruce trees, Skybox, Lighting, and URP Fog applied successfully!");
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

//         [MenuItem("Rung Tram Tra Su/Beautify Phase 1/Upgrade Kit Materials to URP")]
        public static void UpgradeKitMaterials()
        {
            Debug.Log("Upgrading Stylized Nature Kit Lite materials to URP...");
            string[] matGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Proxy Games/Stylized Nature Kit Lite" });
            
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("URP Lit shader not found!");
                return;
            }

            foreach (string guid in matGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                string nameLower = mat.name.ToLower();
                
                // Skip skybox materials as they work fine
                if (nameLower.Contains("skybox"))
                {
                    continue;
                }

                Undo.RecordObject(mat, "Upgrade Material to URP");

                Texture mainTex = mat.mainTexture;
                Color mainColor = mat.color;

                mat.shader = urpLit;

                if (mainTex != null)
                {
                    mat.SetTexture("_BaseMap", mainTex);
                }
                mat.SetColor("_BaseColor", mainColor);

                if (nameLower.Contains("grass") || 
                    nameLower.Contains("leaves") || 
                    nameLower.Contains("flower") || 
                    nameLower.Contains("branch"))
                {
                    // Cutout foliage material
                    mat.SetFloat("_AlphaClip", 1.0f);
                    mat.SetFloat("_Cutoff", 0.4f);
                    mat.EnableKeyword("_ALPHATEST_ON");
                    
                    mat.SetFloat("_Surface", 0.0f); // Opaque surface type
                    mat.SetOverrideTag("RenderType", "TransparentCutout");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                    
                    // Double-sided rendering (Cull Off = 0)
                    mat.SetFloat("_Cull", 0f);
                    mat.EnableKeyword("_DOUBLE_SIDED_NORMAL");
                }
                else if (nameLower.Contains("water"))
                {
                    // Transparent water
                    mat.SetFloat("_Surface", 1.0f); // Transparent
                    mat.SetFloat("_AlphaClip", 0.0f);
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    
                    mat.SetColor("_BaseColor", new Color(0.12f, 0.45f, 0.4f, 0.7f));
                }
                else
                {
                    // Opaque materials
                    mat.SetFloat("_Surface", 0.0f);
                    mat.SetFloat("_AlphaClip", 0.0f);
                    mat.SetFloat("_Smoothness", 0.1f);
                }

                mat.enableInstancing = true;
                EditorUtility.SetDirty(mat);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("Material upgrade to URP completed!");
        }

//         [MenuItem("Rung Tram Tra Su/Beautify Phase 1/Step 6 - Generate Boardwalk and Stepping Stones")]
        public static void GenerateWalkwayAndSteppingStones()
        {
            EnsureSceneLoaded();
            Debug.Log("Generating Wooden Walkway, Stepping-Stone Path, and Water Trees...");

            // 1. Relocate Boat to deep water
            GameObject boatObj = GameObject.Find("Sampan Boat");
            if (boatObj != null)
            {
                Undo.RecordObject(boatObj.transform, "Relocate Boat to Deep Water");
                boatObj.transform.position = new Vector3(19.5f, -1.0f, 8.0f);
                boatObj.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                Debug.Log("Relocated boat to deep water (19.5, -1.0, 8.0).");
            }

            // 2. Destroy Old Pier
            GameObject oldPier = GameObject.Find("WoodenPier");
            if (oldPier != null)
            {
                Undo.DestroyObjectImmediate(oldPier);
            }

            // 3. Create Walkway Container
            GameObject walkwayContainer = GetOrCreateContainer("WoodenWalkway");

            // Define/Load URP Wood Material
            string woodMatPath = "Assets/BoardwalkWoodMat.mat";
            Material woodMat = AssetDatabase.LoadAssetAtPath<Material>(woodMatPath);
            if (woodMat == null)
            {
                Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
                woodMat = new Material(urpLit);
                woodMat.name = "BoardwalkWoodMat";
                woodMat.SetColor("_BaseColor", new Color(0.28f, 0.20f, 0.14f));
                woodMat.SetFloat("_Smoothness", 0.05f);
                AssetDatabase.CreateAsset(woodMat, woodMatPath);
                AssetDatabase.SaveAssets();
            }

            // 4. Generate Stepping Stones from House to Pier entrance
            Vector3 stoneStart = new Vector3(-2f, 0.96f, 0f);
            Vector3 stoneEnd = new Vector3(13.0f, 1.0f, 8.0f);
            float stoneDist = Vector3.Distance(stoneStart, stoneEnd);
            int numStones = Mathf.FloorToInt(stoneDist / 0.7f);
            
            GameObject stonesContainer = new GameObject("SteppingStonesPath");
            stonesContainer.transform.SetParent(walkwayContainer.transform);

            List<GameObject> rockPrefabs = new List<GameObject>();
            foreach (string path in TinyRockPaths)
            {
                GameObject r = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (r != null) rockPrefabs.Add(r);
            }

            if (rockPrefabs.Count > 0)
            {
                for (int i = 0; i <= numStones; i++)
                {
                    float t = (float)i / numStones;
                    float x = Mathf.Lerp(stoneStart.x, stoneEnd.x, t);
                    float z = Mathf.Lerp(stoneStart.z, stoneEnd.z, t) + Mathf.Sin(t * Mathf.PI) * 1.5f;
                    float y = GetHeightAt(x, z) + 0.02f;

                    Vector3 pos = new Vector3(x, y, z);
                    GameObject stonePrefab = rockPrefabs[Random.Range(0, rockPrefabs.Count)];
                    SpawnPrefab(stonePrefab, pos, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), new Vector3(1.2f, 0.06f, 1.2f) * Random.Range(0.8f, 1.2f), stonesContainer.transform);
                }
            }

            // 5. Generate Wooden Boardwalk/Pier
            Vector3 pStart = new Vector3(13.0f, 1.03f, 8.0f);
            Vector3 pEnd = new Vector3(18.0f, 1.03f, 8.0f);
            float pierDist = Vector3.Distance(pStart, pEnd);
            int numPlanks = Mathf.FloorToInt(pierDist / 0.35f);

            for (int i = 0; i <= numPlanks; i++)
            {
                float t = (float)i / numPlanks;
                float x = Mathf.Lerp(pStart.x, pEnd.x, t);
                float z = Mathf.Lerp(pStart.z, pEnd.z, t);
                float y = 1.03f;

                Vector3 plankPos = new Vector3(x, y, z);
                GameObject plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plank.name = $"Plank_{i}";
                plank.transform.position = plankPos;
                plank.transform.rotation = Quaternion.Euler(0f, Random.Range(-2f, 2f), 0f);
                plank.transform.localScale = new Vector3(0.3f, 0.06f, 1.3f);
                plank.transform.SetParent(walkwayContainer.transform);

                // Disable box collider on individual plank to prevent character controller jitter
                BoxCollider col = plank.GetComponent<BoxCollider>();
                if (col != null) col.enabled = false;

                MeshRenderer renderer = plank.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.sharedMaterial = woodMat;

                // Mark static recursively for planks
                SetStaticRecursively(plank);

                // Stilts/Support posts every 4 planks (1.4m)
                if (i % 4 == 0)
                {
                    float terrainY = GetHeightAt(x, z);
                    float postHeight = y - terrainY;

                    if (postHeight > 0f)
                    {
                        float postY = (y + terrainY) / 2.0f;
                        for (int side = -1; side <= 1; side += 2)
                        {
                            float offsetZ = 0.55f * side;
                            GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                            post.name = $"SupportPost_{i}_{side}";
                            post.transform.position = new Vector3(x, postY, z + offsetZ);
                            post.transform.localScale = new Vector3(0.12f, postHeight, 0.12f);
                            post.transform.SetParent(walkwayContainer.transform);

                            MeshRenderer mr = post.GetComponent<MeshRenderer>();
                            if (mr != null) mr.sharedMaterial = woodMat;

                            // Mark static recursively for posts
                            SetStaticRecursively(post);
                        }
                    }
                }
            }

            // 6. Generate single large invisible BoxCollider for Walkway
            GameObject colliderObj = new GameObject("WalkwayCollider");
            colliderObj.transform.position = new Vector3(15.5f, 1.01f, 8.0f);
            colliderObj.transform.SetParent(walkwayContainer.transform);
            BoxCollider mainCollider = colliderObj.AddComponent<BoxCollider>();
            mainCollider.size = new Vector3(5.2f, 0.1f, 1.3f);
            mainCollider.center = Vector3.zero;

            // 7. Spawn supplementary trees in water near banks under a dedicated undo container
            GameObject waterTreesContainer = GetOrCreateContainer("WaterCajeputTrees");

            List<GameObject> spruceTrees = new List<GameObject>();
            foreach (string path in SprucePrefabPaths)
            {
                GameObject tree = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (tree != null) spruceTrees.Add(tree);
            }

            if (spruceTrees.Count > 0)
            {
                for (int i = 0; i < 25; i++)
                {
                    float z = Random.Range(-60f, 60f);
                    float canalCenter = 25f + Mathf.Sin(z * 0.08f) * 5f;
                    float side = Random.value < 0.5f ? -1f : 1f;
                    float x = canalCenter + (Random.Range(8.0f, 11.5f) * side);
                    float y = GetHeightAt(x, z);
                    
                    Vector3 pos = new Vector3(x, y - 0.35f, z);
                    // Correct height range check matching the canal slope
                    if (y >= -2.2f && y <= -1.0f && !IsInExclusionZone(pos))
                    {
                        GameObject treePrefab = spruceTrees[Random.Range(0, spruceTrees.Count)];
                        Quaternion treeRotation = Quaternion.Euler(0f, 0f, Random.Range(5f, 12f) * side) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                        SpawnPrefab(treePrefab, pos, treeRotation, Vector3.one * Random.Range(3.0f, 5.0f), waterTreesContainer.transform);
                    }
                }
            }

            // Register both walkway and water tree containers with Undo at the end after all children are spawned
            Undo.RegisterCreatedObjectUndo(walkwayContainer, "Create WoodenWalkway");
            Undo.RegisterCreatedObjectUndo(waterTreesContainer, "Create WaterCajeputTrees");

            Debug.Log("Boardwalk, Stepping Stones, and Water Trees generated successfully!");
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

//         [MenuItem("Rung Tram Tra Su/Beautify Phase 1/★ Run ALL Steps")]
        public static void BeautifyAll()
        {
            EnsureSceneLoaded();
            Debug.Log("=== STARTING FULL BEAUTIFICATION OF PHASE 1 ===");
            
            UpgradeKitMaterials();
            UpgradeTerrain();
            UpgradeWater();
            PopulateNature();
            AddRocks();
            AddSpruceAndSkybox();
            GenerateWalkwayAndSteppingStones();
            
            // Force a full project save
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
            Debug.Log("=== BEAUTIFICATION COMPLETED AND SCENE SAVED SUCCESSFULLY ===");
        }
    }
}
