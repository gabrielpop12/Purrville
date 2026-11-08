using System;
using UnityEngine;

namespace Purrville.World
{
    public enum TerrainType { Water, Sand, Grass, Forest }

    public sealed class ProceduralWorld : MonoBehaviour
    {
        [SerializeField, Min(8)] private int width = 64;
        [SerializeField, Min(8)] private int height = 64;
        [SerializeField] private int seed = 12345;
        [SerializeField, Min(1f)] private float noiseScale = 22f;
        [SerializeField] private bool randomSeedOnPlay;

        private TerrainType[,] terrain;
        private GameObject generatedRoot;
        private Texture2D tileTexture;
        private Sprite tileSprite;

        public int Seed => seed;
        public int Width => width;
        public int Height => height;

        private void Start()
        {
            if (randomSeedOnPlay) seed = UnityEngine.Random.Range(1, int.MaxValue);
            GenerateWorld();
        }

        public void GenerateWorld()
        {
            if (generatedRoot != null) Destroy(generatedRoot);
            generatedRoot = new GameObject("GeneratedWorld");
            generatedRoot.transform.SetParent(transform, false);
            EnsureSprite();
            terrain = new TerrainType[width, height];
            var rng = new System.Random(seed);
            float ox = rng.Next(0, 100000);
            float oy = rng.Next(0, 100000);

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float noise = Mathf.PerlinNoise((x + ox) / noiseScale, (y + oy) / noiseScale);
                TerrainType type = noise < .32f ? TerrainType.Water :
                    noise < .38f ? TerrainType.Sand :
                    noise < .70f ? TerrainType.Grass : TerrainType.Forest;
                terrain[x, y] = type;
                var tile = new GameObject($"Tile_{x}_{y}");
                tile.transform.SetParent(generatedRoot.transform, false);
                tile.transform.localPosition = new Vector3(x, y, 0);
                var renderer = tile.AddComponent<SpriteRenderer>();
                renderer.sprite = tileSprite;
                renderer.color = type switch
                {
                    TerrainType.Water => new Color(.36f, .67f, .88f),
                    TerrainType.Sand => new Color(.91f, .82f, .57f),
                    TerrainType.Grass => new Color(.53f, .78f, .43f),
                    _ => new Color(.24f, .53f, .31f)
                };
            }
            Debug.Log($"Purrville: generated {width}x{height} terrain, seed {seed}");
        }

        public bool TryGetTerrain(int x, int y, out TerrainType type)
        {
            type = default;
            if (terrain == null || x < 0 || y < 0 || x >= width || y >= height) return false;
            type = terrain[x, y];
            return true;
        }

        private void EnsureSprite()
        {
            if (tileSprite != null) return;
            tileTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tileTexture.filterMode = FilterMode.Point;
            tileTexture.SetPixel(0, 0, Color.white);
            tileTexture.Apply();
            tileSprite = Sprite.Create(tileTexture, new Rect(0, 0, 1, 1), new Vector2(.5f, .5f), 1f);
        }

        private void OnDestroy()
        {
            if (tileSprite != null) Destroy(tileSprite);
            if (tileTexture != null) Destroy(tileTexture);
        }
    }
}
