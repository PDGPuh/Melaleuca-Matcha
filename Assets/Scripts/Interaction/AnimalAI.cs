using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Animations;

namespace RungTramTraSu
{
    public class AnimalAI : MonoBehaviour
    {
        public enum AnimalType { Stork, Snake, Fish, Butterfly, Duck }

        [Header("Animal Settings")]
        [SerializeField] private AnimalType animalType;
        [SerializeField] private float speed = 2.0f;
        [SerializeField] private float range = 5.0f;

        private Vector3 startPos;
        private Transform player;
        private PlayerController playerController;
        private bool isFleeing = false;
        private float actionTimer = 0f;
        [SerializeField] private float spawnGraceSeconds = 8f;
        private float aliveTimer = 0f;

        // Visual feedback when scared
        private bool hasFled = false;

        // References for Stork runtime visual swapping and procedural animation
        private GameObject idleModel;
        private GameObject flyModel;
        private Transform neckBone;
        private Transform headBone;

        private Quaternion initialNeckRotation;
        private Quaternion initialHeadRotation;
        private Vector3 initialNeckScale = Vector3.one;
        private Vector3 initialHeadScale = Vector3.one;
        private bool hasInitialRotations = false;

        private AnimationClip flyClip;
        private PlayableGraph playableGraph;

        private AnimationClip snakeClip;
        private PlayableGraph snakePlayableGraph;

        public AnimalType Type => animalType;

        private void Start()
        {
            startPos = transform.position;
            SetVisualsEnabled(true);
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
                playerController = playerObj.GetComponent<PlayerController>();
            }

            SetupRuntimeVisuals();
        }

