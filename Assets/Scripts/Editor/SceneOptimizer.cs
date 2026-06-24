using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

namespace RungTramTraSu
{
    public class SceneOptimizer : EditorWindow
    {
        [MenuItem("Tools/Optimize Phase 1 Scene")]
        public static void OptimizePhase1()
        {
            // Verify active scene name is Phase1_GrandpaHouse
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!activeScene.name.Equals("Phase1_GrandpaHouse"))
            {
                if (!EditorUtility.DisplayDialog("Warning", "The active scene is not Phase1_GrandpaHouse. Do you want to run optimization anyway?", "Yes", "No"))
                {
                    return;
                }
            }

            int optimizedFoliageCount = 0;
            int deletedGameObjectCount = 0;
            int deletedDuckCount = 0;

            // 1. Optimize Foliage under NatureDecorations
            GameObject natureDecorations = GameObject.Find("NatureDecorations");
            if (natureDecorations != null)
            {
                Undo.RegisterFullObjectHierarchyUndo(natureDecorations, "Optimize Foliage");

                // Get all direct children of NatureDecorations
                List<Transform> children = new List<Transform>();
                for (int i = 0; i < natureDecorations.transform.childCount; i++)
                {
                    children.Add(natureDecorations.transform.GetChild(i));
                }

                foreach (var child in children)
                {
                    if (child == null) continue;

                    LODGroup lodGroup = child.GetComponent<LODGroup>();
                    MeshRenderer mr = child.GetComponent<MeshRenderer>();
                    MeshFilter mf = child.GetComponent<MeshFilter>();

                    if (lodGroup != null)
                    {
                        // Find LOD1 child, or fallback to LOD0, or first child
                        Transform targetLOD = null;
                        for (int i = 0; i < child.childCount; i++)
                        {
                            var subChild = child.GetChild(i);
                            if (subChild.name.EndsWith("_LOD1") || subChild.name.Contains("LOD1"))
                            {
                                targetLOD = subChild;
                                break;
                            }
                        }

                        if (targetLOD == null)
                        {
                            for (int i = 0; i < child.childCount; i++)
                            {
                                var subChild = child.GetChild(i);
                                if (subChild.name.EndsWith("_LOD0") || subChild.name.Contains("LOD0"))
                                {
                                    targetLOD = subChild;
                                    break;
                                }
                            }
                        }

                        if (targetLOD == null && child.childCount > 0)
                        {
                            targetLOD = child.GetChild(0);
                        }

                        if (targetLOD != null)
                        {
                            var targetMF = targetLOD.GetComponent<MeshFilter>();
                            var targetMR = targetLOD.GetComponent<MeshRenderer>();

                            if (targetMF != null && targetMF.sharedMesh != null)
                            {
                                if (mf == null) mf = child.gameObject.AddComponent<MeshFilter>();
                                mf.sharedMesh = targetMF.sharedMesh;
                            }

                            if (targetMR != null)
                            {
                                if (mr == null) mr = child.gameObject.AddComponent<MeshRenderer>();
                                mr.sharedMaterials = targetMR.sharedMaterials;
                            }
                        }

                        // Destroy children of this foliage object
                        List<GameObject> childrenToDestroy = new List<GameObject>();
                        for (int i = 0; i < child.childCount; i++)
                        {
                            childrenToDestroy.Add(child.GetChild(i).gameObject);
                        }

                        foreach (var childGo in childrenToDestroy)
                        {
                            deletedGameObjectCount++;
                            Undo.DestroyObjectImmediate(childGo);
                        }

                        // Destroy LODGroup component
                        Undo.DestroyObjectImmediate(lodGroup);
                        optimizedFoliageCount++;
                    }

                    // Refresh mesh renderer references
                    mr = child.GetComponent<MeshRenderer>();
                    if (mr != null)
                    {
                        // Turn off shadow casting
                        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        mr.receiveShadows = true;
                    }

                    // Mark as Static
                    GameObjectUtility.SetStaticEditorFlags(child.gameObject, 
                        StaticEditorFlags.BatchingStatic | 
                        StaticEditorFlags.OccludeeStatic | 
                        StaticEditorFlags.OccluderStatic | 
                        StaticEditorFlags.ReflectionProbeStatic);
                }
            }
            else
            {
                Debug.LogWarning("NatureDecorations GameObject not found!");
            }

            // 2. Remove all ducks (Snowy White Duck & DUKVKVKV)
            var allGo = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
            List<GameObject> ducksToDestroy = new List<GameObject>();
            foreach (var go in allGo)
            {
                if (go.name.Contains("Snowy White Duck") || go.name.Contains("DUKVKVKV"))
                {
                    ducksToDestroy.Add(go);
                }
            }

            foreach (var duck in ducksToDestroy)
            {
                deletedDuckCount++;
                Undo.DestroyObjectImmediate(duck);
            }

