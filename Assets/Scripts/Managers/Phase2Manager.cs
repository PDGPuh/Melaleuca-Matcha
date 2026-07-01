using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

namespace RungTramTraSu
{
    public class Phase2Manager : MonoBehaviour
    {
        public static Phase2Manager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private Transform boat;
        [SerializeField] private Transform player;
        [SerializeField] private TextMeshProUGUI objectiveText;
        [SerializeField] private PhotoCamera photoCamera;
        [SerializeField] private Transform sunRayTarget;
        [SerializeField] private Transform storkTarget;
        [SerializeField] private GameObject storksFlock;
        [SerializeField] private List<GameObject> birdPrefabs = new List<GameObject>();

        [Header("Movement Settings")]
        [SerializeField] private float boatSpeed = 3.5f;
        [SerializeField] private float rotationSpeed = 3.0f;

        private List<Vector3> waypoints = new List<Vector3>();
        private int currentWaypointIndex = 0;
        private bool isTravelling = true;
        private bool photoCaptured = false;
        private bool event1Triggered = false;
        private bool event2Triggered = false;

        private bool isAtCheckpoint = false;
        private int birdsCapturedAtCurrentCheckpoint = 0;
        private List<GameObject> activeBirds = new List<GameObject>();
        private int currentCheckpoint = 0; // 0, 1, 2, 3
        private Coroutine flightCoroutine;
        private bool checkpoint1Triggered = false;
        private bool checkpoint2Triggered = false;
        private bool checkpoint3Triggered = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            // Find water height to set waypoints Y dynamically (prevents Y fighting/jittering)
            float waterY = 3.70f;
            GameObject waterGo = GameObject.Find("RiverWater_Canal");
            if (waterGo != null)
            {
                waterY = waterGo.transform.position.y;
            }
            float boatY = waterY + 0.18f; // Offsets Y so boat draft aligns smoothly with WaterFloat

            // Generate winding waypoints based on the canal curve formula
            // Start from boat's initial Z to prevent backwards movement and 180 spin
            float zStart = boat != null ? boat.position.z : -52.02f;
            float zEnd = 58f;
            float step = 2.0f;
            for (float z = zStart; z <= zEnd; z += step)
            {
                float canalX = 25f + Mathf.Sin(z * 0.08f) * 5f;
                float x = canalX;
                if (z == zStart)
                {
                    x = 25f; // start at boat's initial position next to pier
                }
                else if (z <= zStart + 2.0f)
                {
                    x = Mathf.Lerp(25f, canalX, 0.5f);
                }
                // Height of boat sits on water dynamically
                waypoints.Add(new Vector3(x, boatY, z));
            }

            // Put player on the boat
            if (player != null && boat != null)
            {
                player.SetParent(boat);
                
                // Bù trừ tỷ lệ scale của thuyền để player không bị phóng to và bay lên trời
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

                // Tắt CharacterController để player di chuyển theo Parent (thuyền) một cách chính xác
                var cc = player.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
            }

            if (storksFlock != null) storksFlock.SetActive(false);

            UpdateObjectiveText("Nhìn ngắm phong cảnh. Ông Ngoại đang chèo xuồng đưa bạn đi...");
            StartCoroutine(GrandpaIntroDialogue());
        }

        private IEnumerator GrandpaIntroDialogue()
        {
            yield return new WaitForSeconds(3f);
            string[] intro = new string[] {
                "Con thấy cảnh quan sông nước miền Tây mình rộng lớn không?",
                "Nước nổi lên là bèo tấm phủ xanh um hết trơn hà, nhìn giống như một thảm lụa vậy đó con.",
                "Hai bên bờ sông tràm mọc san sát nhau, che mát cả dòng kênh. Gió thổi bập bùng nghe sướng tai lạ lùng."
            };
            DialogueManager.Instance.ShowDialogue("Ông Ngoại", intro);
        }

        // Guard tránh double-capture khi coroutine đang chạy
        private bool isHandlingCapture = false;

