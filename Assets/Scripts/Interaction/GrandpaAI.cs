using System.Collections.Generic;
using UnityEngine;

namespace RungTramTraSu
{
    public class GrandpaAI : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 2.0f;
        [SerializeField] private float waitDistance = 9.0f;
        [SerializeField] private float resumeDistance = 4.5f;

        private Transform player;
        private List<Vector3> waypoints = new List<Vector3>();
        private int currentWaypointIndex = 0;
        private bool isWaiting = false;
        private float voiceTimer = 0f;
        private float voiceCooldown = 8.0f;

        // Animation
        private Animator animator;
        private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
        private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
        private static readonly int SpeedHash = Animator.StringToHash("Speed");

        private void Start()
        {
            animator = GetComponent<Animator>();

            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null) player = playerObj.transform;

            // Generate bridge path waypoints (matching the bridge coordinates)
            float zStart = -45f;
            float zEnd = 45f;
            float step = 6.0f;
            for (float z = zStart; z <= zEnd; z += step)
            {
                // Simple winding bridge path
                float x = 5f + Mathf.Sin(z * 0.12f) * 6f;
                // Height sits on top of bamboo bridge (Y = -0.42f)
                waypoints.Add(new Vector3(x, -0.42f, z));
            }
        }

        private void Update()
        {
            if (player == null) return;

            float distToPlayer = Vector3.Distance(transform.position, player.position);

            if (isWaiting)
            {
                // Face the player
                Vector3 lookDirection = (player.position - transform.position).normalized;
                lookDirection.y = 0;
                if (lookDirection != Vector3.zero)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(lookDirection), 5.0f * Time.deltaTime);
                }

                // While waiting, play idle animation
                SetAnimation(false, false);

                // Check if player has caught up
                if (distToPlayer < resumeDistance)
                {
                    isWaiting = false;
                    Debug.Log("GrandpaAI: Player caught up, resuming walk.");
                }
                else
                {
                    // Remind the player periodically
                    voiceTimer += Time.deltaTime;
                    if (voiceTimer >= voiceCooldown)
                    {
                        voiceTimer = 0f;
                        CallOutToPlayer();
                    }
                }
            }
            else
            {
                // Check if player is too far
                if (distToPlayer > waitDistance)
                {
                    isWaiting = true;
                    SetAnimation(false, false);
                    voiceTimer = voiceCooldown - 1f; // Call out almost immediately
                    return;
                }

                MoveAlongWaypoints();
            }
        }

        private void MoveAlongWaypoints()
        {
            if (waypoints.Count == 0 || currentWaypointIndex >= waypoints.Count)
            {
                // Reached the end of the bridge!
                SetAnimation(false, false);
                return;
            }

            // Play walking animation while moving
            SetAnimation(true, false);

            Vector3 targetPos = waypoints[currentWaypointIndex];
            transform.position = Vector3.MoveTowards(transform.position, targetPos, walkSpeed * Time.deltaTime);

            Vector3 direction = (targetPos - transform.position).normalized;
            direction.y = 0;
            if (direction != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 6.0f * Time.deltaTime);
            }

            if (Vector3.Distance(transform.position, targetPos) < 0.3f)
            {
                currentWaypointIndex++;
            }
        }

        private void CallOutToPlayer()
        {
            // Call DialogueManager or update Objective text to display grandpa waiting line
            string[] remindText = new string[] {
                "Lẹ lên con! Đi lối này nè con ơi!",
                "Đứng đó chụp hoài hà, qua đây coi rễ tràm thở nước đẹp lắm nè con!"
            };
            
            // Show dialogue in dialogue manager without freezing player (since they are walking)
            // Wait, DialogueManager.ShowDialogue freezes player, so we just log and show in TMPro objective!
            if (Phase3Manager.Instance != null)
            {
                Phase3Manager.Instance.ShowGrandpaWarning(remindText[Random.Range(0, remindText.Length)]);
            }
        }

        public bool ReachedEnd()
        {
            return currentWaypointIndex >= waypoints.Count;
        }

        public Vector3 GetNextSpot()
        {
            if (waypoints.Count == 0 || currentWaypointIndex >= waypoints.Count) return transform.position;
            return waypoints[currentWaypointIndex];
        }

        /// <summary>
        /// Updates the Animator parameters to play the correct animation state.
        /// </summary>
        private void SetAnimation(bool walking, bool running)
        {
            if (animator == null) return;
            animator.SetBool(IsWalkingHash, walking);
            animator.SetBool(IsRunningHash, running);
            float speed = running ? 1.0f : (walking ? 0.5f : 0.0f);
            animator.SetFloat(SpeedHash, speed);
        }
    }
}
