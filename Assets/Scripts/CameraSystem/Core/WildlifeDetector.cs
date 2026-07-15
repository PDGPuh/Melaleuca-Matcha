using System.Collections.Generic;
using UnityEngine;

namespace RungTramTraSu.CameraSystem
{
    public class WildlifeDetector : MonoBehaviour
    {
        public static WildlifeDetector Instance { get; private set; }

        [Header("Occlusion Checking")]
        [SerializeField] private LayerMask occlusionLayers;

        private Transform customQuestTarget;
        private Camera mainCamera;

        public struct DetectedTarget
        {
            public GameObject go;
            public string displayName;
            public string scientificName;
            public string category;
            public string conservationStatus;
            public bool isRare;
            public bool isFacingCamera;
            public float distance;
            public float screenCoverage; // Estimated percentage of screen filled [0..100]
            public bool isOccluded;
            public Vector3 viewportPos;
            public Vector3 worldCenter;
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            mainCamera = Camera.main;
            if (occlusionLayers == 0)
            {
                // Default fallback to layer 0 (Default) and 31 if not set
                occlusionLayers = LayerMask.GetMask("Default", "Terrain");
            }
        }

        private void Update()
        {
            if (mainCamera == null) mainCamera = Camera.main;
        }

        public void RegisterQuestTarget(Transform target)
        {
            customQuestTarget = target;
        }

        public Transform GetQuestTarget()
        {
            return customQuestTarget;
        }

        // Checks if an object is inside the camera's viewport
        public bool IsInViewfinder(Vector3 worldPoint, out Vector3 viewportPos, float boundaryMargin = 0.15f)
        {
            viewportPos = Vector3.zero;
            if (mainCamera == null) return false;

            viewportPos = mainCamera.WorldToViewportPoint(worldPoint);
            
            // Check boundaries
            return viewportPos.z > 0 &&
                   viewportPos.x >= boundaryMargin && viewportPos.x <= (1f - boundaryMargin) &&
                   viewportPos.y >= boundaryMargin && viewportPos.y <= (1f - boundaryMargin);
        }

        // Performs raycasts to verify if the subject is occluded by obstacles
        public bool IsOccluded(Vector3 targetCenter, Transform targetTransform)
        {
            if (mainCamera == null) return true;

            Vector3 camPos = mainCamera.transform.position;
            Vector3 dir = targetCenter - camPos;
            float maxDist = dir.magnitude;

            // Offset start slightly to avoid self-clipping from lens
            Vector3 rayStart = camPos + dir.normalized * 0.3f;
            float checkDist = maxDist - 0.5f;

            if (checkDist <= 0.1f) return false;

            RaycastHit hit;
            if (Physics.Raycast(rayStart, dir.normalized, out hit, checkDist, occlusionLayers, QueryTriggerInteraction.Ignore))
            {
                // If it hits an obstacle that isn't the target or child of the target
                if (hit.transform != targetTransform && !hit.transform.IsChildOf(targetTransform))
                {
                    // Check if it's the player or boat, which we ignore
                    if (hit.transform.name.Contains("Player") || hit.transform.name.Contains("Boat") || hit.transform.name.Contains("xuong"))
                    {
                        return false;
                    }
                    return true; // Occluded
                }
            }
            return false;
        }

        // Analyzes if the animal is facing the camera direction
        public bool IsSubjectFacingCamera(Transform target)
        {
            if (mainCamera == null) return false;
            
            // Vector pointing from target to camera
            Vector3 targetToCam = (mainCamera.transform.position - target.position).normalized;
            targetToCam.y = 0; // ignore pitch
            
            Vector3 targetForward = target.forward;
            targetForward.y = 0;

            float dot = Vector3.Dot(targetForward.normalized, targetToCam.normalized);
            // If dot > 0.15f, they are facing generally towards the camera
            return dot > 0.15f;
        }

