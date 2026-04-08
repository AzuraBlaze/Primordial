using System.Collections.Generic;
using UnityEngine;

public enum Biome { Sediment, Brine, Bloom }

public class MapGenerator : MonoBehaviour
{
    [Header("Biome Settings")]
    [Tooltip("Approximate number of distinct biome regions along the longest map edge.")]
    public float regionsPerEdge = 6f;

    [Tooltip("How strongly Perlin noise warps the Worley cell lookup — larger values create wilder, more organic borders.")]
    [Range(0f, 2f)]
    public float warpStrength = 0.65f;

    public Biome[,] BiomeMap { get; private set; }
    public Vector2Int Resolution { get; private set; }

    public void GenerateMap(Vector2 mapSize)
    {
        // Resolution
        int resX = Mathf.Max(1, Mathf.RoundToInt(mapSize.x));
        int resY = Mathf.Max(1, Mathf.RoundToInt(mapSize.y));
        Resolution = new Vector2Int(resX, resY);

        float longestEdge  = Mathf.Max(mapSize.x, mapSize.y);
        float cellSize     = longestEdge / Mathf.Max(1f, regionsPerEdge);

        List<Vector2> sites = GenerateWorleySites(mapSize, cellSize);

        BiomeMap = new Biome[resX, resY];

        // Perlin warp scale
        float warpScale = 1f / cellSize;

        // Unique offsets for each axis to prevent directional bias
        float warpOffsetX = 31.41f;
        float warpOffsetY = 92.65f;

        for (int py = 0; py < resY; py++)
        {
            for (int px = 0; px < resX; px++)
            {
                // Map pixel => world space (centred on the map).
                float wx = ((px + 0.5f) / resX) * mapSize.x - mapSize.x * 0.5f;
                float wy = ((py + 0.5f) / resY) * mapSize.y - mapSize.y * 0.5f;

                // Domain warp: shift the lookup point by a Perlin vector
                float displaceX = Mathf.PerlinNoise(
                    wx * warpScale + warpOffsetX,
                    wy * warpScale + warpOffsetX) * 2f - 1f;

                float displaceY = Mathf.PerlinNoise(
                    wx * warpScale + warpOffsetY,
                    wy * warpScale + warpOffsetY) * 2f - 1f;

                float sampleX = wx + displaceX * cellSize * warpStrength;
                float sampleY = wy + displaceY * cellSize * warpStrength;

                // Find the nearest Worley site to the warped point
                int nearestIndex = NearestSiteIndex(sampleX, sampleY, sites);

                // Map site index => biome using a stable, repeatable hash
                BiomeMap[px, py] = SiteIndexToBiome(nearestIndex);
            }
        }
    }

    static List<Vector2> GenerateWorleySites(Vector2 mapSize, float cellSize)
    {
        // Extend the grid by one cell on each side so border regions aren't clipped.
        float startX = -mapSize.x * 0.5f - cellSize;
        float startY = -mapSize.y * 0.5f - cellSize;
        int   cols   = Mathf.CeilToInt(mapSize.x / cellSize) + 2;
        int   rows   = Mathf.CeilToInt(mapSize.y / cellSize) + 2;

        var sites = new List<Vector2>(cols * rows);

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                // Deterministic jitter based on grid position.
                float jx = SeededRandom(col * 1000 + row, 0) * cellSize;
                float jy = SeededRandom(col * 1000 + row, 1) * cellSize;

                sites.Add(new Vector2(
                    startX + col * cellSize + jx,
                    startY + row * cellSize + jy
                ));
            }
        }

        return sites;
    }

    static int NearestSiteIndex(float x, float y, List<Vector2> sites)
    {
        int   best     = 0;
        float bestDist = float.MaxValue;

        for (int i = 0; i < sites.Count; i++)
        {
            float dx   = x - sites[i].x;
            float dy   = y - sites[i].y;
            float dist = dx * dx + dy * dy; // squared: no sqrt needed for comparison
            if (dist < bestDist)
            {
                bestDist = dist;
                best     = i;
            }
        }

        return best;
    }

    static Biome SiteIndexToBiome(int index)
    {
        // Simple int hash to avoid visible patterns (Sediment always left, Bloom always right, etc.)
        uint h = (uint)index;
        h ^= h >> 16;
        h *= 0x45d9f3b;
        h ^= h >> 16;
        return (Biome)(h % 3);
    }

    static float SeededRandom(int seed, int axis)
    {
        uint h = (uint)(seed * 2654435761u + (uint)axis * 2246822519u);
        h ^= h >> 15;
        h *= 0x85ebca6bu;
        h ^= h >> 13;
        return (h & 0x00FFFFFFu) / (float)0x01000000u;
    }
}