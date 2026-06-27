using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;
using System.IO;
using System.Collections.Generic;
using RungTramTraSu.Editor;


namespace RungTramTraSu
{
    public class Phase1Setup : EditorWindow
    {

        private static GameObject LoadAndInstantiate(string path, string newName, Vector3 position, Quaternion rotation)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
            {
                Debug.LogWarning("Không tìm thấy mô hình tại đường dẫn: " + path);
                return null;
            }
            
            GameObject obj = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (obj != null)
            {
                obj.name = newName;
                obj.transform.position = position;
                obj.transform.rotation = rotation;
            }
            return obj;
        }

        private static void SetupGrandpaAnimator(GameObject grandpa)
        {
            if (grandpa == null) return;
            
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Grandpa/GrandpaAnimator.controller");
            if (controller == null)
            {
                Debug.LogWarning("Không tìm thấy GrandpaAnimator.controller tại Assets/Animations/Grandpa/GrandpaAnimator.controller");
                return;
            }

            var oldAnim = grandpa.GetComponent<Animation>();
            if (oldAnim != null)
            {
                DestroyImmediate(oldAnim);
            }

            var animator = grandpa.GetComponent<Animator>();
            if (animator == null)
            {
                animator = grandpa.AddComponent<Animator>();
            }
            animator.runtimeAnimatorController = controller;

            if (grandpa.GetComponent<NPCGrandpa>() == null)
            {
                grandpa.AddComponent<NPCGrandpa>();
            }

            var col = grandpa.GetComponent<CapsuleCollider>();
            if (col != null)
            {
                col.isTrigger = true;
            }
        }

