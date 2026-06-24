using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;

namespace RungTramTraSu
{
    public class Phase3Manager : MonoBehaviour
    {
        public static Phase3Manager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private Transform boat;
        [SerializeField] private Transform player;
        [SerializeField] private TextMeshProUGUI objectiveText;
        [SerializeField] private PhotoCamera photoCamera;

        [Header("Movement Settings")]
        [SerializeField] private float boatSpeed = 1.3f; // Rất chậm, thư thái
        [SerializeField] private float rotationSpeed = 2.0f;

        private List<Vector3> waypoints = new List<Vector3>();
        private int currentWaypointIndex = 0;
        private bool isTravelling = true;
        private bool dialogueCompleted = false;

        private string[] craneStoryDialogue = new string[]
        {
            "Nước trôi lững lờ mát mẻ quá con há. Khúc này rừng tràm rập rạp và hoang sơ nhất đó.",
            "Con ngước nhìn mấy vệt nắng (God Rays) chiếu xiên qua kẽ lá kìa, đẹp y chang tranh vẽ vậy.",
            "Hồi ngoại còn nhỏ bằng con, vùng đất này sếu đầu đỏ tụi nó về nhiều vô số kể.",
            "Sếu đầu đỏ là loài chim quý lắm, cao kiêu sa, sải cánh rộng nhảy múa trên thảm bèo xanh mướt.",
            "Tiếc là sau này thiên nhiên thay đổi, tụi nó hiếm dần rồi bỏ đi mất tăm...",
            "Ngoại mong rừng tràm mình giữ được nét hoang sơ này, để một ngày nào đó đàn sếu lại bay về mái nhà xưa."
        };

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            // Generate waypoints along the bamboo bridge canal path
            float bzStart = -45f;
            float bzEnd = 48f;
            float bStep = 2.0f;
            for (float z = bzStart; z <= bzEnd; z += bStep)
            {
                // Follow bridge path but with offset
                float x = 5f + Mathf.Sin(z * 0.12f) * 6f - 3.5f; // Offset to float alongside bridge
                waypoints.Add(new Vector3(x, -0.82f, z));
            }

            // Put player on the boat
            if (player != null && boat != null)
            {
                player.SetParent(boat);
                Vector3 boatScale = boat.localScale;
                player.localScale = new Vector3(1f / boatScale.x, 1f / boatScale.y, 1f / boatScale.z);
                player.localPosition = new Vector3(-1.0f / boatScale.x, 0.3f / boatScale.y, 0f);
                player.localRotation = Quaternion.Euler(0f, 90f, 0f);

                var controller = player.GetComponent<PlayerController>();
                if (controller != null)
                {
                    controller.SetFrozen(false);
                    controller.SetMovementLocked(true);
                }

                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
            }

            UpdateObjectiveText("Thư giãn ngắm cảnh rừng tràm rậm rạp và lắng nghe Ông Ngoại kể chuyện...");
            StartCoroutine(StoryRoutine());
        }

        private IEnumerator StoryRoutine()
        {
            yield return new WaitForSeconds(3.5f);
            bool dialogueDone = false;
            DialogueManager.Instance.ShowDialogue("Ông Ngoại", craneStoryDialogue, () => {
                dialogueDone = true;
            });
            yield return new WaitUntil(() => dialogueDone);
            dialogueCompleted = true;
        }

        private void Update()
        {
            if (isTravelling)
            {
                MoveBoat();
            }
        }

        private void MoveBoat()
        {
            if (waypoints.Count == 0 || currentWaypointIndex >= waypoints.Count || boat == null)
            {
                ReachEnd();
                return;
            }

            Vector3 targetPos = waypoints[currentWaypointIndex];
            boat.position = Vector3.MoveTowards(boat.position, targetPos, boatSpeed * Time.deltaTime);

            Vector3 direction = (targetPos - boat.position).normalized;
            if (direction != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, -90f, 0f);
                boat.rotation = Quaternion.Slerp(boat.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }

            if (Vector3.Distance(boat.position, targetPos) < 0.6f)
            {
                currentWaypointIndex++;
            }
        }

        private void ReachEnd()
        {
            isTravelling = false;
            UpdateObjectiveText("Xuồng neo lại sát bãi đầm lầy. Chuẩn bị bước xuống...");

            StartCoroutine(TransitionRoutine());
        }

        private IEnumerator TransitionRoutine()
        {
            // Wait for dialogue if not finished
            if (!dialogueCompleted)
            {
                yield return new WaitUntil(() => dialogueCompleted);
            }
            yield return new WaitForSeconds(2.0f);

            if (player != null)
            {
                player.SetParent(null);
                player.localScale = Vector3.one;

                var controller = player.GetComponent<PlayerController>();
                if (controller != null)
                {
                    controller.SetMovementLocked(false);
                }

                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = true;
            }

            if (ScreenFader.Instance != null)
            {
                ScreenFader.Instance.StartFadeOut(2.5f, () => {
                    SceneManager.LoadScene("Phase4_Sanctuary");
                });
            }
            else
            {
                SceneManager.LoadScene("Phase4_Sanctuary");
            }
        }

        private void UpdateObjectiveText(string text)
        {
            if (objectiveText != null) objectiveText.text = text;
        }

        public void ShowGrandpaWarning(string warning)
        {
            if (objectiveText != null) objectiveText.text = warning;
        }

        public void OnPhotoQuestCompleted()
        {
            // Handled internally or no photo quest in Phase 3
        }
    }
}