        private void SetupRuntimeVisuals()
        {
            if (animalType != AnimalType.Duck && animalType != AnimalType.Snake && animalType != AnimalType.Fish && animalType != AnimalType.Stork)
            {
                return;
            }

            string resourcePath = "";
            float scaleVal = 1f;
            Vector3 localPos = Vector3.zero;

            if (animalType == AnimalType.Duck)
            {
                resourcePath = "Fauna/duck_walk_free";
                scaleVal = 2.2f;
                localPos = new Vector3(0f, -0.4f, 0f); // sink duck lower to submerge feet/lower body in water
            }
            else if (animalType == AnimalType.Snake)
            {
                resourcePath = "Fauna/snake";
                scaleVal = 0.05f;
            }
            else if (animalType == AnimalType.Fish)
            {
                resourcePath = "Fauna/Snakehead Fish";
                scaleVal = 0.12f;
            }
            else if (animalType == AnimalType.Stork)
            {
                // Handle Stork loading both Co (idle) and CoBay (fly) models from resources
                GameObject idlePrefab = Resources.Load<GameObject>("Fauna/Co");
                GameObject flyPrefab = Resources.Load<GameObject>("Fauna/CoBay");

                float targetWorldHeight = 1.0f; // Default fallback

                if (idlePrefab != null)
                {
                    MeshRenderer mr = GetComponent<MeshRenderer>();
                    if (mr != null) Destroy(mr);
                    MeshFilter mf = GetComponent<MeshFilter>();
                    if (mf != null) Destroy(mf);

                    foreach (Transform child in transform)
                    {
                        if (child.name.StartsWith("Visual") || child.name == "VisualModel" || child.name.Contains("Stork") || child.name.Contains("crow") || child.name.Contains("Bird"))
                        {
                            Destroy(child.gameObject);
                        }
                    }

                    transform.localScale = Vector3.one;

                    // Create container for Idle model to prevent non-uniform scaling distortion
                    GameObject idleContainer = new GameObject("VisualModel_Idle_Container");
                    idleContainer.transform.SetParent(transform, false);
                    idleContainer.transform.localPosition = Vector3.zero;
                    idleContainer.transform.localRotation = Quaternion.identity;
                    idleContainer.transform.localScale = Vector3.one * 0.25f; // scale container to 0.25f (realistic size)

                    idleModel = Instantiate(idlePrefab, idleContainer.transform);
                    idleModel.name = "VisualModel_Idle";
                    idleModel.transform.localPosition = Vector3.zero;
                    idleModel.transform.localRotation = idlePrefab.transform.localRotation; // Preserve default import rotation!
                    idleModel.transform.localScale = Vector3.one;
                    idleContainer.SetActive(true);

                    // Measure the actual world height of the standing stork
                    float measuredHeight = GetStorkModelHeight(idleModel);
                    if (measuredHeight > 0f)
                    {
                        targetWorldHeight = measuredHeight;
                    }
                }

                if (flyPrefab != null)
                {
                    // Create container for Fly model
                    GameObject flyContainer = new GameObject("VisualModel_Fly_Container");
                    flyContainer.transform.SetParent(transform, false);
                    flyContainer.transform.localPosition = Vector3.zero;
                    flyContainer.transform.localRotation = Quaternion.identity;
                    flyContainer.transform.localScale = Vector3.one; // scale starts at 1.0 to measure raw height

                    flyModel = Instantiate(flyPrefab, flyContainer.transform);
                    flyModel.name = "VisualModel_Fly";
                    flyModel.transform.localPosition = Vector3.zero;
                    flyModel.transform.localRotation = flyPrefab.transform.localRotation; // Preserve default import rotation!
                    flyModel.transform.localScale = Vector3.one;
                    flyContainer.SetActive(false); // Hidden by default

                    // Measure raw height of the flying stork at scale 1
                    float rawFlyHeight = GetStorkModelHeight(flyModel);
                    if (rawFlyHeight > 0f)
                    {
                        // Calculate matching scale and adjust by 0.33f to compensate for aspect ratio difference
                        float matchedScale = (targetWorldHeight / rawFlyHeight) * 0.33f;
                        flyContainer.transform.localScale = Vector3.one * matchedScale;
                        Debug.Log($"[Stork Sizing] Idle height: {targetWorldHeight}, Raw fly height: {rawFlyHeight}, Calculated scale: {matchedScale}");
                    }
                    else
                    {
                        flyContainer.transform.localScale = Vector3.one * 0.025f; // Fallback
                    }

                    // Find animation clip from fly model's sub-assets in Resources
                    Object[] subAssets = Resources.LoadAll("Fauna/CoBay");
                    foreach (var asset in subAssets)
                    {
                        if (asset is AnimationClip)
                        {
                            flyClip = (AnimationClip)asset;
                            break;
                        }
                    }
                }

                Debug.Log($"[AnimalAI] Successfully loaded custom idle and fly models for Stork at runtime!");
                return;
            }

            GameObject modelPrefab = Resources.Load<GameObject>(resourcePath);
            if (modelPrefab != null)
            {
                // Destroy the primitive renderer and filter on this object if it exists
                MeshRenderer mr = GetComponent<MeshRenderer>();
                if (mr != null) Destroy(mr);
                MeshFilter mf = GetComponent<MeshFilter>();
                if (mf != null) Destroy(mf);

                // Destroy any existing visual child objects
                foreach (Transform child in transform)
                {
                    if (child.name.StartsWith("Visual") || child.name == "VisualModel" || child.name == "FishBody" || child.name == "LWingPivot" || child.name == "RWingPivot" || child.name == "VisualSnake" || child.name == "VisualFish")
                    {
                        Destroy(child.gameObject);
                    }
                }

                // Reset the root scale so primitive scale doesn't distort it
                transform.localScale = Vector3.one;

                // Instantiate the model
                GameObject model = Instantiate(modelPrefab, transform);
                model.name = "VisualModel";
                model.transform.localScale = Vector3.one * scaleVal;
                model.transform.localPosition = localPos;
                model.transform.localRotation = Quaternion.identity;

                // Load and play swimming animation for custom Snake model
                if (animalType == AnimalType.Snake)
                {
                    Object[] subAssets = Resources.LoadAll("Fauna/snake");
                    foreach (var asset in subAssets)
                    {
                        if (asset is AnimationClip)
                        {
                            snakeClip = (AnimationClip)asset;
                            break;
                        }
                    }

                    if (snakeClip != null)
                    {
                        var anim = model.GetComponent<Animator>();
                        if (anim == null) anim = model.GetComponentInChildren<Animator>();
                        if (anim != null)
                        {
                            anim.enabled = true;
                            PlaySnakeAnimation(anim, snakeClip);
                        }
                    }
                }
                
                Debug.Log($"[AnimalAI] Successfully loaded custom model for {animalType} at runtime!");
            }
            else
            {
                Debug.LogWarning($"[AnimalAI] Custom model prefab not found in Resources: {resourcePath}");
            }
        }

        public bool HasFled => hasFled;

        private void Update()
        {
            aliveTimer += Time.deltaTime;

            if (isFleeing)
            {
                // Fleeing movement is handled by FleeAndRespawnRoutine
                return;
            }

            if (aliveTimer < spawnGraceSeconds)
            {
                return;
            }

            if (player != null && !hasFled)
            {
                float dist = Vector3.Distance(transform.position, player.position);
                bool playerIsCrouching = playerController != null && playerController.IsCrouching;
                float scareDist = playerIsCrouching ? 3.0f : 8.0f;

                if (dist < scareDist)
                {
                    TriggerFlee();
                    return;
                }
            }



            switch (animalType)
            {
                case AnimalType.Stork:
                    // Stork stands idle on ground/tree
                    break;
                case AnimalType.Snake:
                    HandleSnake();
                    break;
                case AnimalType.Fish:
                    HandleFish();
                    break;
                case AnimalType.Butterfly:
                    HandleButterfly();
                    break;
                case AnimalType.Duck:
                    HandleDuck();
                    break;
            }
        }



