using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

namespace RungTramTraSu.Editor
{
    public class SceneBeautifierAll : EditorWindow
    {
        private const string KitPath = "Assets/Proxy Games/Stylized Nature Kit Lite/";

        // Prefab Paths
        private const string GrassPrefabPath = KitPath + "Prefabs/Foliage/Grass/Grass.prefab";
        private const string FlowerPrefabPath = KitPath + "Prefabs/Foliage/Flower/Flower.prefab";
        private const string BushPrefabPath = KitPath + "Prefabs/Foliage/Bush/Bush.prefab";
        private const string MushroomPrefabPath = KitPath + "Prefabs/Foliage/Mushroom/Mushrooms Patch.prefab";
        
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

        private static readonly string[] RockCliffPaths = new string[]
        {
            KitPath + "Prefabs/Rocks/Rock Cliffs/Rock Cliff 1.prefab",
            KitPath + "Prefabs/Rocks/Rock Cliffs/Rock Cliff 2.prefab",
            KitPath + "Prefabs/Rocks/Rock Cliffs/Rock Cliff 3.prefab"
        };

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

        public static float GetPhase5HeightAt(float x, float z)
        {
            Vector2 towerPos = new Vector2(25f, 15f);
            float distToTower = Vector2.Distance(new Vector2(x, z), towerPos);
            
            float baseHeight = -0.5f;
            
            // Add gentle noise hills further away from the tower
            float n1 = Mathf.PerlinNoise(x * 0.06f + 500f, z * 0.06f + 500f) * 2.5f;
            float n2 = Mathf.PerlinNoise(x * 0.15f + 600f, z * 0.15f + 600f) * 0.5f;
            float noiseHeight = n1 + n2 - 1.2f;
            
            // Blend flat area near tower (12m radius)
            float t = (distToTower - 12f) / 10f;
            t = Mathf.Clamp01(t);
            float smoothT = t * t * (3f - 2f * t);
            
            return Mathf.Lerp(baseHeight, baseHeight + noiseHeight, smoothT);
        }

        private static float GetFoliageHeight(float x, float z, string phaseName)
        {
            if (phaseName == "Phase4" || phaseName == "Phase3" || phaseName == "Phase2")
            {
                return GetHeightAt(x, z);
            }
            else if (phaseName == "Phase5")
            {
                return GetPhase5HeightAt(x, z);
            }
            else
            {
                return GetHeightAt(x, z);
            }
        }

        public static void BeautifyPhase2()
        {
            Scene activeScene = SceneManager.GetActiveScene();

            // 1. Upgrade materials
            UpgradeTerrainAndWater();

            // 2. Setup nature sounds
            GameObject player = GameObject.Find("Player");
            SetupNatureSounds(player);

            // 3. Populate foliage, rocks, cliffs, spruce trees
            PopulateFoliageAndRocks("Phase2");

            // 4. Upgrade primitive ducks/storks/fish
            UpgradeFauna();

            // 5. Setup skybox & fog
            ApplySkybox("Assets/EmaceArt/Slavic World Free/Skybox/Epic_BigCloudsSoft_V2/EA03_LowPolyBigClouds.mat");
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.25f;
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.6f, 0.78f, 0.72f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.015f;

            // 6. Save scene
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
            Debug.Log("==> Done beautifying Phase 2 scene!");
        }

        public static void BeautifyPhase3()
        {
            Scene activeScene = SceneManager.GetActiveScene();

            // 1. Upgrade materials
            UpgradeTerrainAndWater();

            // 2. Setup nature sounds
            GameObject player = GameObject.Find("Player");
            SetupNatureSounds(player);

            // 3. Populate foliage, rocks, cliffs, spruce trees
            PopulateFoliageAndRocks("Phase3");

            // 4. Beautify the Bamboo Bridge with high-quality wood
            GameObject bridgeContainer = GameObject.Find("BambooBridge");
            if (bridgeContainer != null)
            {
                Material woodMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/BoardwalkWoodMat.mat");
                if (woodMat != null)
                {
                    MeshRenderer[] renderers = bridgeContainer.GetComponentsInChildren<MeshRenderer>(true);
                    foreach (var r in renderers)
                    {
                        r.sharedMaterial = woodMat;
                    }
                }
            }

            // 5. Setup skybox & deeper fog
            ApplySkybox("Assets/EmaceArt/Slavic World Free/Skybox/Epic_BigCloudsSoft_V2/EA03_LowPolyBigClouds.mat");
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.25f;
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.55f, 0.74f, 0.68f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.02f; // Deeper swamp fog

            // 6. Save scene
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
            Debug.Log("==> Done beautifying Phase 3 scene!");
        }

