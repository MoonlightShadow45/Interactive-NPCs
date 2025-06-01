using System.Collections.Generic;
using Code.Characters;
using Code.Characters.Memories;
using Code.Managers;
using JetBrains.Annotations;
using UnityEngine;

namespace Code.Tiles
{
    public class MyTile : TileInfo
    {
        [CanBeNull] public Character Character;
        public List<TileEvent> TileEvents = new();
    }
    
    // This is used for the spatial memory
    public class TileInfo
    {
        public Vector2Int Position;
        public TileSector TileSector;
        [CanBeNull] public TileObject TileObject;
        public bool HasRelic = false;
        public bool IsWalkable;
        
        public void UpdateTileInfo(TileInfo tileInfo)
        {
            Position = tileInfo.Position;
            TileSector = tileInfo.TileSector;
            TileObject = tileInfo.TileObject;
            IsWalkable = tileInfo.IsWalkable;
        }
    }

    public class TileObject
    {
        public string Name;
        public string Description;
    }
    
    public class TileSector
    {
        public string Name;
        public string Description;
    }
    
    public class TileEvent
    {
        public GameTime GameTime;
        public Vector2Int Position;
        public string Subject;
        public string Predicate;
        public string Object;
        public string Description;
        public bool IsPersistent = false;
    }

    [System.Serializable]
    public class GameTime
    {
        public int eventTurn;
        public int eventSequence;
        
        public bool IsNewerThan(int turn, int sequence)
        {
            if (eventTurn > turn)
            {
                return true;
            }
            else if (eventTurn == turn && eventSequence > sequence)
            {
                return true;
            }

            return false;
        }

        public bool IsNewerThan(GameTime other)
        {
            if (eventTurn > other.eventTurn)
            {
                return true;
            }
            else if (eventTurn == other.eventTurn && eventSequence > other.eventSequence)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Get the real time of the GameTime object in the format of HH:MM
        /// </summary>
        /// <returns></returns>
        public string ToRealTime()
        {
            return GameManager.Instance.GetRealTime(eventTurn);
        }
    }
}