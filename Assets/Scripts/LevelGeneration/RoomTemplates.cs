using UnityEngine;
using System.Collections.Generic;

namespace PixelProject.LevelGeneration
{
    /// <summary>
    /// ScriptableObject for defining pre-designed room templates.
    /// </summary>
    [CreateAssetMenu(fileName = "New Room Template", menuName = "Pixel Project/Room Template")]
    public class RoomTemplate : ScriptableObject
    {
        public string templateId;
        public string templateName;
        public RoomType roomType = RoomType.Normal;

        [Header("Dimensions")]
        public int width = 10;
        public int height = 10;

        [Header("Prefab")]
        public GameObject roomPrefab;

        [Header("Spawn Points")]
        public List<Vector2Int> enemySpawnPoints = new List<Vector2Int>();
        public List<Vector2Int> itemSpawnPoints = new List<Vector2Int>();
        public Vector2Int playerEntryPoint;

        [Header("Doors")]
        public List<DoorData> doors = new List<DoorData>();

        [Header("Generation")]
        public int minWaveToAppear = 1;
        public int spawnWeight = 10;
        public bool isUnique = false; // Only spawn once per run
    }

    [System.Serializable]
    public class DoorData
    {
        public Vector2Int position;
        public DoorDirection direction;
    }

    public enum DoorDirection
    {
        North,
        South,
        East,
        West
    }

    public enum RoomType
    {
        Normal,
        Treasure,
        Shop,
        Boss,
        Secret,
        Challenge,
        Rest
    }

    /// <summary>
    /// Manages room-based dungeon generation using templates.
    /// </summary>
    public class DungeonGenerator : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private int dungeonWidth = 5;
        [SerializeField] private int dungeonHeight = 5;
        [SerializeField] private int minRooms = 8;
        [SerializeField] private int maxRooms = 15;

        [Header("Room Templates")]
        [SerializeField] private List<RoomTemplate> normalRooms = new List<RoomTemplate>();
        [SerializeField] private List<RoomTemplate> specialRooms = new List<RoomTemplate>();
        [SerializeField] private RoomTemplate startRoom;
        [SerializeField] private RoomTemplate bossRoom;

        [Header("Room Spacing")]
        [SerializeField] private float roomSpacingX = 20f;
        [SerializeField] private float roomSpacingY = 20f;

        private RoomNode[,] dungeonGrid;
        private List<RoomNode> generatedRooms = new List<RoomNode>();
        private System.Random random;

        public void GenerateDungeon(string seed = null)
        {
            if (string.IsNullOrEmpty(seed))
            {
                seed = System.DateTime.Now.Ticks.ToString();
            }
            random = new System.Random(seed.GetHashCode());

            // Initialize grid
            dungeonGrid = new RoomNode[dungeonWidth, dungeonHeight];
            generatedRooms.Clear();

            // Place start room in center
            int startX = dungeonWidth / 2;
            int startY = dungeonHeight / 2;
            PlaceRoom(startX, startY, startRoom, RoomType.Normal, true);

            // Generate connected rooms
            int targetRooms = random.Next(minRooms, maxRooms + 1);
            GenerateRooms(targetRooms);

            // Place special rooms
            PlaceSpecialRooms();

            // Instantiate room prefabs
            InstantiateRooms();

            Debug.Log($"Dungeon generated with {generatedRooms.Count} rooms");
        }

        private void GenerateRooms(int targetCount)
        {
            List<Vector2Int> possiblePositions = new List<Vector2Int>();

            while (generatedRooms.Count < targetCount)
            {
                // Get all valid expansion positions
                possiblePositions.Clear();

                foreach (var room in generatedRooms)
                {
                    AddPossiblePositions(room.GridPosition, possiblePositions);
                }

                if (possiblePositions.Count == 0) break;

                // Pick random position
                Vector2Int newPos = possiblePositions[random.Next(possiblePositions.Count)];

                // Select template
                RoomTemplate template = SelectTemplate(normalRooms);
                if (template != null)
                {
                    PlaceRoom(newPos.x, newPos.y, template, RoomType.Normal, false);
                }
            }
        }

        private void AddPossiblePositions(Vector2Int from, List<Vector2Int> positions)
        {
            Vector2Int[] directions = {
                Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
            };

            foreach (var dir in directions)
            {
                Vector2Int newPos = from + dir;

                if (IsValidPosition(newPos) && dungeonGrid[newPos.x, newPos.y] == null)
                {
                    if (!positions.Contains(newPos))
                    {
                        // Check neighbor count to avoid too clustered layout
                        int neighbors = CountNeighbors(newPos);
                        if (neighbors <= 2)
                        {
                            positions.Add(newPos);
                        }
                    }
                }
            }
        }

