using System.Collections.Generic;
using System.Text;
using Code.Tiles;
using UnityEngine;

namespace Code.Characters.Memories
{
    public class SpatialMemory
    {
        public Dictionary<Vector2Int, TileInfo> tileInfos;
        public SpatialMemory()
        {
            tileInfos = new();
            foreach (var kv in TileManager.Instance.InitialSpitialMemory)
            {
                var newTileInfo = new TileInfo
                {
                    Position = kv.Value.Position,
                    IsWalkable = kv.Value.IsWalkable,
                    TileSector = kv.Value.TileSector,
                    TileObject = kv.Value.TileObject,
                    HasRelic = kv.Value.HasRelic,
                };
                tileInfos.Add(kv.Key, newTileInfo);
            }
        }

        public string GetAllKnownSectors()
        {
            var sb = new StringBuilder();
            HashSet<string> addedSectors = new HashSet<string>();
            // Get where the relic is
            string relicLocation = "";
            foreach (var tile in tileInfos)
            {
                if (tile.Value.HasRelic)
                {
                    relicLocation = tile.Value.TileSector.Name;
                    break;
                }
            }
            
            foreach (var tile in tileInfos)
            {
                if (tile.Value.TileSector != null && !addedSectors.Contains(tile.Value.TileSector.Name))
                {
                    addedSectors.Add(tile.Value.TileSector.Name);
                    string sector = $"{tile.Value.TileSector.Name}: {tile.Value.TileSector.Description}";
                    if (tile.Value.TileSector.Name == relicLocation)
                    {
                        sector += "(The relic is here)";
                    }
                    sb.AppendLine(sector);
                }
            }
            return sb.ToString();
        }
        
        public string GetAllKnownObjectsInSector(string sector)
        {
            var sb = new StringBuilder();
            HashSet<string> addedObjects = new HashSet<string>();
            
            foreach (var tile in tileInfos)
            {
                if (tile.Value.TileSector != null && tile.Value.TileSector.Name == sector)
                {
                    if (tile.Value.TileObject != null && !addedObjects.Contains(tile.Value.TileObject.Name))
                    {
                        addedObjects.Add(tile.Value.TileObject.Name);
                        string obj = $"{tile.Value.TileObject.Name}: {tile.Value.TileObject.Description}";
                        sb.AppendLine(obj);
                    }
                }
            }
            return sb.ToString();
        }
        
        public Vector2Int? FindClosestObjectByPath(
            string sectorName,
            string objectName,
            Vector2Int startPosition)
        {
            var visited = new HashSet<Vector2Int>();
            var queue = new Queue<(Vector2Int pos, int distance)>();
            queue.Enqueue((startPosition, 0));
            visited.Add(startPosition);

            Vector2Int? closestTarget = null;
            int shortestDistance = int.MaxValue;

            while (queue.Count > 0)
            {
                var (current, dist) = queue.Dequeue();

                if (!tileInfos.TryGetValue(current, out var tile))
                {
                    continue;
                }

                // Check if this tile is in the correct sector and contains the object
                if (tile.TileSector?.Name == sectorName &&
                    tile.TileObject != null &&
                    tile.TileObject.Name == objectName)
                {
                    if (dist < shortestDistance)
                    {
                        shortestDistance = dist;
                        closestTarget = current;
                    }
                    continue;
                }
                
                // if the tile is not walkable, do not explore it
                if (!tile.IsWalkable && tile.Position != startPosition)
                {
                    continue;
                }

                // Explore 4 directions (N/S/E/W)
                var directions = new Vector2Int[]
                {
                    new Vector2Int(0, 1),
                    new Vector2Int(0, -1),
                    new Vector2Int(1, 0),
                    new Vector2Int(-1, 0),
                };

                foreach (var dir in directions)
                {
                    var next = current + dir;
                    if (!visited.Contains(next))
                    {
                        visited.Add(next);
                        queue.Enqueue((next, dist + 1));
                    }
                }
            }

            return closestTarget;
        }
    }
}