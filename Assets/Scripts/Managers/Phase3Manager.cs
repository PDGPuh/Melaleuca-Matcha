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
        private bool flock1Triggered = false;
        private bool flock2Triggered = false;
        private bool flock3Triggered = false;

        private string[] craneStoryDialogue = new string[]
        {
            "Nước trôi lững lờ mát mẻ quá con há. Khúc này rừng tràm rậm rạp và hoang sơ nhất đó.",
            "Kìa, con nhìn chốt gác kiểm lâm cao nghệu bên phải kìa. Mùa khô các chú kiểm lâm trực trên đó để canh lửa rừng và bảo vệ chim sếu đó.",
            "Còn bên trái đằng kia là nhà sàn đầm lầy của mấy chú gác rừng đêm, có bến xuồng nhỏ xinh ghê chưa.",
            "Con ngước nhìn mấy vệt nắng chiếu xiên qua kẽ lá kìa, đẹp y chang tranh vẽ vậy.",
            "Hồi ngoại còn nhỏ bằng con, vùng đất này sếu đầu đỏ tụi nó về nhiều vô số kể. Sếu đầu đỏ là loài chim quý lắm, cao kiêu sa, sải cánh rộng nhảy múa trên thảm bèo xanh mướt.",
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
            // Generate waypoints along the actual river channel valley path
            float bzStart = -45f;
            float bzEnd = 48f;
            float bStep = 2.0f;
            for (float z = bzStart; z <= bzEnd; z += bStep)
            {
                float x = GetRiverX(z);
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

            if (photoCamera != null)
            {
                photoCamera.UnlockCamera();
                photoCamera.SetPhotoCategory("Phase3_Canal");
                // Set thông tin chủ thể mặc định – Phase 3 là chụp tự do ngắm cảnh
                photoCamera.SetSubjectInfo(
                    "🌿 Rừng Tràm Trà Sư",
                    "Rừng tràm Trà Sư là khu bảo tồn đất ngập nước quan trọng tại An Giang,\n" +
                    "nơi trú ngụ của hàng trăm loài chim và sinh vật hoang dã quý hiếm."
                );
            }

            UpdateObjectiveText("Thư giãn ngắm cảnh rừng tràm rậm rạp và lắng nghe Ông Ngoại kể chuyện...");
            StartCoroutine(StoryRoutine());
        }

        private IEnumerator StoryRoutine()
        {
            yield return new WaitForSeconds(1.5f);
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
                CheckBirdSpawning();
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
            // Bug 9 fix: Không override targetPos.y bằng boat.position.y
            // Cho phép WaterFloat xử lý Y động lực, waypoint chỉ định hướng X/Z
            // Nếu có WaterFloat thì nó sẽ giữ đúng Y; waypoint Y = -0.82f là giá trị mặc định
            boat.position = Vector3.MoveTowards(boat.position, targetPos, boatSpeed * Time.deltaTime);

            Vector3 direction = targetPos - boat.position;
            direction.y = 0f;
            direction = direction.normalized;
            if (direction != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, -90f, 0f);
                boat.rotation = Quaternion.Slerp(boat.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }

            Vector3 planarDelta = targetPos - boat.position;
            planarDelta.y = 0f;
            if (planarDelta.magnitude < 0.6f)
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
            // Bug 10 fix: Thêm timeout fallback (30s) để tránh block vĩnh viễn
            // nếu DialogueManager null hoặc dialogue không bao giờ complete
            if (!dialogueCompleted)
            {
                float timeoutSeconds = 30f;
                float elapsed = 0f;
                while (!dialogueCompleted && elapsed < timeoutSeconds)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
                if (!dialogueCompleted)
                {
                    Debug.LogWarning("[Phase3Manager] Dialogue timeout sau " + timeoutSeconds + "s, tiếp tục chuyển cảnh.");
                }
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
                ScreenFader.Instance.StartFadeOut(2.5f, () =>
                {
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

        private float GetRiverX(float z)
        {
            if (z <= -45f) return 1.0f;
            if (z <= -35f) return Mathf.Lerp(1.0f, 1.5f, (z - -45f) / 10f);
            if (z <= -25f) return Mathf.Lerp(1.5f, -3.0f, (z - -35f) / 10f);
            if (z <= -15f) return Mathf.Lerp(-3.0f, -6.0f, (z - -25f) / 10f);
            if (z <= -5f) return Mathf.Lerp(-6.0f, -14.5f, (z - -15f) / 10f);
            if (z <= 5f) return Mathf.Lerp(-14.5f, -22.0f, (z - -5f) / 10f);
            if (z <= 15f) return Mathf.Lerp(-22.0f, -25.0f, (z - 5f) / 10f);
            if (z <= 25f) return Mathf.Lerp(-25.0f, -37.5f, (z - 15f) / 10f);
            if (z <= 35f) return Mathf.Lerp(-37.5f, -45.5f, (z - 25f) / 10f);
            if (z <= 45f) return Mathf.Lerp(-45.5f, -55.0f, (z - 35f) / 10f);
            return Mathf.Lerp(-55.0f, -60.0f, (z - 45f) / 5f);
        }

        private void CheckBirdSpawning()
        {
            if (boat == null) return;
            float z = boat.position.z;

            // Trigger Flock 1 at Z = -35f
            // Bug 11 fix: mở rộng window từ 5 lên 15 đơn vị Z để không bỏ lỡ
            if (!flock1Triggered && z >= -35f && z < -20f)
            {
                flock1Triggered = true;
                SpawnFlyingFlock(
                    startPos: new Vector3(-45f, 15f, -25f),
                    endPos: new Vector3(35f, 18f, -15f),
                    birdCount: 5,
                    speed: 4.5f,
                    scale: 1.8f
                );
            }

            // Trigger Flock 2 at Z = 0f
            if (!flock2Triggered && z >= 0f && z < 15f)
            {
                flock2Triggered = true;
                SpawnFlyingFlock(
                    startPos: new Vector3(30f, 12f, 15f),
                    endPos: new Vector3(-50f, 14f, 5f),
                    birdCount: 6,
                    speed: 5.5f,
                    scale: 1.2f
                );
            }

            // Trigger Flock 3 at Z = 25f
            if (!flock3Triggered && z >= 25f && z < 40f)
            {
                flock3Triggered = true;
                SpawnFlyingFlock(
                    startPos: new Vector3(-35f, 16f, 15f),
                    endPos: new Vector3(25f, 18f, 45f),
                    birdCount: 5,
                    speed: 5.0f,
                    scale: 1.5f
                );
            }
        }

        private void SpawnFlyingFlock(Vector3 startPos, Vector3 endPos, int birdCount, float speed, float scale)
        {
            string[] species = new string[] { "lb_robinHQ", "lb_sparrowHQ", "lb_goldFinchHQ", "lb_blueJayHQ", "lb_cardinalHQ" };
            List<GameObject> birds = new List<GameObject>();

            for (int i = 0; i < birdCount; i++)
            {
                string prefabName = species[Random.Range(0, species.Length)];
                GameObject prefab = Resources.Load<GameObject>(prefabName);
                if (prefab == null) continue;

                Vector3 offset = new Vector3(Random.Range(-3f, 3f), Random.Range(-1.5f, 1.5f), Random.Range(-3f, 3f));
                GameObject bird = Instantiate(prefab, startPos + offset, Quaternion.identity);
                bird.transform.localScale = Vector3.one * scale;

                var anim = bird.GetComponent<Animator>();
                if (anim != null)
                {
                    anim.SetBool("flying", true);
                }

                var lbBird = bird.GetComponent<lb_Bird>();
                if (lbBird != null)
                {
                    lbBird.enabled = false;
                    Destroy(lbBird);
                }

                birds.Add(bird);
            }

            StartCoroutine(FlyFlockRoutine(birds, startPos, endPos, speed));
        }

        private IEnumerator FlyFlockRoutine(List<GameObject> birds, Vector3 startPos, Vector3 endPos, float speed)
        {
            float duration = Vector3.Distance(startPos, endPos) / speed;
            float elapsed = 0f;

            Vector3 direction = (endPos - startPos).normalized;
            Quaternion lookRot = Quaternion.LookRotation(direction);
            foreach (var bird in birds)
            {
                if (bird != null) bird.transform.rotation = lookRot;
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;

                for (int i = 0; i < birds.Count; i++)
                {
                    var bird = birds[i];
                    if (bird != null)
                    {
                        Vector3 basePos = Vector3.Lerp(startPos, endPos, progress);
                        Vector3 offset = new Vector3(
                            (i % 3 - 1) * 1.5f,
                            Mathf.Sin(Time.time * 2f + i) * 0.5f,
                            (i / 3 - 1) * 1.5f
                        );
                        bird.transform.position = basePos + offset;
                    }
                }
                yield return null;
            }

            foreach (var bird in birds)
            {
                if (bird != null) Destroy(bird);
            }
        }
    }
}
