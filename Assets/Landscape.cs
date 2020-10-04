//#define ONLY_LOD0
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class Landscape
{
    public List<List<Mesh>> LODMeshList = new List<List<Mesh>>();

    /// <summary>
    /// 
    /// </summary>
    /// <param name="heightMap">height map</param>
    /// <param name="size">size in world unit</param>
    /// <param name="baseHeight"></param>
    /// <param name="totalHeight"></param>
    private static Landscape Create(Texture2D heightMap, Vector2 size, float baseHeight,
        float totalHeight)
    {
        if (heightMap == null)
        {
            throw new System.ArgumentNullException(nameof(heightMap));
        }

        Landscape landscape = new Landscape();
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
        const int blockQuadNumber = 16;
        int LOD = 0;
        while (quadNumber.x > 16 && quadNumber.y > 16)
        {
            var blockNumber = quadNumber / blockQuadNumber;
            var blockMeshes = new List<Mesh>(blockNumber.x * blockNumber.y);
            var blockPixelSize =
                new Vector2Int(resolution.x / blockNumber.x, resolution.y / blockNumber.y);
            var quadSize = size / resolution;
            var blockPixelStep = new Vector2Int(
                (resolution.x - 1)/ quadNumber.x,
                (resolution.y - 1)/ quadNumber.y);
            for (int blockY = 0; blockY < blockNumber.y; blockY++)
            {
                for (int blockX = 0; blockX < blockNumber.x; blockX++)
                {
                    var blockPixelMin = new Vector2Int(blockX, blockY) * blockPixelSize;
                    var blockPixelMax = blockPixelMin + Vector2Int.one * blockPixelSize;

                    Debug.Log($"LOD{LOD} Block<{blockX},{blockY}>:" +
                              $" from {blockPixelMin} to {blockPixelMax} step {blockPixelStep}" +
                              " on the height map");

                    var mesh = CreateBlockMesh(
                        blockPixelMin, blockPixelMax, blockPixelStep,
                        baseHeight, totalHeight, quadSize, heightMap);
                    mesh.name = $"Block<{blockX},{blockY}>";
                    blockMeshes.Add(mesh);
                }
            }

            landscape.LODMeshList.Add(blockMeshes);
            LOD++;
#if ONLY_LOD0
            break;
#endif
            quadNumber /= 2;
        }

        return landscape;
    }
    
    private static Mesh CreateBlockMesh(
        Vector2Int pixelMin, Vector2Int pixelMax,
        Vector2Int pixelStep,
        float baseHeight, float totalHeight, Vector2 quadSize, Texture2D heightMap)
    {
        var pixelNumber = pixelMax - pixelMin + Vector2Int.one;

        var vertexNumber
            = new Vector2Int(
                (pixelNumber.x - 1) / pixelStep.x + 1, (pixelNumber.y - 1) / pixelStep.y + 1);
        var positions = new Vector3[vertexNumber.x * vertexNumber.y];
        var texCoords = new Vector2[vertexNumber.x * vertexNumber.y];
        var indexes = new int[(vertexNumber.x - 1) * (vertexNumber.y - 1) * 6];

        var w = vertexNumber.x;
        var h = vertexNumber.y;
        int i = 0;
        for (var y = pixelMin.y; y <= pixelMax.y; y+=pixelStep.y)
        {
            for (var x = pixelMin.x; x <= pixelMax.x; x+=pixelStep.x)
            {
                //R channel is used as the height
                var normalizedHeight = heightMap.GetPixel(x, y).r;
                var height = baseHeight + normalizedHeight * totalHeight;
                positions[i]
                    = new Vector3(x * quadSize.x, height, y * quadSize.y);

                //TODO consider texCoords
                texCoords[i]
                    = new Vector2((float) x / (w - 1), (float) y / (h - 1));
                //TODO consider normal, especially at edges neighbor to another block
                i++;
            }
        }

        var index = 0;
        for (var y = 0; y < h - 1; y++)
        {
            for (var x = 0; x < w - 1; x++)
            {
                //different split direction of a cell
#if f
                indexes[index++] = (y*w) + x;
                indexes[index++] = ((y+1) * w) + x;
                indexes[index++] = (y*w) + x + 1;

                indexes[index++] = ((y+1) * w) + x;
                indexes[index++] = ((y+1) * w) + x + 1;
                indexes[index++] = (y*w) + x + 1;
#else
                indexes[index++] = (y * w) + x;
                indexes[index++] = ((y + 1) * w) + x;
                indexes[index++] = ((y + 1) * w) + x + 1;

                indexes[index++] = (y * w) + x;
                indexes[index++] = ((y + 1) * w) + x + 1;
                indexes[index++] = (y * w) + x + 1;
#endif
            }
        }

        Mesh mesh = new Mesh();
        mesh.vertices = positions;
        mesh.uv = texCoords;
        mesh.triangles = indexes;
        mesh.RecalculateNormals();
        return mesh;
    }

    [MenuItem("GeoMipMaps/Create Test Landscape Objects")]
    public static void CreateTestLandscapeObjects()
    {
        var heightMap = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/HeightMap.png");
        var landscape = Create(heightMap, new Vector2(1000, 1000), 10, 50);
        var material = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Diffuse.mat");

        var LODCount = landscape.LODMeshList.Count;
        for (var LOD = 0; LOD < LODCount; LOD++)
        {
            var blockMeshList = landscape.LODMeshList[LOD];
            GameObject LODObj = new GameObject($"Landscape_LOD{LOD}");
            LODObj.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var LODColor = new Color(0.2f, 0.2f, 1.0f - LOD * 0.8f / LODCount);
            for (int i = 0; i < blockMeshList.Count; i++)
            {
                var mesh = blockMeshList[i];
                var blockObj = new GameObject(mesh.name);
                blockObj.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                blockObj.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = blockObj.AddComponent<MeshRenderer>();
                renderer.material = Object.Instantiate(material);
                renderer.sharedMaterial.color = LODColor;
                blockObj.transform.SetParent(LODObj.transform, true);
            }
        }
    }
}