        private void Update()
        {
            if (isTravelling)
            {
                MoveBoat();
                CheckEvents();
            }

            // Kiểm tra ngắm chụp chim khi đang dừng ở Checkpoint
            // Sử dụng coroutine để có thể chụp screenshot async + show PhotoResultUI
            if (isAtCheckpoint && !isHandlingCapture &&
                Mouse.current.leftButton.wasPressedThisFrame && photoCamera != null)
            {
                if (photoCamera.IsZooming)
                {
                    StartCoroutine(BirdCaptureWithPhotoRoutine());
                }
            }
        }

        private void MoveBoat()
        {
            if (waypoints.Count == 0 || currentWaypointIndex >= waypoints.Count || boat == null)
            {
                // Reached the end wood pier!
                ReachEnd();
                return;
            }

            Vector3 targetPos = waypoints[currentWaypointIndex];
            boat.position = Vector3.MoveTowards(boat.position, targetPos, boatSpeed * Time.deltaTime);

            // Rotate smoothly towards the waypoint
            Vector3 direction = (targetPos - boat.position).normalized;
            if (direction != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, -90f, 0f);
                boat.rotation = Quaternion.Slerp(boat.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }

            if (Vector3.Distance(boat.position, targetPos) < 0.5f)
            {
                currentWaypointIndex++;
            }
        }

        private void CheckEvents()
        {
            float z = boat.position.z;

            // Checkpoint 1: Z >= -20f
            // Bug 5 fix: bỏ upper bound để không bao giờ bỏ lỡ nếu boat bắt đầu ở Z cao hơn
            if (!checkpoint1Triggered && z >= -20f)
            {
                checkpoint1Triggered = true;
                TriggerCheckpoint(1, 1.8f, "Phase2_Ch1", "Nhấn chuột phải ngắm, trái chụp 3 con chim bay (Tốc độ CHẬM).");
            }

            // Checkpoint 2: Z >= 15f (sau khi đã qua Checkpoint 1)
            if (!checkpoint2Triggered && z >= 15f)
            {
                checkpoint2Triggered = true;
                TriggerCheckpoint(2, 4.0f, "Phase2_Ch2", "Nhấn chuột phải ngắm, trái chụp 3 con chim bay (Tốc độ VỪA).");
            }

            // Checkpoint 3: Z >= 40f
            if (!checkpoint3Triggered && z >= 40f)
            {
                checkpoint3Triggered = true;
                TriggerCheckpoint(3, 7.5f, "Phase2_Ch3", "Nhấn chuột phải ngắm, trái chụp 3 con chim bay (Tốc độ NHANH).");
            }
        }

        private bool sarusCraneCapturedAtCurrentCheckpoint = false;

        private struct BirdInfo
        {
            public string vietnameseName;
            public Color bodyColor;
            public Color headColor;
            public Vector3 scale;
            public string description;
        }

        private static readonly BirdInfo[] Level1Species = new BirdInfo[]
        {
            new BirdInfo { vietnameseName = "Cò trắng", bodyColor = Color.white, headColor = Color.white, scale = new Vector3(0.7f, 0.15f, 0.5f), description = "Cò trắng bay lững lờ trên ngọn tràm." },
            new BirdInfo { vietnameseName = "Diệc xám", bodyColor = new Color(0.5f, 0.55f, 0.6f), headColor = new Color(0.5f, 0.55f, 0.6f), scale = new Vector3(0.9f, 0.2f, 0.6f), description = "Diệc xám bay điềm tĩnh." },
            new BirdInfo { vietnameseName = "Cò ốc", bodyColor = new Color(0.85f, 0.85f, 0.85f), headColor = new Color(0.2f, 0.2f, 0.2f), scale = new Vector3(0.8f, 0.2f, 0.5f), description = "Cò ốc bay hơi nặng nề." },
            new BirdInfo { vietnameseName = "Già đẫy", bodyColor = new Color(0.3f, 0.3f, 0.3f), headColor = new Color(0.9f, 0.6f, 0.6f), scale = new Vector3(1.0f, 0.25f, 0.7f), description = "Già đẫy to lớn bay lờ đờ." }
        };

