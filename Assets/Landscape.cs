using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class Landscape
{
    public List<Mesh> LODMeshes = new List<Mesh>();

    /// <summary>
    /// 
    /// </summary>
    /// <param name="heightMap">height map</param>
    /// <param name="size">size in world unit</param>
    private static Landscape Create(Texture2D heightMap, Vector2 size)
    {
        if (heightMap == null)
        {
            throw new System.ArgumentNullException(nameof(heightMap));
        }

        Landscape Landscape = new Landscape();
        var resolution = new Vector2Int(heightMap.width, heightMap.height);

        if (resolution.x > 4097 || resolution.y > 4097 || resolution.x < 33 || resolution.y < 33)
        {
            throw new System.ArgumentException(
                "resolution must be in range 33x33 to 4097x4097", nameof(resolution));
        }

        if (!Mathf.IsPowerOfTwo(resolution.x-1) || !Mathf.IsPowerOfTwo(resolution.y-1))
        {
            throw new System.ArgumentException(
                "resolution value must be (power of 2) + 1", nameof(resolution));
        }
        
        var quadNumber = resolution - Vector2Int.one;

        while (quadNumber.x > 16 && quadNumber.y > 16)
        {
            var vertexNumber = quadNumber + Vector2Int.one;
            var quadSize = new Vector2(size.x / quadNumber.x, size.y / quadNumber.y);
            
            var positions = new Vector3[vertexNumber.x * vertexNumber.y];
            var texCoords = new Vector2[vertexNumber.x * vertexNumber.y];
            var indexes = new int[quadNumber.x * quadNumber.y * 6];
            var index = 0;

            unsafe
            {
                var w = vertexNumber.x;
                var h = vertexNumber.y;
                fixed (Vector3* positions_ptr = positions)
                fixed (Vector2* texCoords_ptr = texCoords)
                {
                    for (var y = 0; y < h; y++)
                    {
                        for (var x = 0; x < w; x++)
                        {
                            var u = 1.0f * x / w;
                            var v = 1.0f * y / h;
                            //R channel is used as the height
                            var height = heightMap.GetPixelBilinear(u, v).r;
                            positions_ptr[y * w + x]
                                = new Vector3(x * quadSize.x, height, y * quadSize.y);
                            texCoords_ptr[y * w + x]
                                = new Vector2((float) x / (w - 1), (float) y / (h - 1));
                        }
                    }
                }

                fixed (int* indexes_ptr = indexes)
                {
                    for (var y = 0; y < h - 1; y++)
                    {
                        for (var x = 0; x < w - 1; x++)
                        {
                            //different split direction of a cell
#if f
                            indexes_ptr[index++] = (y*w) + x;
                            indexes_ptr[index++] = ((y+1) * w) + x;
                            indexes_ptr[index++] = (y*w) + x + 1;

                            indexes_ptr[index++] = ((y+1) * w) + x;
                            indexes_ptr[index++] = ((y+1) * w) + x + 1;
                            indexes_ptr[index++] = (y*w) + x + 1;
#else
                            indexes_ptr[index++] = (y * w) + x;
                            indexes_ptr[index++] = ((y + 1) * w) + x;
                            indexes_ptr[index++] = ((y + 1) * w) + x + 1;

                            indexes_ptr[index++] = (y * w) + x;
                            indexes_ptr[index++] = ((y + 1) * w) + x + 1;
                            indexes_ptr[index++] = (y * w) + x + 1;
#endif
                        }
                    }
                }
            }

            Mesh mesh = new Mesh();
            mesh.vertices = positions;
            mesh.uv = texCoords;
            mesh.triangles = indexes;
            mesh.RecalculateNormals();
            Landscape.LODMeshes.Add(mesh);

            quadNumber /= 2;
        }

        return Landscape;
    }

    [MenuItem("GeoMipMaps/Create Test Landscape Objects")]
    public static void CreateTestLandscapeObjects()
    {
        var heightMap = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/HeightMap.png");
        var landscape = Create(heightMap, new Vector2(500, 500));
        var meshes = landscape.LODMeshes;
        var material = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Diffuse.mat");
        for (var LOD = 0; LOD < meshes.Count; LOD++)
        {
            var mesh = meshes[LOD];
            GameObject obj = new GameObject($"Landscape_LOD{LOD}");
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.AddComponent<MeshRenderer>();
            renderer.material = Object.Instantiate(material);
            renderer.sharedMaterial.color = new Color(0.2f, 0.2f, 1.0f - LOD * 0.8f / meshes.Count);
        }
    }
}