        private void TriggerFlee()
        {
            isFleeing = true;
            hasFled = true;
            Debug.Log("[AnimalAI] scared and fleeing: " + animalType);

            // Swap to flying visual model for Stork when fleeing
            if (animalType == AnimalType.Stork)
            {
                if (idleModel != null && idleModel.transform.parent != null) 
                    idleModel.transform.parent.gameObject.SetActive(false); // Hide container
                if (flyModel != null && flyModel.transform.parent != null)
                {
                    flyModel.transform.parent.gameObject.SetActive(true); // Show container
                    var anim = flyModel.GetComponent<Animator>();
                    if (anim == null) anim = flyModel.GetComponentInChildren<Animator>();
                    if (anim != null && flyClip != null)
                    {
                        anim.enabled = true;
                        PlayFlyAnimation(anim, flyClip);
                    }
                }
            }

            if (Phase4Manager.Instance != null)
            {
                Phase4Manager.Instance.NotifyAnimalScared(animalType);
            }
            StartCoroutine(FleeAndRespawnRoutine());
        }

        private System.Collections.IEnumerator FleeAndRespawnRoutine()
        {
            float elapsed = 0f;
            Quaternion originalRotation = transform.rotation;

            Vector3 horizontalDir = (transform.position - player.position);
            horizontalDir.y = 0f;
            horizontalDir.Normalize();

            // Set up flight vector for Stork (diagonal taking off path)
            Vector3 fleeVec = (horizontalDir + Vector3.up * 0.4f).normalized;

            if (animalType == AnimalType.Stork)
            {
                // Rotate stork body to face the horizontal direction of flight
                if (horizontalDir != Vector3.zero)
                {
                    transform.rotation = Quaternion.LookRotation(horizontalDir);
                }
            }

            while (elapsed < 2.0f)
            {
                elapsed += Time.deltaTime;

                if (animalType == AnimalType.Stork)
                {
                    // Move stork along diagonal path
                    transform.Translate(fleeVec * speed * 2.8f * Time.deltaTime, Space.World);
                }
                else if (animalType == AnimalType.Butterfly)
                {
                    Vector3 bfFlee = (horizontalDir + Vector3.up * 0.5f).normalized;
                    transform.Translate(bfFlee * speed * 2.5f * Time.deltaTime, Space.World);
                }
                else
                {
                    // Snake, Fish, Duck dive under water / swim away
                    Vector3 otherFlee = (horizontalDir + Vector3.down * 0.5f).normalized;
                    transform.Translate(otherFlee * speed * 2.0f * Time.deltaTime, Space.World);
                }

                yield return null;
            }

            // Hide the animal
            SetVisualsEnabled(false);

            // Wait 6 seconds
            yield return new WaitForSeconds(6.0f);

            // Reset position and states
            transform.position = startPos;
            transform.rotation = originalRotation; // Restore original rotation!
            isFleeing = false;
            hasFled = false;
            actionTimer = 0f;

            // Switch visual models back to Idle when respawned
            if (animalType == AnimalType.Stork)
            {
                StopFlyAnimation();
                if (idleModel != null && idleModel.transform.parent != null) 
                    idleModel.transform.parent.gameObject.SetActive(true); // Show container
                if (flyModel != null && flyModel.transform.parent != null)
                {
                    flyModel.transform.parent.gameObject.SetActive(false); // Hide container
                }
            }

            SetVisualsEnabled(true);
        }

        private void FindBones(Transform parent)
        {
            foreach (Transform child in parent)
            {
                string nameLower = child.name.ToLower();
                if (nameLower.Contains("neck") && neckBone == null)
                {
                    neckBone = child;
                }
                else if (nameLower.Contains("head") && headBone == null)
                {
                    headBone = child;
                }
                FindBones(child);
            }
        }

        private void AnimateStorkIdleProcedurally()
        {
            if (!hasInitialRotations) return;

            if (neckBone != null)
            {
                float timeScale = Time.time * 0.3f;
                float neckY = (Mathf.PerlinNoise(timeScale, 0f) - 0.5f) * 20f; 
                float neckX = (Mathf.PerlinNoise(0f, timeScale) - 0.5f) * 10f; 
                neckBone.localRotation = initialNeckRotation * Quaternion.Euler(neckX, neckY, 0f);
                neckBone.localScale = initialNeckScale;
            }

            if (headBone != null)
            {
                float timeScale = Time.time * 0.5f;
                float headY = (Mathf.PerlinNoise(timeScale, 10f) - 0.5f) * 15f;
                float headX = (Mathf.PerlinNoise(10f, timeScale) - 0.5f) * 8f;
                headBone.localRotation = initialHeadRotation * Quaternion.Euler(headX, headY, 0f);
                headBone.localScale = initialHeadScale;
            }
        }

