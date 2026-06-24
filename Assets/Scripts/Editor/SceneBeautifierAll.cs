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

            // 4. Setup skybox & fog
            ApplySkybox("Assets/EmaceArt/Slavic World Free/Skybox/Epic_BigCloudsSoft_V2/EA03_LowPolyBigClouds.mat");
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.25f;
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.6f, 0.78f, 0.72f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.015f;

            // 5. Save scene
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
                float y = GetHeightAt(x, z);

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
                float y = GetHeightAt(x, z);

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
                float y = GetHeightAt(x, z);

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
            }
            else if (phaseName == "Phase5")
            {
                // Observation tower zone
                if (Vector2.Distance(new Vector2(x, z), new Vector2(25f, 15f)) < 14f) return true;
            }

            return false;
        }

        private static void UpgradeFauna()
        {
            GameObject duckPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Vịt/source/Snowy White Duck.glb");
            if (duckPrefab == null)
            {
                Debug.LogWarning("Snowy White Duck.glb not found!");
                return;
            }

            // Find all AnimalAI components in the active scene
            AnimalAI[] animals = Object.FindObjectsByType<AnimalAI>(FindObjectsInactive.Exclude);
            foreach (var ai in animals)
            {
                if (ai.Type == AnimalAI.AnimalType.Duck || ai.Type == AnimalAI.AnimalType.Stork)
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
                        
                        if (ai.Type == AnimalAI.AnimalType.Duck)
                        {
                            model.transform.localScale = Vector3.one * 0.28f;
                            model.transform.localPosition = new Vector3(0f, -0.1f, 0f);
                        }
                        else // Stork
                        {
                            model.transform.localScale = Vector3.one * 0.25f;
                            model.transform.localPosition = new Vector3(0f, -0.15f, 0f);
                            model.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                        }
                    }
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