        private static readonly BirdInfo[] Level2Species = new BirdInfo[]
        {
            new BirdInfo { vietnameseName = "Vạc", bodyColor = new Color(0.2f, 0.3f, 0.4f), headColor = new Color(0.2f, 0.3f, 0.4f), scale = new Vector3(0.6f, 0.2f, 0.45f), description = "Vạc bay tầm thấp đều nhịp." },
            new BirdInfo { vietnameseName = "Cồng cộc", bodyColor = new Color(0.05f, 0.05f, 0.05f), headColor = new Color(0.05f, 0.05f, 0.05f), scale = new Vector3(0.5f, 0.12f, 0.4f), description = "Cồng cộc bay thẳng đường vỗ cánh liên tục." },
            new BirdInfo { vietnameseName = "Cò bợ", bodyColor = new Color(0.55f, 0.45f, 0.35f), headColor = Color.white, scale = new Vector3(0.6f, 0.15f, 0.45f), description = "Cò bợ bay khoe đôi cánh trắng." },
            new BirdInfo { vietnameseName = "Trích cùi", bodyColor = new Color(0.3f, 0.2f, 0.6f), headColor = Color.red, scale = new Vector3(0.55f, 0.18f, 0.45f), description = "Trích cùi mỏ đỏ rực sặc sỡ." },
            new BirdInfo { vietnameseName = "Điêng điểng", bodyColor = new Color(0.15f, 0.15f, 0.15f), headColor = new Color(0.15f, 0.15f, 0.15f), scale = new Vector3(0.7f, 0.12f, 0.5f), description = "Điêng điểng cổ rắn dài ngoằn ngoèo." }
        };

        private static readonly BirdInfo[] Level3Species = new BirdInfo[]
        {
            new BirdInfo { vietnameseName = "Bói cá", bodyColor = new Color(0f, 0.7f, 0.9f), headColor = new Color(0.9f, 0.4f, 0.1f), scale = new Vector3(0.3f, 0.08f, 0.25f), description = "Bói cá nhỏ xíu xẹt ngang như mũi tên." },
            new BirdInfo { vietnameseName = "Le le", bodyColor = new Color(0.65f, 0.5f, 0.35f), headColor = new Color(0.65f, 0.5f, 0.35f), scale = new Vector3(0.4f, 0.12f, 0.35f), description = "Le le vỗ cánh cực nhanh sát mặt nước." },
            new BirdInfo { vietnameseName = "Bìm bịp", bodyColor = new Color(0.1f, 0.1f, 0.1f), headColor = new Color(0.6f, 0.3f, 0.1f), scale = new Vector3(0.6f, 0.15f, 0.45f), description = "Bìm bịp cánh nâu đỏ bay chuyền bụi rậm." },
            new BirdInfo { vietnameseName = "Én", bodyColor = new Color(0.1f, 0.12f, 0.2f), headColor = new Color(0.1f, 0.12f, 0.2f), scale = new Vector3(0.25f, 0.06f, 0.2f), description = "Chim én lượn nhanh đổi hướng liên tục." }
        };

        private static readonly BirdInfo SarusCraneSpecies = new BirdInfo
        {
            vietnameseName = "Sếu đầu đỏ",
            bodyColor = new Color(0.7f, 0.7f, 0.7f),
            headColor = new Color(0.9f, 0.1f, 0.1f),
            scale = new Vector3(1.1f, 0.22f, 0.8f),
            description = "Cực phẩm Sếu đầu đỏ quý hiếm xuất hiện!"
        };