        // Estimates the percentage of the screen width/height the target occupies
        public float EstimateScreenCoverage(Transform target, Vector3 worldCenter, float distance)
        {
            if (mainCamera == null || distance <= 0f) return 0f;

            // Get target bounds if available, or approximate with scale
            float sizeEstimate = 1.0f;
            Collider col = target.GetComponent<Collider>();
            if (col != null)
            {
                sizeEstimate = col.bounds.size.magnitude;
            }
            else
            {
                Renderer r = target.GetComponentInChildren<Renderer>();
                if (r != null)
                {
                    sizeEstimate = r.bounds.size.magnitude;
                }
                else
                {
                    sizeEstimate = target.localScale.magnitude;
                }
            }

            // Simple projection math: angular size
            float fov = mainCamera.fieldOfView;
            float sizeInViewport = (sizeEstimate / (2f * distance * Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad)));
            float coveragePercent = Mathf.Clamp(sizeInViewport * 100f, 0.1f, 100f);
            
            return coveragePercent;
        }

        // Scans the active scene for all visible wildlife targets
        public List<DetectedTarget> ScanForVisibleTargets()
        {
            List<DetectedTarget> list = new List<DetectedTarget>();
            if (mainCamera == null) return list;

            // 1. Scan for custom registered quest target
            if (customQuestTarget != null)
            {
                ProcessTargetCandidate(customQuestTarget.gameObject, list);
            }

            // 2. Scan for AnimalAI components
            AnimalAI[] animals = FindObjectsByType<AnimalAI>(FindObjectsSortMode.None);
            foreach (var animal in animals)
            {
                if (animal != null && !animal.HasFled)
                {
                    ProcessTargetCandidate(animal.gameObject, list);
                }
            }

            // 3. Scan for BirdDataHolder components (Phase 2 birds)
            BirdDataHolder[] birds = FindObjectsByType<BirdDataHolder>(FindObjectsSortMode.None);
            foreach (var bird in birds)
            {
                if (bird != null)
                {
                    ProcessTargetCandidate(bird.gameObject, list);
                }
            }

            // 4. Scan by name fallback if quest targets are missing (e.g. MangoTree, SunsetQuestTarget)
            GameObject mango = GameObject.Find("MangoTreeTarget");
            if (mango != null) ProcessTargetCandidate(mango, list);

            GameObject sunset = GameObject.Find("SunsetQuestTarget");
            if (sunset != null) ProcessTargetCandidate(sunset, list);

            return list;
        }

        private void ProcessTargetCandidate(GameObject go, List<DetectedTarget> list)
        {
            // Avoid duplicates
            if (list.Exists(x => x.go == go)) return;

            Vector3 center = go.transform.position;
            Collider col = go.GetComponent<Collider>();
            if (col == null) col = go.GetComponentInChildren<Collider>();
            if (col != null) center = col.bounds.center;

            Vector3 viewportPos;
            // Visible if in viewfinder frame
            if (IsInViewfinder(center, out viewportPos, 0.1f))
            {
                float dist = Vector3.Distance(mainCamera.transform.position, center);
                bool occluded = IsOccluded(center, go.transform);

                DetectedTarget target = new DetectedTarget
                {
                    go = go,
                    worldCenter = center,
                    viewportPos = viewportPos,
                    distance = dist,
                    isOccluded = occluded,
                    isFacingCamera = IsSubjectFacingCamera(go.transform),
                    screenCoverage = EstimateScreenCoverage(go.transform, center, dist)
                };

                // Map target parameters based on scripts attached
                PopulateTargetMetadata(go, ref target);

                list.Add(target);
            }
        }