            // 3. Optimize URP settings (Shadow Cascades = 2)
            string urpAssetPath = "Assets/Settings/PC_RPAsset.asset";
            UniversalRenderPipelineAsset urpAsset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(urpAssetPath);
            if (urpAsset != null)
            {
                urpAsset.shadowCascadeCount = 2;
                EditorUtility.SetDirty(urpAsset);
                AssetDatabase.SaveAssetIfDirty(urpAsset);
                Debug.Log("URP Asset Shadow Cascades set to 2.");
            }
            else
            {
                Debug.LogWarning("URP Asset not found at path: " + urpAssetPath);
            }

            // Mark active scene dirty and save it
            if (natureDecorations != null)
            {
                EditorUtility.SetDirty(natureDecorations);
            }
            
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(activeScene);

            AssetDatabase.SaveAssets();

            string report = $"Phase 1 Optimization Completed Successfully!\n" +
                            $"- Optimized foliage objects (LODs flattened): {optimizedFoliageCount}\n" +
                            $"- Deleted child LOD GameObjects: {deletedGameObjectCount}\n" +
                            $"- Deleted duck GameObjects: {deletedDuckCount}\n" +
                            $"- Shadow casting disabled on all {optimizedFoliageCount} foliage objects\n" +
                            $"- URP Shadow Cascades set to 2\n" +
                            $"Scene has been saved.";
            
            EditorUtility.DisplayDialog("Optimization Complete", report, "OK");
            Debug.Log(report);
        }

        [MenuItem("Tools/Setup Nature Sounds")]
        public static void SetupNatureSounds()
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            
            // 1. Setup Player Footsteps
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                EditorUtility.DisplayDialog("Error", "Player GameObject not found in the scene!", "OK");
                return;
            }

            Undo.RegisterCompleteObjectUndo(player, "Setup Player Footsteps");
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
                else
                {
                    Debug.LogWarning($"Could not find footstep clip at: {path}");
                }
            }
            footsteps.grassFootsteps = footstepClips.ToArray();
            EditorUtility.SetDirty(footsteps);

            // 2. Setup Ambient Audio Root
            GameObject ambientRoot = GameObject.Find("AmbientAudio");
            if (ambientRoot == null)
            {
                ambientRoot = new GameObject("AmbientAudio");
                Undo.RegisterCreatedObjectUndo(ambientRoot, "Create Ambient Audio Root");
            }

            // A. Setup Ambient Wind (2D Loop - playing Nature Birds + Water)
            Transform windTransform = ambientRoot.transform.Find("Ambient_Wind");
            GameObject windObj = windTransform != null ? windTransform.gameObject : null;
            if (windObj == null)
            {
                windObj = new GameObject("Ambient_Wind");
                windObj.transform.SetParent(ambientRoot.transform);
                Undo.RegisterCreatedObjectUndo(windObj, "Create Ambient Wind");
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
            windSource.volume = 0.45f; // Increased volume for prominent bird sounds
            EditorUtility.SetDirty(windSource);

            // B. Remove Ambient Foliage if it exists
            Transform foliageTransform = ambientRoot.transform.Find("Ambient_Foliage");
            if (foliageTransform != null)
            {
                Undo.DestroyObjectImmediate(foliageTransform.gameObject);
            }

            // C. Setup Ambient River (3D Loop)
            Transform riverTransform = ambientRoot.transform.Find("Ambient_River");
            GameObject riverObj = riverTransform != null ? riverTransform.gameObject : null;
            if (riverObj == null)
            {
                riverObj = new GameObject("Ambient_River");
                riverObj.transform.SetParent(ambientRoot.transform);
                Undo.RegisterCreatedObjectUndo(riverObj, "Create Ambient River");
            }
            // Position near the RiverWater_Canal center
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
            riverSource.volume = 0.12f; // Reduced volume to prevent drowning out bird sounds
            EditorUtility.SetDirty(riverSource);

            // 3. Setup Ambient Controller (Muffle wind when indoors)
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

            // Mark active scene dirty and save it
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(activeScene);

            AssetDatabase.SaveAssets();

            string report = "Nature Sounds Setup Completed Successfully!\n" +
                            $"- Attached PlayerFootsteps component and assigned {footsteps.grassFootsteps.Length} grass footstep clips.\n" +
                            $"- Configured Ambient_Wind (2D loop, Nature Birds + Water ambient).\n" +
                            $"- Removed Ambient_Foliage (Fake tree blowing wind sound).\n" +
                            $"- Configured Ambient_River (3D loop, River + birds at x=25, y=-1, z=0).\n" +
                            $"- Setup AmbientController on AmbientAudio to muffle outdoor sounds inside the house.\n" +
                            "Scene has been saved.";
            
            EditorUtility.DisplayDialog("Audio Setup Complete", report, "OK");
            Debug.Log(report);
        }
    }
}
