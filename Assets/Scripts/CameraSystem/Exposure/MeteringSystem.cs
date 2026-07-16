using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace RungTramTraSu.CameraSystem
{
    public class MeteringSystem : MonoBehaviour
    {
        public static MeteringSystem Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        // Returns target EV value based on the metering mode and visible targets
        public float EvaluateAmbientEV(MeteringMode mode, List<WildlifeDetector.DetectedTarget> visibleTargets)
        {
            float baseEV = GetBaseSceneEV();

            switch (mode)
            {
                case MeteringMode.Matrix:
                    // Matrix metering evaluates the average illumination of the entire scene
                    return baseEV;

                case MeteringMode.CenterWeighted:
                    // Center-Weighted prioritizes the center of the viewport (75% weight)
                    float centerWeight = 0f;
                    if (visibleTargets != null && visibleTargets.Count > 0)
                    {
                        foreach (var target in visibleTargets)
                        {
                            if (target.isOccluded) continue;
                            float distFromCenter = Vector2.Distance(new Vector2(target.viewportPos.x, target.viewportPos.y), new Vector2(0.5f, 0.5f));
                            if (distFromCenter < 0.25f)
                            {
                                // Center targets influence exposure slightly (e.g. brighter/darker depending on rarity or specific factors)
                                centerWeight += target.isRare ? 1.0f : 0.5f; 
                            }
                        }
                    }
                    // A higher center weight alters exposure requirements slightly
                    return baseEV + Mathf.Clamp(centerWeight, -1.0f, 1.0f);

                case MeteringMode.Spot:
                    // Spot metering evaluates a tiny zone (2-5% of screen center)
                    bool hasSpotTarget = false;
                    float spotModifier = 0f;
                    if (visibleTargets != null && visibleTargets.Count > 0)
                    {
                        float closestDist = float.MaxValue;
                        foreach (var target in visibleTargets)
                        {
                            if (target.isOccluded) continue;
                            float distFromCenter = Vector2.Distance(new Vector2(target.viewportPos.x, target.viewportPos.y), new Vector2(0.5f, 0.5f));
                            if (distFromCenter < 0.08f && distFromCenter < closestDist)
                            {
                                closestDist = distFromCenter;
                                hasSpotTarget = true;
                                // If the spot target is bright/dark (simulated here by target category)
                                if (target.displayName.Contains("Sếu") || target.displayName.Contains("Cò"))
                                {
                                    spotModifier = -0.5f; // Bright white birds need slightly lower EV (underexp compensation)
                                }
                                else
                                {
                                    spotModifier = 0.3f; // Darker wildlife
                                }
                            }
                        }
                    }
                    return baseEV + (hasSpotTarget ? spotModifier : 0f);

                default:
                    return baseEV;
            }
        }

        private float GetBaseSceneEV()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (sceneName.Contains("Phase5"))
            {
                return 8.5f; // Sunset/dim light
            }
            if (sceneName.Contains("Phase1"))
            {
                return 11.5f; // Daytime garden
            }
            if (sceneName.Contains("Phase4"))
            {
                return 11.0f; // Swamp shadow
            }
            // Phase 2, 3 (canal river)
            return 12.5f; // Bright daylight
        }
    }
}
