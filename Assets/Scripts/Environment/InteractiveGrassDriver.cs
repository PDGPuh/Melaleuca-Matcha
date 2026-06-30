using UnityEngine;

namespace RungTramTraSu
{
    [ExecuteAlways]
    public class InteractiveGrassDriver : MonoBehaviour
    {
        [Header("Tracked Characters")]
        [SerializeField] private Transform player;
        [SerializeField] private Transform grandpa;

        [Header("Wind")]
        [SerializeField] private Vector2 windDirection = new Vector2(0.75f, 0.35f);
        [SerializeField, Range(0f, 1f)] private float windStrength = 0.22f;
        [SerializeField, Range(0f, 5f)] private float windSpeed = 1.25f;
        [SerializeField, Range(0.01f, 2f)] private float windScale = 0.24f;
        [SerializeField, Range(0f, 1f)] private float flutter = 0.16f;

        [Header("Character Push")]
        [SerializeField] private float playerRadius = 1.15f;
        [SerializeField] private float playerPush = 0.95f;
        [SerializeField] private float playerFlatten = 0.72f;
        [SerializeField] private float grandpaRadius = 1.05f;
        [SerializeField] private float grandpaPush = 0.78f;
        [SerializeField] private float grandpaFlatten = 0.58f;
        [SerializeField] private float speedForFullBend = 2.6f;

        private static readonly int GrassWindDirection = Shader.PropertyToID("_GrassWindDirection");
        private static readonly int GrassWindParams = Shader.PropertyToID("_GrassWindParams");
        private static readonly int GrassInteractor0 = Shader.PropertyToID("_GrassInteractor0");
        private static readonly int GrassInteractor1 = Shader.PropertyToID("_GrassInteractor1");
        private static readonly int GrassInteractor0Data = Shader.PropertyToID("_GrassInteractor0Data");
        private static readonly int GrassInteractor1Data = Shader.PropertyToID("_GrassInteractor1Data");

        private Vector3 lastPlayerPosition;
        private Vector3 lastGrandpaPosition;
        private bool hasLastPlayer;
        private bool hasLastGrandpa;

        private void Awake()
        {
            ResolveTargets();
            CacheInitialPositions();
            PushGlobals(0f, 0f);
        }

        private void OnEnable()
        {
            ResolveTargets();
            CacheInitialPositions();
            PushGlobals(0f, 0f);
        }

        private void LateUpdate()
        {
            if (player == null || grandpa == null)
            {
                ResolveTargets();
            }

            float playerSpeed01 = GetSpeed01(player, ref lastPlayerPosition, ref hasLastPlayer);
            float grandpaSpeed01 = GetSpeed01(grandpa, ref lastGrandpaPosition, ref hasLastGrandpa);
            PushGlobals(playerSpeed01, grandpaSpeed01);
        }

        private void ResolveTargets()
        {
            if (player == null)
            {
                PlayerController controller = FindAnyObjectByType<PlayerController>();
                if (controller != null)
                {
                    player = controller.transform;
                }
            }

            if (grandpa == null)
            {
                NPCGrandpa npc = FindAnyObjectByType<NPCGrandpa>();
                if (npc != null) grandpa = npc.transform;
            }
        }

        private void CacheInitialPositions()
        {
            if (player != null)
            {
                lastPlayerPosition = player.position;
                hasLastPlayer = true;
            }

            if (grandpa != null)
            {
                lastGrandpaPosition = grandpa.position;
                hasLastGrandpa = true;
            }
        }

        private float GetSpeed01(Transform target, ref Vector3 lastPosition, ref bool hasLast)
        {
            if (target == null)
            {
                hasLast = false;
                return 0f;
            }

            if (!hasLast)
            {
                lastPosition = target.position;
                hasLast = true;
                return 0f;
            }

            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 delta = target.position - lastPosition;
            delta.y = 0f;
            lastPosition = target.position;
            return Mathf.Clamp01(delta.magnitude / dt / Mathf.Max(speedForFullBend, 0.01f));
        }

        private void PushGlobals(float playerSpeed01, float grandpaSpeed01)
        {
            Vector2 dir = windDirection.sqrMagnitude > 0.001f ? windDirection.normalized : Vector2.right;
            Shader.SetGlobalVector(GrassWindDirection, new Vector4(dir.x, dir.y, 0f, 0f));
            Shader.SetGlobalVector(GrassWindParams, new Vector4(windStrength, windSpeed, windScale, flutter));

            PushInteractor(GrassInteractor0, GrassInteractor0Data, player, playerRadius, playerPush, playerFlatten, playerSpeed01);
            PushInteractor(GrassInteractor1, GrassInteractor1Data, grandpa, grandpaRadius, grandpaPush, grandpaFlatten, grandpaSpeed01);
        }

        private static void PushInteractor(int positionId, int dataId, Transform target, float radius, float push, float flatten, float speed01)
        {
            if (target == null || radius <= 0f)
            {
                Shader.SetGlobalVector(positionId, new Vector4(99999f, -99999f, 99999f, 0f));
                Shader.SetGlobalVector(dataId, Vector4.zero);
                return;
            }

            Vector3 p = target.position;
            Shader.SetGlobalVector(positionId, new Vector4(p.x, p.y, p.z, radius));
            Shader.SetGlobalVector(dataId, new Vector4(push, flatten, speed01, 0f));
        }
    }
}
