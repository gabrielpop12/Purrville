using System;
using System.Collections.Generic;
using UnityEngine;

namespace Purrville.World
{
    public enum TerrainType { Water, Sand, Grass, Forest }
    public enum DiscoveryState : byte { Unknown, Explored, Visible }

    /// <summary>Seeded terrain with chunked meshes and independent fog-of-war data.</summary>
    public sealed class ProceduralWorld : MonoBehaviour
    {
        [Header("Terrain")]
        [SerializeField, Min(16)] private int width = 128;
        [SerializeField, Min(16)] private int height = 128;
        [SerializeField] private int seed = 12345;
        [SerializeField, Min(1f)] private float noiseScale = 22f;
        [SerializeField] private bool randomSeedOnPlay;
        [SerializeField, Range(8, 32)] private int chunkSize = 16;

        [Header("Exploration prototype")]
        [SerializeField] private bool fogOfWar = true;
        [SerializeField] private bool revealAroundCameraForTesting = false;
        [SerializeField, Range(2, 20)] private int revealRadius = 8;
        [SerializeField, Range(0f, 1f)] private float exploredDarkness = .58f;

        private TerrainType[,] terrain;
        private bool[,] discovered;
        private DiscoveryState[,] visibility;
        private GameObject generatedRoot;
        private Material terrainMaterial;
        private Material fogMaterial;
        private readonly List<ChunkView> chunks = new List<ChunkView>();
        private Camera activeCamera;
        private Vector2Int lastCameraCell = new Vector2Int(int.MinValue, int.MinValue);
        private Vector2Int lastCameraChunk = new Vector2Int(int.MinValue, int.MinValue);
        private float lastCameraSize = -1;
        private bool lastFogEnabled;

        private sealed class ChunkView
        {
            public GameObject root;
            public Mesh terrainMesh;
            public Mesh fogMesh;
            public Color[] fogColors;
            public int startX, startY, tileWidth, tileHeight;
        }

        public int Seed => seed;
        public int Width => width;
        public int Height => height;
        public bool IsGenerated => terrain != null;
        public Vector3 CellToWorld(int x, int y) => transform.TransformPoint(new Vector3(x, y, 0));

        private void Start()
        {
            activeCamera = Camera.main;
            if (randomSeedOnPlay) seed = UnityEngine.Random.Range(1, int.MaxValue);
            GenerateWorld();
        }

        public void GenerateWorld()
        {
            ClearGenerated();
            terrain = new TerrainType[width, height];
            discovered = new bool[width, height];
            visibility = new DiscoveryState[width, height];
            var random = new System.Random(seed);
            float offsetX = random.Next(0, 100000);
            float offsetY = random.Next(0, 100000);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float n = Mathf.PerlinNoise((x + offsetX) / noiseScale, (y + offsetY) / noiseScale);
                terrain[x, y] = n < .32f ? TerrainType.Water :
                    n < .38f ? TerrainType.Sand :
                    n < .70f ? TerrainType.Grass : TerrainType.Forest;
            }

            generatedRoot = new GameObject("GeneratedWorld");
            generatedRoot.transform.SetParent(transform, false);
            terrainMaterial = new Material(Shader.Find("Sprites/Default"));
            Shader fogShader = Shader.Find("Purrville/FogOverlay");
            if (fogShader == null) Debug.LogError("Purrville FogOverlay shader missing!");
            fogMaterial = new Material(fogShader != null ? fogShader : Shader.Find("Sprites/Default"));
            lastFogEnabled = fogOfWar;
            for (int y = 0; y < height; y += chunkSize)
            for (int x = 0; x < width; x += chunkSize)
                BuildChunk(x, y, Mathf.Min(chunkSize, width - x), Mathf.Min(chunkSize, height - y));

            lastCameraCell = new Vector2Int(int.MinValue, int.MinValue);
            lastCameraChunk = lastCameraCell;
            Debug.Log($"Purrville: {width}x{height}, {chunks.Count} chunks, seed {seed}");
        }

