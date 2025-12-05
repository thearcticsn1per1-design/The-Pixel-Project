using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

namespace PixelProject.LevelGeneration
{
    /// <summary>
    /// Procedural level generator using cellular automata and room-based generation.
    /// </summary>
    public class LevelGenerator : MonoBehaviour
    {
        public static LevelGenerator Instance { get; private set; }

        [Header("Generation Settings")]
        [SerializeField] private int width = 100;
        [SerializeField] private int height = 100;
        [SerializeField] private string seed;
        [SerializeField] private bool useRandomSeed = true;

        [Header("Cellular Automata")]
        [SerializeField] [Range(0, 100)] private int fillPercent = 45;
        [SerializeField] private int smoothIterations = 5;
        [SerializeField] private int wallThreshold = 4;

        [Header("Room Settings")]
        [SerializeField] private int minRoomSize = 50;
        [SerializeField] private int roomConnectionRadius = 3;

        [Header("Tilemaps")]
        [SerializeField] private Tilemap groundTilemap;
        [SerializeField] private Tilemap wallTilemap;
        [SerializeField] private Tilemap decorationTilemap;

        [Header("Tiles")]
        [SerializeField] private TileBase groundTile;
        [SerializeField] private TileBase wallTile;
        [SerializeField] private TileBase[] decorationTiles;

        [Header("Spawn Points")]
        [SerializeField] private Transform playerSpawnPoint;

        private int[,] map;
        private List<Room> rooms = new List<Room>();
        private System.Random random;

        public Vector3 PlayerSpawnPosition => playerSpawnPoint != null ? playerSpawnPoint.position : Vector3.zero;
        public int Width => width;
        public int Height => height;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void GenerateLevel()
        {
            // Initialize random seed
            if (useRandomSeed)
            {
                seed = Time.time.ToString();
            }
            random = new System.Random(seed.GetHashCode());

            // Generate map
            GenerateMap();

            // Smooth map
            for (int i = 0; i < smoothIterations; i++)
            {
                SmoothMap();
            }

            // Process rooms
            ProcessRooms();

            // Render to tilemaps
            RenderMap();

            // Place spawn point
            PlaceSpawnPoint();

            Debug.Log($"Level generated with seed: {seed}");
        }