        private static void AddMeshCollidersRecursively(GameObject obj)
        {
            MeshFilter[] meshFilters = obj.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in meshFilters)
            {
                if (mf.sharedMesh == null) continue;

                // Kiểm tra số lượng tam giác để tránh tạo MeshCollider trên mesh quá lớn gây lag và lỗi PhysX (> 2 triệu)
                int triCount = 0;
                if (mf.sharedMesh.subMeshCount > 0)
                {
                    triCount = (int)mf.sharedMesh.GetIndexCount(0) / 3;
                }

                if (triCount > 100000)
                {
                    Debug.LogWarning($"[Rung Tram Tra Su] Bo qua MeshCollider cho '{mf.name}' vi qua nang ({triCount} tam giac) de tranh loi PhysX va lag. Thay bang BoxCollider.");
                    if (mf.gameObject.GetComponent<Collider>() == null)
                    {
                        mf.gameObject.AddComponent<BoxCollider>();
                    }
                    continue;
                }

                if (mf.gameObject.GetComponent<Collider>() == null)
                {
                    var mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                }
            }
        }

        private static void SetupPerfectBoatCollider(GameObject boat)
        {
            // 1. Thêm MeshCollider tự động cho các mesh con để đứng vững trong lòng thuyền
            AddMeshCollidersRecursively(boat);

            // 2. Tính toán Bounds cục bộ để tạo BoxCollider bệ đỡ ở đáy thuyền chống lún
            MeshRenderer[] renderers = boat.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            Vector3 worldCenter = bounds.center;
            Vector3 worldSize = bounds.size;

            Vector3 localCenter = boat.transform.InverseTransformPoint(worldCenter);
            Vector3 localScale = boat.transform.lossyScale;
            Vector3 localSize = new Vector3(
                worldSize.x / (localScale.x == 0 ? 1f : localScale.x),
                worldSize.y / (localScale.y == 0 ? 1f : localScale.y),
                worldSize.z / (localScale.z == 0 ? 1f : localScale.z)
            );

            // Thêm BoxCollider bệ đỡ ở đáy thuyền
            var solidCol = boat.AddComponent<BoxCollider>();
            
            // Đảm bảo Y local tối thiểu là 0.4f (scale 5 tương đương 2m thế giới) để làm bệ đỡ dày chống xuyên thủng
            float finalLocalY = Mathf.Max(localSize.y, 0.4f);
            solidCol.size = new Vector3(localSize.x, finalLocalY, localSize.z);

            // Hạ center của BoxCollider xuống một chút để không che hết khoang lòng thuyền của MeshCollider
            solidCol.center = new Vector3(localCenter.x, localCenter.y - (finalLocalY - localSize.y) / 2f, localCenter.z);
        }

        private static void CreateLayer(string layerName)
        {
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            
            bool exists = false;
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty layerProp = layers.GetArrayElementAtIndex(i);
                if (layerProp.stringValue == layerName)
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                for (int i = 8; i < layers.arraySize; i++)
                {
                    SerializedProperty layerProp = layers.GetArrayElementAtIndex(i);
                    if (string.IsNullOrEmpty(layerProp.stringValue))
                    {
                        layerProp.stringValue = layerName;
                        tagManager.ApplyModifiedProperties();
                        Debug.Log("Tự động tạo Layer: " + layerName);
                        break;
                    }
                }
            }
        }

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

        [MenuItem("Rung Tram Tra Su/Setup All Scenes")]
        public static void CreateAllScenes()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Lỗi!", "Không thể chạy thiết lập khi đang ở chế độ Play mode. Hãy tắt nút Play trước khi chạy!", "Đồng ý");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            // A. Cập nhật Skybox và Ambient Lighting cho Phase 1
            Scene phase1Scene = EditorSceneManager.OpenScene("Assets/Scenes/Phase1_GrandpaHouse.unity", OpenSceneMode.Single);
            Material p1Skybox = AssetDatabase.LoadAssetAtPath<Material>("Assets/EmaceArt/Slavic World Free/Skybox/Epic_BigCloudsSoft_V2/EA03_LowPolyBigClouds.mat");
            if (p1Skybox != null)
            {
                RenderSettings.skybox = p1Skybox;
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.25f;
            DynamicGI.UpdateEnvironment();
            EditorSceneManager.MarkSceneDirty(phase1Scene);
            EditorSceneManager.SaveScene(phase1Scene);

            // 1. Chạy Setup Phase 2 -> 5 (các phase này tự động beautify ở cuối mỗi CreatePhaseXScene)
            CreatePhase2Scene();
            CreatePhase3Scene();
            CreatePhase4Scene();
            CreatePhase5Scene();
            
            // 2. Đăng ký Build Settings (giữ Phase 1 nguyên bản từ Git)
            List<EditorBuildSettingsScene> buildScenes = new List<EditorBuildSettingsScene>();
            buildScenes.Add(new EditorBuildSettingsScene("Assets/Scenes/Phase1_GrandpaHouse.unity", true));
            buildScenes.Add(new EditorBuildSettingsScene("Assets/Scenes/Phase2_Canal.unity", true));
            buildScenes.Add(new EditorBuildSettingsScene("Assets/Scenes/Phase3_BambooBridge.unity", true));
            buildScenes.Add(new EditorBuildSettingsScene("Assets/Scenes/Phase4_Sanctuary.unity", true));
            buildScenes.Add(new EditorBuildSettingsScene("Assets/Scenes/Phase5_Sunset.unity", true));
            EditorBuildSettings.scenes = buildScenes.ToArray();

            // 3. Tự động mở lại Scene Phase 1 làm Scene hoạt động hiện tại trong Editor
            EditorSceneManager.OpenScene("Assets/Scenes/Phase1_GrandpaHouse.unity", OpenSceneMode.Single);
            
            Debug.Log("==> HOÀN TẤT TẠO CÁC SCENES PHASE 2-5 (GIỮ NGUYÊN BẢN PHASE 1 TỪ GIT) VÀ ĐĂNG KÝ BUILD SETTINGS!");
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Thành công!", "Đã thiết lập xong các Phase 2-5 và đăng ký Build Settings (Phase 1 giữ nguyên bản từ Git) thành công!", "Đồng ý");
            }
        }

        [MenuItem("Rung Tram Tra Su/Setup Phase 2 Scene")]
        public static void CreatePhase2Scene()
        {
            AssetDatabase.Refresh();
            CreateLayer("Interactable");
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            GameObject defaultCam = GameObject.FindWithTag("MainCamera");
            if (defaultCam != null) DestroyImmediate(defaultCam);

            GameObject dirLight = GameObject.Find("Directional Light");
            if (dirLight != null)
            {
                dirLight.transform.rotation = Quaternion.Euler(20, -55, 0);
                var lightComp = dirLight.GetComponent<Light>();
                if (lightComp != null)
                {
                    lightComp.color = new Color(1.0f, 0.95f, 0.82f);
                    lightComp.intensity = 1.5f;
                    lightComp.shadows = LightShadows.Soft;
                    lightComp.shadowStrength = 0.85f;
                }
            }

            Texture2D grassTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/grass_dirt_texture.png");
            Texture2D waterTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/duckweed_water_texture.png");

            Material grassMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (grassTex != null)
            {
                grassMat.mainTexture = grassTex;
                grassMat.mainTextureScale = new Vector2(15f, 20f);
            }
            else grassMat.color = new Color(0.15f, 0.32f, 0.18f);

            Material waterMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (waterTex != null)
            {
                waterMat.mainTexture = waterTex;
                waterMat.mainTextureScale = new Vector2(6f, 18f);
            }
            else waterMat.color = new Color(0.08f, 0.22f, 0.18f);
            if (waterMat.HasProperty("_Smoothness")) waterMat.SetFloat("_Smoothness", 0.85f);
            waterMat.SetFloat("_Metallic", 0.15f);

            // A. Grid terrain
            GameObject terrainObj = new GameObject("OrganicTerrain_Bank");
            MeshFilter meshFilter = terrainObj.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = terrainObj.AddComponent<MeshRenderer>();
            MeshCollider meshCollider = terrainObj.AddComponent<MeshCollider>();

            Mesh terrainMesh = new Mesh();
            int xSegments = 120;
            int zSegments = 120;
            int numVertices = (xSegments + 1) * (zSegments + 1);
            Vector3[] vertices = new Vector3[numVertices];
            Vector2[] uvs = new Vector2[numVertices];
            int[] triangles = new int[xSegments * zSegments * 6];

            float xMin = -55f, xMax = 55f, zMin = -65f, zMax = 65f;
            int vIndex = 0;
            for (int z = 0; z <= zSegments; z++)
            {
                float zPct = (float)z / zSegments;
                float zPos = Mathf.Lerp(zMin, zMax, zPct);
                for (int x = 0; x <= xSegments; x++)
                {
                    float xPct = (float)x / xSegments;
                    float xPos = Mathf.Lerp(xMin, xMax, xPct);
                    float yPos = GetHeightAt(xPos, zPos);
                    vertices[vIndex] = new Vector3(xPos, yPos, zPos);
                    uvs[vIndex] = new Vector2(xPos * 0.12f, zPos * 0.12f);
                    vIndex++;
                }
            }

            int tIndex = 0;
            for (int z = 0; z < zSegments; z++)
            {
                for (int x = 0; x < xSegments; x++)
                {
                    int row1 = z * (xSegments + 1);
                    int row2 = (z + 1) * (xSegments + 1);
                    triangles[tIndex++] = row1 + x;
                    triangles[tIndex++] = row2 + x;
                    triangles[tIndex++] = row1 + x + 1;
                    triangles[tIndex++] = row1 + x + 1;
                    triangles[tIndex++] = row2 + x;
                    triangles[tIndex++] = row2 + x + 1;
                }
            }
            terrainMesh.vertices = vertices;
            terrainMesh.uv = uvs;
            terrainMesh.triangles = triangles;
            terrainMesh.RecalculateNormals();
            meshFilter.sharedMesh = terrainMesh;
            meshCollider.sharedMesh = terrainMesh;
            meshRenderer.sharedMaterial = grassMat;

            // B. Canal water
            GameObject river = GameObject.CreatePrimitive(PrimitiveType.Plane);
            river.name = "RiverWater_Canal";
            river.transform.position = new Vector3(25f, -1.0f, 0f);
            river.transform.localScale = new Vector3(4.5f, 1f, 15f);
            DestroyImmediate(river.GetComponent<MeshCollider>());
            river.GetComponent<Renderer>().sharedMaterial = waterMat;

            // Boat
            GameObject boat = LoadAndInstantiate("Assets/Models/VietnameseBoat/mô+hình+thuyền+sampan+gỗ+3d.glb", "Sampan Boat", new Vector3(25f, -0.82f, -55f), Quaternion.Euler(0f, -90f, 0f));
            if (boat != null)
            {
                boat.transform.localScale = new Vector3(5f, 5f, 5f);
                SetupPerfectBoatCollider(boat);
                var wf = boat.AddComponent<WaterFloat>();
                wf.initialYOffset = 0.32f;

                // Thêm Ông Ngoại ngồi ở mũi thuyền chèo xuồng
                GameObject grandpa = LoadAndInstantiate("Assets/Models/VietnameseGrandpa/Meshy_AI_Old_Man_with_Open_Arm_biped/Meshy_AI_Old_Man_with_Open_Arm_biped_Character_output.glb", "Grandpa_NPC", Vector3.zero, Quaternion.identity);
                if (grandpa != null)
                {
                    grandpa.transform.SetParent(boat.transform, false);
                    // Bù trừ tỷ lệ scale 5x của thuyền (giữ kích thước của ông ngoại ở mức 0.85x chuẩn trong thế giới thực)
                    grandpa.transform.localScale = new Vector3(0.85f / 5f, 0.85f / 5f, 0.85f / 5f);
                    // Đặt ông ngoại ở đầu thuyền, quay mặt về phía trước cùng chiều di chuyển
                    grandpa.transform.localPosition = new Vector3(1.5f / 5f, 0.3f / 5f, 0f);
                    grandpa.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

                    grandpa.layer = LayerMask.NameToLayer("Interactable");
                    var col = grandpa.AddComponent<CapsuleCollider>();
                    col.center = new Vector3(0, 0.9f, 0);
                    col.radius = 0.35f;
                    col.height = 1.8f;

                    SetupGrandpaAnimator(grandpa);
                }
            }

            // Pier at start and end
            GameObject startPier = GameObject.CreatePrimitive(PrimitiveType.Cube);
            startPier.name = "StartPier";
            startPier.transform.position = new Vector3(25f, -0.4f, -56f);
            startPier.transform.localScale = new Vector3(3f, 0.1f, 2f);
            Material woodMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            woodMat.color = new Color(0.32f, 0.2f, 0.11f);
            startPier.GetComponent<Renderer>().sharedMaterial = woodMat;

            // Biển báo gỗ mộc mạc đầu kênh dẫn
            CreateScenicWoodenSign("Kênh Dẫn Trà Sư\n(Mùa Nước Nổi)", new Vector3(22f, -0.4f, -54f), -30f);

            GameObject endPier = GameObject.CreatePrimitive(PrimitiveType.Cube);
            endPier.name = "EndPier";
            float endPierY = GetHeightAt(25f, 55f);
            endPier.transform.position = new Vector3(25f, endPierY + 0.1f, 55f);
            endPier.transform.localScale = new Vector3(3f, 0.1f, 2f);
            endPier.GetComponent<Renderer>().sharedMaterial = woodMat;

            // Winding Forest (Massive tree layout)
            Random.InitState(54321);
            GameObject forestContainer = new GameObject("TeaTree_Forest");
            int totalTrees = 120;
            for (int i = 0; i < totalTrees; i++)
            {
                float zPos = Random.Range(-55f, 55f);
                float canalCenter = 25f + Mathf.Sin(zPos * 0.08f) * 5f;
                float xPos = (i % 2 == 0) ? Random.Range(-38f, canalCenter - 8.5f) : Random.Range(canalCenter + 8.5f, canalCenter + 26f);
                float yPos = GetHeightAt(xPos, zPos);

                float distToCanal = Mathf.Abs(xPos - canalCenter);
                float tiltAngle = (distToCanal < 13f) ? Mathf.Clamp01((13f - distToCanal) / 4.5f) * Random.Range(12f, 25f) : 0f;
                float zRotationOffset = (xPos < canalCenter) ? -tiltAngle : tiltAngle;

                GameObject forestTree = LoadAndInstantiate("Assets/Models/TeaTree/low-poly+tree+3d+model.glb", "TeaTree_" + i, new Vector3(xPos, yPos + 3.5f, zPos), Quaternion.Euler(Random.Range(-5f, 5f), Random.Range(0f, 360f), zRotationOffset));
                if (forestTree != null)
                {
                    forestTree.transform.SetParent(forestContainer.transform);
                    float rndScale = Random.Range(9.5f, 13.5f);
                    forestTree.transform.localScale = new Vector3(rndScale, rndScale, rndScale);
                    forestTree.AddComponent<WindSway>();

                    // Thêm rễ thở và cò trắng
                    CreateBreathingRootsAroundTree(new Vector3(xPos, yPos + 3.5f, zPos), forestContainer.transform, Random.Range(2, 6));
                    CreateBirdInTree(new Vector3(xPos, yPos + 3.5f, zPos), forestTree.transform);
                }
            }

            // Lily pads & reeds
            GameObject lilyContainer = new GameObject("FloatingLilyPads");
            Material lilyMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            lilyMat.color = new Color(0.12f, 0.45f, 0.22f);
            for (int i = 0; i < 50; i++)
            {
                float zPos = Random.Range(-45f, 45f);
                float canalCenter = 25f + Mathf.Sin(zPos * 0.08f) * 5f;
                float xPos = canalCenter + Random.Range(-5f, 5f);
                float yPos = -0.98f;
                GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pad.name = "LilyPad_" + i;
                pad.transform.SetParent(lilyContainer.transform);
                pad.transform.position = new Vector3(xPos, yPos, zPos);
                pad.transform.localScale = new Vector3(Random.Range(0.6f, 1.4f), 0.01f, Random.Range(0.6f, 1.4f));
                DestroyImmediate(pad.GetComponent<Collider>());
                pad.GetComponent<Renderer>().sharedMaterial = lilyMat;

                // Sinh hoa súng hồng rực rỡ nổi trên thảm bèo xanh
                if (Random.value < 0.35f)
                {
                    CreateLotusFlower(new Vector3(xPos, yPos, zPos), pad.transform);
                }
            }

            GameObject reedContainer = new GameObject("ShorelineReeds");
            Material reedMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            reedMat.color = new Color(0.24f, 0.38f, 0.14f);
            for (int i = 0; i < 120; i++)
            {
                float zPos = Random.Range(-50f, 50f);
                float canalCenter = 25f + Mathf.Sin(zPos * 0.08f) * 5f;
                float xPos = (Random.value > 0.5f) ? canalCenter - Random.Range(5.2f, 6.8f) : canalCenter + Random.Range(5.2f, 6.8f);
                float yPos = GetHeightAt(xPos, zPos);
                GameObject reed = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                reed.name = "Reed_" + i;
                reed.transform.SetParent(reedContainer.transform);
                float reedHeight = Random.Range(1.2f, 2.3f);
                reed.transform.position = new Vector3(xPos, yPos + reedHeight * 0.5f - 0.2f, zPos);
                reed.transform.localScale = new Vector3(0.06f, reedHeight, 0.06f);
                DestroyImmediate(reed.GetComponent<Collider>());
                reed.GetComponent<Renderer>().sharedMaterial = reedMat;
            }

            // Spawn ambient jumping fish along the canal in Phase 2!
            GameObject fishContainer = new GameObject("AmbientFish");
            for (int i = 0; i < 8; i++)
            {
                float zPos = -40f + i * 11.5f;
                float canalCenter = 25f + Mathf.Sin(zPos * 0.08f) * 5f;
                float xPos = canalCenter + Random.Range(-1.5f, 1.5f);
                float yPos = -1.5f; // Swim below water surface (-1.0f)

                GameObject fishObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                fishObj.name = "JumpingFish_P2_" + i;
                fishObj.transform.SetParent(fishContainer.transform);
                fishObj.transform.position = new Vector3(xPos, yPos, zPos);
                fishObj.transform.localScale = new Vector3(0.15f, 0.6f, 0.15f);

                Material animMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                animMat.color = new Color(0.18f, 0.22f, 0.42f);
                fishObj.GetComponent<Renderer>().sharedMaterial = animMat;

                var sCol = fishObj.AddComponent<SphereCollider>();
                sCol.isTrigger = true;
                sCol.radius = 1.2f;

                var ai = fishObj.AddComponent<AnimalAI>();
                var serAI = new SerializedObject(ai);
                serAI.FindProperty("animalType").intValue = (int)AnimalAI.AnimalType.Fish;
                serAI.FindProperty("speed").floatValue = 0f;
                serAI.FindProperty("range").floatValue = 0f;
                serAI.ApplyModifiedProperties();
            }

            // Event targets
            GameObject sunRayObj = new GameObject("SunRayQuestTarget");
            sunRayObj.transform.position = new Vector3(23f, 4f, -10f);
            for (int i = 0; i < 3; i++)
            {
                GameObject shaft = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                shaft.name = "Sunshaft_Visual";
                shaft.transform.SetParent(sunRayObj.transform, false);
                shaft.transform.localPosition = new Vector3(Random.Range(-2f, 2f), 0f, Random.Range(-2f, 2f));
                shaft.transform.localScale = new Vector3(0.8f, 6f, 0.8f);
                shaft.transform.rotation = Quaternion.Euler(20f, 0f, 15f);
                DestroyImmediate(shaft.GetComponent<Collider>());
                Material shaftMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                shaftMat.color = new Color(1.0f, 0.95f, 0.6f, 0.24f);
                shaftMat.SetFloat("_Surface", 1.0f);
                shaftMat.SetOverrideTag("RenderType", "Transparent");
                shaftMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                shaftMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                shaftMat.SetInt("_ZWrite", 0);
                shaftMat.DisableKeyword("_ALPHATEST_ON");
                shaftMat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                shaftMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                shaft.GetComponent<Renderer>().sharedMaterial = shaftMat;
            }

            GameObject flock = new GameObject("StorksFlock");
            flock.transform.position = new Vector3(25f, 10f, 30f);
            GameObject storkLeader = null;
            for (int i = 0; i < 6; i++)
            {
                GameObject bird = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bird.name = "Stork_Bird_" + i;
                bird.transform.SetParent(flock.transform, false);
                bird.transform.localPosition = new Vector3(Random.Range(-2f, 2f), Random.Range(-1f, 1f), Random.Range(-2f, 2f));
                bird.transform.localScale = new Vector3(0.7f, 0.15f, 0.5f);
                Material birdMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                birdMat.color = Color.white;
                bird.GetComponent<Renderer>().sharedMaterial = birdMat;
                DestroyImmediate(bird.GetComponent<Collider>());
                if (i == 0) storkLeader = bird;
            }

            // Setup Player
            GameObject player = new GameObject("Player");
            player.tag = "Player";
            player.transform.position = new Vector3(25f, 0.5f, -55f);
            var charController = player.AddComponent<CharacterController>();
            charController.height = 2f;
            charController.radius = 0.4f;
            charController.center = new Vector3(0, 1f, 0);
            
            InputActionAsset inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            var playerInput = player.AddComponent<PlayerInput>();
            if (inputAsset != null)
            {
                playerInput.actions = inputAsset;
                playerInput.defaultActionMap = "Player";
            }
            var playerCtrl = player.AddComponent<PlayerController>();
            var playerInteract = player.AddComponent<PlayerInteraction>();

            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            camObj.transform.SetParent(player.transform, false);
            camObj.transform.localPosition = new Vector3(0, 1.8f, 0);
            camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
            var cameraData = camObj.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            var photoCam = camObj.AddComponent<PhotoCamera>();

            GameObject cameraHandModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cameraHandModel.name = "CameraHandModel";
            cameraHandModel.transform.SetParent(camObj.transform, false);
            cameraHandModel.transform.localPosition = new Vector3(0.2f, -0.25f, 0.4f);
            cameraHandModel.transform.localScale = new Vector3(0.12f, 0.08f, 0.1f);
            DestroyImmediate(cameraHandModel.GetComponent<BoxCollider>());

            SerializedObject serPlayer = new SerializedObject(playerCtrl);
            serPlayer.FindProperty("playerCamera").objectReferenceValue = camObj.transform;
            serPlayer.ApplyModifiedProperties();
            var serInteract = new SerializedObject(playerInteract);
            serInteract.FindProperty("cameraTransform").objectReferenceValue = camObj.transform;
            serInteract.FindProperty("interactableLayer").intValue = 1 << LayerMask.NameToLayer("Interactable");
            serInteract.ApplyModifiedProperties();

            GameObject gameUI = CreateBaseGameUI(photoCam, cameraHandModel, out TextMeshProUGUI objText);

            GameObject managersObj = new GameObject("Managers");
            managersObj.AddComponent<DialogueManager>();
            var p2Manager = managersObj.AddComponent<Phase2Manager>();

            var serPhase2 = new SerializedObject(p2Manager);
            serPhase2.FindProperty("boat").objectReferenceValue = boat.transform;
            serPhase2.FindProperty("player").objectReferenceValue = player.transform;
            serPhase2.FindProperty("objectiveText").objectReferenceValue = objText;
            serPhase2.FindProperty("photoCamera").objectReferenceValue = photoCam;
            serPhase2.FindProperty("sunRayTarget").objectReferenceValue = sunRayObj.transform;
            serPhase2.FindProperty("storkTarget").objectReferenceValue = storkLeader.transform;
            serPhase2.FindProperty("storksFlock").objectReferenceValue = flock;

            // Load and assign bird prefabs directly so checkpoint 1/2/3 works seamlessly
            List<GameObject> birds = new List<GameObject>();
            string[] birdNames = new string[] { "lb_robinHQ", "lb_sparrowHQ", "lb_goldFinchHQ", "lb_blueJayHQ", "lb_cardinalHQ" };
            foreach (var bName in birdNames)
            {
                GameObject birdPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/living birds/resources/{bName}.prefab");
                if (birdPrefab == null)
                {
                    string[] guids = AssetDatabase.FindAssets($"{bName} t:GameObject");
                    if (guids.Length > 0)
                    {
                        birdPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
                    }
                }
                if (birdPrefab != null)
                {
                    birds.Add(birdPrefab);
                }
            }
            var birdPrefabsProp = serPhase2.FindProperty("birdPrefabs");
            birdPrefabsProp.ClearArray();
            for (int i = 0; i < birds.Count; i++)
            {
                birdPrefabsProp.InsertArrayElementAtIndex(i);
                birdPrefabsProp.GetArrayElementAtIndex(i).objectReferenceValue = birds[i];
            }

            serPhase2.ApplyModifiedProperties();

            SetupPostProcessingAndFog(camObj);

            EditorSceneManager.SaveScene(newScene, "Assets/Scenes/Phase2_Canal.unity");
            SceneBeautifierAll.BeautifyPhase2();
            Debug.Log("Successfully created Phase 2 scene!");
        }

        [MenuItem("Rung Tram Tra Su/Setup Phase 3 Scene")]
        public static void CreatePhase3Scene()
        {
            AssetDatabase.Refresh();
            CreateLayer("Interactable");
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            GameObject defaultCam = GameObject.FindWithTag("MainCamera");
            if (defaultCam != null) DestroyImmediate(defaultCam);

            GameObject dirLight = GameObject.Find("Directional Light");
            if (dirLight != null)
            {
                dirLight.transform.rotation = Quaternion.Euler(32, -45, 0);
                var lightComp = dirLight.GetComponent<Light>();
                if (lightComp != null)
                {
                    lightComp.color = new Color(0.92f, 0.95f, 0.9f);
                    lightComp.intensity = 1.0f;
                    lightComp.shadows = LightShadows.Soft;
                }
            }

            Texture2D grassTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/grass_dirt_texture.png");
            Texture2D waterTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/duckweed_water_texture.png");

            Material grassMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (grassTex != null)
            {
                grassMat.mainTexture = grassTex;
                grassMat.mainTextureScale = new Vector2(15f, 20f);
            }
            else grassMat.color = new Color(0.12f, 0.28f, 0.14f);

            Material waterMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (waterTex != null)
            {
                waterMat.mainTexture = waterTex;
                waterMat.mainTextureScale = new Vector2(5f, 15f);
            }
            else waterMat.color = new Color(0.06f, 0.18f, 0.14f);
            if (waterMat.HasProperty("_Smoothness")) waterMat.SetFloat("_Smoothness", 0.8f);

            Material woodMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            woodMat.color = new Color(0.32f, 0.2f, 0.11f);

            GameObject swampFloor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            swampFloor.name = "SwampFloor_Ground";
            swampFloor.transform.position = new Vector3(25f, -1.8f, 0f);
            swampFloor.transform.localScale = new Vector3(8f, 1f, 12f);
            swampFloor.GetComponent<Renderer>().sharedMaterial = grassMat;

            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "SwampWater";
            water.transform.position = new Vector3(25f, -1.0f, 0f);
            water.transform.localScale = new Vector3(8f, 1f, 12f);
            DestroyImmediate(water.GetComponent<MeshCollider>());
            water.GetComponent<Renderer>().sharedMaterial = waterMat;

            // Bamboo Bridge
            GameObject bridgeContainer = new GameObject("BambooBridge");
            List<Vector3> bridgePoints = new List<Vector3>();
            float bzStart = -48f;
            float bzEnd = 48f;
            float bStep = 2.0f;
            for (float z = bzStart; z <= bzEnd; z += bStep)
            {
                float x = 5f + Mathf.Sin(z * 0.12f) * 6f;
                bridgePoints.Add(new Vector3(x, -0.5f, z));
            }

            for (int i = 0; i < bridgePoints.Count; i++)
            {
                Vector3 pt = bridgePoints[i];
                GameObject plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
                plank.name = "BridgePlank_" + i;
                plank.transform.SetParent(bridgeContainer.transform);
                plank.transform.position = pt;
                plank.transform.localScale = new Vector3(1.6f, 0.1f, 2.2f);
                plank.GetComponent<Renderer>().sharedMaterial = woodMat;

                if (i % 2 == 0)
                {
                    GameObject leftPost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    leftPost.name = "RailPostL_" + i;
                    leftPost.transform.SetParent(bridgeContainer.transform);
                    leftPost.transform.position = pt + new Vector3(-0.75f, 0.6f, 0f);
                    leftPost.transform.localScale = new Vector3(0.08f, 1.2f, 0.08f);
                    leftPost.GetComponent<Renderer>().sharedMaterial = woodMat;
                    DestroyImmediate(leftPost.GetComponent<Collider>());

                    GameObject rightPost = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    rightPost.name = "RailPostR_" + i;
                    rightPost.transform.SetParent(bridgeContainer.transform);
                    rightPost.transform.position = pt + new Vector3(0.75f, 0.6f, 0f);
                    rightPost.transform.localScale = new Vector3(0.08f, 1.2f, 0.08f);
                    rightPost.GetComponent<Renderer>().sharedMaterial = woodMat;
                    DestroyImmediate(rightPost.GetComponent<Collider>());
                }
            }

            // Biển báo gỗ mộc mạc tại cầu tre
            CreateScenicWoodenSign("Cầu Tre Vạn Bước\n(Lõi Rừng Tràm)", new Vector3(5f + Mathf.Sin(-45f * 0.12f) * 6f - 2f, -0.3f, -48f), 15f);

            // Spawning 150 dense trees (Deep forest feel)
            Random.InitState(9999);
            GameObject forestContainer = new GameObject("TeaTree_DenseForest");
            for (int i = 0; i < 150; i++)
            {
                float zPos = Random.Range(-55f, 55f);
                float xCenter = 5f + Mathf.Sin(zPos * 0.12f) * 6f;
                float xPos = (i % 2 == 0) ? Random.Range(xCenter - 25f, xCenter - 6.5f) : Random.Range(xCenter + 2.5f, xCenter + 25f);

                GameObject forestTree = LoadAndInstantiate("Assets/Models/TeaTree/low-poly+tree+3d+model.glb", "TeaTree_" + i, new Vector3(xPos, -1.8f + 3.5f, zPos), Quaternion.Euler(0, Random.Range(0f, 360f), Random.Range(-8f, 8f)));
                if (forestTree != null)
                {
                    forestTree.transform.SetParent(forestContainer.transform);
                    float rndScale = Random.Range(9f, 13f);
                    forestTree.transform.localScale = new Vector3(rndScale, rndScale, rndScale);
                    forestTree.AddComponent<WindSway>();

                    // Phát triển ngoại cảnh: Rễ thở tràm sinh ngẫu nhiên quanh gốc và cò trắng đậu trên ngọn
                    CreateBreathingRootsAroundTree(new Vector3(xPos, -1.8f + 3.5f, zPos), forestContainer.transform, Random.Range(2, 6));
                    CreateBirdInTree(new Vector3(xPos, -1.8f + 3.5f, zPos), forestTree.transform);

                    GameObject trunkCol = new GameObject("TrunkCollider");
                    trunkCol.transform.SetParent(forestTree.transform, false);
                    trunkCol.transform.localScale = new Vector3(1f/rndScale, 1f/rndScale, 1f/rndScale);
                    var col = trunkCol.AddComponent<CapsuleCollider>();
                    col.center = new Vector3(0f, 1.5f, 0f);
                    col.radius = 0.5f;
                    col.height = 4.0f;
                }
            }

            // Lily pads & reeds for Phase 3 (swamp water)
            GameObject lilyContainer = new GameObject("FloatingLilyPads");
            Material lilyMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            lilyMat.color = new Color(0.12f, 0.45f, 0.22f);
            for (int i = 0; i < 60; i++)
            {
                float zPos = Random.Range(-50f, 50f);
                float xCenter = 5f + Mathf.Sin(zPos * 0.12f) * 6f;
                
                // Lily pads float in water, not on the bridge
                float xPos = xCenter + Random.Range(2.5f, 12f) * (Random.value > 0.5f ? 1f : -1f);
                float yPos = -0.98f;
                
                GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pad.name = "LilyPad_" + i;
                pad.transform.SetParent(lilyContainer.transform);
                pad.transform.position = new Vector3(xPos, yPos, zPos);
                float padScale = Random.Range(0.6f, 1.4f);
                pad.transform.localScale = new Vector3(padScale, 0.01f, padScale);
                pad.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                DestroyImmediate(pad.GetComponent<Collider>());
                pad.GetComponent<Renderer>().sharedMaterial = lilyMat;
                
                if (Random.value < 0.35f)
                {
                    CreateLotusFlower(new Vector3(xPos, yPos, zPos), pad.transform);
                }
            }

            // Breathing roots target (Quest item)
            GameObject rootGroup = new GameObject("BreathingRootsQuestTarget");
            float bridgeXAtZ8 = 5f + Mathf.Sin(-8f * 0.12f) * 6f;
            rootGroup.transform.position = new Vector3(bridgeXAtZ8 - 4.8f, -1.0f, -8f);
            rootGroup.layer = LayerMask.NameToLayer("Interactable");
            
            var rootCol = rootGroup.AddComponent<SphereCollider>();
            rootCol.center = Vector3.zero;
            rootCol.radius = 1.8f;
            rootCol.isTrigger = true;

            for (int i = 0; i < 15; i++)
            {
                GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                root.name = "Pneumatophore_" + i;
                root.transform.SetParent(rootGroup.transform, false);
                root.transform.localPosition = new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(0.2f, 0.6f), Random.Range(-1.2f, 1.2f));
                root.transform.localScale = new Vector3(0.06f, Random.Range(0.4f, 0.8f), 0.06f);
                root.transform.rotation = Quaternion.Euler(Random.Range(-10f, 10f), 0f, Random.Range(-10f, 10f));
                DestroyImmediate(root.GetComponent<Collider>());
                Material spikeMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                spikeMat.color = new Color(0.28f, 0.18f, 0.1f);
                root.GetComponent<Renderer>().sharedMaterial = spikeMat;
            }

            // Setup Player
            GameObject player = new GameObject("Player");
            player.tag = "Player";
            float startX = 5f + Mathf.Sin(-45f * 0.12f) * 6f;
            player.transform.position = new Vector3(startX, 0.5f, -45f);

            var charController = player.AddComponent<CharacterController>();
            charController.height = 2f;
            charController.radius = 0.4f;
            charController.center = new Vector3(0, 1f, 0);

            InputActionAsset inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            var playerInput = player.AddComponent<PlayerInput>();
            if (inputAsset != null)
            {
                playerInput.actions = inputAsset;
                playerInput.defaultActionMap = "Player";
            }
            var playerCtrl = player.AddComponent<PlayerController>();
            var playerInteract = player.AddComponent<PlayerInteraction>();

            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            camObj.transform.SetParent(player.transform, false);
            camObj.transform.localPosition = new Vector3(0, 1.8f, 0);
            camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
            var cameraData = camObj.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            var photoCam = camObj.AddComponent<PhotoCamera>();

            GameObject cameraHandModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cameraHandModel.name = "CameraHandModel";
            cameraHandModel.transform.SetParent(camObj.transform, false);
            cameraHandModel.transform.localPosition = new Vector3(0.2f, -0.25f, 0.4f);
            cameraHandModel.transform.localScale = new Vector3(0.12f, 0.08f, 0.1f);
            DestroyImmediate(cameraHandModel.GetComponent<BoxCollider>());

            SerializedObject serPlayer = new SerializedObject(playerCtrl);
            serPlayer.FindProperty("playerCamera").objectReferenceValue = camObj.transform;
            serPlayer.ApplyModifiedProperties();
            var serInteract = new SerializedObject(playerInteract);
            serInteract.FindProperty("cameraTransform").objectReferenceValue = camObj.transform;
            serInteract.FindProperty("interactableLayer").intValue = 1 << LayerMask.NameToLayer("Interactable");
            serInteract.ApplyModifiedProperties();

            GameObject gameUI = CreateBaseGameUI(photoCam, cameraHandModel, out TextMeshProUGUI objText);

            // Chiếc xuồng ba lá (Sampan Boat) neo sát bến để tự động trôi dọc kênh
            GameObject boat = LoadAndInstantiate("Assets/Models/VietnameseBoat/mô+hình+thuyền+sampan+gỗ+3d.glb", "Sampan Boat", new Vector3(startX - 3.5f, -0.82f, -45f), Quaternion.Euler(0f, -90f, 0f));
            if (boat != null)
            {
                boat.transform.localScale = new Vector3(5f, 5f, 5f);
                SetupPerfectBoatCollider(boat);
                var wf = boat.AddComponent<WaterFloat>();
                wf.initialYOffset = 0.32f;
            }

            // Grandpa NPC đứng trên thuyền
            GameObject grandpa = LoadAndInstantiate("Assets/Models/VietnameseGrandpa/Meshy_AI_Old_Man_with_Open_Arm_biped/Meshy_AI_Old_Man_with_Open_Arm_biped_Character_output.glb", "Grandpa_NPC", Vector3.zero, Quaternion.identity);
            if (grandpa != null)
            {
                grandpa.transform.SetParent(boat.transform, false);
                grandpa.transform.localScale = new Vector3(0.85f / 5f, 0.85f / 5f, 0.85f / 5f);
                grandpa.transform.localPosition = new Vector3(1.2f / 5f, 0.35f / 5f, 0f);
                grandpa.transform.localRotation = Quaternion.Euler(0f, -90f, 0f); // facing player (who is at local -X)

                grandpa.layer = LayerMask.NameToLayer("Interactable");
                var col = grandpa.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0, 0.9f, 0);
                col.radius = 0.35f;
                col.height = 1.8f;

                SetupGrandpaAnimator(grandpa);
            }

            // Managers
            GameObject managersObj = new GameObject("Managers");
            managersObj.AddComponent<DialogueManager>();
            var p3Manager = managersObj.AddComponent<Phase3Manager>();

            var serPhase3 = new SerializedObject(p3Manager);
            serPhase3.FindProperty("boat").objectReferenceValue = boat != null ? boat.transform : null;
            serPhase3.FindProperty("player").objectReferenceValue = player.transform;
            serPhase3.FindProperty("objectiveText").objectReferenceValue = objText;
            serPhase3.FindProperty("photoCamera").objectReferenceValue = photoCam;
            serPhase3.ApplyModifiedProperties();

            SetupPostProcessingAndFog(camObj);

            EditorSceneManager.SaveScene(newScene, "Assets/Scenes/Phase3_BambooBridge.unity");
            SceneBeautifierAll.BeautifyPhase3();
            Debug.Log("Successfully created Phase 3 scene!");
        }

        [MenuItem("Rung Tram Tra Su/Setup Phase 4 Scene")]
        public static void CreatePhase4Scene()
        {
            AssetDatabase.Refresh();
            CreateLayer("Interactable");
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            GameObject defaultCam = GameObject.FindWithTag("MainCamera");
            if (defaultCam != null) DestroyImmediate(defaultCam);

            GameObject dirLight = GameObject.Find("Directional Light");
            if (dirLight != null)
            {
                dirLight.transform.rotation = Quaternion.Euler(30, -55, 0);
                var lightComp = dirLight.GetComponent<Light>();
                if (lightComp != null)
                {
                    lightComp.color = new Color(0.95f, 0.98f, 0.92f);
                    lightComp.intensity = 1.2f;
                    lightComp.shadows = LightShadows.Soft;
                }
            }

            Texture2D grassTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/grass_dirt_texture.png");
            Texture2D waterTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/duckweed_water_texture.png");

            Material grassMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (grassTex != null)
            {
                grassMat.mainTexture = grassTex;
                grassMat.mainTextureScale = new Vector2(20f, 25f);
            }
            else grassMat.color = new Color(0.12f, 0.28f, 0.14f);

            Material waterMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (waterTex != null)
            {
                waterMat.mainTexture = waterTex;
                waterMat.mainTextureScale = new Vector2(8f, 12f);
            }
            else waterMat.color = new Color(0.06f, 0.18f, 0.14f);
            if (waterMat.HasProperty("_Smoothness")) waterMat.SetFloat("_Smoothness", 0.75f);

            // A. Grid terrain (same beautiful organic bank as Phase 2!)
            GameObject terrainObj = new GameObject("OrganicTerrain_Bank");
            MeshFilter meshFilter = terrainObj.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = terrainObj.AddComponent<MeshRenderer>();
            MeshCollider meshCollider = terrainObj.AddComponent<MeshCollider>();

            Mesh terrainMesh = new Mesh();
            int xSegments = 120;
            int zSegments = 120;
            int numVertices = (xSegments + 1) * (zSegments + 1);
            Vector3[] vertices = new Vector3[numVertices];
            Vector2[] uvs = new Vector2[numVertices];
            int[] triangles = new int[xSegments * zSegments * 6];

            float xMin = -55f, xMax = 55f, zMin = -65f, zMax = 65f;
            int vIndex = 0;
            for (int z = 0; z <= zSegments; z++)
            {
                float zPct = (float)z / zSegments;
                float zPos = Mathf.Lerp(zMin, zMax, zPct);
                for (int x = 0; x <= xSegments; x++)
                {
                    float xPct = (float)x / xSegments;
                    float xPos = Mathf.Lerp(xMin, xMax, xPct);
                    float yPos = GetHeightAt(xPos, zPos);
                    vertices[vIndex] = new Vector3(xPos, yPos, zPos);
                    uvs[vIndex] = new Vector2(xPos * 0.12f, zPos * 0.12f);
                    vIndex++;
                }
            }

            int tIndex = 0;
            for (int z = 0; z < zSegments; z++)
            {
                for (int x = 0; x < xSegments; x++)
                {
                    int row1 = z * (xSegments + 1);
                    int row2 = (z + 1) * (xSegments + 1);
                    triangles[tIndex++] = row1 + x;
                    triangles[tIndex++] = row2 + x;
                    triangles[tIndex++] = row1 + x + 1;
                    triangles[tIndex++] = row1 + x + 1;
                    triangles[tIndex++] = row2 + x;
                    triangles[tIndex++] = row2 + x + 1;
                }
            }
            terrainMesh.vertices = vertices;
            terrainMesh.uv = uvs;
            terrainMesh.triangles = triangles;
            terrainMesh.RecalculateNormals();
            meshFilter.sharedMesh = terrainMesh;
            meshCollider.sharedMesh = terrainMesh;
            meshRenderer.sharedMaterial = grassMat;

            // B. Canal water (same beautiful river as Phase 2!)
            GameObject water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "RiverWater_Canal";
            water.transform.position = new Vector3(25f, -1.0f, 0f);
            water.transform.localScale = new Vector3(4.5f, 1f, 15f);
            DestroyImmediate(water.GetComponent<MeshCollider>());
            water.GetComponent<Renderer>().sharedMaterial = waterMat;

            // Biển báo gỗ mộc mạc tại khu bảo tồn
            float signY = GetHeightAt(22f, -46f);
            CreateScenicWoodenSign("Khu Bảo Tồn Đầm Lầy\n(Yêu Cầu Đi Nhẹ Nói Khẽ)", new Vector3(22f, signY, -46f), 10f);

            // Spawning 150 cajuput trees (dense sanctuary forest!)
            Random.InitState(777);
            GameObject forestContainer = new GameObject("TeaTree_SanctuaryForest");
            for (int i = 0; i < 150; i++)
            {
                float xPos = Random.Range(-25f, 65f);
                float zPos = Random.Range(-55f, 55f);
                
                // Exclude trees from spawning inside the organic canal pathway
                float canalCenter = 25f + Mathf.Sin(zPos * 0.08f) * 5f;
                if (Mathf.Abs(xPos - canalCenter) < 5.5f)
                {
                    // Push the tree to the left or right bank
                    xPos += (xPos > canalCenter) ? 6.5f : -6.5f;
                }

                GameObject forestTree = LoadAndInstantiate("Assets/Models/TeaTree/low-poly+tree+3d+model.glb", "TeaTree_" + i, new Vector3(xPos, -1.8f + 3.5f, zPos), Quaternion.Euler(0, Random.Range(0f, 360f), Random.Range(-6f, 6f)));
                if (forestTree != null)
                {
                    forestTree.transform.SetParent(forestContainer.transform);
                    float rndScale = Random.Range(9f, 13f);
                    forestTree.transform.localScale = new Vector3(rndScale, rndScale, rndScale);
                    forestTree.AddComponent<WindSway>();

                    // Phát triển ngoại cảnh: Rễ thở tràm sinh ngẫu nhiên quanh gốc và cò trắng đậu trên ngọn
                    CreateBreathingRootsAroundTree(new Vector3(xPos, -1.8f + 3.5f, zPos), forestContainer.transform, Random.Range(2, 6));
                    CreateBirdInTree(new Vector3(xPos, -1.8f + 3.5f, zPos), forestTree.transform);
                    
                    GameObject trunkCol = new GameObject("TrunkCollider");
                    trunkCol.transform.SetParent(forestTree.transform, false);
                    trunkCol.transform.localScale = new Vector3(1f/rndScale, 1f/rndScale, 1f/rndScale);
                    var col = trunkCol.AddComponent<CapsuleCollider>();
                    col.center = new Vector3(0f, 1.5f, 0f);
                    col.radius = 0.5f;
                    col.height = 4.0f;
                }
            }

            // Lily pads & reeds for Phase 4 (swamp sanctuary water)
            GameObject lilyContainer = new GameObject("FloatingLilyPads");
            Material lilyMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            lilyMat.color = new Color(0.12f, 0.45f, 0.22f);
            for (int i = 0; i < 60; i++)
            {
                float zPos = Random.Range(-50f, 50f);
                float xPos = Random.Range(-20f, 60f);
                float yPos = -0.98f;
                
                GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pad.name = "LilyPad_" + i;
                pad.transform.SetParent(lilyContainer.transform);
                pad.transform.position = new Vector3(xPos, yPos, zPos);
                float padScale = Random.Range(0.6f, 1.4f);
                pad.transform.localScale = new Vector3(padScale, 0.01f, padScale);
                pad.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                DestroyImmediate(pad.GetComponent<Collider>());
                pad.GetComponent<Renderer>().sharedMaterial = lilyMat;
                
                if (Random.value < 0.35f)
                {
                    CreateLotusFlower(new Vector3(xPos, yPos, zPos), pad.transform);
                }
            }

            // Setup Player
            GameObject player = new GameObject("Player");
            player.tag = "Player";
            float playerStartY = GetHeightAt(20f, -48f) + 1.0f; // Spawn slightly above organic terrain
            player.transform.position = new Vector3(20f, playerStartY, -48f);

            var charController = player.AddComponent<CharacterController>();
            charController.height = 2f;
            charController.radius = 0.4f;
            charController.center = new Vector3(0, 1f, 0);

            InputActionAsset inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            var playerInput = player.AddComponent<PlayerInput>();
            if (inputAsset != null)
            {
                playerInput.actions = inputAsset;
                playerInput.defaultActionMap = "Player";
            }
            var playerCtrl = player.AddComponent<PlayerController>();
            var playerInteract = player.AddComponent<PlayerInteraction>();

            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            camObj.transform.SetParent(player.transform, false);
            camObj.transform.localPosition = new Vector3(0, 1.8f, 0);
            camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
            var cameraData = camObj.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            var photoCam = camObj.AddComponent<PhotoCamera>();

            GameObject cameraHandModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cameraHandModel.name = "CameraHandModel";
            cameraHandModel.transform.SetParent(camObj.transform, false);
            cameraHandModel.transform.localPosition = new Vector3(0.2f, -0.25f, 0.4f);
            cameraHandModel.transform.localScale = new Vector3(0.12f, 0.08f, 0.1f);
            DestroyImmediate(cameraHandModel.GetComponent<BoxCollider>());

            SerializedObject serPlayer = new SerializedObject(playerCtrl);
            serPlayer.FindProperty("playerCamera").objectReferenceValue = camObj.transform;
            serPlayer.ApplyModifiedProperties();
            var serInteract = new SerializedObject(playerInteract);
            serInteract.FindProperty("cameraTransform").objectReferenceValue = camObj.transform;
            serInteract.FindProperty("interactableLayer").intValue = 1 << LayerMask.NameToLayer("Interactable");
            serInteract.ApplyModifiedProperties();

            GameObject gameUI = CreateBaseGameUI(photoCam, cameraHandModel, out TextMeshProUGUI objText);

            // Grandpa NPC (Dialogue guide) - Stand on organic ground
            float grandpaY = GetHeightAt(22.0f, -46f);
            GameObject grandpa = LoadAndInstantiate("Assets/Models/VietnameseGrandpa/Meshy_AI_Old_Man_with_Open_Arm_biped/Meshy_AI_Old_Man_with_Open_Arm_biped_Character_output.glb", "Grandpa_NPC", new Vector3(22.0f, grandpaY, -46f), Quaternion.identity);
            if (grandpa != null)
            {
                grandpa.transform.localScale = new Vector3(0.85f, 0.85f, 0.85f);
                var col = grandpa.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0, 0.9f, 0);
                col.radius = 0.35f;
                col.height = 1.8f;
                SetupGrandpaAnimator(grandpa);
            }

            // Spawn animals
            List<AnimalAI> spawnedFauna = new List<AnimalAI>();
            System.Action<string, AnimalAI.AnimalType, Vector3, float, float> spawnAnimal = (name, type, pos, speed, range) => {
                GameObject obj = null;
                if (type == AnimalAI.AnimalType.Stork)
                {
                    obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    obj.transform.localScale = new Vector3(0.5f, 0.3f, 0.8f);
                }
                else if (type == AnimalAI.AnimalType.Snake)
                {
                    obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    obj.transform.localScale = new Vector3(0.12f, 0.8f, 0.12f);
                    obj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                }
                else if (type == AnimalAI.AnimalType.Fish)
                {
                    obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    obj.transform.localScale = new Vector3(0.15f, 0.6f, 0.15f);
                }
                else if (type == AnimalAI.AnimalType.Butterfly)
                {
                    obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    obj.transform.localScale = new Vector3(0.15f, 0.05f, 0.25f);
                }
                else if (type == AnimalAI.AnimalType.Duck)
                {
                    obj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    obj.transform.localScale = new Vector3(0.4f, 0.25f, 0.5f);
                }

                if (obj != null)
                {
                    obj.name = name;
                    obj.layer = LayerMask.NameToLayer("Interactable");
                    obj.transform.position = pos;
                    
                    Material animMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    if (type == AnimalAI.AnimalType.Stork) animMat.color = Color.white;
                    else if (type == AnimalAI.AnimalType.Snake) animMat.color = new Color(0.15f, 0.35f, 0.18f);
                    else if (type == AnimalAI.AnimalType.Fish) animMat.color = new Color(0.18f, 0.22f, 0.42f);
                    else if (type == AnimalAI.AnimalType.Butterfly) animMat.color = new Color(0.85f, 0.35f, 0.72f);
                    else if (type == AnimalAI.AnimalType.Duck) animMat.color = new Color(0.42f, 0.32f, 0.18f);
                    obj.GetComponent<Renderer>().sharedMaterial = animMat;

                    var sCol = obj.AddComponent<SphereCollider>();
                    sCol.isTrigger = true;
                    sCol.radius = 1.2f;

                    var ai = obj.AddComponent<AnimalAI>();
                    var serAI = new SerializedObject(ai);
                    serAI.FindProperty("animalType").intValue = (int)type;
                    serAI.FindProperty("speed").floatValue = speed;
                    serAI.FindProperty("range").floatValue = range;
                    serAI.ApplyModifiedProperties();

                    spawnedFauna.Add(ai);
                }
            };

            // Explicitly spawn cajuput trees for the perched storks to sit on
            float tree1Y = GetHeightAt(14f, -12f);
            GameObject stork1Tree = LoadAndInstantiate("Assets/Models/TeaTree/low-poly+tree+3d+model.glb", "TeaTree_Stork1", new Vector3(14f, tree1Y + 3.5f, -12f), Quaternion.Euler(0, 45f, 0));
            if (stork1Tree != null)
            {
                stork1Tree.transform.SetParent(forestContainer.transform);
                stork1Tree.transform.localScale = new Vector3(11f, 11f, 11f);
                stork1Tree.AddComponent<WindSway>();
                GameObject trunkCol = new GameObject("TrunkCollider");
                trunkCol.transform.SetParent(stork1Tree.transform, false);
                trunkCol.transform.localScale = new Vector3(1f/11f, 1f/11f, 1f/11f);
                var col = trunkCol.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0f, 1.5f, 0f);
                col.radius = 0.5f;
                col.height = 4.0f;
            }

            float tree2Y = GetHeightAt(28f, 15f);
            GameObject stork2Tree = LoadAndInstantiate("Assets/Models/TeaTree/low-poly+tree+3d+model.glb", "TeaTree_Stork2", new Vector3(28f, tree2Y + 3.5f, 15f), Quaternion.Euler(0, -30f, 0));
            if (stork2Tree != null)
            {
                stork2Tree.transform.SetParent(forestContainer.transform);
                stork2Tree.transform.localScale = new Vector3(11f, 11f, 11f);
                stork2Tree.AddComponent<WindSway>();
                GameObject trunkCol = new GameObject("TrunkCollider");
                trunkCol.transform.SetParent(stork2Tree.transform, false);
                trunkCol.transform.localScale = new Vector3(1f/11f, 1f/11f, 1f/11f);
                var col = trunkCol.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0f, 1.5f, 0f);
                col.radius = 0.5f;
                col.height = 4.0f;
            }

            // 6 con cò trắng lội trong kênh rải đều theo chiều dài
            spawnAnimal("WadingStork_1", AnimalAI.AnimalType.Stork, new Vector3(20f, -0.3f, -25f), 3.0f, 3f);
            spawnAnimal("WadingStork_2", AnimalAI.AnimalType.Stork, new Vector3(22f, -0.3f, -12f), 3.0f, 3f);
            spawnAnimal("WadingStork_3", AnimalAI.AnimalType.Stork, new Vector3(21f, -0.3f,  -2f), 3.0f, 3f);
            spawnAnimal("WadingStork_4", AnimalAI.AnimalType.Stork, new Vector3(23f, -0.3f,  10f), 3.0f, 3f);
            spawnAnimal("WadingStork_5", AnimalAI.AnimalType.Stork, new Vector3(20f, -0.3f,  20f), 3.0f, 3f);
            spawnAnimal("WadingStork_6", AnimalAI.AnimalType.Stork, new Vector3(24f, -0.3f,  30f), 3.0f, 3f);
            
            // Multiple snakes swimming in different parts of the canal
            spawnAnimal("SwimmingSnake_1", AnimalAI.AnimalType.Snake, new Vector3(22f, -0.95f, -22f), 2.0f, 5f);
            spawnAnimal("SwimmingSnake_2", AnimalAI.AnimalType.Snake, new Vector3(24f, -0.95f, 2f), 1.8f, 4.5f);

            // A school of multiple jumping fish in the canal/ponds!
            spawnAnimal("JumpingFish_1", AnimalAI.AnimalType.Fish, new Vector3(18f, -1.5f, -4f), 0f, 0f);
            spawnAnimal("JumpingFish_2", AnimalAI.AnimalType.Fish, new Vector3(25f, -1.5f, -15f), 0f, 0f);
            spawnAnimal("JumpingFish_3", AnimalAI.AnimalType.Fish, new Vector3(21f, -1.5f, 10f), 0f, 0f);
            spawnAnimal("JumpingFish_4", AnimalAI.AnimalType.Fish, new Vector3(27f, -1.5f, -30f), 0f, 0f);
            spawnAnimal("JumpingFish_5", AnimalAI.AnimalType.Fish, new Vector3(23f, -1.5f, 22f), 0f, 0f);

            spawnAnimal("FlyingButterfly_1", AnimalAI.AnimalType.Butterfly, new Vector3(16f, 2.5f, 8f), 2.5f, 5.0f);
            
            // Multiple ducks swimming around
            spawnAnimal("FloatingDuck_1", AnimalAI.AnimalType.Duck, new Vector3(25f, -0.95f, 6f), 1.5f, 4f);
            spawnAnimal("FloatingDuck_2", AnimalAI.AnimalType.Duck, new Vector3(22f, -0.95f, -8f), 1.7f, 3.5f);
            spawnAnimal("FloatingDuck_3", AnimalAI.AnimalType.Duck, new Vector3(26f, -0.95f, 18f), 1.4f, 4.2f);

            // Spawn wooden signs as visual checkpoint indicators for each animal habitat
            float snakeSignY = GetHeightAt(18f, -22f);
            CreateScenicWoodenSign("Đầm Rắn Nước", new Vector3(18f, snakeSignY, -22f), 45f);

            float stork1SignY = GetHeightAt(17f, -12f);
            CreateScenicWoodenSign("Nơi Cò Trắng Đậu", new Vector3(17f, stork1SignY, -12f), -45f);

            float fishSignY = GetHeightAt(15f, -4f);
            CreateScenicWoodenSign("Vùng Cá Lóc Nhảy", new Vector3(15f, fishSignY, -4f), 90f);

            float duckSignY = GetHeightAt(20f, 6f);
            CreateScenicWoodenSign("Đầm Vịt Trời", new Vector3(20f, duckSignY, 6f), -90f);

            float butterflySignY = GetHeightAt(13f, 8f);
            CreateScenicWoodenSign("Bướm Hoa Súng", new Vector3(13f, butterflySignY, 8f), 0f);

            float stork2SignY = GetHeightAt(23f, 15f);
            CreateScenicWoodenSign("Nơi Cò Trắng Đậu 2", new Vector3(23f, stork2SignY, 15f), 180f);

            // Managers
            GameObject managersObj = new GameObject("Managers");
            managersObj.AddComponent<DialogueManager>();
            var p4Manager = managersObj.AddComponent<Phase4Manager>();

            var serPhase4 = new SerializedObject(p4Manager);
            serPhase4.FindProperty("player").objectReferenceValue = player.transform;
            serPhase4.FindProperty("objectiveText").objectReferenceValue = objText;
            
            var animsProp = serPhase4.FindProperty("animals");
            animsProp.ClearArray();
            for (int i = 0; i < spawnedFauna.Count; i++)
            {
                animsProp.InsertArrayElementAtIndex(i);
                animsProp.GetArrayElementAtIndex(i).objectReferenceValue = spawnedFauna[i];
            }
            serPhase4.ApplyModifiedProperties();

            SetupPostProcessingAndFog(camObj);

            EditorSceneManager.SaveScene(newScene, "Assets/Scenes/Phase4_Sanctuary.unity");
            SceneBeautifierAll.BeautifyPhase4();
            Debug.Log("Successfully created Phase 4 scene!");
        }

        [MenuItem("Rung Tram Tra Su/Setup Phase 5 Scene")]
        public static void CreatePhase5Scene()
        {
            AssetDatabase.Refresh();
            CreateLayer("Interactable");
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            GameObject defaultCam = GameObject.FindWithTag("MainCamera");
            if (defaultCam != null) DestroyImmediate(defaultCam);

            GameObject dirLight = GameObject.Find("Directional Light");
            if (dirLight != null)
            {
                dirLight.transform.rotation = Quaternion.Euler(20, -55, 0);
                var lightComp = dirLight.GetComponent<Light>();
                if (lightComp != null)
                {
                    lightComp.color = new Color(1.0f, 0.95f, 0.82f);
                    lightComp.intensity = 1.5f;
                    lightComp.shadows = LightShadows.Soft;
                }
            }

            Texture2D grassTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Textures/grass_dirt_texture.png");
            Material grassMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (grassTex != null)
            {
                grassMat.mainTexture = grassTex;
                grassMat.mainTextureScale = new Vector2(20f, 20f);
            }
            else grassMat.color = new Color(0.12f, 0.28f, 0.14f);

            Material woodMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            woodMat.color = new Color(0.32f, 0.2f, 0.11f);

            // A. Grid terrain (beautiful organic terrain with flat center for tower!)
            GameObject terrainObj = new GameObject("OrganicTerrain_Bank");
            MeshFilter meshFilter = terrainObj.AddComponent<MeshFilter>();
            MeshRenderer meshRenderer = terrainObj.AddComponent<MeshRenderer>();
            MeshCollider meshCollider = terrainObj.AddComponent<MeshCollider>();

            Mesh terrainMesh = new Mesh();
            int xSegments = 120;
            int zSegments = 120;
            int numVertices = (xSegments + 1) * (zSegments + 1);
            Vector3[] vertices = new Vector3[numVertices];
            Vector2[] uvs = new Vector2[numVertices];
            int[] triangles = new int[xSegments * zSegments * 6];

            float xMin = -55f, xMax = 55f, zMin = -65f, zMax = 65f;
            int vIndex = 0;
            for (int z = 0; z <= zSegments; z++)
            {
                float zPct = (float)z / zSegments;
                float zPos = Mathf.Lerp(zMin, zMax, zPct);
                for (int x = 0; x <= xSegments; x++)
                {
                    float xPct = (float)x / xSegments;
                    float xPos = Mathf.Lerp(xMin, xMax, xPct);
                    float yPos = SceneBeautifierAll.GetPhase5HeightAt(xPos, zPos);
                    vertices[vIndex] = new Vector3(xPos, yPos, zPos);
                    uvs[vIndex] = new Vector2(xPos * 0.12f, zPos * 0.12f);
                    vIndex++;
                }
            }

            int tIndex = 0;
            for (int z = 0; z < zSegments; z++)
            {
                for (int x = 0; x < xSegments; x++)
                {
                    int row1 = z * (xSegments + 1);
                    int row2 = (z + 1) * (xSegments + 1);
                    triangles[tIndex++] = row1 + x;
                    triangles[tIndex++] = row2 + x;
                    triangles[tIndex++] = row1 + x + 1;
                    triangles[tIndex++] = row1 + x + 1;
                    triangles[tIndex++] = row2 + x;
                    triangles[tIndex++] = row2 + x + 1;
                }
            }
            terrainMesh.vertices = vertices;
            terrainMesh.uv = uvs;
            terrainMesh.triangles = triangles;
            terrainMesh.RecalculateNormals();
            meshFilter.sharedMesh = terrainMesh;
            meshCollider.sharedMesh = terrainMesh;
            meshRenderer.sharedMaterial = grassMat;

            // Observation Tower (Premium structure with corner posts, landing platforms, straight stairs and smooth ramp colliders)
            Vector3 towerCenter = new Vector3(25f, -0.5f, 15f);
            
            // Try to load high-quality wood material
            Material customWoodMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/BoardwalkWoodMat.mat");
            Material activeWoodMat = customWoodMat != null ? customWoodMat : woodMat;

            GameObject towerContainer = new GameObject("ObservationTower");

            // 1. Spawning 4 giant corner posts (Pillars)
            Vector3[] pillarPositions = new Vector3[]
            {
                new Vector3(21.5f, 5.75f, 11.5f), // LF
                new Vector3(28.5f, 5.75f, 11.5f), // RF
                new Vector3(28.5f, 5.75f, 18.5f), // RB
                new Vector3(21.5f, 5.75f, 18.5f)  // LB
            };
            for (int i = 0; i < pillarPositions.Length; i++)
            {
                GameObject pillarObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillarObj.name = (i == 0) ? "ObservationTower_Pillar" : "ObservationTower_Pillar_" + i;
                pillarObj.transform.SetParent(towerContainer.transform);
                pillarObj.transform.position = pillarPositions[i];
                pillarObj.transform.localScale = new Vector3(0.5f, 6.25f, 0.5f);
                pillarObj.GetComponent<Renderer>().sharedMaterial = activeWoodMat;
            }

            // 2. Horizontal support beams connecting the posts at different heights
            float[] beamHeights = new float[] { 3.0f, 6.8f, 10.65f };
            foreach (float h in beamHeights)
            {
                // Left & Right beams
                GameObject leftBeam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leftBeam.name = "SupportBeam_Left_" + h;
                leftBeam.transform.SetParent(towerContainer.transform);
                leftBeam.transform.position = new Vector3(21.5f, h, 15.0f);
                leftBeam.transform.localScale = new Vector3(0.25f, 0.25f, 7.0f);
                leftBeam.GetComponent<Renderer>().sharedMaterial = activeWoodMat;
                Object.DestroyImmediate(leftBeam.GetComponent<Collider>());

                GameObject rightBeam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rightBeam.name = "SupportBeam_Right_" + h;
                rightBeam.transform.SetParent(towerContainer.transform);
                rightBeam.transform.position = new Vector3(28.5f, h, 15.0f);
                rightBeam.transform.localScale = new Vector3(0.25f, 0.25f, 7.0f);
                rightBeam.GetComponent<Renderer>().sharedMaterial = activeWoodMat;
                Object.DestroyImmediate(rightBeam.GetComponent<Collider>());

                // Front & Back beams
                GameObject frontBeam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                frontBeam.name = "SupportBeam_Front_" + h;
                frontBeam.transform.SetParent(towerContainer.transform);
                frontBeam.transform.position = new Vector3(25.0f, h, 11.5f);
                frontBeam.transform.localScale = new Vector3(7.0f, 0.25f, 0.25f);
                frontBeam.GetComponent<Renderer>().sharedMaterial = activeWoodMat;
                Object.DestroyImmediate(frontBeam.GetComponent<Collider>());

                GameObject backBeam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                backBeam.name = "SupportBeam_Back_" + h;
                backBeam.transform.SetParent(towerContainer.transform);
                backBeam.transform.position = new Vector3(25.0f, h, 18.5f);
                backBeam.transform.localScale = new Vector3(7.0f, 0.25f, 0.25f);
                backBeam.GetComponent<Renderer>().sharedMaterial = activeWoodMat;
                Object.DestroyImmediate(backBeam.GetComponent<Collider>());
            }

            // 3. Spawning landing platforms
            GameObject landing1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            landing1.name = "ObservationTower_Landing_1";
            landing1.transform.SetParent(towerContainer.transform);
            landing1.transform.position = new Vector3(21.5f, 3.0f, 18.5f);
            landing1.transform.localScale = new Vector3(2.5f, 0.2f, 2.5f);
            landing1.GetComponent<Renderer>().sharedMaterial = activeWoodMat;

            GameObject landing2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            landing2.name = "ObservationTower_Landing_2";
            landing2.transform.SetParent(towerContainer.transform);
            landing2.transform.position = new Vector3(28.5f, 6.8f, 18.5f);
            landing2.transform.localScale = new Vector3(2.5f, 0.2f, 2.5f);
            landing2.GetComponent<Renderer>().sharedMaterial = activeWoodMat;

            // 4. Spawning Viewing Deck Platform
            GameObject deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
            deck.name = "ViewingDeck_Platform";
            deck.transform.SetParent(towerContainer.transform);
            deck.transform.position = new Vector3(25.0f, 10.65f, 15.0f);
            deck.transform.localScale = new Vector3(7.4f, 0.2f, 7.4f);
            deck.GetComponent<Renderer>().sharedMaterial = activeWoodMat;

            // 5. Spawning straight stair flights (using smooth ramp helper)
            CreateStairFlight("StairFlight_1", new Vector3(21.5f, -0.5f, 11.5f), new Vector3(21.5f, 3.0f, 17.25f), 1.2f, activeWoodMat, towerContainer);
            CreateStairFlight("StairFlight_2", new Vector3(22.75f, 3.0f, 18.5f), new Vector3(27.25f, 6.8f, 18.5f), 1.2f, activeWoodMat, towerContainer);
            CreateStairFlight("StairFlight_3", new Vector3(28.5f, 6.8f, 17.25f), new Vector3(28.5f, 10.65f, 12.5f), 1.2f, activeWoodMat, towerContainer);

            // 6. Spawning safety railings for Landings and Top Deck
            // Landing 1 Railings
            GameObject land1Rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            land1Rail.name = "Landing1_Rail";
            land1Rail.transform.SetParent(towerContainer.transform);
            land1Rail.transform.position = new Vector3(20.3f, 3.6f, 18.5f);
            land1Rail.transform.localScale = new Vector3(0.08f, 1.0f, 2.5f);
            land1Rail.GetComponent<Renderer>().sharedMaterial = activeWoodMat;

            // Landing 2 Railings
            GameObject land2Rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            land2Rail.name = "Landing2_Rail";
            land2Rail.transform.SetParent(towerContainer.transform);
            land2Rail.transform.position = new Vector3(29.7f, 7.4f, 18.5f);
            land2Rail.transform.localScale = new Vector3(0.08f, 1.0f, 2.5f);
            land2Rail.GetComponent<Renderer>().sharedMaterial = activeWoodMat;

            // Top Deck Railings
            Vector3[] deckRailPos = new Vector3[]
            {
                new Vector3(21.3f, 11.25f, 15.0f),
                new Vector3(25.0f, 11.25f, 18.7f),
                new Vector3(24.3f, 11.25f, 11.3f), // front left
                new Vector3(28.7f, 11.25f, 15.8f)  // right back
            };
            Vector3[] deckRailScales = new Vector3[]
            {
                new Vector3(0.08f, 1.0f, 7.4f),
                new Vector3(7.4f, 1.0f, 0.08f),
                new Vector3(6.0f, 1.0f, 0.08f), // leaves entrance open on right
                new Vector3(0.08f, 1.0f, 5.8f)  // leaves entrance open on front
            };
            for (int i = 0; i < 4; i++)
            {
                GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rail.name = "DeckRail_" + i;
                rail.transform.SetParent(towerContainer.transform);
                rail.transform.position = deckRailPos[i];
                rail.transform.localScale = deckRailScales[i];
                rail.GetComponent<Renderer>().sharedMaterial = activeWoodMat;
            }

            // 7. Spawning Roof support posts and sloped A-frame Gable Roof
            Vector3[] roofPillarPos = new Vector3[]
            {
                new Vector3(21.5f, 12.125f, 11.5f),
                new Vector3(28.5f, 12.125f, 11.5f),
                new Vector3(28.5f, 12.125f, 18.5f),
                new Vector3(21.5f, 12.125f, 18.5f)
            };
            for (int i = 0; i < roofPillarPos.Length; i++)
            {
                GameObject rp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                rp.name = "RoofPillar_" + i;
                rp.transform.SetParent(towerContainer.transform);
                rp.transform.position = roofPillarPos[i];
                rp.transform.localScale = new Vector3(0.25f, 1.375f, 0.25f);
                rp.GetComponent<Renderer>().sharedMaterial = activeWoodMat;
            }

            GameObject roofLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roofLeft.name = "Roof_Left";
            roofLeft.transform.SetParent(towerContainer.transform);
            roofLeft.transform.position = new Vector3(23.2f, 13.7f, 15.0f);
            roofLeft.transform.rotation = Quaternion.Euler(0f, 0f, 15f);
            roofLeft.transform.localScale = new Vector3(4.2f, 0.15f, 8.0f);
            roofLeft.GetComponent<Renderer>().sharedMaterial = activeWoodMat;

            GameObject roofRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roofRight.name = "Roof_Right";
            roofRight.transform.SetParent(towerContainer.transform);
            roofRight.transform.position = new Vector3(26.8f, 13.7f, 15.0f);
            roofRight.transform.rotation = Quaternion.Euler(0f, 0f, -15f);
            roofRight.transform.localScale = new Vector3(4.2f, 0.15f, 8.0f);
            roofRight.GetComponent<Renderer>().sharedMaterial = activeWoodMat;

            // Biển báo gỗ mộc mạc tại vọng cảnh đài
            float signY = SceneBeautifierAll.GetPhase5HeightAt(towerCenter.x + 3f, 10f);
            CreateScenicWoodenSign("Vọng Cảnh Đài\n(Đỉnh Kính Vọng)", new Vector3(towerCenter.x + 3f, signY, 10f), -20f);

            // Spawning 120 trees surrounding the clearing (circle boundary forest)
            Random.InitState(888);
            GameObject forestContainer = new GameObject("TeaTree_TowerForest");
            for (int i = 0; i < 120; i++)
            {
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float radius = Random.Range(24f, 42f);
                float xPos = towerCenter.x + Mathf.Cos(angle) * radius;
                float zPos = towerCenter.z + Mathf.Sin(angle) * radius;
                float yPos = SceneBeautifierAll.GetPhase5HeightAt(xPos, zPos);

                GameObject forestTree = LoadAndInstantiate("Assets/Models/TeaTree/low-poly+tree+3d+model.glb", "TeaTree_" + i, new Vector3(xPos, yPos + 3.5f, zPos), Quaternion.Euler(0, Random.Range(0f, 360f), 0));
                if (forestTree != null)
                {
                    forestTree.transform.SetParent(forestContainer.transform);
                    float rndScale = Random.Range(9.5f, 13.5f);
                    forestTree.transform.localScale = new Vector3(rndScale, rndScale, rndScale);
                    forestTree.AddComponent<WindSway>();

                    // Phát triển ngoại cảnh: Rễ thở tràm sinh ngẫu nhiên quanh gốc và cò trắng đậu trên ngọn
                    CreateBreathingRootsAroundTree(new Vector3(xPos, yPos + 3.5f, zPos), forestContainer.transform, Random.Range(2, 6));
                    CreateBirdInTree(new Vector3(xPos, yPos + 3.5f, zPos), forestTree.transform);
                }
            }

            // Sunset target
            GameObject sunsetObj = new GameObject("SunsetQuestTarget");
            sunsetObj.transform.position = new Vector3(35f, 16f, 120f);
            sunsetObj.layer = LayerMask.NameToLayer("Interactable");
            var sunsetCol = sunsetObj.AddComponent<SphereCollider>();
            sunsetCol.isTrigger = true;
            sunsetCol.radius = 15f;

            // Setup Player
            GameObject player = new GameObject("Player");
            player.tag = "Player";
            player.transform.position = towerCenter + new Vector3(2.8f, 0.5f, 0f);

            var charController = player.AddComponent<CharacterController>();
            charController.height = 2f;
            charController.radius = 0.4f;
            charController.center = new Vector3(0, 1f, 0);

            InputActionAsset inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            var playerInput = player.AddComponent<PlayerInput>();
            if (inputAsset != null)
            {
                playerInput.actions = inputAsset;
                playerInput.defaultActionMap = "Player";
            }
            var playerCtrl = player.AddComponent<PlayerController>();
            var playerInteract = player.AddComponent<PlayerInteraction>();

            GameObject camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            camObj.transform.SetParent(player.transform, false);
            camObj.transform.localPosition = new Vector3(0, 1.8f, 0);
            camObj.AddComponent<Camera>();
            camObj.AddComponent<AudioListener>();
            var cameraData = camObj.AddComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            var photoCam = camObj.AddComponent<PhotoCamera>();

            GameObject cameraHandModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cameraHandModel.name = "CameraHandModel";
            cameraHandModel.transform.SetParent(camObj.transform, false);
            cameraHandModel.transform.localPosition = new Vector3(0.2f, -0.25f, 0.4f);
            cameraHandModel.transform.localScale = new Vector3(0.12f, 0.08f, 0.1f);
            DestroyImmediate(cameraHandModel.GetComponent<BoxCollider>());

            SerializedObject serPlayer = new SerializedObject(playerCtrl);
            serPlayer.FindProperty("playerCamera").objectReferenceValue = camObj.transform;
            serPlayer.ApplyModifiedProperties();
            var serInteract = new SerializedObject(playerInteract);
            serInteract.FindProperty("cameraTransform").objectReferenceValue = camObj.transform;
            serInteract.FindProperty("interactableLayer").intValue = 1 << LayerMask.NameToLayer("Interactable");
            serInteract.ApplyModifiedProperties();

            GameObject gameUI = CreateBaseGameUI(photoCam, cameraHandModel, out TextMeshProUGUI objText);

            // Grandpa NPC standing on the top deck
            GameObject grandpa = LoadAndInstantiate("Assets/Models/VietnameseGrandpa/Meshy_AI_Old_Man_with_Open_Arm_biped/Meshy_AI_Old_Man_with_Open_Arm_biped_Character_output.glb", "Grandpa_NPC", towerCenter + new Vector3(0.8f, 11.25f, 0.8f), Quaternion.Euler(0, 135, 0));
            if (grandpa != null)
            {
                grandpa.transform.localScale = new Vector3(0.85f, 0.85f, 0.85f);
                var col = grandpa.AddComponent<CapsuleCollider>();
                col.center = new Vector3(0, 0.9f, 0);
                col.radius = 0.35f;
                col.height = 1.8f;
                SetupGrandpaAnimator(grandpa);
            }

            // Build Ending Diary Canvas
            GameObject diaryCanvas = new GameObject("DiaryCanvas");
            var dCanvas = diaryCanvas.AddComponent<Canvas>();
            dCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            diaryCanvas.AddComponent<CanvasScaler>();
            diaryCanvas.AddComponent<GraphicRaycaster>();
            diaryCanvas.transform.SetParent(gameUI.transform, false);

            GameObject bgPanel = new GameObject("BackgroundPanel");
            bgPanel.transform.SetParent(diaryCanvas.transform, false);
            var bgRect = bgPanel.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0.05f, 0.05f);
            bgRect.anchorMax = new Vector2(0.95f, 0.95f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImg = bgPanel.AddComponent<Image>();
            bgImg.color = new Color(0.12f, 0.1f, 0.08f, 0.96f);

            RawImage[] polaroids = new RawImage[5];
            for (int i = 0; i < 5; i++)
            {
                GameObject polFrame = new GameObject("Polaroid_" + i);
                polFrame.transform.SetParent(bgPanel.transform, false);
                var pRect = polFrame.AddComponent<RectTransform>();
                pRect.anchorMin = new Vector2(0.1f + i * 0.2f, 0.72f);
                pRect.anchorMax = new Vector2(0.1f + i * 0.2f, 0.72f);
                pRect.pivot = new Vector2(0.5f, 0.5f);
                pRect.anchoredPosition = Vector2.zero;
                pRect.sizeDelta = new Vector2(150f, 175f);
                var frameImg = polFrame.AddComponent<Image>();
                frameImg.color = Color.white;

                GameObject polImg = new GameObject("Photo");
                polImg.transform.SetParent(polFrame.transform, false);
                var piRect = polImg.AddComponent<RectTransform>();
                piRect.anchorMin = new Vector2(0.05f, 0.15f);
                piRect.anchorMax = new Vector2(0.95f, 0.95f);
                piRect.offsetMin = Vector2.zero;
                piRect.offsetMax = Vector2.zero;
                var rImg = polImg.AddComponent<RawImage>();
                rImg.color = Color.gray;
                polaroids[i] = rImg;
            }

            GameObject diaryTextObj = new GameObject("DiaryText");
            diaryTextObj.transform.SetParent(bgPanel.transform, false);
            var dtRect = diaryTextObj.AddComponent<RectTransform>();
            dtRect.anchorMin = new Vector2(0.15f, 0.18f);
            dtRect.anchorMax = new Vector2(0.85f, 0.48f);
            dtRect.offsetMin = Vector2.zero;
            dtRect.offsetMax = Vector2.zero;
            var dText = diaryTextObj.AddComponent<TextMeshProUGUI>();
            dText.fontSize = 20;
            dText.color = new Color(0.95f, 0.9f, 0.82f);
            dText.text = "Cuốn sổ nhật ký hành trình...";

            GameObject replayBtnObj = new GameObject("ReplayButton");
            replayBtnObj.transform.SetParent(bgPanel.transform, false);
            var rbRect = replayBtnObj.AddComponent<RectTransform>();
            rbRect.anchorMin = new Vector2(0.5f, 0.08f);
            rbRect.anchorMax = new Vector2(0.5f, 0.08f);
            rbRect.pivot = new Vector2(0.5f, 0.5f);
            rbRect.anchoredPosition = Vector2.zero;
            rbRect.sizeDelta = new Vector2(180, 45);
            var rbImg = replayBtnObj.AddComponent<Image>();
            rbImg.color = new Color(0.25f, 0.45f, 0.28f);
            var btn = replayBtnObj.AddComponent<Button>();
            
            GameObject btnTextObj = new GameObject("Text");
            btnTextObj.transform.SetParent(replayBtnObj.transform, false);
            var btRect = btnTextObj.AddComponent<RectTransform>();
            btRect.anchorMin = Vector2.zero;
            btRect.anchorMax = Vector2.one;
            btRect.offsetMin = Vector2.zero;
            btRect.offsetMax = Vector2.zero;
            var btText = btnTextObj.AddComponent<TextMeshProUGUI>();
            btText.text = "Chơi Lại";
            btText.fontSize = 18;
            btText.alignment = TextAlignmentOptions.Center;
            btText.color = Color.white;

            // Managers
            GameObject managersObj = new GameObject("Managers");
            managersObj.AddComponent<DialogueManager>();
            var p5Manager = managersObj.AddComponent<Phase5Manager>();

            var serPhase5 = new SerializedObject(p5Manager);
            serPhase5.FindProperty("player").objectReferenceValue = player.transform;
            serPhase5.FindProperty("grandpa").objectReferenceValue = grandpa.transform;
            serPhase5.FindProperty("dirLight").objectReferenceValue = dirLight.GetComponent<Light>();
            serPhase5.FindProperty("objectiveText").objectReferenceValue = objText;
            serPhase5.FindProperty("photoCamera").objectReferenceValue = photoCam;
            serPhase5.FindProperty("sunsetTarget").objectReferenceValue = sunsetObj.transform;
            serPhase5.FindProperty("diaryCanvas").objectReferenceValue = diaryCanvas;
            
            var pArrayProp = serPhase5.FindProperty("polaroidImages");
            pArrayProp.ClearArray();
            for (int i = 0; i < polaroids.Length; i++)
            {
                pArrayProp.InsertArrayElementAtIndex(i);
                pArrayProp.GetArrayElementAtIndex(i).objectReferenceValue = polaroids[i];
            }
            serPhase5.FindProperty("diaryText").objectReferenceValue = dText;
            serPhase5.FindProperty("replayButton").objectReferenceValue = btn;
            serPhase5.ApplyModifiedProperties();

            SetupPostProcessingAndFog(camObj);

            EditorSceneManager.SaveScene(newScene, "Assets/Scenes/Phase5_Sunset.unity");
            SceneBeautifierAll.BeautifyPhase5();
            Debug.Log("Successfully created Phase 5 scene!");
        }

        private static GameObject CreateBaseGameUI(PhotoCamera photoCam, GameObject cameraHandModel, out TextMeshProUGUI objectiveTextOut)
        {
            GameObject gameUI = new GameObject("GameUI");
            var canvas = gameUI.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            gameUI.AddComponent<CanvasScaler>();
            gameUI.AddComponent<GraphicRaycaster>();
            
            var interactUI = gameUI.AddComponent<InteractionUI>();

            GameObject promptPanel = new GameObject("InteractionPromptPanel");
            promptPanel.transform.SetParent(gameUI.transform, false);
            var promptRect = promptPanel.AddComponent<RectTransform>();
            promptRect.anchorMin = new Vector2(0.5f, 0.5f);
            promptRect.anchorMax = new Vector2(0.5f, 0.5f);
            promptRect.pivot = new Vector2(0.5f, 0.5f);
            promptRect.anchoredPosition = new Vector2(0, -60);
            promptRect.sizeDelta = new Vector2(400, 50);

            var promptText = promptPanel.AddComponent<TextMeshProUGUI>();
            promptText.alignment = TextAlignmentOptions.Center;
            promptText.fontSize = 20;
            promptText.color = Color.yellow;
            promptText.text = "[E] Nói chuyện";

            SerializedObject serIntUI = new SerializedObject(interactUI);
            serIntUI.FindProperty("promptPanel").objectReferenceValue = promptPanel;
            serIntUI.FindProperty("promptText").objectReferenceValue = promptText;
            serIntUI.ApplyModifiedProperties();

            GameObject objTextObj = new GameObject("ObjectiveText");
            objTextObj.transform.SetParent(gameUI.transform, false);
            var objRect = objTextObj.AddComponent<RectTransform>();
            objRect.anchorMin = new Vector2(0.5f, 1f);
            objRect.anchorMax = new Vector2(0.5f, 1f);
            objRect.pivot = new Vector2(0.5f, 1f);
            objRect.anchoredPosition = new Vector2(0, -40);
            objRect.sizeDelta = new Vector2(800, 60);

            var objText = objTextObj.AddComponent<TextMeshProUGUI>();
            objText.alignment = TextAlignmentOptions.Center;
            objText.fontSize = 22;
            objText.color = Color.white;
            objText.text = "Mục tiêu: Đang cập nhật...";
            objectiveTextOut = objText;

            GameObject viewfinderCanvas = new GameObject("ViewfinderCanvas");
            viewfinderCanvas.transform.SetParent(gameUI.transform, false);
            var vfRect = viewfinderCanvas.AddComponent<RectTransform>();
            vfRect.anchorMin = Vector2.zero;
            vfRect.anchorMax = Vector2.one;
            vfRect.sizeDelta = Vector2.zero;

            GameObject borderObj = new GameObject("ViewfinderBorder");
            borderObj.transform.SetParent(viewfinderCanvas.transform, false);
            var borderRect = borderObj.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.sizeDelta = Vector2.zero;
            var borderImg = borderObj.AddComponent<Image>();
            borderImg.color = new Color(0, 0, 0, 0.4f);

            GameObject recTextObj = new GameObject("RECText");
            recTextObj.transform.SetParent(viewfinderCanvas.transform, false);
            var recRect = recTextObj.AddComponent<RectTransform>();
            recRect.anchorMin = new Vector2(0.1f, 0.9f);
            recRect.anchorMax = new Vector2(0.1f, 0.9f);
            recRect.anchoredPosition = Vector2.zero;
            recRect.sizeDelta = new Vector2(100, 30);
            var recText = recTextObj.AddComponent<TextMeshProUGUI>();
            recText.text = "● REC";
            recText.color = Color.red;
            recText.fontSize = 20;

            GameObject flashObj = new GameObject("FlashImage");
            flashObj.transform.SetParent(gameUI.transform, false);
            var flashRect = flashObj.AddComponent<RectTransform>();
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.sizeDelta = Vector2.zero;
            var flashImg = flashObj.AddComponent<Image>();
            flashImg.color = new Color(1, 1, 1, 0);

            SerializedObject serCam = new SerializedObject(photoCam);
            serCam.FindProperty("viewfinderCanvas").objectReferenceValue = viewfinderCanvas;
            serCam.FindProperty("flashImage").objectReferenceValue = flashImg;
            serCam.FindProperty("normalFOV").floatValue = 60f;
            serCam.FindProperty("zoomFOV").floatValue = 30f;
            serCam.FindProperty("occlusionLayers").intValue = 1 << 0;
            serCam.ApplyModifiedProperties();

            GameObject diagPanel = new GameObject("DialoguePanel");
            diagPanel.transform.SetParent(gameUI.transform, false);
            var diagRect = diagPanel.AddComponent<RectTransform>();
            diagRect.anchorMin = new Vector2(0.5f, 0f);
            diagRect.anchorMax = new Vector2(0.5f, 0f);
            diagRect.pivot = new Vector2(0.5f, 0f);
            diagRect.anchoredPosition = new Vector2(0, 30);
            diagRect.sizeDelta = new Vector2(700, 160);
            var diagImg = diagPanel.AddComponent<Image>();
            diagImg.color = new Color(0.1f, 0.1f, 0.1f, 0.85f);

            GameObject speakerTextObj = new GameObject("SpeakerNameText");
            speakerTextObj.transform.SetParent(diagPanel.transform, false);
            var spkRect = speakerTextObj.AddComponent<RectTransform>();
            spkRect.anchorMin = new Vector2(0f, 1f);
            spkRect.anchorMax = new Vector2(0f, 1f);
            spkRect.pivot = new Vector2(0f, 1f);
            spkRect.anchoredPosition = new Vector2(15, -10);
            spkRect.sizeDelta = new Vector2(200, 30);
            var spkText = speakerTextObj.AddComponent<TextMeshProUGUI>();
            spkText.fontSize = 20;
            spkText.color = Color.green;
            spkText.text = "Ông Ngoại";

            GameObject dialogueTextObj = new GameObject("DialogueText");
            dialogueTextObj.transform.SetParent(diagPanel.transform, false);
            var textRect = dialogueTextObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(1f, 1f);
            textRect.offsetMin = new Vector2(15, 15);
            textRect.offsetMax = new Vector2(-15, -45);
            var diagTextComp = dialogueTextObj.AddComponent<TextMeshProUGUI>();
            diagTextComp.fontSize = 18;
            diagTextComp.color = Color.white;
            diagTextComp.text = "Đang chạy lời thoại...";

            GameObject continueIndicator = new GameObject("ContinueIndicator");
            continueIndicator.transform.SetParent(diagPanel.transform, false);
            var cntRect = continueIndicator.AddComponent<RectTransform>();
            cntRect.anchorMin = new Vector2(1f, 0f);
            cntRect.anchorMax = new Vector2(1f, 0f);
            cntRect.pivot = new Vector2(1f, 0f);
            cntRect.anchoredPosition = new Vector2(-15, 10);
            cntRect.sizeDelta = new Vector2(120, 25);
            var cntText = continueIndicator.AddComponent<TextMeshProUGUI>();
            cntText.fontSize = 14;
            cntText.color = Color.gray;
            cntText.text = "[Click / Space] Tiếp tục";

            GameObject faderCanvasObj = new GameObject("FaderCanvas");
            var faderCanvas = faderCanvasObj.AddComponent<Canvas>();
            faderCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            faderCanvas.sortingOrder = 999;
            faderCanvasObj.AddComponent<CanvasScaler>();
            faderCanvasObj.AddComponent<GraphicRaycaster>();
            var screenFader = faderCanvasObj.AddComponent<ScreenFader>();

            GameObject fadeImgObj = new GameObject("FadeImage");
            fadeImgObj.transform.SetParent(faderCanvasObj.transform, false);
            var fimgRect = fadeImgObj.AddComponent<RectTransform>();
            fimgRect.anchorMin = Vector2.zero;
            fimgRect.anchorMax = Vector2.one;
            fimgRect.sizeDelta = Vector2.zero;
            var fImg = fadeImgObj.AddComponent<Image>();
            fImg.color = Color.black;

            SerializedObject serFade = new SerializedObject(screenFader);
            serFade.FindProperty("fadeImage").objectReferenceValue = fImg;
            serFade.ApplyModifiedProperties();

            BuildDiaryAndPopupUI(gameUI);

            return gameUI;
        }

        private static void SetupPostProcessingAndFog(GameObject camObj)
        {
            // 1. Cấu hình Fog (Sương mù buổi sáng vùng sông nước miền Tây)
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.6f, 0.78f, 0.72f); // Sương mù trà xanh nhạt
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.015f; // Độ đậm sương mù nhẹ nhàng thanh thoát

            // 2. Áp dụng Skybox
            Material skyboxMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/EmaceArt/Slavic World Free/Skybox/Epic_BigCloudsSoft_V2/EA03_LowPolyBigClouds.mat");
            if (skyboxMat != null)
            {
                RenderSettings.skybox = skyboxMat;
            }
            else
            {
                Debug.LogWarning("Không tìm thấy Skybox material tại Assets/EmaceArt/Slavic World Free/Skybox/Epic_BigCloudsSoft_V2/EA03_LowPolyBigClouds.mat");
            }

            // 3. Cấu hình Ambient fill light để tránh bóng đổ quá đen tối màu (sử dụng Skybox cho trung thực)
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.25f;
            DynamicGI.UpdateEnvironment();

            // 4. Tạo đối tượng Global Volume chứa hậu kỳ
            GameObject volumeObj = new GameObject("Global PostProcess Volume");
            var volume = volumeObj.AddComponent<Volume>();
            volume.isGlobal = true;

            // Tải Volume Profile từ disk hoặc tạo mới nếu chưa tồn tại
            string profilePath = "Assets/Scenes/Phase1_VolumeProfile.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();

                // - ACES Tonemapping
                var tonemapping = profile.Add<Tonemapping>();
                tonemapping.active = true;
                tonemapping.mode.Override(TonemappingMode.ACES);

                // - Bloom (Nắng vàng tỏa rực rỡ qua sương sớm)
                var bloom = profile.Add<Bloom>();
                bloom.active = true;
                bloom.threshold.Override(0.78f);
                bloom.intensity.Override(2.2f);
                bloom.scatter.Override(0.72f);
                bloom.tint.Override(new Color(1f, 0.94f, 0.80f));

                // - Color Adjustments (Nâng độ rực và tương phản của bèo xanh, cỏ cây)
                var colorAdjust = profile.Add<ColorAdjustments>();
                colorAdjust.active = true;
                colorAdjust.contrast.Override(25f);
                colorAdjust.saturation.Override(32f);
                colorAdjust.postExposure.Override(0.24f);

                // - Vignette (Cinematic border)
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

        private static void BuildDiaryAndPopupUI(GameObject gameUI)
        {
            // 1. Build Camera Popup UI
            GameObject cameraPopupPanel = new GameObject("CameraPopupPanel");
            cameraPopupPanel.transform.SetParent(gameUI.transform, false);
            cameraPopupPanel.SetActive(false);
            
            var popupRect = cameraPopupPanel.AddComponent<RectTransform>();
            popupRect.anchorMin = new Vector2(0.5f, 0.5f);
            popupRect.anchorMax = new Vector2(0.5f, 0.5f);
            popupRect.pivot = new Vector2(0.5f, 0.5f);
            popupRect.anchoredPosition = Vector2.zero;
            popupRect.sizeDelta = new Vector2(450, 300);

            var popupBg = cameraPopupPanel.AddComponent<Image>();
            popupBg.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);

            // Title
            GameObject titleObj = new GameObject("TitleText");
            titleObj.transform.SetParent(cameraPopupPanel.transform, false);
            var titleRect = titleObj.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 1f);
            titleRect.anchorMax = new Vector2(0.5f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0, -25);
            titleRect.sizeDelta = new Vector2(400, 40);
            var titleText = titleObj.AddComponent<TextMeshProUGUI>();
            titleText.text = "NHẬN MÁY ẢNH PHIM CŨ";
            titleText.fontSize = 22;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = Color.yellow;
            titleText.alignment = TextAlignmentOptions.Center;

            // Description
            GameObject descObj = new GameObject("DescText");
            descObj.transform.SetParent(cameraPopupPanel.transform, false);
            var descRect = descObj.AddComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0.5f, 0.5f);
            descRect.anchorMax = new Vector2(0.5f, 0.5f);
            descRect.pivot = new Vector2(0.5f, 0.5f);
            descRect.anchoredPosition = new Vector2(0, -10);
            descRect.sizeDelta = new Vector2(380, 120);
            var descText = descObj.AddComponent<TextMeshProUGUI>();
            descText.text = "Máy ảnh phim cũ ba mua hồi năm ngoái. Vẫn còn xài tốt nhưng ống kính hơi rít...\n\nSử dụng phím [Chuột Phải] để ngắm, [Chuột Trái] để chụp hình.\nNhấn [Tab] hoặc [I] để mở Sổ Nhật Ký.";
            descText.fontSize = 16;
            descText.color = Color.white;
            descText.alignment = TextAlignmentOptions.Center;

            // Button hint
            GameObject hintObj = new GameObject("HintText");
            hintObj.transform.SetParent(cameraPopupPanel.transform, false);
            var hintRect = hintObj.AddComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.5f, 0f);
            hintRect.anchorMax = new Vector2(0.5f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.anchoredPosition = new Vector2(0, 20);
            hintRect.sizeDelta = new Vector2(400, 30);
            var hintText = hintObj.AddComponent<TextMeshProUGUI>();
            hintText.text = "[Chuột Trái / Space] Đóng";
            hintText.fontSize = 14;
            hintText.color = Color.gray;
            hintText.alignment = TextAlignmentOptions.Center;


            // 2. Build Sổ Nhật Ký (Diary UI)
            GameObject diaryPanel = new GameObject("DiaryPanel");
            diaryPanel.transform.SetParent(gameUI.transform, false);
            diaryPanel.SetActive(false);
            
            var diaryRect = diaryPanel.AddComponent<RectTransform>();
            diaryRect.anchorMin = Vector2.zero;
            diaryRect.anchorMax = Vector2.one;
            diaryRect.sizeDelta = Vector2.zero;
            
            var diaryBg = diaryPanel.AddComponent<Image>();
            diaryBg.color = new Color(0.08f, 0.08f, 0.08f, 0.96f);

            // Title
            GameObject dTitleObj = new GameObject("DiaryTitle");
            dTitleObj.transform.SetParent(diaryPanel.transform, false);
            var dTitleRect = dTitleObj.AddComponent<RectTransform>();
            dTitleRect.anchorMin = new Vector2(0.5f, 1f);
            dTitleRect.anchorMax = new Vector2(0.5f, 1f);
            dTitleRect.pivot = new Vector2(0.5f, 1f);
            dTitleRect.anchoredPosition = new Vector2(0, -30);
            dTitleRect.sizeDelta = new Vector2(600, 50);
            var dTitleText = dTitleObj.AddComponent<TextMeshProUGUI>();
            dTitleText.text = "SỔ NHẬT KÝ HÀNH TRÌNH TRÀ SƯ";
            dTitleText.fontSize = 28;
            dTitleText.fontStyle = FontStyles.Bold;
            dTitleText.color = new Color(0.95f, 0.85f, 0.6f);
            dTitleText.alignment = TextAlignmentOptions.Center;

            // Inventory Item Icon (Camera)
            GameObject cameraInvObj = new GameObject("CameraInventoryIcon");
            cameraInvObj.transform.SetParent(diaryPanel.transform, false);
            var camInvRect = cameraInvObj.AddComponent<RectTransform>();
            camInvRect.anchorMin = new Vector2(1f, 1f);
            camInvRect.anchorMax = new Vector2(1f, 1f);
            camInvRect.pivot = new Vector2(1f, 1f);
            camInvRect.anchoredPosition = new Vector2(-40, -40);
            camInvRect.sizeDelta = new Vector2(120, 50);
            
            var camInvBg = cameraInvObj.AddComponent<Image>();
            camInvBg.color = new Color(0.2f, 0.3f, 0.2f, 0.8f);
            
            GameObject camInvTextObj = new GameObject("Text");
            camInvTextObj.transform.SetParent(cameraInvObj.transform, false);
            var camInvTextRect = camInvTextObj.AddComponent<RectTransform>();
            camInvTextRect.anchorMin = Vector2.zero;
            camInvTextRect.anchorMax = Vector2.one;
            camInvTextRect.sizeDelta = Vector2.zero;
            var camInvText = camInvTextObj.AddComponent<TextMeshProUGUI>();
            camInvText.text = "📷 MÁY ẢNH";
            camInvText.fontSize = 16;
            camInvText.color = Color.white;
            camInvText.alignment = TextAlignmentOptions.Center;

            var diaryController = diaryPanel.AddComponent<DiaryUIController>();

            // Polaroid slot instantiator helper
            System.Func<string, Vector2, GameObject> createPolaroidSlot = (label, pos) => {
                GameObject slot = new GameObject("Polaroid_" + label);
                slot.transform.SetParent(diaryPanel.transform, false);
                var r = slot.AddComponent<RectTransform>();
                r.anchorMin = new Vector2(0.5f, 0.5f);
                r.anchorMax = new Vector2(0.5f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.anchoredPosition = pos;
                r.sizeDelta = new Vector2(140, 170);

                var img = slot.AddComponent<Image>();
                img.color = Color.white; // Polaroid white frame

                GameObject photoObj = new GameObject("Photo");
                photoObj.transform.SetParent(slot.transform, false);
                var photoRect = photoObj.AddComponent<RectTransform>();
                photoRect.anchorMin = new Vector2(0.5f, 1f);
                photoRect.anchorMax = new Vector2(0.5f, 1f);
                photoRect.pivot = new Vector2(0.5f, 1f);
                photoRect.anchoredPosition = new Vector2(0, -8);
                photoRect.sizeDelta = new Vector2(124, 124);
                var raw = photoObj.AddComponent<RawImage>();

                GameObject labelObj = new GameObject("Label");
                labelObj.transform.SetParent(slot.transform, false);
                var labelRect = labelObj.AddComponent<RectTransform>();
                labelRect.anchorMin = new Vector2(0.5f, 0f);
                labelRect.anchorMax = new Vector2(0.5f, 0f);
                labelRect.pivot = new Vector2(0.5f, 0f);
                labelRect.anchoredPosition = new Vector2(0, 6);
                labelRect.sizeDelta = new Vector2(124, 25);
                var lblTxt = labelObj.AddComponent<TextMeshProUGUI>();
                lblTxt.text = label;
                lblTxt.fontSize = 11;
                lblTxt.color = Color.black;
                lblTxt.fontStyle = FontStyles.Bold;
                lblTxt.alignment = TextAlignmentOptions.Center;

                return photoObj; // return photo raw image object
            };

            // Layout row 1: 4 photos
            RawImage rPhase1Mango = createPolaroidSlot("Cây Xoài Nhiệm Vụ", new Vector2(-300, 100)).GetComponent<RawImage>();
            RawImage rPhase2Ch1 = createPolaroidSlot("Đàn Chim Điểm 1", new Vector2(-100, 100)).GetComponent<RawImage>();
            RawImage rPhase2Ch2 = createPolaroidSlot("Đàn Chim Điểm 2", new Vector2(100, 100)).GetComponent<RawImage>();
            RawImage rPhase2Ch3 = createPolaroidSlot("Đàn Chim Điểm 3", new Vector2(300, 100)).GetComponent<RawImage>();

            // Layout row 2: 6 photos (smaller spacing)
            RawImage rPhase4Stork = createPolaroidSlot("Cò Trắng", new Vector2(-375, -120)).GetComponent<RawImage>();
            RawImage rPhase4Snake = createPolaroidSlot("Rắn Nước", new Vector2(-225, -120)).GetComponent<RawImage>();
            RawImage rPhase4Fish = createPolaroidSlot("Cá Lóc Trà Sư", new Vector2(-75, -120)).GetComponent<RawImage>();
            RawImage rPhase4Butterfly = createPolaroidSlot("Bướm Tràm", new Vector2(75, -120)).GetComponent<RawImage>();
            RawImage rPhase4Duck = createPolaroidSlot("Vịt Trời", new Vector2(225, -120)).GetComponent<RawImage>();
            RawImage rPhase5Sunset = createPolaroidSlot("Hoàng Hôn Trà Sư", new Vector2(375, -120)).GetComponent<RawImage>();

            // Link to DiaryUIController
            SerializedObject serController = new SerializedObject(diaryController);
            serController.FindProperty("diaryPanel").objectReferenceValue = diaryPanel;
            serController.FindProperty("imgPhase1Mango").objectReferenceValue = rPhase1Mango;
            serController.FindProperty("imgPhase2Ch1").objectReferenceValue = rPhase2Ch1;
            serController.FindProperty("imgPhase2Ch2").objectReferenceValue = rPhase2Ch2;
            serController.FindProperty("imgPhase2Ch3").objectReferenceValue = rPhase2Ch3;
            serController.FindProperty("imgPhase4Stork").objectReferenceValue = rPhase4Stork;
            serController.FindProperty("imgPhase4Snake").objectReferenceValue = rPhase4Snake;
            serController.FindProperty("imgPhase4Fish").objectReferenceValue = rPhase4Fish;
            serController.FindProperty("imgPhase4Butterfly").objectReferenceValue = rPhase4Butterfly;
            serController.FindProperty("imgPhase4Duck").objectReferenceValue = rPhase4Duck;
            serController.FindProperty("imgPhase5Sunset").objectReferenceValue = rPhase5Sunset;
            serController.FindProperty("cameraInventoryIcon").objectReferenceValue = cameraInvObj;
            serController.ApplyModifiedProperties();
        }

        private static void CreateLotusFlower(Vector3 position, Transform parent)
        {
            GameObject flower = new GameObject("LotusFlower");
            flower.transform.SetParent(parent, false);
            flower.transform.position = position + new Vector3(0f, 0.02f, 0f); // Hơi nhô lên trên lá bèo

            Material petalMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            petalMat.color = new Color(0.96f, 0.52f, 0.74f); // Hồng nhạt hoa sen/súng
            if (petalMat.HasProperty("_Smoothness")) petalMat.SetFloat("_Smoothness", 0.1f);

            Material centerMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            centerMat.color = new Color(1.0f, 0.84f, 0f); // Nhụy vàng

            // Nhụy tròn ở giữa
            GameObject center = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            center.name = "Pistil";
            center.transform.SetParent(flower.transform, false);
            center.transform.localScale = new Vector3(0.18f, 0.08f, 0.18f);
            center.transform.localPosition = new Vector3(0, 0.02f, 0);
            center.GetComponent<Renderer>().sharedMaterial = centerMat;
            DestroyImmediate(center.GetComponent<Collider>());

            // 6 cánh hoa xung quanh xếp tròn xòe
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
                DestroyImmediate(petal.GetComponent<Collider>());
            }
        }

        private static void CreateBreathingRootsAroundTree(Vector3 treePosition, Transform parent, int count)
        {
            Material rootMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            rootMat.color = new Color(0.26f, 0.16f, 0.10f); // Nâu vỏ cây tràm
            if (rootMat.HasProperty("_Smoothness")) rootMat.SetFloat("_Smoothness", 0.05f);

            for (int i = 0; i < count; i++)
            {
                // Sinh ngẫu nhiên vòng quanh gốc cây từ 0.6m đến 1.8m
                float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float distance = Random.Range(0.6f, 1.8f);
                float x = treePosition.x + Mathf.Sin(angle) * distance;
                float z = treePosition.z + Mathf.Cos(angle) * distance;
                float y = -1.02f; // Chân rễ cắm dưới nước

                GameObject spike = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                spike.name = "BreathingRoot_Spike";
                spike.transform.SetParent(parent, false);

                float height = Random.Range(0.25f, 0.65f); // Cao khoảng 25cm - 65cm
                spike.transform.position = new Vector3(x, y + height * 0.5f, z);

                float radius = Random.Range(0.04f, 0.09f); // Rễ thở nhỏ nhọn
                spike.transform.localScale = new Vector3(radius, height, radius);
                spike.transform.rotation = Quaternion.Euler(Random.Range(-5f, 5f), 0f, Random.Range(-5f, 5f));

                spike.GetComponent<Renderer>().sharedMaterial = rootMat;
                DestroyImmediate(spike.GetComponent<Collider>());
            }
        }

        private static void CreateBirdInTree(Vector3 treePosition, Transform parent)
        {
            // Đã tắt sinh cò đậu trên cây theo yêu cầu của người dùng
        }

        private static void CreateScenicWoodenSign(string mainText, Vector3 position, float rotationY)
        {
            GameObject signObj = new GameObject("WoodenSign_" + mainText.Replace("\n", "_"));
            signObj.transform.position = position;
            signObj.transform.rotation = Quaternion.Euler(0f, rotationY, 0f);

            Material woodMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            woodMat.color = new Color(0.35f, 0.22f, 0.12f); // Gỗ nâu mộc mạc
            if (woodMat.HasProperty("_Smoothness")) woodMat.SetFloat("_Smoothness", 0.05f);

            // Cột biển
            GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "Post";
            post.transform.SetParent(signObj.transform, false);
            post.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            post.transform.localScale = new Vector3(0.12f, 1.6f, 0.12f);
            post.GetComponent<Renderer>().sharedMaterial = woodMat;

            // Bảng gỗ biển
            GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Board";
            board.transform.SetParent(signObj.transform, false);
            board.transform.localPosition = new Vector3(0f, 1.4f, 0f);
            board.transform.localScale = new Vector3(1.2f, 0.5f, 0.06f);
            board.GetComponent<Renderer>().sharedMaterial = woodMat;

            // Canvas hiển thị chữ trong không gian 3D
            GameObject canvasObj = new GameObject("SignCanvas");
            canvasObj.transform.SetParent(board.transform, false);
            canvasObj.transform.localPosition = new Vector3(0f, 0f, 0.04f); // Nhô ra trước biển
            canvasObj.transform.localRotation = Quaternion.identity;

            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = canvasObj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(1.2f, 0.5f);
            rect.localScale = Vector3.one;

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(canvasObj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;

            var tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = mainText;
            tmp.fontSize = 0.14f; // Kích thước chữ 3D chuẩn thế giới thực
            tmp.color = Color.yellow;
            tmp.alignment = TextAlignmentOptions.Center;
        }

        private static void CreateStairFlight(string name, Vector3 startPos, Vector3 endPos, float width, Material mat, GameObject parent)
        {
            GameObject flightObj = new GameObject(name);
            flightObj.transform.SetParent(parent.transform);

            Vector3 diff = endPos - startPos;
            float length = diff.magnitude;
            float rise = diff.y;
            float run = Mathf.Sqrt(diff.x * diff.x + diff.z * diff.z);

            // 1. Create the smooth invisible ramp collider
            GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = name + "_RampCollider";
            ramp.transform.SetParent(flightObj.transform);

            // Position at the center of the flight
            ramp.transform.position = startPos + diff * 0.5f + new Vector3(0f, 0.08f, 0f);
            if (diff != Vector3.zero)
            {
                ramp.transform.rotation = Quaternion.LookRotation(diff);
            }

            // Scale: width, thickness 0.1f, length
            ramp.transform.localScale = new Vector3(width, 0.15f, length);

            // Disable mesh renderer so it is invisible
            MeshRenderer mr = ramp.GetComponent<MeshRenderer>();
            if (mr != null) Object.DestroyImmediate(mr);

            // 2. Create visual steps
            int stepCount = Mathf.Max(8, Mathf.RoundToInt(length / 0.35f));
            Vector3 horizontalDir = new Vector3(diff.x, 0f, diff.z);
            for (int i = 0; i < stepCount; i++)
            {
                float t = (float)i / (stepCount - 1);
                Vector3 stepPos = Vector3.Lerp(startPos, endPos, t);

                GameObject step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                step.name = name + "_VisualStep_" + i;
                step.transform.SetParent(flightObj.transform);
                step.transform.position = stepPos;
                if (horizontalDir != Vector3.zero)
                {
                    step.transform.rotation = Quaternion.LookRotation(horizontalDir);
                }

                // Step size: width, thickness 0.08f, depth 0.35f
                step.transform.localScale = new Vector3(width, 0.08f, 0.38f);
                step.GetComponent<Renderer>().sharedMaterial = mat;

                // Destroy collider on the visual step to avoid CharacterController collision issues
                Collider c = step.GetComponent<Collider>();
                if (c != null) Object.DestroyImmediate(c);
            }
        }
    }
}

