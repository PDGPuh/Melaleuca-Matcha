using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace RungTramTraSu
{
    [ExecuteAlways]
    public class VolumetricSunShafts : MonoBehaviour
    {
        private const string ChildPrefix = "SunShaft_";

        [Header("References")]
        [SerializeField] private Light sunLight;
        [SerializeField] private Material shaftMaterial;

        [Header("Placement")]
        [SerializeField] private bool useTerrainTreesAsAnchors = true;
        [SerializeField] private Vector3 areaCenter = Vector3.zero;
        [SerializeField] private Vector2 areaSize = new Vector2(120f, 120f);
        [SerializeField] private int shaftCount = 38;
        [SerializeField] private int randomSeed = 4045;

        [Header("Shape")]
        [SerializeField] private Vector2 canopyHeightRange = new Vector2(8.0f, 17.0f);
        [SerializeField] private Vector2 widthRange = new Vector2(1.1f, 3.8f);
        [SerializeField] private Vector2 alphaRange = new Vector2(0.16f, 0.36f);
        [SerializeField] private float topWidthScale = 0.16f;
        [SerializeField] private float bottomWidthScale = 0.58f;
        [SerializeField] private float crossedPlaneAngle = 63f;

        [Header("Editor")]
        [SerializeField] private bool rebuildOnEnable = true;

        private readonly List<Mesh> generatedMeshes = new List<Mesh>();

        private void Reset()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();

            if (rebuildOnEnable && CountShaftChildren() == 0)
            {
                Rebuild();
            }
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
            {
                ClearGeneratedMeshes();
            }
        }

        private void OnValidate()
        {
            shaftCount = Mathf.Clamp(shaftCount, 0, 96);
            areaSize.x = Mathf.Max(1f, areaSize.x);
            areaSize.y = Mathf.Max(1f, areaSize.y);
            canopyHeightRange.x = Mathf.Max(1f, canopyHeightRange.x);
            canopyHeightRange.y = Mathf.Max(canopyHeightRange.x + 0.1f, canopyHeightRange.y);
            widthRange.x = Mathf.Max(0.1f, widthRange.x);
            widthRange.y = Mathf.Max(widthRange.x + 0.1f, widthRange.y);
            alphaRange.x = Mathf.Clamp01(alphaRange.x);
            alphaRange.y = Mathf.Max(alphaRange.x, Mathf.Clamp01(alphaRange.y));
        }

        [ContextMenu("Rebuild Sun Shafts")]
        public void Rebuild()
        {
            ResolveReferences();
            ClearShaftChildren();

            if (shaftMaterial == null || shaftCount <= 0)
            {
                return;
            }

            Random.InitState(randomSeed);

            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                Terrain[] terrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
                if (terrains.Length > 0)
                {
                    terrain = terrains[0];
                }
            }

            List<Vector3> anchors = useTerrainTreesAsAnchors ? CollectTreeAnchors(terrain) : new List<Vector3>();
            Vector3 lightDirection = GetSunTravelDirection();

            for (int i = 0; i < shaftCount; i++)
            {
                Vector3 basePoint = PickBasePoint(terrain, anchors);
                float groundY = SampleGroundY(terrain, basePoint);
                float topHeight = Random.Range(canopyHeightRange.x, canopyHeightRange.y);
                Vector3 top = new Vector3(basePoint.x, groundY + topHeight, basePoint.z);

                float lengthToGround = Mathf.Max(3f, (top.y - (groundY + 0.12f)) / Mathf.Max(0.08f, -lightDirection.y));
                float length = lengthToGround * Random.Range(0.92f, 1.22f);
                float width = Random.Range(widthRange.x, widthRange.y);
                float alpha = Random.Range(alphaRange.x, alphaRange.y);

                Mesh mesh = CreateShaftMesh(i, top, lightDirection, length, width, alpha);
                generatedMeshes.Add(mesh);

                GameObject shaft = new GameObject(ChildPrefix + i.ToString("00"));
                shaft.transform.SetParent(transform, false);
                shaft.transform.localPosition = Vector3.zero;
                shaft.transform.localRotation = Quaternion.identity;
                shaft.transform.localScale = Vector3.one;

                MeshFilter meshFilter = shaft.AddComponent<MeshFilter>();
                MeshRenderer meshRenderer = shaft.AddComponent<MeshRenderer>();
                meshFilter.sharedMesh = mesh;
                meshRenderer.sharedMaterial = shaftMaterial;
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
                meshRenderer.receiveShadows = false;
                meshRenderer.allowOcclusionWhenDynamic = false;
            }
        }

        private void ResolveReferences()
        {
            if (sunLight == null)
            {
                Light[] lights = FindObjectsByType<Light>(FindObjectsSortMode.None);
                for (int i = 0; i < lights.Length; i++)
                {
                    if (lights[i] != null && lights[i].type == LightType.Directional)
                    {
                        sunLight = lights[i];
                        break;
                    }
                }
            }

#if UNITY_EDITOR
            if (shaftMaterial == null)
            {
                shaftMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Effects/Lighting/Materials/M_Phase4_SunShaft.mat");
            }
#endif
        }

        private Vector3 GetSunTravelDirection()
        {
            Vector3 direction = sunLight != null ? sunLight.transform.forward : new Vector3(0.48f, -0.72f, 0.50f);

            if (direction.y > -0.08f)
            {
                direction.y = -0.42f;
            }

            return direction.normalized;
        }

        private List<Vector3> CollectTreeAnchors(Terrain terrain)
        {
            List<Vector3> anchors = new List<Vector3>();
            if (terrain == null || terrain.terrainData == null)
            {
                return anchors;
            }

            TerrainData data = terrain.terrainData;
            Vector3 terrainPosition = terrain.transform.position;
            Vector3 terrainSize = data.size;
            TreeInstance[] trees = data.treeInstances;

            for (int i = 0; i < trees.Length; i++)
            {
                Vector3 normalized = trees[i].position;
                Vector3 world = new Vector3(
                    terrainPosition.x + normalized.x * terrainSize.x,
                    terrainPosition.y + normalized.y * terrainSize.y,
                    terrainPosition.z + normalized.z * terrainSize.z
                );

                if (Mathf.Abs(world.x - areaCenter.x) <= areaSize.x * 0.5f &&
                    Mathf.Abs(world.z - areaCenter.z) <= areaSize.y * 0.5f)
                {
                    anchors.Add(world);
                }
            }

            return anchors;
        }

        private Vector3 PickBasePoint(Terrain terrain, List<Vector3> anchors)
        {
            if (anchors != null && anchors.Count > 0 && Random.value < 0.82f)
            {
                Vector3 anchor = anchors[Random.Range(0, anchors.Count)];
                Vector2 offset = Random.insideUnitCircle * Random.Range(0.8f, 4.2f);
                return new Vector3(anchor.x + offset.x, anchor.y, anchor.z + offset.y);
            }

            Vector2 randomXZ = new Vector2(
                Random.Range(-areaSize.x * 0.5f, areaSize.x * 0.5f),
                Random.Range(-areaSize.y * 0.5f, areaSize.y * 0.5f)
            );

            Vector3 point = new Vector3(areaCenter.x + randomXZ.x, areaCenter.y, areaCenter.z + randomXZ.y);
            if (terrain != null && terrain.terrainData != null)
            {
                Vector3 terrainPosition = terrain.transform.position;
                Vector3 terrainSize = terrain.terrainData.size;
                point.x = Mathf.Clamp(point.x, terrainPosition.x + 2f, terrainPosition.x + terrainSize.x - 2f);
                point.z = Mathf.Clamp(point.z, terrainPosition.z + 2f, terrainPosition.z + terrainSize.z - 2f);
            }

            return point;
        }

        private float SampleGroundY(Terrain terrain, Vector3 worldPoint)
        {
            if (terrain == null || terrain.terrainData == null)
            {
                return worldPoint.y;
            }

            return terrain.SampleHeight(worldPoint) + terrain.transform.position.y;
        }

        private Mesh CreateShaftMesh(int index, Vector3 topWorld, Vector3 directionWorld, float length, float width, float alpha)
        {
            Vector3 bottomWorld = topWorld + directionWorld * length;
            Vector3 sideA = Vector3.Cross(directionWorld, Vector3.up);
            if (sideA.sqrMagnitude < 0.0001f)
            {
                sideA = Vector3.Cross(directionWorld, Vector3.right);
            }

            sideA.Normalize();
            sideA = Quaternion.AngleAxis(Random.Range(0f, 180f), directionWorld) * sideA;
            Vector3 sideB = Quaternion.AngleAxis(crossedPlaneAngle, directionWorld) * sideA;

            Vector3[] vertices = new Vector3[8];
            Vector2[] uvs = new Vector2[8];
            Color[] colors = new Color[8];
            int[] triangles = new int[12];

            AddPlane(vertices, uvs, colors, triangles, 0, topWorld, bottomWorld, sideA, width, alpha);
            AddPlane(vertices, uvs, colors, triangles, 4, topWorld, bottomWorld, sideB, width * Random.Range(0.72f, 1.15f), alpha * Random.Range(0.58f, 0.9f));

            Mesh mesh = new Mesh();
            mesh.name = "Phase4_VolumetricSunShaft_" + index.ToString("00");
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            return mesh;
        }

        private void AddPlane(Vector3[] vertices, Vector2[] uvs, Color[] colors, int[] triangles, int start, Vector3 topWorld, Vector3 bottomWorld, Vector3 side, float width, float alpha)
        {
            float topHalfWidth = width * topWidthScale * 0.5f;
            float bottomHalfWidth = width * bottomWidthScale * 0.5f;

            vertices[start] = transform.InverseTransformPoint(topWorld - side * topHalfWidth);
            vertices[start + 1] = transform.InverseTransformPoint(topWorld + side * topHalfWidth);
            vertices[start + 2] = transform.InverseTransformPoint(bottomWorld - side * bottomHalfWidth);
            vertices[start + 3] = transform.InverseTransformPoint(bottomWorld + side * bottomHalfWidth);

            uvs[start] = new Vector2(0f, 0f);
            uvs[start + 1] = new Vector2(1f, 0f);
            uvs[start + 2] = new Vector2(0f, 1f);
            uvs[start + 3] = new Vector2(1f, 1f);

            Color tint = new Color(Random.Range(0.92f, 1.0f), Random.Range(0.88f, 1.0f), Random.Range(0.70f, 0.88f), alpha);
            colors[start] = tint;
            colors[start + 1] = tint;
            colors[start + 2] = tint;
            colors[start + 3] = tint;

            int triStart = start == 0 ? 0 : 6;
            triangles[triStart] = start;
            triangles[triStart + 1] = start + 2;
            triangles[triStart + 2] = start + 1;
            triangles[triStart + 3] = start + 2;
            triangles[triStart + 4] = start + 3;
            triangles[triStart + 5] = start + 1;
        }

        private int CountShaftChildren()
        {
            int count = 0;
            for (int i = 0; i < transform.childCount; i++)
            {
                if (transform.GetChild(i).name.StartsWith(ChildPrefix))
                {
                    count++;
                }
            }

            return count;
        }

        private void ClearShaftChildren()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (!child.name.StartsWith(ChildPrefix))
                {
                    continue;
                }

                MeshFilter meshFilter = child.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    DestroyGeneratedObject(meshFilter.sharedMesh);
                }

                DestroyGeneratedObject(child.gameObject);
            }

            generatedMeshes.Clear();
        }

        private void ClearGeneratedMeshes()
        {
            for (int i = 0; i < generatedMeshes.Count; i++)
            {
                if (generatedMeshes[i] != null)
                {
                    DestroyGeneratedObject(generatedMeshes[i]);
                }
            }

            generatedMeshes.Clear();
        }

        private void DestroyGeneratedObject(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