        private int CountNeighbors(Vector2Int pos)
        {
            int count = 0;
            Vector2Int[] directions = {
                Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
            };

            foreach (var dir in directions)
            {
                Vector2Int checkPos = pos + dir;
                if (IsValidPosition(checkPos) && dungeonGrid[checkPos.x, checkPos.y] != null)
                {
                    count++;
                }
            }

            return count;
        }

        private bool IsValidPosition(Vector2Int pos)
        {
            return pos.x >= 0 && pos.x < dungeonWidth && pos.y >= 0 && pos.y < dungeonHeight;
        }

        private void PlaceRoom(int x, int y, RoomTemplate template, RoomType type, bool isStart)
        {
            RoomNode room = new RoomNode
            {
                GridPosition = new Vector2Int(x, y),
                Template = template,
                RoomType = type,
                IsStartRoom = isStart
            };

            dungeonGrid[x, y] = room;
            generatedRooms.Add(room);

            // Connect to neighbors
            ConnectToNeighbors(room);
        }

        private void ConnectToNeighbors(RoomNode room)
        {
            Vector2Int[] directions = {
                Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
            };

            foreach (var dir in directions)
            {
                Vector2Int neighborPos = room.GridPosition + dir;

                if (IsValidPosition(neighborPos))
                {
                    RoomNode neighbor = dungeonGrid[neighborPos.x, neighborPos.y];
                    if (neighbor != null)
                    {
                        room.ConnectedRooms.Add(neighbor);
                        if (!neighbor.ConnectedRooms.Contains(room))
                        {
                            neighbor.ConnectedRooms.Add(room);
                        }
                    }
                }
            }
        }

        private void PlaceSpecialRooms()
        {
            // Find dead ends for special rooms
            List<RoomNode> deadEnds = new List<RoomNode>();

            foreach (var room in generatedRooms)
            {
                if (room.ConnectedRooms.Count == 1 && !room.IsStartRoom)
                {
                    deadEnds.Add(room);
                }
            }

            // Place boss room at furthest dead end from start
            if (deadEnds.Count > 0 && bossRoom != null)
            {
                RoomNode startRoom = generatedRooms.Find(r => r.IsStartRoom);
                RoomNode furthest = null;
                int maxDistance = 0;

                foreach (var deadEnd in deadEnds)
                {
                    int distance = CalculateDistance(startRoom, deadEnd);
                    if (distance > maxDistance)
                    {
                        maxDistance = distance;
                        furthest = deadEnd;
                    }
                }

                if (furthest != null)
                {
                    furthest.Template = bossRoom;
                    furthest.RoomType = RoomType.Boss;
                    deadEnds.Remove(furthest);
                }
            }

            // Place treasure/shop rooms at other dead ends
            foreach (var deadEnd in deadEnds)
            {
                if (specialRooms.Count > 0)
                {
                    RoomTemplate special = SelectTemplate(specialRooms);
                    if (special != null)
                    {
                        deadEnd.Template = special;
                        deadEnd.RoomType = special.roomType;
                    }
                }
            }
        }

        private int CalculateDistance(RoomNode from, RoomNode to)
        {
            // Simple Manhattan distance
            return Mathf.Abs(from.GridPosition.x - to.GridPosition.x) +
                   Mathf.Abs(from.GridPosition.y - to.GridPosition.y);
        }

        private RoomTemplate SelectTemplate(List<RoomTemplate> templates)
        {
            if (templates.Count == 0) return null;

            int totalWeight = 0;
            foreach (var template in templates)
            {
                totalWeight += template.spawnWeight;
            }

            int randomValue = random.Next(totalWeight);
            int currentWeight = 0;

            foreach (var template in templates)
            {
                currentWeight += template.spawnWeight;
                if (randomValue < currentWeight)
                {
                    return template;
                }
            }

            return templates[0];
        }

        private void InstantiateRooms()
        {
            foreach (var room in generatedRooms)
            {
                Vector3 worldPos = new Vector3(
                    room.GridPosition.x * roomSpacingX,
                    room.GridPosition.y * roomSpacingY,
                    0
                );

                if (room.Template != null && room.Template.roomPrefab != null)
                {
                    room.Instance = Instantiate(room.Template.roomPrefab, worldPos, Quaternion.identity, transform);
                    room.Instance.name = $"Room_{room.GridPosition.x}_{room.GridPosition.y}_{room.RoomType}";
                }
            }
        }

        public Vector3 GetStartPosition()
        {
            var start = generatedRooms.Find(r => r.IsStartRoom);
            if (start != null)
            {
                return new Vector3(
                    start.GridPosition.x * roomSpacingX,
                    start.GridPosition.y * roomSpacingY,
                    0
                );
            }
            return Vector3.zero;
        }
    }

    public class RoomNode
    {
        public Vector2Int GridPosition;
        public RoomTemplate Template;
        public RoomType RoomType;
        public bool IsStartRoom;
        public bool IsCleared;
        public List<RoomNode> ConnectedRooms = new List<RoomNode>();
        public GameObject Instance;
    }
}