        private void BuildChunk(int x0, int y0, int w, int h)
        {
            var chunk = new ChunkView { startX = x0, startY = y0, tileWidth = w, tileHeight = h };
            chunk.root = new GameObject($"Chunk_{x0 / chunkSize}_{y0 / chunkSize}");
            chunk.root.transform.SetParent(generatedRoot.transform, false);
            var terrainObject = new GameObject("Terrain");
            terrainObject.transform.SetParent(chunk.root.transform, false);
            var fogObject = new GameObject("Fog");
            fogObject.transform.SetParent(chunk.root.transform, false);

            int count = w * h;
            var vertices = new Vector3[count * 4];
            var uv = new Vector2[count * 4];
            var triangles = new int[count * 6];
            var colors = new Color[count * 4];
            chunk.fogColors = new Color[count * 4];
            int i = 0;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++, i++)
            {
                int wx = x0 + x, wy = y0 + y, v = i * 4, t = i * 6;
                vertices[v] = new Vector3(wx - .5f, wy - .5f, 0);
                vertices[v + 1] = new Vector3(wx - .5f, wy + .5f, 0);
                vertices[v + 2] = new Vector3(wx + .5f, wy + .5f, 0);
                vertices[v + 3] = new Vector3(wx + .5f, wy - .5f, 0);
                uv[v] = Vector2.zero; uv[v + 1] = Vector2.up;
                uv[v + 2] = Vector2.one; uv[v + 3] = Vector2.right;
                triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
                triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
                Color c = TerrainColor(terrain[wx, wy]);
                for (int j = 0; j < 4; j++) { colors[v + j] = c; chunk.fogColors[v + j] = Color.black; }
            }

            chunk.terrainMesh = new Mesh { name = "TerrainMesh" };
            chunk.terrainMesh.vertices = vertices;
            chunk.terrainMesh.uv = uv;
            chunk.terrainMesh.triangles = triangles;
            chunk.terrainMesh.colors = colors;
            chunk.terrainMesh.RecalculateBounds();
            terrainObject.AddComponent<MeshFilter>().sharedMesh = chunk.terrainMesh;
            var terrainRenderer = terrainObject.AddComponent<MeshRenderer>();
            terrainRenderer.sharedMaterial = terrainMaterial;
            terrainRenderer.sortingOrder = 0;

