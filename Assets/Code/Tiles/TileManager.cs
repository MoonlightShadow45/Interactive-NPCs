using System.Collections.Generic;
using System.Linq;
using Code.Characters;
using Code.Managers;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Code.Tiles
{
    public class TileManager : MonoBehaviour
    {
        public static TileManager Instance;
    
        [SerializeField] private Tilemap tilemapGround;
        [SerializeField] private Tilemap tilemapWall;
        [SerializeField] private Tilemap tilemapItems;
        [SerializeField] private Tilemap tilemapFence;

        public Dictionary<Vector2Int, TileInfo> InitialSpitialMemory;
        private Dictionary<Vector2Int, MyTile> _tiles = new();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
            
            InitiateTiles();
        }
    
        private void InitiateTiles()
        {
            var tileObjectsGround = GetTileObjectsInTraverse(tilemapGround);
            var tileObjectsWall = GetTileObjectsInTraverse(tilemapWall);
            var tileObjectsItems = GetTileObjectsInTraverse(tilemapItems);
            var tileObjectsFence = GetTileObjectsInTraverse(tilemapFence);
            foreach (var groundTile in tileObjectsGround)
            {
                Vector2Int position = groundTile.Position;
                var wallTile = GetTileObjectInTraverse(position, tileObjectsWall);
                var itemTile = GetTileObjectInTraverse(position, tileObjectsItems);
                var fenceTile = GetTileObjectInTraverse(position, tileObjectsFence);
                TileObject tileObject = GetTileObject(wallTile, itemTile, fenceTile);
                TileSector tileSector = GetTileSector(position);
                MyTile tile = new MyTile
                {
                    Position = position,
                    TileSector = tileSector,
                    TileObject = tileObject,
                    HasRelic = tileObject?.Name == "relic",
                    IsWalkable = wallTile == null && itemTile == null && fenceTile == null
                };
                _tiles.Add(position, tile);
            }
            
            InitialSpitialMemory = _tiles.ToDictionary(kvp => kvp.Key, kvp => (TileInfo)kvp.Value);
            
            // // add a testing event to the tile 6,-2
            // TileManager.Instance.AddEventToTile(new Vector2Int(6, -2), new TileEvent
            // {
            //     GameTime = GameManager.Instance.CurrentGameTime,
            //     Position = new Vector2Int(6, -2),
            //     Subject = "Elira Duskwood",
            //     Predicate = "is killing",
            //     Object = "herself",
            //     Description = "Elira Duskwood is killing herself with a knife."
            // });
        }
        
        public MyTile GetTile(Vector2Int position)
        {
            if (_tiles.TryGetValue(position, out var tile))
            {
                return tile;
            }
            else
            {
                return null;
            }
        }
    
        private List<TileObjectInTraverse> GetTileObjectsInTraverse(Tilemap tilemap)
        {
            List<TileObjectInTraverse> tileObjects = new List<TileObjectInTraverse>();
            BoundsInt bounds = tilemap.cellBounds;
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                for (int y = bounds.yMin; y < bounds.yMax; y++)
                {
                    Vector3Int position = new Vector3Int(x, y, 0);
                    TileBase tile = tilemap.GetTile(position);
                    if (tile != null)
                    {
                        TileObjectInTraverse tileObjectInTraverse = new TileObjectInTraverse
                        {
                            Position = new Vector2Int(Mathf.FloorToInt(x), Mathf.FloorToInt(y)),
                            Name = tile.name
                        };
                        tileObjects.Add(tileObjectInTraverse);
                    }
                }
            }
            return tileObjects;
        }
    
        private TileObjectInTraverse GetTileObjectInTraverse(Vector2Int position, List<TileObjectInTraverse> tileObjects)
        {
            foreach (var tileObject in tileObjects)
            {
                if (tileObject.Position == position)
                {
                    return tileObject;
                }
            }
            return null;
        }
    
        private TileObject GetTileObject(TileObjectInTraverse wallTile, TileObjectInTraverse itemTile, TileObjectInTraverse fenceTile)
        {
            if (wallTile != null)
            {
                return new TileObject
                {
                    Name = "wall",
                    Description = "A wall."
                };
            }
        
            if (fenceTile != null)
            {
                if (fenceTile.Name == "Edric_Duty_Point")
                {
                    return new TileObject
                    {
                        Name = "Edric_Duty_Point",
                        Description = "Where Edric, the guard of the manor door in the street, should be standing when he is on duty."
                    };
                }
                
                if (fenceTile.Name == "Gareth_Rest_Point")
                {
                    return new TileObject
                    {
                        Name = "Gareth_Standing_Point",
                        Description = "Where Gareth likes to take rest and do his own daily things when there is no real 'business' to attend."
                    };
                }

                return new TileObject
                {
                    Name = "fence",
                    Description = "A 2-meter high fence."
                };
            }
        
            if (itemTile != null)
            {
                if (itemTile.Name == "Roland_Duty_Point")
                {
                    return new TileObject
                    {
                        Name = "Roland_Duty_Point",
                        Description = "Where Roland, the guard of the house door in the manor, should be standing when he is on duty."
                    };
                }
                
                if (itemTile.Name == "decor_desk")
                {
                    return new TileObject
                    {
                        Name = "desk",
                        Description = "A desk with all kinds of essential books on it."
                    };
                }

                if (itemTile.Name == "decor_bed")
                {
                    return new TileObject
                    {
                        Name = "bed",
                        Description = "A bed with a soft mattress. Only the landlord can legally sleep on it."
                    };
                }

                if (itemTile.Name == "decor_chest")
                {
                    return new TileObject
                    {
                        Name = "relic",
                        Description = "A wooden chest with the relic in it. It is glowing with a faint light."
                    };
                }
                
                if (itemTile.Name == "decor_escape")
                {
                    return new TileObject
                    {
                        Name = "escape point",
                        Description = "A path to escape from the street. This shouldn't be a target unless the character wants to leave the town with the relic in hand."
                    };
                }
                
                if (itemTile.Name == "decor_tree")
                {
                    return new TileObject
                    {
                        Name = "obstacle",
                        Description = "A tree."
                    };
                }

                return new TileObject
                {
                    Name = "obstacle",
                    Description = "An obstacle."
                };
            }
            return null;
        }

        private class TileObjectInTraverse
        {
            public Vector2Int Position;
            public string Name;
        }
        
        private TileSector GetTileSector(Vector2Int position)
        {
            if (position.y < 5)
            {
                return new TileSector
                {
                    Name = "Street",
                    Description = "The street outside the manor. Anyone has access to the area. From the street people can escape from the town. Edric should be on duty here"
                };
            }

            else if (position.y < 13)
            {
                return new TileSector
                {
                    Name = "Manor",
                    Description = "The manor surrounding the landlord's house. Only Thane, Edric and Roland have legal access to the area. Roland should be on duty here"
                };
            }
            
            else
            {
                return new TileSector
                {
                    Name = "House",
                    Description = "The landlord's house. Only the Thane has legal access to the area - or if Edric or Roland has a really good reason to enter the house."
                };
            }

        }
        
        /// <summary>
        /// Find a path from startPosition to endPosition using BFS.
        /// If the end position is not reachable, return null
        /// If the path length is more than maxDistance, return the position of the tile at maxDistance
        /// </summary>
        /// <param name="startPosition"></param>
        /// <param name="endPosition"></param>
        /// <param name="maxDistance">The maximum length of the path</param>
        /// <param name="pathLength">The actual length of the path</param>
        /// <returns></returns>
        public Vector2Int? FindPath(Vector2Int startPosition, Vector2Int endPosition, int maxDistance, out int pathLength)
        {
            pathLength = 0;

            var directions = new Vector2Int[]
            {
                Vector2Int.up,
                Vector2Int.down,
                Vector2Int.left,
                Vector2Int.right
            };

            var queue = new Queue<Vector2Int>();
            var visited = new HashSet<Vector2Int>();
            var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
            var distanceMap = new Dictionary<Vector2Int, int>();

            queue.Enqueue(startPosition);
            visited.Add(startPosition);
            distanceMap[startPosition] = 0;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                int currentDistance = distanceMap[current];

                if (current == endPosition)
                    break;

                foreach (var dir in directions)
                {
                    var neighbor = current + dir;
                    if (visited.Contains(neighbor)) continue;

                    var tile = GetTile(neighbor);
                    if (tile == null || !tile.IsWalkable) continue;

                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                    cameFrom[neighbor] = current;
                    distanceMap[neighbor] = currentDistance + 1;
                }
            }

            if (!cameFrom.ContainsKey(endPosition))
            {
                return null;
            }

            // Reconstruct the path
            var path = new List<Vector2Int>();
            var currentPos = endPosition;
            while (currentPos != startPosition)
            {
                path.Add(currentPos);
                currentPos = cameFrom[currentPos];
            }
            path.Reverse();
            pathLength = path.Count;

            if (pathLength <= maxDistance)
                return endPosition;
            else
                return path[maxDistance - 1];
        }

        /// <summary>
        /// Find a path to any of the adjacent tiles of the endPosition
        /// If startPosition is the same as endPosition or is already adjacent to endPosition, return startPosition
        /// If endPosition is unreachable, return startPosition
        /// </summary>
        /// <param name="startPosition"></param>
        /// <param name="endPosition"></param>
        /// <param name="maxDistance">the maximum length of the path</param>
        /// <returns>The position of the end of the path</returns>
        public Vector2Int FindPathToAdjacent(Vector2Int startPosition, Vector2Int endPosition, int maxDistance)
        {
            if (startPosition == endPosition)
            {
                return startPosition;
            }
            
            var directions = new Vector2Int[]
            {
                Vector2Int.up,
                Vector2Int.down,
                Vector2Int.left,
                Vector2Int.right
            };
            
            // Check if the endPosition is adjacent to startPosition
            // if yes, return startPosition
            foreach (var dir in directions)
            {
                var neighbor = startPosition + dir;
                if (neighbor == endPosition)
                {
                    return startPosition;
                }

            }

            List<(Vector2Int pos, int pathLength)> reachable = new List<(Vector2Int, int)>();

            foreach (var dir in directions)
            {
                var neighbor = endPosition + dir;
                var tile = GetTile(neighbor);
                if (tile == null || !tile.IsWalkable)
                {
                    continue;
                }

                var result = FindPath(startPosition, neighbor, maxDistance, out var length);
                if (result.HasValue)
                {
                    reachable.Add((result.Value, length));
                }
            }

            if (reachable.Count == 0)
            {
                return startPosition;
            }
            
            // Sort reachable positions by path length
            return reachable.OrderBy(x => x.pathLength).First().pos;
        }
        
        /// <summary>
        /// Register the character's movement by updating the tile information.
        /// </summary>
        /// <param name="character"></param>
        /// <param name="previousPosition">Can be null</param>
        /// <param name="newPosition">Can be null</param>
        public void RegisterCharacterMovement(Character character, Vector2Int previousPosition, Vector2Int newPosition)
        {
            // Update the tile information for the character's previous and new positions
            var previousTile = GetTile(previousPosition);
            if (previousTile != null)
            {
                previousTile.IsWalkable = true;
                previousTile.Character = null;
                
            }

            var newTile = GetTile(newPosition);
            if (newTile != null)
            {
                newTile.IsWalkable = false;
                newTile.Character = character;
            }

            // If the character has actually moved from the previous position to the new position,
            // register the movement event
            if (previousPosition != newPosition)
            {
                // get the sector of the two tiles
                var previousSector = previousTile.TileSector.Name;
                var newSector = newTile.TileSector.Name;
                
                // TileEvent leaveEvent = new TileEvent
                // {
                //     GameTime = GameManager.Instance.CurrentGameTime,
                //     Position = previousPosition,
                //     Subject = character.characterName,
                //     Predicate = "leaves",
                //     Object = $"position {previousPosition.x},{previousPosition.y}",
                //     Description =
                //         $"{character.characterName} leaves position {previousPosition.x}, {previousPosition.y}{previousSectorText}."
                // };
                
                TileEvent enterEvent = new TileEvent
                {
                    GameTime = GameManager.Instance.CurrentGameTime,
                    Position = newPosition,
                    Subject = character.characterName,
                    Predicate = "enters",
                    Object = $"position {newPosition.x}, {newPosition.y}",
                    Description =
                        $"{character.characterName} moves from position ({previousPosition.x}, {previousPosition.y}) in {previousSector} to ({newPosition.x}, {newPosition.y}) in {newSector}."
                };
                // Check whether the relic is on the character
                if (character.Inventory.Any(x => x.Name == "relic"))
                {
                    enterEvent.Description += $" {character.characterName} is glowing with a faint light.";
                }
                
                // AddEventToTile(previousPosition, leaveEvent);
                AddEventToTile(newPosition, enterEvent);
            }
        }
        
        public Character GetCharacterAtPosition(Vector2Int position)
        {
            var tile = GetTile(position);
            return tile?.Character;
        }

        /// <summary>
        /// Get all tiles within the vision range of a given position.
        /// Can be enhanced to also count in whether the tile is visible or not.
        /// </summary>
        /// <param name="position"></param>
        /// <param name="vision"></param>
        /// <returns></returns>
        public List<MyTile> GetNearbyTiles(Vector2Int position, int vision)
        {
            List<MyTile> nearbyTiles = new List<MyTile>();

            // Get all tiles within the vision range
            for (int x = -vision; x <= vision; x++)
            {
                for (int y = -vision; y <= vision; y++)
                {
                    var neighbor = position + new Vector2Int(x, y);
                    if (Vector2Int.Distance(position, neighbor) > vision) continue;
                    var tile = GetTile(neighbor);
                    if (tile != null)
                    {
                        nearbyTiles.Add(tile);
                    }
                }
            }

            return nearbyTiles;
        }
        
        public void AddEventToTile(Vector2Int position, TileEvent tileEvent)
        {
            if (_tiles.TryGetValue(position, out var tile))
            {
                if (tile.TileEvents == null)
                {
                    tile.TileEvents = new List<TileEvent>();
                }
                tile.TileEvents.Add(tileEvent);
            }
        }

        public void RegisterCharacterDeath(Character character)
        {
            var tile = GetTile(character.position);
            if (tile != null)
            {
                tile.IsWalkable = false;
                
                // If the character has the relic, remove it from the inventory and drop it on the tile
                if (character.Inventory.Any(x => x.Name == "relic"))
                {
                    character.Inventory.Remove(character.Inventory.First(x => x.Name == "relic"));
                    tile.TileObject = new TileObject
                    {
                        Name = "relic",
                        Description = $"The relic is glowing with a faint light. {character.characterName}'s dead body is lying beside it."
                    };
                    tile.HasRelic = true;
                }
                else
                {
                    tile.TileObject = new TileObject
                    {
                        Name = $"{character.characterName}'s body",
                        Description = $"{character.characterName}'s dead body is lying on the ground."
                    };
                }
            }
            Debug.Log("Character " + character.characterName + " has died.");
        }

        public Vector2Int GetEscapeTile()
        {
            foreach (var tile in _tiles.Values)
            {
                if (tile.TileObject != null && tile.TileObject.Name == "escape point")
                {
                    return tile.Position;
                }
            }

            return Vector2Int.zero;
        }

        public Vector2Int? GetRelicTile()
        {
            foreach (var tile in _tiles.Values)
            {
                if (tile.HasRelic)
                {
                    return tile.Position;
                }
            }

            return null;
        }

        public void RegisterRelicLooted()
        {
            // there are two possible cases:
            // 1. The relic was in the original place
            // 2. The relic was in a dead character's inventory
            
            foreach (var tile in _tiles.Values)
            {
                if (tile.HasRelic)
                {
                    tile.HasRelic = false;
                    if (tile.Character != null)
                    {
                        // The relic was in a dead character's inventory
                        // Set the tile to the original body without the relic
                        tile.Character.RelicSprite.SetActive(false);
                        tile.TileObject = new TileObject
                        {
                            Name = $"{tile.Character.characterName}'s body",
                            Description = $"{tile.Character.characterName}'s dead body is lying on the ground."
                        };
                    }
                    else
                    {
                        // The relic was in the original place
                        tile.TileObject = new TileObject
                        {
                            Name = "empty chest",
                            Description = "An empty chest - the relic has been taken."
                        };
                        
                        // release a persistent event
                        TileEvent eventLooted = new TileEvent
                        {
                            GameTime = GameManager.Instance.CurrentGameTime,
                            Position = tile.Position,
                            Subject = "relic",
                            Predicate = "is not in",
                            Object = "the manor house",
                            Description = "The relic is not in the manor house. It is missing.",
                            IsPersistent = true
                        };
                        tile.TileEvents.Add(eventLooted);
                    }
                }
            }
        }

        public void RemainInPlace(Character character, Vector2Int position)
        {
            var tile = GetTile(position);
            if (tile == null)
            {
                Debug.LogWarning($"Tile at position {position} does not exist.");
                return;
            }
            
            // Release an event that the character stays in place
            TileEvent stayEvent = new TileEvent
            {
                GameTime = GameManager.Instance.CurrentGameTime,
                Position = position,
                Subject = character.characterName,
                Predicate = "stays in",
                Object = $"position {position.x}, {position.y}",
                Description = $"{character.characterName} stays in position ({position.x}, {position.y}) in {tile.TileSector.Name}."
            };
            AddEventToTile(position, stayEvent);
        }
    }
    
}