        private void PopulateTargetMetadata(GameObject go, ref DetectedTarget target)
        {
            // Set defaults
            target.displayName = go.name;
            target.scientificName = "Flora/Fauna";
            target.category = "Landscape";
            target.conservationStatus = "LC (Least Concern)";
            target.isRare = false;

            // Phase 4 Animals
            var animal = go.GetComponent<AnimalAI>();
            if (animal != null)
            {
                target.category = GetCategoryFromType(animal.Type);
                target.displayName = GetVietnameseNameFromType(animal.Type);
                target.scientificName = GetScientificNameFromType(animal.Type);
                target.conservationStatus = GetStatusFromType(animal.Type);
                target.isRare = (animal.Type == AnimalAI.AnimalType.Stork); // Cò Trắng is slightly rarer here
                return;
            }

            // Phase 2 Birds
            var bird = go.GetComponent<BirdDataHolder>();
            if (bird != null)
            {
                target.category = "Birds";
                target.displayName = bird.vietnameseName;
                target.isRare = bird.isSarus;
                target.scientificName = GetScientificNameByName(bird.vietnameseName);
                target.conservationStatus = bird.isSarus ? "EN (Endangered)" : "LC (Least Concern)";
                return;
            }

            // Custom Quest Targets
            if (go.name.Contains("Mango") || go.name.Contains("Xoài"))
            {
                target.displayName = "Cây Xoài Cổ Thụ";
                target.scientificName = "Mangifera indica";
                target.category = "Landscape";
                target.conservationStatus = "Vườn Nhà";
            }
            else if (go.name.Contains("Sunset") || go.name.Contains("Hoàng hôn"))
            {
                target.displayName = "Hoàng Hôn Rừng Tràm";
                target.scientificName = "Sol Occidens";
                target.category = "Landscape";
                target.conservationStatus = "Thiên Nhiên";
            }
        }

        private string GetCategoryFromType(AnimalAI.AnimalType type)
        {
            switch (type)
            {
                case AnimalAI.AnimalType.Stork:
                case AnimalAI.AnimalType.Duck:
                    return "Birds";
                case AnimalAI.AnimalType.Fish:
                    return "Fish";
                case AnimalAI.AnimalType.Butterfly:
                    return "Insects";
                case AnimalAI.AnimalType.Snake:
                    return "Reptiles";
                default:
                    return "Landscape";
            }
        }

        private string GetVietnameseNameFromType(AnimalAI.AnimalType type)
        {
            switch (type)
            {
                case AnimalAI.AnimalType.Stork: return "Cò Trắng";
                case AnimalAI.AnimalType.Snake: return "Rắn Nước";
                case AnimalAI.AnimalType.Fish: return "Cá Lóc";
                case AnimalAI.AnimalType.Butterfly: return "Bướm Hoa Súng";
                case AnimalAI.AnimalType.Duck: return "Vịt Trời";
                default: return "Sinh Vật Lạ";
            }
        }

        private string GetScientificNameFromType(AnimalAI.AnimalType type)
        {
            switch (type)
            {
                case AnimalAI.AnimalType.Stork: return "Egretta alba";
                case AnimalAI.AnimalType.Snake: return "Enhydris enhydris";
                case AnimalAI.AnimalType.Fish: return "Channa striata";
                case AnimalAI.AnimalType.Butterfly: return "Nymphaea lepidoptera";
                case AnimalAI.AnimalType.Duck: return "Anas poecilorhyncha";
                default: return "Fauna";
            }
        }

        private string GetStatusFromType(AnimalAI.AnimalType type)
        {
            switch (type)
            {
                case AnimalAI.AnimalType.Stork: return "VU (Vulnerable)";
                case AnimalAI.AnimalType.Snake: return "LC (Least Concern)";
                case AnimalAI.AnimalType.Fish: return "LC (Least Concern)";
                case AnimalAI.AnimalType.Butterfly: return "LC (Least Concern)";
                case AnimalAI.AnimalType.Duck: return "LC (Least Concern)";
                default: return "LC";
            }
        }

        private string GetScientificNameByName(string vnName)
        {
            switch (vnName)
            {
                case "Cò trắng": return "Egretta alba";
                case "Diệc xám": return "Ardea cinerea";
                case "Cò ốc": return "Anastomus oscitans";
                case "Già đẫy": return "Leptoptilos dubius";
                case "Vạc": return "Nycticorax nycticorax";
                case "Cồng cộc": return "Phalacrocorax carbo";
                case "Cò bợ": return "Ardeola bacchus";
                case "Trích cùi": return "Porphyrio porphyrio";
                case "Điêng điểng": return "Anhinga melanogaster";
                case "Bói cá": return "Alcedo atthis";
                case "Le le": return "Dendrocygna javanica";
                case "Bìm bịp": return "Centropus sinensis";
                case "Én": return "Hirundo rustica";
                case "Sếu đầu đỏ": return "Grus antigone";
                default: return "Aves";
            }
        }
    }
}