        private void PlayFlyAnimation(Animator animator, AnimationClip clip)
        {
            if (animator == null || clip == null) return;
            
            StopFlyAnimation(); // Ensure cleanup

            playableGraph = PlayableGraph.Create("StorkFlyGraph");
            playableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            
            var playableOutput = AnimationPlayableOutput.Create(playableGraph, "Animation", animator);
            var clipPlayable = AnimationClipPlayable.Create(playableGraph, clip);
            playableOutput.SetSourcePlayable(clipPlayable);
            
            playableGraph.Play();
        }

        private void StopFlyAnimation()
        {
            if (playableGraph.IsValid())
            {
                playableGraph.Destroy();
            }
        }

        private void PlaySnakeAnimation(Animator animator, AnimationClip clip)
        {
            if (animator == null || clip == null) return;
            
            StopSnakeAnimation(); // Ensure cleanup

            snakePlayableGraph = PlayableGraph.Create("SnakeSwimGraph");
            snakePlayableGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            
            var playableOutput = AnimationPlayableOutput.Create(snakePlayableGraph, "Animation", animator);
            var clipPlayable = AnimationClipPlayable.Create(snakePlayableGraph, clip);
            clip.wrapMode = WrapMode.Loop;
            playableOutput.SetSourcePlayable(clipPlayable);
            
            snakePlayableGraph.Play();
        }

        private void StopSnakeAnimation()
        {
            if (snakePlayableGraph.IsValid())
            {
                snakePlayableGraph.Destroy();
            }
        }

        private void OnDestroy()
        {
            StopFlyAnimation();
            StopSnakeAnimation();
        }

        private void SetVisualsEnabled(bool enabled)
        {
            var r = GetComponent<Renderer>();
            if (r != null) r.enabled = enabled;
            foreach (var childRenderer in GetComponentsInChildren<Renderer>())
            {
                childRenderer.enabled = enabled;
            }
            var c = GetComponent<Collider>();
            if (c != null) c.enabled = enabled;
        }

        private void HandleSnake()
        {
            // Swim back and forth along X
            float offset = Mathf.PingPong(Time.time * speed, range) - (range / 2f);
            transform.position = startPos + new Vector3(offset, 0f, 0f);
            
            // Look direction
            float velocityX = Mathf.Cos(Time.time * speed);
            if (velocityX > 0.05f) transform.rotation = Quaternion.Euler(0, 90, 0);
            else if (velocityX < -0.05f) transform.rotation = Quaternion.Euler(0, -90, 0);
        }

        private void HandleFish()
        {
            // Fish swims under water, periodically leaps out
            actionTimer += Time.deltaTime;
            if (actionTimer > 4.5f)
            {
                actionTimer = 0f;
                StartCoroutine(JumpRoutine());
            }
        }

        private System.Collections.IEnumerator JumpRoutine()
        {
            float elapsed = 0f;
            float duration = 0.8f;
            Vector3 peakPos = startPos + Vector3.up * 1.5f; // Jump out of water
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                // Parabolic jump path
                float height = Mathf.Sin(t * Mathf.PI);
                transform.position = Vector3.Lerp(startPos, peakPos, height);
                yield return null;
            }
            transform.position = startPos;
        }

        private void HandleButterfly()
        {
            // Fly in circle path around start pos
            float angle = Time.time * speed;
            float x = startPos.x + Mathf.Cos(angle) * range;
            float z = startPos.z + Mathf.Sin(angle) * range;
            float y = startPos.y + Mathf.Sin(Time.time * 3f) * 0.3f;
            transform.position = new Vector3(x, y, z);
            
            // Face direction of circle path
            Vector3 tangent = new Vector3(-Mathf.Sin(angle), 0.1f * Mathf.Cos(Time.time * 3f), Mathf.Cos(angle));
            transform.rotation = Quaternion.LookRotation(tangent);
        }

        private void HandleDuck()
        {
            // Swim in figure-eight
            float t = Time.time * speed * 0.5f;
            float x = startPos.x + Mathf.Sin(t) * range;
            float z = startPos.z + Mathf.Sin(2f * t) * (range / 2f);
            Vector3 nextPos = new Vector3(x, startPos.y, z);

            Vector3 direction = (nextPos - transform.position).normalized;
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 4.0f * Time.deltaTime);
            }
            transform.position = nextPos;
        }

        private float GetStorkModelHeight(GameObject model)
        {
            if (model == null) return 0f;
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return 0f;
            
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                b.Encapsulate(renderers[i].bounds);
            }
            return b.size.y;
        }
    }
}
