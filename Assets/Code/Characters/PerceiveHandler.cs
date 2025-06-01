using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Code.Managers;
using Code.Tiles;
using UnityEngine;
using Code.Characters.Memories;

namespace Code.Characters.Perceive
{
    public class PerceiveHandler
    {
        private readonly NonPlayerCharacter _npc;

        public PerceiveHandler(NonPlayerCharacter npc)
        {
            _npc = npc;
        }

        public async Task<List<ConceptNode>> Perceive()
        {
            var nearbyTiles = TileManager.Instance.GetNearbyTiles(_npc.position, _npc.vision);
            
            // Update spatial memory
            // Todo: the changes in the environment is not actually perceived and reacted
            // For now only the change that the relic is missing will be perceived
            foreach (var tile in nearbyTiles)
            {
                _npc.SpatialMemory.tileInfos[tile.Position] = new TileInfo
                {
                    Position = tile.Position,
                    IsWalkable = tile.IsWalkable,
                    TileSector = tile.TileSector,
                    TileObject = tile.TileObject,
                    HasRelic = tile.HasRelic,
                };
            }

            // Perceive the events in the tiles
            // First retrieve all the new events in the tiles,
            // then sort them by their event time,
            // and finally take the first _perceiveBandwidth events
            var perceivedEvents = nearbyTiles
                .SelectMany(tile => tile.TileEvents)
                .Where(e => e.GameTime.IsNewerThan(GameManager.Instance.currentTurn - 1,
                    GameManager.Instance.currentSequence) || (e.IsPersistent && !_npc.HasPerceivedMissingEvent))
                .OrderBy(e => Vector2Int.Distance(e.Position, _npc.position))
                .Take(_npc.PerceiveBandwidth)
                .ToList();
            
            // if it includes the persistent event, set the flag to true
            if (perceivedEvents.Any(e => e.IsPersistent))
            {
                _npc.HasPerceivedMissingEvent = true;
            }

            // Todo: Do we actually need the retention events?
            // For example, if one character is attacking another, then it should always be perceived
            // // retrieve the retention events
            // var retentionEvents = _npc.AssociativeMemory.GetSummarizedLastestEvents(_npc.Retention);
            // // if any of the perceived events are already in the retention events, then remove them from the perceived events
            // perceivedEvents = perceivedEvents
            //     .Where(e => !retentionEvents.Any(re => re.Subject == e.Subject && re.Predicate == e.Predicate &&
            //                                            re.Object == e.Object))
            //     .ToList();

            List<ConceptNode> eventNodes = new List<ConceptNode>();
            
            // Start to add the perceived events to the associative memory
            foreach (var e in perceivedEvents)
            {
                // make the keywords of the event
                var keywords = new HashSet<string>();

                keywords.Add(e.Subject);
                keywords.Add(e.Object);

                // get the embedding
                // // remove possible parentheses from the description to get the embedding key
                // var embeddingKey = System.Text.RegularExpressions.Regex.Match(e.Description, @"\((.*?)\)").Success
                //     ? System.Text.RegularExpressions.Regex.Match(e.Description, @"\((.*?)\)").Groups[1].Value
                //     : e.Description;
                // Todo: does the embedding key actually contain the parentheses?
                var embeddingKey = e.Description;
                float[] embedding;
                // check if the embedding is already in the associative memory
                // if it is, then use it
                if (_npc.AssociativeMemory.Embeddings.TryGetValue(embeddingKey, out var value))
                {
                    embedding = value;
                }
                else
                {
                    embedding = await OpenaiApi.OpenaiApi.GetEmbedding(embeddingKey);
                }

                // get the poignancy of the event
                int poignancyScore = await GetPoignancyScore(NodeType.Event, e.Description);
                
                // add the event to the associative memory
                var eventNode = _npc.AssociativeMemory.AddEvent(GameManager.Instance.CurrentGameTime, null,
                    e.Subject, e.Predicate, e.Object, e.Description, new KeyValuePair<string, float[]>(embeddingKey,
                        embedding), poignancyScore, keywords.ToList(), new EventThoughtFilling
                    {
                        ReferencedNodeIds = new(),
                    });
                eventNodes.Add(eventNode);
                _npc.currentImportanceTrigger += poignancyScore;
                _npc.reflectionCount++;
            }
            return eventNodes;
        }

        private async Task<int> GetPoignancyScore(NodeType eventType, string description)
        {
            if (description.Contains("is idle"))
            {
                return 1;
            }

            if (eventType == NodeType.Event)
            {
                return await OpenaiApi.OpenaiApi.GetEventPoignancy(_npc, description);
            }

            if (eventType == NodeType.Chat)
            {
                return await OpenaiApi.OpenaiApi.GetChatPoignancy(_npc);
            }

            throw new Exception("Unknown event type: " + eventType);
        }
    }
}