        private void TriggerCheckpoint(int number, float birdSpeed, string category, string instructionText)
        {
            isTravelling = false;
            isAtCheckpoint = true;
            currentCheckpoint = number;
            birdsCapturedAtCurrentCheckpoint = 0;
            sarusCraneCapturedAtCurrentCheckpoint = false;

            if (photoCamera != null)
            {
                photoCamera.UnlockCamera();
                photoCamera.SetPhotoCategory(category);
                // Bug 6 fix: Tắt auto-capture của PhotoCamera để tránh double-capture
                // Phase2Manager tự xử lý click qua CheckBirdCapture()
                photoCamera.SetCaptureEnabled(false);
            }

            UpdateObjectiveText($"Checkpoint {number}: {instructionText} (0/3)");
            DialogueManager.Instance.ShowDialogue("Ông Ngoại", new string[] {
                "Tới Checkpoint rồi nè con. Chim sắp sửa bay ngang qua đó con.",
                "Con lấy máy ảnh ra sẵn đi, ngắm sẵn rồi chụp nghe!"
            });

            // Sinh đàn chim và bắt đầu bay
            SpawnBirdFlock(boat.position.z);
            if (flightCoroutine != null) StopCoroutine(flightCoroutine);
            flightCoroutine = StartCoroutine(FlightRoutine(birdSpeed, boat.position.z));
        }

        private void SpawnBirdFlock(float zCenter)
        {
            ClearActiveBirds();

            int count = Random.Range(5, 8);
            string[] species = new string[] { "lb_robinHQ", "lb_sparrowHQ", "lb_goldFinchHQ", "lb_blueJayHQ", "lb_cardinalHQ" };

            for (int i = 0; i < count; i++)
            {
                GameObject prefab = null;
                if (birdPrefabs != null && birdPrefabs.Count > 0)
                {
                    prefab = birdPrefabs[Random.Range(0, birdPrefabs.Count)];
                }

                if (prefab == null)
                {
                    string prefabName = species[Random.Range(0, species.Length)];
                    prefab = Resources.Load<GameObject>(prefabName);
                }

                if (prefab == null) continue;

                GameObject bird = Instantiate(prefab, Vector3.zero, Quaternion.identity);
                bird.name = "CheckpointBird_" + i;

                // Disable and destroy lb_Bird script to prevent it from running its own AI behavior
                var lbBird = bird.GetComponent<lb_Bird>();
                if (lbBird != null)
                {
                    lbBird.enabled = false;
                    Destroy(lbBird);
                }

                // Force play flying animation state
                var anim = bird.GetComponent<Animator>();
                if (anim != null)
                {
                    anim.SetBool("flying", true);
                }

                // Make sure it has a sphere collider for any photo/viewport checks
                var col = bird.GetComponent<Collider>();
                if (col == null)
                {
                    var scol = bird.AddComponent<SphereCollider>();
                    scol.isTrigger = true;
                    scol.radius = 1.5f;
                }

                // Add BirdDataHolder for metadata and Sarus Crane detection
                var birdInfoHolder = bird.AddComponent<BirdDataHolder>();
                birdInfoHolder.vietnameseName = GetBirdNameFromPrefab(prefab);
                
                // 15% chance to spawn a Sarus Crane representation (scaled up)
                if (i == 0 && Random.value < 0.15f)
                {
                    birdInfoHolder.isSarus = true;
                    birdInfoHolder.vietnameseName = "Sếu đầu đỏ";
                    bird.name = "Sarus_Crane";
                    bird.transform.localScale = Vector3.one * 2.2f;
                }
                else
                {
                    birdInfoHolder.isSarus = false;
                }

                activeBirds.Add(bird);
            }
        }

        private void ClearActiveBirds()
        {
            foreach (var bird in activeBirds)
            {
                if (bird != null) Destroy(bird);
            }
            activeBirds.Clear();
        }

        private string TranslateToLocalBird(string engName)
        {
            switch (engName)
            {
                case "robin": return "Cò bợ";
                case "sparrow": return "Bìm bịp";
                case "goldFinch": return "Le le";
                case "blueJay": return "Cồng cộc";
                case "cardinal": return "Vạc";
                default: return "Chim hoang dã";
            }
        }

        private string GetBirdNameFromPrefab(GameObject prefab)
        {
            if (prefab == null) return "Chim hoang dã";

            string engName = prefab.name.Replace("(Clone)", "").Replace("lb_", "").Replace("HQ", "");
            return TranslateToLocalBird(engName);
        }