            chunk.fogMesh = new Mesh { name = "FogMesh" };
            var fogVertices = (Vector3[])vertices.Clone();
            for (int v = 0; v < fogVertices.Length; v++) fogVertices[v].z = -0.1f;
            chunk.fogMesh.vertices = fogVertices;
            chunk.fogMesh.uv = uv;
            chunk.fogMesh.triangles = triangles;
            chunk.fogMesh.colors = chunk.fogColors;
            chunk.fogMesh.RecalculateBounds();
            fogObject.AddComponent<MeshFilter>().sharedMesh = chunk.fogMesh;
            var fogRenderer = fogObject.AddComponent<MeshRenderer>();
            fogRenderer.sharedMaterial = fogMaterial;
            fogRenderer.sortingOrder = 100;
            fogObject.SetActive(fogOfWar);
            chunks.Add(chunk);
        }

        private static Color TerrainColor(TerrainType type)
        {
            switch (type)
            {
                case TerrainType.Water: return new Color(.36f, .67f, .88f);
                case TerrainType.Sand: return new Color(.91f, .82f, .57f);
                case TerrainType.Grass: return new Color(.53f, .78f, .43f);
                default: return new Color(.24f, .53f, .31f);
            }
        }

        private void LateUpdate()
        {
            if (activeCamera == null) activeCamera = Camera.main;
            if (activeCamera == null || terrain == null) return;
            if (lastFogEnabled != fogOfWar)
            {
                foreach (var chunk in chunks)
                    if (chunk.root != null && chunk.root.transform.childCount > 1)
                        chunk.root.transform.GetChild(1).gameObject.SetActive(fogOfWar);
                lastFogEnabled = fogOfWar;
            }
            Vector3 local = transform.InverseTransformPoint(activeCamera.transform.position);
            var cell = new Vector2Int(Mathf.RoundToInt(local.x), Mathf.RoundToInt(local.y));
            if (fogOfWar && revealAroundCameraForTesting && cell != lastCameraCell)
                RevealCircle(cell.x, cell.y, revealRadius);
            lastCameraCell = cell;

            var cameraChunk = new Vector2Int(Mathf.FloorToInt(local.x / chunkSize), Mathf.FloorToInt(local.y / chunkSize));
            if (cameraChunk != lastCameraChunk || !Mathf.Approximately(activeCamera.orthographicSize, lastCameraSize))
            {
                UpdateChunkCulling();
                lastCameraChunk = cameraChunk;
                lastCameraSize = activeCamera.orthographicSize;
            }
        }

        private void UpdateChunkCulling()
        {
            // Orthographic camera: only instantiate meshes once, but render nearby chunks.
            float halfH = activeCamera.orthographicSize;
            float halfW = halfH * activeCamera.aspect;
            Vector3 center = transform.InverseTransformPoint(activeCamera.transform.position);
            foreach (var chunk in chunks)
            {
                bool visible = chunk.startX <= center.x + halfW + 2 &&
                    chunk.startX + chunk.tileWidth >= center.x - halfW - 2 &&
                    chunk.startY <= center.y + halfH + 2 &&
                    chunk.startY + chunk.tileHeight >= center.y - halfH - 2;
                chunk.root.SetActive(visible);
            }
        }

        /// <summary>Later, cats and lookout buildings call this with their own position.</summary>
        public void RevealCircle(int cx, int cy, int radius)
        {
            if (visibility == null) return;
            int r2 = radius * radius;
            // Previously visible tiles become explored; discovered tiles never revert to unknown.
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                visibility[x, y] = discovered[x, y] ? DiscoveryState.Explored : DiscoveryState.Unknown;

            for (int y = Mathf.Max(0, cy - radius); y <= Mathf.Min(height - 1, cy + radius); y++)
            for (int x = Mathf.Max(0, cx - radius); x <= Mathf.Min(width - 1, cx + radius); x++)
                if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r2)
                {
                    discovered[x, y] = true;
                    visibility[x, y] = DiscoveryState.Visible;
                }
            RefreshFog();
        }

        public void RevealFromUnits(IReadOnlyList<Vector2Int> positions, int radius)
        {
            if (visibility == null) return;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                visibility[x,y] = discovered[x,y] ? DiscoveryState.Explored : DiscoveryState.Unknown;
            int r2 = radius * radius;
            foreach (var p in positions)
            for (int y = Mathf.Max(0,p.y-radius); y <= Mathf.Min(height-1,p.y+radius); y++)
            for (int x = Mathf.Max(0,p.x-radius); x <= Mathf.Min(width-1,p.x+radius); x++)
                if ((x-p.x)*(x-p.x)+(y-p.y)*(y-p.y)<=r2)
                { discovered[x,y]=true; visibility[x,y]=DiscoveryState.Visible; }
            RefreshFog();
        }

        private void RefreshFog()
        {
            foreach (var chunk in chunks)
            {
                int i = 0;
                for (int y = 0; y < chunk.tileHeight; y++)
                for (int x = 0; x < chunk.tileWidth; x++, i++)
                {
                    DiscoveryState state = visibility[chunk.startX + x, chunk.startY + y];
                    Color c = state == DiscoveryState.Visible ? Color.clear :
                        state == DiscoveryState.Explored ? new Color(0, 0, 0, exploredDarkness) : Color.black;
                    int v = i * 4;
                    for (int j = 0; j < 4; j++) chunk.fogColors[v + j] = c;
                }
                chunk.fogMesh.colors = chunk.fogColors;
            }
        }

        public bool TryGetTerrain(int x, int y, out TerrainType type)
        {
            type = default;
            if (terrain == null || x < 0 || y < 0 || x >= width || y >= height) return false;
            type = terrain[x, y];
            return true;
        }

        public DiscoveryState GetDiscoveryState(int x, int y)
        {
            if (visibility == null || x < 0 || y < 0 || x >= width || y >= height)
                return DiscoveryState.Unknown;
            return visibility[x, y];
        }

        private void ClearGenerated()
        {
            foreach (var chunk in chunks)
            {
                if (chunk.terrainMesh != null) Destroy(chunk.terrainMesh);
                if (chunk.fogMesh != null) Destroy(chunk.fogMesh);
            }
            chunks.Clear();
            if (generatedRoot != null) Destroy(generatedRoot);
            if (terrainMaterial != null) Destroy(terrainMaterial);
            if (fogMaterial != null) Destroy(fogMaterial);
        }

        private void OnDestroy() => ClearGenerated();
    }
}