        public static void BeautifyPhase4()
        {
            Scene activeScene = SceneManager.GetActiveScene();

            // 1. Upgrade materials
            UpgradeTerrainAndWater();

            // 2. Setup nature sounds
            GameObject player = GameObject.Find("Player");
            SetupNatureSounds(player);

            // 3. Populate foliage, rocks, cliffs, spruce trees
            PopulateFoliageAndRocks("Phase4");

            // 4. Upgrade primitive ducks/storks to Snowy White Duck model
            UpgradeFauna();

            // 5. Setup skybox & fog
            ApplySkybox("Assets/EmaceArt/Slavic World Free/Skybox/Epic_BigCloudsSoft_V2/EA03_LowPolyBigClouds.mat");
            SetupPhase4VolumeAndAtmosphere();

            // 6. Save scene
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
            Debug.Log("==> Done beautifying Phase 4 scene!");
        }

        public static void BeautifyPhase5()
        {
            Scene activeScene = SceneManager.GetActiveScene();

            // 1. Upgrade materials (only terrain exists here, no canal)
            GameObject terrainObj = GameObject.Find("OrganicTerrain_Bank");
            if (terrainObj != null)
            {
                MeshRenderer renderer = terrainObj.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    Material terrainMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/StylizedTerrainMaterial.mat");
                    if (terrainMat != null)
                    {
                        renderer.sharedMaterial = terrainMat;
                    }
                }
            }

            // 2. Setup nature sounds
            GameObject player = GameObject.Find("Player");
            SetupNatureSounds(player);

            // 3. Populate foliage, rocks, cliffs, spruce trees
            PopulateFoliageAndRocks("Phase5");

            // 4. Beautify the Observation Tower with high-quality wood
            Material customWoodMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/BoardwalkWoodMat.mat");
            if (customWoodMat != null)
            {
                GameObject tower = GameObject.Find("ObservationTower");
                if (tower != null)
                {
                    MeshRenderer[] renderers = tower.GetComponentsInChildren<MeshRenderer>(true);
                    foreach (var r in renderers)
                    {
                        if (r != null) r.sharedMaterial = customWoodMat;
                    }
                }
            }

            // 5. Setup warm sunset lighting, fog, skybox, volume
            ApplySkybox("Assets/EmaceArt/Slavic World Free/Skybox/Epic_BigCloudsSoft_V2/EA03_LowPolyBigClouds.mat");
            SetupSunsetVolumeAndAtmosphere();

            // 6. Save scene
            EditorSceneManager.MarkSceneDirty(activeScene);
            EditorSceneManager.SaveScene(activeScene);
            AssetDatabase.SaveAssets();
            Debug.Log("==> Done beautifying Phase 5 scene!");
        }

        private static void UpgradeTerrainAndWater()
        {
            // Upgrade terrain
            GameObject terrainObj = GameObject.Find("OrganicTerrain_Bank");
            if (terrainObj != null)
            {
                MeshRenderer renderer = terrainObj.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    Material terrainMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/StylizedTerrainMaterial.mat");
                    if (terrainMat != null)
                    {
                        renderer.sharedMaterial = terrainMat;
                    }
                }
            }