        private IEnumerator FlightRoutine(float speed, float zCenter)
        {
            while (isAtCheckpoint)
            {
                float t = 0f;
                float duration = 28f / speed;

                // Reset positions to start (left side of canal)
                for (int i = activeBirds.Count - 1; i >= 0; i--)
                {
                    if (activeBirds[i] != null)
                    {
                        activeBirds[i].transform.position = new Vector3(10f - i * 0.8f, 12f + Mathf.PingPong(i, 2f), zCenter + 16f + Random.Range(-1f, 1f));
                        activeBirds[i].SetActive(true);
                    }
                }

                // Fly left-to-right crossing the canal in front of the boat
                while (t < duration && isAtCheckpoint)
                {
                    t += Time.deltaTime;
                    float progress = t / duration;

                    for (int i = activeBirds.Count - 1; i >= 0; i--)
                    {
                        if (activeBirds[i] != null)
                        {
                            float curX = Mathf.Lerp(10f - i * 0.8f, 38f, progress);
                            float curY = 12f + Mathf.PingPong(i + Time.time, 2.5f);
                            float curZ = zCenter + 16f - progress * 4f;
                            activeBirds[i].transform.position = new Vector3(curX, curY, curZ);
                        }
                    }
                    yield return null;
                }

                yield return new WaitForSeconds(0.8f);
            }
        }

        // ─── Bảng mô tả các loài chim theo tên tiếng Việt ──────────────────────────────
        private static readonly System.Collections.Generic.Dictionary<string, string> BirdDescriptions =
            new System.Collections.Generic.Dictionary<string, string>
        {
            { "Cò trắng",    "Áo trắng muốt, chân đen đặc trưng. Cò trắng bay lướt trên ngọn tràm, kiếm ăn ở vùng đất ngập nước miền Tây." },
            { "Diệc xám",    "Diệc xám là loài chim lớn bay điềm tĩnh, cổ dài đặc trưng, thường đứng cô đơn trên mặt nước." },
            { "Cò ốc",       "Cò ốc có mỏ cong chuyên ăn ốc và nhuyễn thể nước. Là loài quen thuộc của đồng bằng sông Cửu Long." },
            { "Già đẫy",     "Già đẫy có màu nâu xám, bay chậm rãi một mình. Loài này rất hiếm gặp ở các khu rừng ngập nước." },
            { "Vạc",         "Vạc thường kiếm ăn vào ban đêm. Ban ngày chúng trú ẩn trong tán tràm rậm rạp gần mặt nước." },
            { "Cồng cộc",    "Cồng cộc hay lặn sâu xuống nước bắt cá. Lông đen bóng, mắt xanh lá đặc biệt nổi bật." },
            { "Cò bợ",       "Cò bợ nhỏ nhắn, thường kiếm ăn ở rìa đầm lầy. Thành lập từng đàn lớn theo mùa nước nổi." },
            { "Trích cùi",   "Trích cùi nhỏ nhắn, bay lướt trên mặt nước. Là loài chim di cư quý theo mùa." },
            { "Điêng điểng", "Điêng điểng có màu nâu đốm, tiếng kêu vắng vỏi giữa rừng tràm yên tĩnh." },
            { "Bói cá",      "Bói cá sặc sỡ nhất trong số các loài chim rừng tràm – đầu xanh, ngực cam rực rỡ." },
            { "Le le",       "Le le là loài vịt hoang phổ biến miền Tây, thường lướt qua mặt kênh theo đàn lớn." },
            { "Bìm bịp",     "Bìm bịp tiếng kêu như tiếng búa lớn, thường ẩn mình trong bụi sậy và được người nông thôn mến yêu." },
            { "Én",          "Én bay lướt rất nhanh – dấu hiệu báo mưa đến gần theo tiết trời miền Tây." },
            { "Sếu đầu đỏ",  "Loài chim quý nằm trong Sách Đỏ Việt Nam! Sếu đầu đỏ rất hiếm gặp, là biểu tượng của sự bảo tồn thiên nhiên Việt Nam." },
        };

