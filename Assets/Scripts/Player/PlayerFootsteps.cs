using UnityEngine;

namespace RungTramTraSu
{
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(CharacterController))]
    public class PlayerFootsteps : MonoBehaviour
    {
        [Header("Audio Clips")]
        public AudioClip[] grassFootsteps;
        public AudioClip[] woodFootsteps; 
        public AudioClip[] stoneFootsteps;
        public AudioClip[] dirtFootsteps;

        [Header("Settings")]
        [SerializeField] private float walkInterval = 0.45f;
        [SerializeField] private float crouchInterval = 0.7f;
        [SerializeField] private float volume = 0.35f;

        private PlayerController playerController;
        private CharacterController characterController;
        private AudioSource audioSource;
        private float footstepTimer;

        private void Awake()
        {
            playerController = GetComponent<PlayerController>();
            characterController = GetComponent<CharacterController>();

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.spatialBlend = 0.1f; 
            audioSource.playOnAwake = false;
            audioSource.loop = false;

            // Generate procedural clips if arrays are empty or unassigned
            if (grassFootsteps == null || grassFootsteps.Length == 0)
            {
                grassFootsteps = new AudioClip[] { CreateSyntheticFootstep("grass") };
            }
            if (woodFootsteps == null || woodFootsteps.Length == 0)
            {
                woodFootsteps = new AudioClip[] { CreateSyntheticFootstep("wood") };
            }
            if (stoneFootsteps == null || stoneFootsteps.Length == 0)
            {
                stoneFootsteps = new AudioClip[] { CreateSyntheticFootstep("stone") };
            }
            if (dirtFootsteps == null || dirtFootsteps.Length == 0)
            {
                dirtFootsteps = new AudioClip[] { CreateSyntheticFootstep("dirt") };
            }
        }

        private AudioClip CreateSyntheticFootstep(string type)
        {
            int sampleRate = 22050;
            float duration = 0.12f;
            int sampleCount = Mathf.RoundToInt(sampleRate * duration);
            float[] samples = new float[sampleCount];

            float freq = 120f;
            float decayFactor = 150f;
            float noiseMix = 0.5f;

            if (type == "wood")
            {
                freq = 95f;
                decayFactor = 70f;
                noiseMix = 0.35f;
            }
            else if (type == "stone")
            {
                freq = 450f;
                decayFactor = 220f;
                noiseMix = 0.7f;
            }
            else if (type == "dirt")
            {
                freq = 150f;
                decayFactor = 120f;
                noiseMix = 0.8f;
            }
            else // grass
            {
                freq = 280f;
                decayFactor = 180f;
                noiseMix = 0.9f;
            }

            for (int i = 0; i < sampleCount; i++)
            {
                float t = (float)i / sampleRate;
                float decay = Mathf.Exp(-t * decayFactor);
                float noise = Random.Range(-1f, 1f) * noiseMix;
                float tone = Mathf.Sin(2f * Mathf.PI * freq * t) * (1f - noiseMix);
                
                samples[i] = (noise + tone) * decay * 0.5f;
            }

            AudioClip clip = AudioClip.Create("SyntheticFootstep_" + type, sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void Update()
        {
            if (characterController == null || !characterController.enabled) return;

            if (!characterController.isGrounded)
            {
                return;
            }

            Vector3 horizontalVelocity = new Vector3(characterController.velocity.x, 0f, characterController.velocity.z);
            if (horizontalVelocity.magnitude < 0.15f)
            {
                footstepTimer = 0.1f;
                return;
            }

            footstepTimer -= Time.deltaTime;
            if (footstepTimer <= 0f)
            {
                string surface = DetectSurface();
                PlayFootstep(surface);
                footstepTimer = playerController.IsCrouching ? crouchInterval : walkInterval;
            }
        }

        private string DetectSurface()
        {
            if (Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, out RaycastHit hit, 1.5f))
            {
                string hitName = hit.collider.gameObject.name.ToLower();
                
                Transform parent = hit.collider.transform.parent;
                while (parent != null)
                {
                    hitName += "/" + parent.gameObject.name.ToLower();
                    parent = parent.parent;
                }

                if (hitName.Contains("wood") || hitName.Contains("bridge") || 
                    hitName.Contains("house") || hitName.Contains("plank") || 
                    hitName.Contains("walkway") || hitName.Contains("deck") || hitName.Contains("go"))
                {
                    return "wood";
                }
                
                if (hitName.Contains("stone") || hitName.Contains("rock") || 
                    hitName.Contains("boulder") || hitName.Contains("concrete") || 
                    hitName.Contains("brick") || hitName.Contains("gravel") || hitName.Contains("da"))
                {
                    return "stone";
                }

                if (hitName.Contains("dirt") || hitName.Contains("mud") || 
                    hitName.Contains("soil") || hitName.Contains("ground") || 
                    hitName.Contains("dat"))
                {
                    return "dirt";
                }
            }
            return "grass";
        }

        private void PlayFootstep(string surface)
        {
            AudioClip[] clips = grassFootsteps;
            if (surface == "wood")
            {
                clips = woodFootsteps;
            }
            else if (surface == "stone")
            {
                clips = stoneFootsteps;
            }
            else if (surface == "dirt")
            {
                clips = dirtFootsteps;
            }

            if (clips == null || clips.Length == 0)
            {
                clips = grassFootsteps;
            }
            if (clips == null || clips.Length == 0) return;

            int index = Random.Range(0, clips.Length);
            AudioClip clip = clips[index];
            if (clip == null) return;

            float currentPitch = Random.Range(0.85f, 1.15f);
            float currentVolume = volume;

            if (surface == "wood")
            {
                currentPitch = Random.Range(0.9f, 1.1f);
                currentVolume = volume * 1.1f;
            }
            else if (surface == "stone")
            {
                currentPitch = Random.Range(0.95f, 1.05f);
                currentVolume = volume * 0.9f;
            }
            else if (surface == "dirt")
            {
                currentPitch = Random.Range(0.9f, 1.1f);
                currentVolume = volume * 0.95f;
            }
            else // grass
            {
                currentPitch = Random.Range(0.9f, 1.1f);
                currentVolume = volume;
            }

            audioSource.pitch = currentPitch;
            audioSource.PlayOneShot(clip, currentVolume);
        }
    }
}