        private void GenerateMap()
        {
            map = new int[width, height];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    // Make borders always walls
                    if (x == 0 || x == width - 1 || y == 0 || y == height - 1)
                    {
                        map[x, y] = 1;
                    }
                    else
                    {
                        map[x, y] = (random.Next(0, 100) < fillPercent) ? 1 : 0;
                    }
                }
            }
        }

        private void SmoothMap()
        {
            int[,] newMap = new int[width, height];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    int neighborWalls = GetSurroundingWallCount(x, y);

                    if (neighborWalls > wallThreshold)
                    {
                        newMap[x, y] = 1;
                    }
                    else if (neighborWalls < wallThreshold)
                    {
                        newMap[x, y] = 0;
                    }
                    else
                    {
                        newMap[x, y] = map[x, y];
                    }
                }
            }

            map = newMap;
        }

        private int GetSurroundingWallCount(int gridX, int gridY)
        {
            int wallCount = 0;

            for (int neighborX = gridX - 1; neighborX <= gridX + 1; neighborX++)
            {
                for (int neighborY = gridY - 1; neighborY <= gridY + 1; neighborY++)
                {
                    if (neighborX >= 0 && neighborX < width && neighborY >= 0 && neighborY < height)
                    {
                        if (neighborX != gridX || neighborY != gridY)
                        {
                            wallCount += map[neighborX, neighborY];
                        }
                    }
                    else
                    {
                        wallCount++;
                    }
                }
            }

            return wallCount;
        }

        private void ProcessRooms()
        {
            rooms.Clear();

            // Find all regions
            List<List<Vector2Int>> wallRegions = GetRegions(1);
            List<List<Vector2Int>> roomRegions = GetRegions(0);

            // Remove small wall regions
            foreach (var region in wallRegions)
            {
                if (region.Count < minRoomSize)
                {
                    foreach (var tile in region)
                    {
                        map[tile.x, tile.y] = 0;
                    }
                }
            }

            // Remove small rooms
            List<Room> survivingRooms = new List<Room>();
            foreach (var region in roomRegions)
            {
                if (region.Count < minRoomSize)
                {
                    foreach (var tile in region)
                    {
                        map[tile.x, tile.y] = 1;
                    }
                }
                else
                {
                    survivingRooms.Add(new Room(region, map));
                }
            }

            // Sort rooms by size
            survivingRooms.Sort((a, b) => b.Size.CompareTo(a.Size));

            if (survivingRooms.Count > 0)
            {
                survivingRooms[0].IsMainRoom = true;
                survivingRooms[0].IsAccessibleFromMainRoom = true;
            }

            rooms = survivingRooms;

            // Connect rooms
            ConnectClosestRooms();
        }

        private List<List<Vector2Int>> GetRegions(int tileType)
        {
            List<List<Vector2Int>> regions = new List<List<Vector2Int>>();
            bool[,] visited = new bool[width, height];

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (!visited[x, y] && map[x, y] == tileType)
                    {
                        List<Vector2Int> region = GetRegionTiles(x, y, visited);
                        regions.Add(region);
                    }
                }
            }

            return regions;
        }

        private List<Vector2Int> GetRegionTiles(int startX, int startY, bool[,] visited)
        {
            List<Vector2Int> tiles = new List<Vector2Int>();
            int tileType = map[startX, startY];

            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            queue.Enqueue(new Vector2Int(startX, startY));
            visited[startX, startY] = true;

            while (queue.Count > 0)
            {
                Vector2Int tile = queue.Dequeue();
                tiles.Add(tile);

                // Check 4 neighbors
                for (int x = tile.x - 1; x <= tile.x + 1; x++)
                {
                    for (int y = tile.y - 1; y <= tile.y + 1; y++)
                    {
                        if (x >= 0 && x < width && y >= 0 && y < height && (x == tile.x || y == tile.y))
                        {
                            if (!visited[x, y] && map[x, y] == tileType)
                            {
                                visited[x, y] = true;
                                queue.Enqueue(new Vector2Int(x, y));
                            }
                        }
                    }
                }
            }

            return tiles;
        }

        private void ConnectClosestRooms()
        {
            List<Room> roomListA = new List<Room>();
            List<Room> roomListB = new List<Room>();

            foreach (var room in rooms)
            {
                if (room.IsAccessibleFromMainRoom)
                {
                    roomListB.Add(room);
                }
                else
                {
                    roomListA.Add(room);
                }
            }

            int bestDistance = int.MaxValue;
            Vector2Int bestTileA = Vector2Int.zero;
            Vector2Int bestTileB = Vector2Int.zero;
            Room bestRoomA = null;
            Room bestRoomB = null;
            bool possibleConnection = false;

            foreach (var roomA in roomListA)
            {
                if (!possibleConnection)
                {
                    possibleConnection = false;
                    bestDistance = int.MaxValue;

                    foreach (var roomB in roomListB)
                    {
                        if (roomA == roomB || roomA.IsConnected(roomB)) continue;

                        foreach (var tileA in roomA.EdgeTiles)
                        {
                            foreach (var tileB in roomB.EdgeTiles)
                            {
                                int distance = (tileA.x - tileB.x) * (tileA.x - tileB.x) +
                                               (tileA.y - tileB.y) * (tileA.y - tileB.y);

                                if (distance < bestDistance)
                                {
                                    bestDistance = distance;
                                    possibleConnection = true;
                                    bestTileA = tileA;
                                    bestTileB = tileB;
                                    bestRoomA = roomA;
                                    bestRoomB = roomB;
                                }
                            }
                        }
                    }

                    if (possibleConnection)
                    {
                        CreatePassage(bestRoomA, bestRoomB, bestTileA, bestTileB);
                    }
                }
            }

            if (!possibleConnection)
            {
                // All rooms connected
                return;
            }

            // Recursively connect remaining rooms
            if (roomListA.Count > 0)
            {
                ConnectClosestRooms();
            }
        }

        private void CreatePassage(Room roomA, Room roomB, Vector2Int tileA, Vector2Int tileB)
        {
            Room.ConnectRooms(roomA, roomB);

            List<Vector2Int> line = GetLine(tileA, tileB);
            foreach (var point in line)
            {
                DrawCircle(point, roomConnectionRadius);
            }
        }

        private void DrawCircle(Vector2Int center, int radius)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    if (x * x + y * y <= radius * radius)
                    {
                        int drawX = center.x + x;
                        int drawY = center.y + y;

                        if (drawX >= 0 && drawX < width && drawY >= 0 && drawY < height)
                        {
                            map[drawX, drawY] = 0;
                        }
                    }
                }
            }
        }

        private List<Vector2Int> GetLine(Vector2Int from, Vector2Int to)
        {
            List<Vector2Int> line = new List<Vector2Int>();

            int x = from.x;
            int y = from.y;

            int dx = Mathf.Abs(to.x - from.x);
            int dy = Mathf.Abs(to.y - from.y);

            int sx = from.x < to.x ? 1 : -1;
            int sy = from.y < to.y ? 1 : -1;

            int err = dx - dy;

            while (true)
            {
                line.Add(new Vector2Int(x, y));

                if (x == to.x && y == to.y) break;

                int e2 = err * 2;

                if (e2 > -dy)
                {
                    err -= dy;
                    x += sx;
                }

                if (e2 < dx)
                {
                    err += dx;
                    y += sy;
                }
            }

            return line;
        }

        private void RenderMap()
        {
            if (groundTilemap != null) groundTilemap.ClearAllTiles();
            if (wallTilemap != null) wallTilemap.ClearAllTiles();
            if (decorationTilemap != null) decorationTilemap.ClearAllTiles();

            Vector3Int offset = new Vector3Int(-width / 2, -height / 2, 0);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Vector3Int tilePos = new Vector3Int(x, y, 0) + offset;

                    if (map[x, y] == 0)
                    {
                        // Ground tile
                        if (groundTilemap != null && groundTile != null)
                        {
                            groundTilemap.SetTile(tilePos, groundTile);
                        }

                        // Random decoration
                        if (decorationTilemap != null && decorationTiles != null && decorationTiles.Length > 0)
                        {
                            if (random.Next(100) < 5)
                            {
                                TileBase decor = decorationTiles[random.Next(decorationTiles.Length)];
                                decorationTilemap.SetTile(tilePos, decor);
                            }
                        }
                    }
                    else
                    {
                        // Wall tile
                        if (wallTilemap != null && wallTile != null)
                        {
                            wallTilemap.SetTile(tilePos, wallTile);
                        }
                    }
                }
            }
        }

        private void PlaceSpawnPoint()
        {
            if (rooms.Count == 0) return;

            // Place player in main room
            Room mainRoom = rooms[0];
            if (mainRoom.Tiles.Count > 0)
            {
                Vector2Int spawnTile = mainRoom.Tiles[random.Next(mainRoom.Tiles.Count)];
                Vector3 spawnPos = TileToWorld(spawnTile);

                if (playerSpawnPoint != null)
                {
                    playerSpawnPoint.position = spawnPos;
                }

                // Move player to spawn
                var player = Player.PlayerController.Instance;
                if (player != null)
                {
                    player.Teleport(spawnPos);
                }
            }
        }

        public Vector3 TileToWorld(Vector2Int tile)
        {
            return new Vector3(tile.x - width / 2 + 0.5f, tile.y - height / 2 + 0.5f, 0);
        }

        public Vector2Int WorldToTile(Vector3 worldPos)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPos.x + width / 2),
                Mathf.FloorToInt(worldPos.y + height / 2)
            );
        }

        public bool IsWalkable(Vector3 worldPos)
        {
            Vector2Int tile = WorldToTile(worldPos);

            if (tile.x < 0 || tile.x >= width || tile.y < 0 || tile.y >= height)
            {
                return false;
            }

            return map[tile.x, tile.y] == 0;
        }

        public Vector3 GetRandomWalkablePosition()
        {
            if (rooms.Count == 0) return Vector3.zero;

            Room room = rooms[random.Next(rooms.Count)];
            Vector2Int tile = room.Tiles[random.Next(room.Tiles.Count)];

            return TileToWorld(tile);
        }

        private void OnDrawGizmos()
        {
            if (map == null) return;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    Gizmos.color = map[x, y] == 1 ? Color.black : Color.white;
                    Vector3 pos = new Vector3(x - width / 2 + 0.5f, y - height / 2 + 0.5f, 0);
                    Gizmos.DrawCube(pos, Vector3.one * 0.9f);
                }
            }
        }
    }

    /// <summary>
    /// Represents a room in the generated level.
    /// </summary>
    public class Room
    {
        public List<Vector2Int> Tiles { get; private set; }
        public List<Vector2Int> EdgeTiles { get; private set; }
        public List<Room> ConnectedRooms { get; private set; }
        public int Size => Tiles.Count;
        public bool IsMainRoom { get; set; }
        public bool IsAccessibleFromMainRoom { get; set; }

        public Room(List<Vector2Int> tiles, int[,] map)
        {
            Tiles = tiles;
            ConnectedRooms = new List<Room>();
            EdgeTiles = new List<Vector2Int>();

            foreach (var tile in tiles)
            {
                for (int x = tile.x - 1; x <= tile.x + 1; x++)
                {
                    for (int y = tile.y - 1; y <= tile.y + 1; y++)
                    {
                        if (x == tile.x || y == tile.y)
                        {
                            if (x >= 0 && x < map.GetLength(0) && y >= 0 && y < map.GetLength(1))
                            {
                                if (map[x, y] == 1)
                                {
                                    EdgeTiles.Add(tile);
                                }
                            }
                        }
                    }
                }
            }
        }

        public static void ConnectRooms(Room roomA, Room roomB)
        {
            if (roomA.IsAccessibleFromMainRoom)
            {
                roomB.SetAccessibleFromMainRoom();
            }
            else if (roomB.IsAccessibleFromMainRoom)
            {
                roomA.SetAccessibleFromMainRoom();
            }

            roomA.ConnectedRooms.Add(roomB);
            roomB.ConnectedRooms.Add(roomA);
        }

        public bool IsConnected(Room other)
        {
            return ConnectedRooms.Contains(other);
        }

        public void SetAccessibleFromMainRoom()
        {
            if (IsAccessibleFromMainRoom) return;

            IsAccessibleFromMainRoom = true;

            foreach (var room in ConnectedRooms)
            {
                room.SetAccessibleFromMainRoom();
            }
        }
    }
}