        private string GetBirdDescription(string birdName)
        {
            if (BirdDescriptions.TryGetValue(birdName, out string desc))
                return desc;
            return "Một loài chim của vùng đất ngập nước miền Tây Nam Bộ.";
        }

        // ─── Coroutine: chụp screenshot + hiển thị PhotoResultUI ─────────────────────
        private IEnumerator BirdCaptureWithPhotoRoutine()
        {
            isHandlingCapture = true;

            // 1. Phát hiện chim trong viewport (sync)
            if (activeBirds.Count == 0) { isHandlingCapture = false; yield break; }
            Camera cam = Camera.main;
            if (cam == null) { isHandlingCapture = false; yield break; }

            int hits = 0;
            List<GameObject> capturedThisFrame = new List<GameObject>();
            string firstBirdName = "Chim hoang dã";
            bool firstIsSarus = false;

            foreach (var bird in activeBirds)
            {
                if (bird == null) continue;
                Vector3 vp = cam.WorldToViewportPoint(bird.transform.position);
                if (vp.z > 0 && vp.x >= 0.22f && vp.x <= 0.78f && vp.y >= 0.22f && vp.y <= 0.78f)
                {
                    // Occlusion check: chỉ kiểm tra xem có vật thể nào ở phía TRƯỚC che khuất con chim không
                    Vector3 dir = bird.transform.position - cam.transform.position;
                    float checkDist = dir.magnitude - 0.5f;
                    if (checkDist > 0.5f)
                    {
                        RaycastHit[] raycastHits = Physics.RaycastAll(cam.transform.position, dir.normalized, checkDist, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                        bool isOccluded = false;
                        foreach (var hit in raycastHits)
                        {
                            // Bỏ qua thuyền, người chơi, ông ngoại và các vật phẩm gắn trên thuyền
                            if (hit.transform == boat || hit.transform.IsChildOf(boat)) continue;
                            if (player != null && (hit.transform == player || hit.transform.IsChildOf(player))) continue;

                            isOccluded = true;
                            break;
                        }
                        if (isOccluded)
                        {
                            continue; // Có vật cản thực sự ở trước
                        }
                    }

                    hits++;
                    capturedThisFrame.Add(bird);

                    var data = bird.GetComponent<BirdDataHolder>();
                    if (data != null)
                    {
                        if (hits == 1) // chỉ lấy tên con đầu tiên cho UI
                        {
                            firstBirdName = data.vietnameseName;
                            firstIsSarus  = data.isSarus;
                        }
                        if (data.isSarus) sarusCraneCapturedAtCurrentCheckpoint = true;
                    }
                }
            }

            // Ẩn HUD GameUI để không bị dính chữ mục tiêu vào ảnh chụp
            GameObject gameUI = GameObject.Find("GameUI");
            if (gameUI != null) gameUI.SetActive(false);

            // 2. Chụp screenshot sau WaitForEndOfFrame (trước khi flash sáng trắng)
            yield return new WaitForEndOfFrame();
            Texture2D screenshot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            screenshot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            screenshot.Apply();

            // Khôi phục HUD GameUI
            if (gameUI != null) gameUI.SetActive(true);

            // Kích hoạt hiệu ứng flash + âm thanh chụp
            if (photoCamera != null)
            {
                photoCamera.PlayShutterAndFlash();
            }

            // Lưu ảnh vào PersistentGameManager
            string category = "Phase2_Ch" + currentCheckpoint;
            if (PersistentGameManager.Instance != null)
                PersistentGameManager.Instance.SavePhoto(category, screenshot);

            // 3. Thiết lập thông tin và hiển thị PhotoResultUI (đã loại bỏ các emoji để tránh hiển thị ký tự ô vuông lỗi)
            string displayName = "Không rõ chủ thể";
            string birdDesc = "Mục tiêu không nằm trong khung ngắm trung tâm. Thử lại nhé!";
            
            if (hits > 0)
            {
                displayName = firstIsSarus ? "Sếu đầu đỏ" : firstBirdName;
                if (hits > 1) displayName += $" (+{hits - 1} con khác)";
                birdDesc = GetBirdDescription(firstBirdName);
            }

            bool uiClosed = false;
            PhotoResultUI.Instance.ShowResult(screenshot, displayName, birdDesc, firstIsSarus,
                onClose: () => uiClosed = true);
            yield return new WaitUntil(() => uiClosed);

            // 4. Xử lý logic checkpoint sau khi UI đóng (chỉ cộng điểm nếu chụp trúng)
            if (hits > 0)
            {
                birdsCapturedAtCurrentCheckpoint += hits;
                foreach (var b in capturedThisFrame)
                {
                    activeBirds.Remove(b);
                    if (b != null) Destroy(b);
                }

                UpdateObjectiveText($"Checkpoint {currentCheckpoint}: Chụp ảnh đàn chim ({birdsCapturedAtCurrentCheckpoint}/3)");

                if (birdsCapturedAtCurrentCheckpoint >= 3)
                {
                    ClearCheckpoint();
                }
            }

            isHandlingCapture = false;
        }

        // Giữ lại để tương thích ngược nếu có code khác gọi
        private void CheckBirdCapture()
        {
            StartCoroutine(BirdCaptureWithPhotoRoutine());
        }


        private void ClearCheckpoint()
        {
            isAtCheckpoint = false;
            ClearActiveBirds();

            if (flightCoroutine != null) StopCoroutine(flightCoroutine);

            // Bug 6 fix: Bật lại PhotoCamera capture khi rời bird-checkpoint mode
            if (photoCamera != null) photoCamera.SetCaptureEnabled(true);

            string[] dialogueLines;
            if (sarusCraneCapturedAtCurrentCheckpoint)
            {
                dialogueLines = new string[] {
                    "Trời đất ơi con ơi! Con chụp dính con Sếu đầu đỏ kìa! Loài này cực kỳ quý hiếm luôn đó con, lâu lắm rồi ông mới thấy lại tụi nó bay về đây. Tấm hình này thực sự là vô giá đó con. Thôi, ông cháu mình nổ máy đi tiếp nghen!"
                };
            }
            else
            {
                dialogueLines = new string[] {
                    "Ừa giỏi quá con ơi! Chụp dính rồi kìa. Được mấy tấm hình đẹp rồi đó. Ông cháu mình nổ máy đi tiếp nghen."
                };
            }

            DialogueManager.Instance.ShowDialogue("Ông Ngoại", dialogueLines, () => {
                isTravelling = true;
                UpdateObjectiveText("Nhìn ngắm phong cảnh. Ông Ngoại đang chèo xuồng đưa bạn đi...");
            });
        }

        public void OnPhotoQuestCompleted()
        {
            // Handled internally in CheckBirdCapture
        }

        private void OnDestroy()
        {
            // Bug 8 fix: Dọn dẹp khi scene unload để tránh MissingReferenceException
            isAtCheckpoint = false;
            isTravelling = false;
            if (flightCoroutine != null)
            {
                StopCoroutine(flightCoroutine);
                flightCoroutine = null;
            }
            ClearActiveBirds();
            // Bật lại capture nếu bị tắt trước khi destroy
            if (photoCamera != null) photoCamera.SetCaptureEnabled(true);
        }

        private void ReachEnd()
        {
            isTravelling = false;
            UpdateObjectiveText("Xuồng cập bến gỗ lõi rừng. Chuẩn bị lên bờ...");
            
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
                    SceneManager.LoadScene("Phase3_BambooBridge");
                });
            }
            else
            {
                SceneManager.LoadScene("Phase3_BambooBridge");
            }
        }

        private void UpdateObjectiveText(string text)
        {
            if (objectiveText != null) objectiveText.text = text;
        }
    }

    public class BirdDataHolder : MonoBehaviour
    {
        public string vietnameseName;
        public bool isSarus;
    }
}


