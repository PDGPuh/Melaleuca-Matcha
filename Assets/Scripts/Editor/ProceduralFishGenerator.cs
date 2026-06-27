using UnityEditor;
using UnityEngine;

namespace RungTramTraSu.Editor
{
    public class ProceduralFishGenerator
    {
        [MenuItem("Rung Tram Tra Su/Generate 3D Fish Model")]
        public static void GenerateFish()
        {
            Mesh mesh = new Mesh();
            mesh.name = "ProceduralFishBody";

            int segments = 10;
            int radialSegments = 8;
            float length = 0.7f;
            float maxRadius = 0.12f;
            float widthScale = 0.5f;   // Slim body
            float heightScale = 1.3f;  // Tall body (cá lóc profile)

            int numVertices = (segments + 1) * (radialSegments + 1);
            Vector3[] vertices = new Vector3[numVertices];
            Vector2[] uvs = new Vector2[numVertices];
            
            // Build body vertices
            int v = 0;
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                // Sin profile for fish body tapering
                float r = Mathf.Sin(t * Mathf.PI) * maxRadius;
                
                // Nose is slightly open, tail is tapered narrow
                if (i == 0) r = maxRadius * 0.25f;
                else if (i == segments) r = maxRadius * 0.1f;
                
                float z = (t - 0.5f) * length;

                for (int j = 0; j <= radialSegments; j++)
                {
                    float radPct = (float)j / radialSegments;
                    float angle = radPct * Mathf.PI * 2f;
                    
                    float x = Mathf.Cos(angle) * r * widthScale;
                    float y = Mathf.Sin(angle) * r * heightScale;

                    // Orient fish along Z axis (head at +Z, tail at -Z)
                    // We flip Z coordinates so i=0 is head (+Z) and i=segments is tail (-Z)
                    vertices[v] = new Vector3(x, y, -z);
                    uvs[v] = new Vector2(radPct, t);
                    v++;
                }
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;

            // Triangles
            System.Collections.Generic.List<int> tris = new System.Collections.Generic.List<int>();

            // Body faces
            for (int i = 0; i < segments; i++)
            {
                int r1 = i * (radialSegments + 1);
                int r2 = (i + 1) * (radialSegments + 1);

                for (int j = 0; j < radialSegments; j++)
                {
                    int nextJ = j + 1;
                    
                    tris.Add(r1 + j);
                    tris.Add(r2 + j);
                    tris.Add(r1 + nextJ);

                    tris.Add(r1 + nextJ);
                    tris.Add(r2 + j);
                    tris.Add(r2 + nextJ);
                }
            }

            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            // Create Directory if not exists
            if (!System.IO.Directory.Exists("Assets/Models/Generated"))
            {
                System.IO.Directory.CreateDirectory("Assets/Models/Generated");
            }

            // Save Mesh to asset database
            string path = "Assets/Models/Generated/ProceduralFishMesh.asset";

            // If the asset already exists, delete it first so CreateAsset succeeds cleanly
            if (AssetDatabase.LoadAssetAtPath<Mesh>(path) != null)
            {
                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.CreateAsset(mesh, path);
            AssetDatabase.SaveAssets();
            // Force Unity to immediately pick up the new asset
            AssetDatabase.Refresh();

            Debug.Log("==> PROCEDURAL 3D FISH MESH GENERATED AND SAVED TO: " + path);
        }
    }
}