            // Upgrade water
            GameObject riverObj = GameObject.Find("RiverWater_Canal");
            if (riverObj != null)
            {
                MeshRenderer renderer = riverObj.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    Material waterMat = AssetDatabase.LoadAssetAtPath<Material>(KitPath + "Materials/Water.mat");
                    if (waterMat != null)
                    {
                        renderer.sharedMaterial = waterMat;
                    }
                }
            }
        }

        private static void ApplySkybox(string skyboxPath)
        {
            Material skyboxMat = AssetDatabase.LoadAssetAtPath<Material>(skyboxPath);
            if (skyboxMat != null)
            {
                RenderSettings.skybox = skyboxMat;
            }
        }

        private static void SetupSunsetVolumeAndAtmosphere()
        {
            // 1. Setup warm fog
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.85f, 0.45f, 0.3f); // Sunset orange-pink
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.018f;

            // 2. Setup warm directional light
            GameObject dirLight = GameObject.Find("Directional Light");
            if (dirLight != null)
            {
                dirLight.transform.rotation = Quaternion.Euler(12f, -50f, 0f); // Low angle sunset sun
                Light light = dirLight.GetComponent<Light>();
                if (light != null)
                {
                    light.color = new Color(1.0f, 0.52f, 0.22f); // Warm sun color
                    light.intensity = 1.6f;
                    light.shadows = LightShadows.Soft;
                }
            }

            // 3. Setup warm ambient light (use Skybox mode)
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.25f;
            DynamicGI.UpdateEnvironment();

            // 4. Create or load Sunset Volume Profile
            GameObject volumeObj = GameObject.Find("Global PostProcess Volume");
            if (volumeObj == null)
            {
                volumeObj = new GameObject("Global PostProcess Volume");
            }
            Volume volume = volumeObj.GetComponent<Volume>();
            if (volume == null)
            {
                volume = volumeObj.AddComponent<Volume>();
            }
            volume.isGlobal = true;

            string profilePath = "Assets/Scenes/Phase5_VolumeProfile.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();

                var tonemapping = profile.Add<Tonemapping>();
                tonemapping.active = true;
                tonemapping.mode.Override(TonemappingMode.ACES);

                var bloom = profile.Add<Bloom>();
                bloom.active = true;
                bloom.threshold.Override(0.65f);
                bloom.intensity.Override(3.5f); // High sunset bloom intensity
                bloom.scatter.Override(0.75f);
                bloom.tint.Override(new Color(1f, 0.82f, 0.6f)); // Warm sunset bloom

                var colorAdjust = profile.Add<ColorAdjustments>();
                colorAdjust.active = true;
                colorAdjust.contrast.Override(30f);
                colorAdjust.saturation.Override(40f); // Vivid sunset saturation
                colorAdjust.postExposure.Override(0.15f);

                var vignette = profile.Add<Vignette>();
                vignette.active = true;
                vignette.intensity.Override(0.3f);
                vignette.smoothness.Override(0.45f);
                vignette.rounded.Override(true);

                AssetDatabase.CreateAsset(profile, profilePath);
                AssetDatabase.SaveAssets();
            }
            volume.sharedProfile = profile;
        }

        private static void SetupPhase4VolumeAndAtmosphere()
        {
            // 1. Setup beautiful misty swamp fog
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.55f, 0.74f, 0.68f); // Soft green-blue swamp fog
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.02f; // Deeper swamp fog

            // 2. Setup soft warm directional light
            GameObject dirLight = GameObject.Find("Directional Light");
            if (dirLight != null)
            {
                dirLight.transform.rotation = Quaternion.Euler(30f, -55f, 0f);
                Light light = dirLight.GetComponent<Light>();
                if (light != null)
                {
                    light.color = new Color(0.95f, 0.98f, 0.92f); // Warm soft morning/afternoon sun
                    light.intensity = 1.25f;
                    light.shadows = LightShadows.Soft;
                }
            }

            // 3. Setup ambient light
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.25f;
            DynamicGI.UpdateEnvironment();

            // 4. Create or load Phase 4 Volume Profile
            GameObject volumeObj = GameObject.Find("Global PostProcess Volume");
            if (volumeObj == null)
            {
                volumeObj = new GameObject("Global PostProcess Volume");
            }
            Volume volume = volumeObj.GetComponent<Volume>();
            if (volume == null)
            {
                volume = volumeObj.AddComponent<Volume>();
            }
            volume.isGlobal = true;

            string profilePath = "Assets/Scenes/Phase4_VolumeProfile.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();

                var tonemapping = profile.Add<Tonemapping>();
                tonemapping.active = true;
                tonemapping.mode.Override(TonemappingMode.ACES);

                var bloom = profile.Add<Bloom>();
                bloom.active = true;
                bloom.threshold.Override(0.78f);
                bloom.intensity.Override(2.2f);
                bloom.scatter.Override(0.72f);
                bloom.tint.Override(new Color(1f, 0.94f, 0.80f));

                var colorAdjust = profile.Add<ColorAdjustments>();
                colorAdjust.active = true;
                colorAdjust.contrast.Override(25f);
                colorAdjust.saturation.Override(32f);
                colorAdjust.postExposure.Override(0.12f);

                var vignette = profile.Add<Vignette>();
                vignette.active = true;
                vignette.intensity.Override(0.28f);
                vignette.smoothness.Override(0.4f);
                vignette.rounded.Override(true);

                AssetDatabase.CreateAsset(profile, profilePath);
                AssetDatabase.SaveAssets();
            }
            volume.sharedProfile = profile;
        }

        private static void PopulateFoliageAndRocks(string phaseName)
        {
            GameObject foliageContainer = new GameObject("BeautifiedFoliage");
            GameObject rocksContainer = new GameObject("BeautifiedRocks");

            // Load prefabs
            GameObject grassPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GrassPrefabPath);
            GameObject flowerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FlowerPrefabPath);
            GameObject bushPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BushPrefabPath);
            GameObject mushroomPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MushroomPrefabPath);
            
            List<GameObject> spruceTrees = new List<GameObject>();
            foreach (var p in SprucePrefabPaths)
            {
                GameObject t = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (t != null) spruceTrees.Add(t);
            }

            List<GameObject> rocks = new List<GameObject>();
            foreach (var p in StandardRockPaths)
            {
                GameObject r = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (r != null) rocks.Add(r);
            }

            List<GameObject> cliffs = new List<GameObject>();
            foreach (var p in RockCliffPaths)
            {
                GameObject c = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (c != null) cliffs.Add(c);
            }

            // Populate Nature Elements
            int grassCount = 1800;
            int treeCount = 180;
            int rockCount = 45;

            // Generate elements
            for (int i = 0; i < grassCount; i++)
            {
                float x = Random.Range(-55f, 55f);
                float z = Random.Range(-65f, 65f);
                float y = GetFoliageHeight(x, z, phaseName);

                if (IsPositionExcluded(x, z, phaseName)) continue;

                // Only spawn grass/foliage on banks (dry land)
                if (y >= -0.2f)
                {
                    Vector3 pos = new Vector3(x, y - 0.05f, z);
                    Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    
                    float rVal = Random.value;
                    if (rVal < 0.65f)
                    {
                        SpawnPrefab(grassPrefab, pos, rot, Vector3.one * Random.Range(0.8f, 1.5f), foliageContainer.transform);
                    }
                    else if (rVal < 0.85f)
                    {
                        SpawnPrefab(flowerPrefab, pos, rot, Vector3.one * Random.Range(0.7f, 1.2f), foliageContainer.transform);
                    }
                    else if (rVal < 0.95f)
                    {
                        SpawnPrefab(bushPrefab, pos, rot, Vector3.one * Random.Range(0.6f, 1.1f), foliageContainer.transform);
                    }
                    else
                    {
                        SpawnPrefab(mushroomPrefab, pos, rot, Vector3.one * Random.Range(0.6f, 1.0f), foliageContainer.transform);
                    }
                }
            }

            // Generate trees
            for (int i = 0; i < treeCount; i++)
            {
                float x = Random.Range(-55f, 55f);
                float z = Random.Range(-65f, 65f);
                float y = GetFoliageHeight(x, z, phaseName);

                if (IsPositionExcluded(x, z, phaseName)) continue;

                if (y >= -0.2f && spruceTrees.Count > 0)
                {
                    Vector3 pos = new Vector3(x, y - 0.2f, z);
                    Quaternion rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                    GameObject treePrefab = spruceTrees[Random.Range(0, spruceTrees.Count)];
                    SpawnPrefab(treePrefab, pos, rot, Vector3.one * Random.Range(1.8f, 3.5f), foliageContainer.transform);
                }
            }

            // Generate rocks & cliffs
            for (int i = 0; i < rockCount; i++)
            {
                float x = Random.Range(-55f, 55f);
                float z = Random.Range(-65f, 65f);
                float y = GetFoliageHeight(x, z, phaseName);

                if (IsPositionExcluded(x, z, phaseName)) continue;

                if (y >= -0.2f)
                {
                    Vector3 pos = new Vector3(x, y - 0.3f, z);
                    Quaternion rot = Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 360f), Random.Range(-10f, 10f));
                    
                    if (Random.value < 0.8f && rocks.Count > 0)
                    {
                        GameObject rockPrefab = rocks[Random.Range(0, rocks.Count)];
                        SpawnPrefab(rockPrefab, pos, rot, Vector3.one * Random.Range(1.2f, 2.5f), rocksContainer.transform);
                    }
                    else if (cliffs.Count > 0)
                    {
                        GameObject cliffPrefab = cliffs[Random.Range(0, cliffs.Count)];
                        // Lower the cliff position to bury its bottom edge and prevent floating
                        Vector3 cliffPos = new Vector3(x, y - 2.5f, z);
                        SpawnPrefab(cliffPrefab, cliffPos, rot, Vector3.one * Random.Range(2.0f, 4.0f), rocksContainer.transform);
                    }
                }
            }
            
            // Mark static for performance optimization
            SetStaticRecursively(foliageContainer);
            SetStaticRecursively(rocksContainer);
        }

        private static bool IsPositionExcluded(float x, float z, string phaseName)
        {
            if (phaseName == "Phase2")
            {
                // Grandpa NPC & Boat start zone
                if (Vector2.Distance(new Vector2(x, z), new Vector2(25f, -55f)) < 8f) return true;
            }
            else if (phaseName == "Phase3")
            {
                // Bamboo bridge path is bridgeX = 5f + Mathf.Sin(z * 0.12f) * 6f
                // Boat path is boatX = bridgeX - 3.5f
                // Clear a wide corridor (bridgeX - 8.5m to bridgeX + 5.5m) to prevent boat clipping or hitting rocks/trees
                float bridgeX = 5f + Mathf.Sin(z * 0.12f) * 6f;
                if (x > (bridgeX - 8.5f) && x < (bridgeX + 5.5f)) return true;
                
                // Grandpa NPC & Boat start zone
                float startX = 5f + Mathf.Sin(-45f * 0.12f) * 6f;
                if (Vector2.Distance(new Vector2(x, z), new Vector2(startX - 3.5f, -45f)) < 10f) return true;
            }
            else if (phaseName == "Phase4")
            {
                // Grandpa NPC zone
                if (Vector2.Distance(new Vector2(x, z), new Vector2(22.0f, -46f)) < 8f) return true;

                // Clear the canal pathway so it is free of rocks, cliffs, and decorative trees/foliage
                float canalCenter = 25f + Mathf.Sin(z * 0.08f) * 5f;
                if (Mathf.Abs(x - canalCenter) < 5.0f) return true;
            }
            else if (phaseName == "Phase5")
            {
                // Observation tower zone
                if (Vector2.Distance(new Vector2(x, z), new Vector2(25f, 15f)) < 14f) return true;
            }

            return false;
        }

        /// <summary>
        /// Procedurally generates a low-poly tapered-cylinder fish body mesh and saves it
        /// to Assets/Models/Generated/ProceduralFishMesh.asset so it can be reused at runtime.
        /// This is inlined here to avoid a cross-file dependency on ProceduralFishGenerator.
        /// </summary>
        private static void EnsureProceduralFishMesh()
        {
            string path = "Assets/Models/Generated/ProceduralFishMesh.asset";

            // Return early if the asset is already up-to-date in the database
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null) return;

            Mesh mesh = new Mesh();
            mesh.name = "ProceduralFishBody";

            int segments = 10;
            int radialSegments = 8;
            float length = 0.7f;
            float maxRadius = 0.12f;
            float widthScale = 0.5f;
            float heightScale = 1.3f;

            int numVertices = (segments + 1) * (radialSegments + 1);
            Vector3[] vertices = new Vector3[numVertices];
            Vector2[] uvs = new Vector2[numVertices];

            int v = 0;
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float r = Mathf.Sin(t * Mathf.PI) * maxRadius;
                if (i == 0) r = maxRadius * 0.25f;
                else if (i == segments) r = maxRadius * 0.1f;
                float z = (t - 0.5f) * length;

                for (int j = 0; j <= radialSegments; j++)
                {
                    float radPct = (float)j / radialSegments;
                    float angle = radPct * Mathf.PI * 2f;
                    float x = Mathf.Cos(angle) * r * widthScale;
                    float y = Mathf.Sin(angle) * r * heightScale;
                    vertices[v] = new Vector3(x, y, -z);
                    uvs[v] = new Vector2(radPct, t);
                    v++;
                }
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;

            var tris = new System.Collections.Generic.List<int>();
            for (int i = 0; i < segments; i++)
            {
                int r1 = i * (radialSegments + 1);
                int r2 = (i + 1) * (radialSegments + 1);
                for (int j = 0; j < radialSegments; j++)
                {
                    int nextJ = j + 1;
                    tris.Add(r1 + j); tris.Add(r2 + j); tris.Add(r1 + nextJ);
                    tris.Add(r1 + nextJ); tris.Add(r2 + j); tris.Add(r2 + nextJ);
                }
            }

            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            if (!System.IO.Directory.Exists("Assets/Models/Generated"))
                System.IO.Directory.CreateDirectory("Assets/Models/Generated");

            // Delete stale asset first so CreateAsset doesn't throw
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null)
                AssetDatabase.DeleteAsset(path);

            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("==> ProceduralFishMesh generated and saved to: " + path);
        }

        private static void UpgradeFauna()
        {
            GameObject duckPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Vịt/source/Snowy White Duck.glb");
            if (duckPrefab == null)
            {
                string[] guids = AssetDatabase.FindAssets("Snowy White Duck t:GameObject");
                if (guids.Length > 0)
                {
                    duckPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }

            GameObject storkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/living birds/resources/lb_crowHQ.prefab");
            if (storkPrefab == null)
            {
                string[] guids = AssetDatabase.FindAssets("lb_crowHQ t:GameObject");
                if (guids.Length > 0)
                {
                    storkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }
            if (storkPrefab == null)
            {
                storkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/living birds/resources/lb_sparrowHQ.prefab");
                if (storkPrefab == null)
                {
                    string[] guids = AssetDatabase.FindAssets("lb_sparrowHQ t:GameObject");
                    if (guids.Length > 0)
                    {
                        storkPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
                    }
                }
            }

            // Find all AnimalAI components in the active scene
            AnimalAI[] animals = Object.FindObjectsByType<AnimalAI>(FindObjectsInactive.Exclude);
            Debug.Log($"[UpgradeFauna] Found {animals.Length} AnimalAI objects in scene.");

            foreach (var ai in animals)
            {
                // Remove any previous visual children to allow clean regeneration
                for (int i = ai.transform.childCount - 1; i >= 0; i--)
                {
                    var child = ai.transform.GetChild(i);
                    if (child.name.StartsWith("Visual") || child.name == "VisualModel" || child.name == "FishBody" || child.name == "LWingPivot" || child.name == "RWingPivot")
                    {
                        Object.DestroyImmediate(child.gameObject);
                    }
                }

                // Always reset the root scale so the primitive's distorted scale
                // (e.g. 0.5 x 0.3 x 0.8) doesn't deform the 3D replacement model.
                ai.transform.localScale = Vector3.one;

                if (ai.Type == AnimalAI.AnimalType.Duck && duckPrefab != null)
                {
                    // Destroy the primitive renderer and filter
                    MeshRenderer mr = ai.GetComponent<MeshRenderer>();
                    if (mr != null) Object.DestroyImmediate(mr);
                    MeshFilter mf = ai.GetComponent<MeshFilter>();
                    if (mf != null) Object.DestroyImmediate(mf);

                    // Instantiate the model as child
                    GameObject model = PrefabUtility.InstantiatePrefab(duckPrefab) as GameObject;
                    if (model != null)
                    {
                        model.name = "VisualModel";
                        model.transform.SetParent(ai.transform, false);
                        model.transform.localScale = Vector3.one * 0.28f;
                        model.transform.localPosition = new Vector3(0f, -0.1f, 0f);
                    }
                }
                else if (ai.Type == AnimalAI.AnimalType.Stork && storkPrefab != null)
                {
                    // Destroy the primitive renderer and filter
                    MeshRenderer mr = ai.GetComponent<MeshRenderer>();
                    if (mr != null) Object.DestroyImmediate(mr);
                    MeshFilter mf = ai.GetComponent<MeshFilter>();
                    if (mf != null) Object.DestroyImmediate(mf);

                    // Instantiate the model as child
                    GameObject model = PrefabUtility.InstantiatePrefab(storkPrefab) as GameObject;
                    if (model != null)
                    {
                        model.name = "VisualModel";
                        model.transform.SetParent(ai.transform, false);
                        model.transform.localScale = Vector3.one * 2.2f; // Scale up to 2.2x to be clearly visible from below!
                        model.transform.localPosition = new Vector3(0f, -0.15f, 0f);
                        model.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

                        // Disable and destroy all scripts on the instantiated model (like lb_Bird, lb_CrowProximity, etc.)
                        var behaviours = model.GetComponentsInChildren<MonoBehaviour>(true);
                        foreach (var b in behaviours)
                        {
                            if (b != null) Object.DestroyImmediate(b);
                        }

                        // Force animator to loop perched state
                        var anim = model.GetComponent<Animator>();
                        if (anim != null)
                        {
                            anim.SetBool("flying", false);
                            anim.Play("perch");
                        }

                        // Apply pure white material to make it look like a white stork
                        Material whiteMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                        whiteMat.color = Color.white;
                        if (whiteMat.HasProperty("_Smoothness")) whiteMat.SetFloat("_Smoothness", 0.1f);
                        
                        var renderers = model.GetComponentsInChildren<Renderer>(true);
                        foreach (var r in renderers)
                        {
                            r.sharedMaterial = whiteMat;
                        }
                    }
                }
                else if (ai.Type == AnimalAI.AnimalType.Snake)
                {
                    // Destroy the primitive renderer and filter
                    MeshRenderer mr = ai.GetComponent<MeshRenderer>();
                    if (mr != null) Object.DestroyImmediate(mr);
                    MeshFilter mf = ai.GetComponent<MeshFilter>();
                    if (mf != null) Object.DestroyImmediate(mf);

                    // Create a procedural snake container
                    GameObject visualSnake = new GameObject("VisualSnake");
                    visualSnake.transform.SetParent(ai.transform, false);

                    Material snakeMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    snakeMat.color = new Color(0.12f, 0.42f, 0.16f); // Glossy green snake skin
                    if (snakeMat.HasProperty("_Smoothness")) snakeMat.SetFloat("_Smoothness", 0.65f);

                    int segmentCount = 6;
                    for (int i = 0; i < segmentCount; i++)
                    {
                        GameObject seg = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        seg.name = "SnakeSegment_" + i;
                        seg.transform.SetParent(visualSnake.transform, false);
                        float size = Mathf.Lerp(0.28f, 0.12f, (float)i / (segmentCount - 1));
                        seg.transform.localScale = new Vector3(size, size, size);
                        seg.transform.localPosition = new Vector3(-i * 0.22f, 0f, 0f);
                        seg.GetComponent<Renderer>().sharedMaterial = snakeMat;
                        Object.DestroyImmediate(seg.GetComponent<Collider>());
                    }
                }
                else if (ai.Type == AnimalAI.AnimalType.Fish)
                {
                    // Destroy the primitive renderer and filter
                    MeshRenderer mr = ai.GetComponent<MeshRenderer>();
                    if (mr != null) Object.DestroyImmediate(mr);
                    MeshFilter mf = ai.GetComponent<MeshFilter>();
                    if (mf != null) Object.DestroyImmediate(mf);

                    // Ensure the procedural 3D fish mesh is generated (inline so no cross-file dep)
                    EnsureProceduralFishMesh();

                    // Create 3D fish container
                    GameObject visualFish = new GameObject("VisualFish");
                    visualFish.transform.SetParent(ai.transform, false);

                    // Create body GameObject
                    GameObject body = new GameObject("FishBody");
                    body.transform.SetParent(visualFish.transform, false);
                    body.transform.localScale = Vector3.one;
                    body.transform.localRotation = Quaternion.identity; // face forward (+Z)

                    // Mesh filter and renderer
                    MeshFilter filter = body.AddComponent<MeshFilter>();
                    Mesh proceduralMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/Generated/ProceduralFishMesh.asset");
                    if (proceduralMesh != null)
                    {
                        filter.sharedMesh = proceduralMesh;
                    }
                    else
                    {
                        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        filter.sharedMesh = sphere.GetComponent<MeshFilter>().sharedMesh;
                        Object.DestroyImmediate(sphere);
                    }

                    MeshRenderer renderer = body.AddComponent<MeshRenderer>();

                    // Load generated fish texture — use Alpha Cutout so transparent edges are clipped
                    Texture2D fishTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/fish_sprite.png");
                    Material fishMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    if (fishTex != null)
                    {
                        fishMat.mainTexture = fishTex;
                        // Enable Alpha Clipping so the transparent background of the sprite is cut out
                        fishMat.SetFloat("_AlphaClip", 1f);
                        fishMat.SetFloat("_Cutoff", 0.25f);
                        fishMat.EnableKeyword("_ALPHATEST_ON");
                        fishMat.SetOverrideTag("RenderType", "TransparentCutout");
                        fishMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                        // Double-sided so the body is visible from all angles
                        fishMat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
                        if (fishMat.HasProperty("_Smoothness")) fishMat.SetFloat("_Smoothness", 0.55f);
                    }
                    else
                    {
                        fishMat.color = new Color(0.25f, 0.55f, 0.45f); // Fallback teal-green
                        fishMat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
                    }
                    renderer.sharedMaterial = fishMat;

                    // Spawn tail fin pivot (for wagging animation to rotate around 0 local Y)
                    GameObject tailPivot = new GameObject("FishTail");
                    tailPivot.transform.SetParent(body.transform, false);
                    tailPivot.transform.localPosition = new Vector3(0f, 0f, -0.38f); // at the tail end
                    tailPivot.transform.localRotation = Quaternion.identity;

                    // Spawn tail Quad visual as child of the pivot
                    GameObject tailVisual = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    tailVisual.name = "TailVisual";
                    tailVisual.transform.SetParent(tailPivot.transform, false);
                    tailVisual.transform.localPosition = Vector3.zero;
                    tailVisual.transform.localScale = new Vector3(0.38f, 0.38f, 1f);
                    tailVisual.transform.localRotation = Quaternion.Euler(0f, 90f, 0f); // Face sideways

                    // Fin material — double-sided so it shows from both sides
                    Material finMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    finMat.color = new Color(0.18f, 0.55f, 0.45f); // Teal-green matching fish body
                    finMat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
                    tailVisual.GetComponent<Renderer>().sharedMaterial = finMat;
                    Object.DestroyImmediate(tailVisual.GetComponent<Collider>());
                }
                else if (ai.Type == AnimalAI.AnimalType.Butterfly)
                {
                    // Destroy the primitive renderer and filter
                    MeshRenderer mr = ai.GetComponent<MeshRenderer>();
                    if (mr != null) Object.DestroyImmediate(mr);
                    MeshFilter mf = ai.GetComponent<MeshFilter>();
                    if (mf != null) Object.DestroyImmediate(mf);

                    // Create procedural butterfly
                    GameObject visualButterfly = new GameObject("VisualButterfly");
                    visualButterfly.transform.SetParent(ai.transform, false);

                    Material bodyMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    bodyMat.color = new Color(0.2f, 0.1f, 0.25f);

                    Material wingMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    wingMat.color = new Color(0.95f, 0.35f, 0.72f); // Vivid pink wings
                    // Double-sided so wings show from both faces during flap
                    wingMat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
                    if (wingMat.HasProperty("_Smoothness")) wingMat.SetFloat("_Smoothness", 0.5f);

                    // Body
                    GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    body.name = "ButterflyBody";
                    body.transform.SetParent(visualButterfly.transform, false);
                    body.transform.localScale = new Vector3(0.04f, 0.15f, 0.04f);
                    body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    body.GetComponent<Renderer>().sharedMaterial = bodyMat;
                    Object.DestroyImmediate(body.GetComponent<Collider>());

                    // Left wing
                    GameObject lWingPivot = new GameObject("LWingPivot");
                    lWingPivot.transform.SetParent(visualButterfly.transform, false);
                    lWingPivot.transform.localPosition = new Vector3(-0.02f, 0f, 0f);

                    GameObject lWing = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    lWing.name = "LeftWing";
                    lWing.transform.SetParent(lWingPivot.transform, false);
                    lWing.transform.localPosition = new Vector3(-0.15f, 0f, 0f);
                    lWing.transform.localScale = new Vector3(0.3f, 0.25f, 1f);
                    lWing.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    lWing.GetComponent<Renderer>().sharedMaterial = wingMat;
                    Object.DestroyImmediate(lWing.GetComponent<Collider>());

                    // Right wing
                    GameObject rWingPivot = new GameObject("RWingPivot");
                    rWingPivot.transform.SetParent(visualButterfly.transform, false);
                    rWingPivot.transform.localPosition = new Vector3(0.02f, 0f, 0f);

                    GameObject rWing = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    rWing.name = "RightWing";
                    rWing.transform.SetParent(rWingPivot.transform, false);
                    rWing.transform.localPosition = new Vector3(0.15f, 0f, 0f);
                    rWing.transform.localScale = new Vector3(0.3f, 0.25f, 1f);
                    rWing.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    rWing.GetComponent<Renderer>().sharedMaterial = wingMat;
                    Object.DestroyImmediate(rWing.GetComponent<Collider>());
                }
            }
        }

        private static void SetupNatureSounds(GameObject player)
        {
            if (player == null) return;

            // 1. Setup Player Footsteps
            PlayerFootsteps footsteps = player.GetComponent<PlayerFootsteps>();
            if (footsteps == null)
            {
                footsteps = player.AddComponent<PlayerFootsteps>();
            }

            // Find all grass footstep clips
            List<AudioClip> footstepClips = new List<AudioClip>();
            for (int i = 1; i <= 9; i++)
            {
                string path = $"Assets/Nature Sounds Pack/Footstep/FootstepGrass0{i}.wav";
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip != null)
                {
                    footstepClips.Add(clip);
                }
            }
            footsteps.grassFootsteps = footstepClips.ToArray();
            EditorUtility.SetDirty(footsteps);

            // 2. Setup Ambient Audio Root
            GameObject ambientRoot = GameObject.Find("AmbientAudio");
            if (ambientRoot == null)
            {
                ambientRoot = new GameObject("AmbientAudio");
            }

            // A. Setup Ambient Wind (2D Loop - playing Nature Birds + Water)
            Transform windTransform = ambientRoot.transform.Find("Ambient_Wind");
            GameObject windObj = windTransform != null ? windTransform.gameObject : null;
            if (windObj == null)
            {
                windObj = new GameObject("Ambient_Wind");
                windObj.transform.SetParent(ambientRoot.transform);
            }
            AudioSource windSource = windObj.GetComponent<AudioSource>();
            if (windSource == null)
            {
                windSource = windObj.AddComponent<AudioSource>();
            }
            AudioClip windClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Nature Sounds Pack/Ambient/AmbientNatureBirdsWater01.wav");
            windSource.clip = windClip;
            windSource.loop = true;
            windSource.playOnAwake = true;
            windSource.spatialBlend = 0.0f; // 2D
            windSource.volume = 0.45f;
            EditorUtility.SetDirty(windSource);

            // B. Setup Ambient River (3D Loop)
            Transform riverTransform = ambientRoot.transform.Find("Ambient_River");
            GameObject riverObj = riverTransform != null ? riverTransform.gameObject : null;
            if (riverObj == null)
            {
                riverObj = new GameObject("Ambient_River");
                riverObj.transform.SetParent(ambientRoot.transform);
            }
            riverObj.transform.position = new Vector3(25.0f, -1.0f, 0.0f);
            
            AudioSource riverSource = riverObj.GetComponent<AudioSource>();
            if (riverSource == null)
            {
                riverSource = riverObj.AddComponent<AudioSource>();
            }
            AudioClip riverClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Nature Sounds Pack/Stream/StreamAndBirds02.wav");
            riverSource.clip = riverClip;
            riverSource.loop = true;
            riverSource.playOnAwake = true;
            riverSource.spatialBlend = 0.85f; // 3D
            riverSource.minDistance = 5.0f;
            riverSource.maxDistance = 45.0f;
            riverSource.volume = 0.12f;
            EditorUtility.SetDirty(riverSource);

            // 3. Setup Ambient Controller
            AmbientController ambientCtrl = ambientRoot.GetComponent<AmbientController>();
            if (ambientCtrl == null)
            {
                ambientCtrl = ambientRoot.AddComponent<AmbientController>();
            }
            ambientCtrl.windSource = windSource;
            ambientCtrl.foliageSource = null;
            ambientCtrl.normalWindVolume = 0.45f;
            ambientCtrl.normalFoliageVolume = 0.0f;
            EditorUtility.SetDirty(ambientCtrl);
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
    }
}
