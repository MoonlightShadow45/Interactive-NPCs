using System;
using System.Collections.Generic;
using System.Linq;
using Code.Tiles;
using JetBrains.Annotations;

namespace Code.Characters.Memories
{
    [System.Serializable]
    public class AssociativeMemory
    {
        public Dictionary<string, ConceptNode> IdToConceptNode = new();

        // the list of all events, thoughts and chats
        // newer events are at the beginning of the list
        public List<ConceptNode> Events = new();
        public List<ConceptNode> Thoughts = new();
        public List<ConceptNode> Chats = new();

        // caches for fast access
        public Dictionary<string, List<ConceptNode>> KeywordToEvents = new();
        public Dictionary<string, List<ConceptNode>> KeywordToThoughts = new();
        public Dictionary<string, List<ConceptNode>> KeywordToChats = new();

        // the strength of keywords for events or thoughts
        public Dictionary<string, int> KeywordStrengthEvents = new();
        public Dictionary<string, int> KeywordStrengthThoughts = new();

        // embedding vectors for the memories
        public Dictionary<string, float[]> Embeddings = new();

        public ConceptNode AddEvent(GameTime creationTime, [CanBeNull] GameTime expirationTime, string subject, string predicate,
            string @object,
            string description, KeyValuePair<string, float[]> embeddingPair, int poignancy,
            List<string> keywords, Filling filling)
        {
            // Don't actually understand why "cleanup" is needed here in the original code in the generative agents paper
            var node = new ConceptNode
            {
                NodeId = $"ConceptNode_{IdToConceptNode.Count + 1}",
                NodeCount = IdToConceptNode.Count + 1,
                TypeCount = Events.Count + 1,
                Type = NodeType.Event,
                Depth = 0,
                CreationTime = creationTime,
                ExpirationTime = expirationTime,
                LastAccessTime = creationTime,
                Subject = subject,
                Predicate = predicate,
                Object = @object,
                Description = description,
                EmbeddingKey = embeddingPair.Key,
                Poignancy = poignancy,
                Keywords = new List<string>(keywords.ToList()),
                Filling = filling
            };

            IdToConceptNode.Add(node.NodeId, node);
            // insert the node into the start of the list of events
            Events.Insert(0, node);
            Embeddings[embeddingPair.Key] = embeddingPair.Value;

            // update the keyword to event mapping
            foreach (var kw in node.Keywords)
            {
                if (KeywordToEvents.ContainsKey(kw))
                {
                    KeywordToEvents[kw].Insert(0, node);
                }
                else
                {
                    KeywordToEvents.Add(kw, new List<ConceptNode> { node });
                }
            }

            // update the keyword strength
            if (predicate != "is" && @object != "idle")
            {
                foreach (var kw in node.Keywords)
                {
                    if (KeywordStrengthEvents.ContainsKey(kw))
                    {
                        KeywordStrengthEvents[kw]++;
                    }
                    else
                    {
                        KeywordStrengthEvents.Add(kw, 1);
                    }
                }
            }

            return node;
        }

        public ConceptNode AddThought(GameTime creationTime, [CanBeNull] GameTime expirationTime, string subject, string predicate,
            string @object,
            string description, KeyValuePair<string, float[]> embeddingPair, int poignancy,
            List<string> keywords, EventThoughtFilling filling)
        {
            int depth = 1;
            if (filling != null && filling.ReferencedNodeIds != null && filling.ReferencedNodeIds.Count > 0)
            {
                depth += filling.ReferencedNodeIds.Select(s => IdToConceptNode[s].Depth).Max();
            }

            var node = new ConceptNode
            {
                NodeId = $"ConceptNode_{IdToConceptNode.Count + 1}",
                NodeCount = IdToConceptNode.Count + 1,
                TypeCount = Thoughts.Count + 1,
                Type = NodeType.Thought,
                Depth = depth,
                CreationTime = creationTime,
                ExpirationTime = expirationTime,
                LastAccessTime = creationTime,
                Subject = subject,
                Predicate = predicate,
                Object = @object,
                Description = description,
                EmbeddingKey = embeddingPair.Key,
                Poignancy = poignancy,
                Keywords = new List<string>(keywords.ToList()),
                Filling = filling
            };

            IdToConceptNode.Add(node.NodeId, node);
            // insert the node into the start of the list of thoughts
            Thoughts.Insert(0, node);
            Embeddings[embeddingPair.Key] = embeddingPair.Value;
            
            // update the keyword to thought mapping
            foreach (var kw in node.Keywords)
            {
                if (KeywordToThoughts.ContainsKey(kw))
                {
                    KeywordToThoughts[kw].Insert(0, node);
                }
                else
                {
                    KeywordToThoughts.Add(kw, new List<ConceptNode> { node });
                }
            }

            // update the keyword strength
            if (predicate != "is" && @object != "idle")
            {
                foreach (var kw in node.Keywords)
                {
                    if (KeywordStrengthThoughts.ContainsKey(kw))
                    {
                        KeywordStrengthThoughts[kw]++;
                    }
                    else
                    {
                        KeywordStrengthThoughts.Add(kw, 1);
                    }
                }
            }

            return node;
        }

        public ConceptNode AddChat(GameTime creationTime, [CanBeNull] GameTime expirationTime, string subject, string predicate,
            string @object,
            string description, KeyValuePair<string, float[]> embeddingPair, int poignancy,
            List<string> keywords, ChatFilling filling)
        {
            var node = new ConceptNode
            {
                NodeId = $"ConceptNode_{IdToConceptNode.Count + 1}",
                NodeCount = IdToConceptNode.Count + 1,
                TypeCount = Chats.Count + 1,
                Type = NodeType.Chat,
                Depth = 0,
                CreationTime = creationTime,
                ExpirationTime = expirationTime,
                LastAccessTime = creationTime,
                Subject = subject,
                Predicate = predicate,
                Object = @object,
                Description = description,
                EmbeddingKey = embeddingPair.Key,
                Poignancy = poignancy,
                Keywords = new List<string>(keywords.ToList()),
                Filling = filling
            };

            IdToConceptNode.Add(node.NodeId, node);
            // insert the node into the start of the list of chats
            Chats.Insert(0, node);
            Embeddings[embeddingPair.Key] = embeddingPair.Value;

            // update the keyword to chat mapping
            foreach (var kw in node.Keywords)
            {
                if (KeywordToChats.ContainsKey(kw))
                {
                    KeywordToChats[kw].Insert(0, node);
                }
                else
                {
                    KeywordToChats.Add(kw, new List<ConceptNode> { node });
                }
            }

            return node;
        }

        public HashSet<NodeSummary> GetSummarizedLastestEvents(int retention)
        {
            return new HashSet<NodeSummary>(Events.Take(retention).Select(e => e.Summary));
        }

        public List<ConceptNode> GetThoughtsAndEvents()
        {
            var res = new List<ConceptNode>();
            res.AddRange(Events);
            res.AddRange(Thoughts);
            return res;
        }

        public string GetAllSequencedEvents()
        {
            var content = new List<string>();
            for (int i = 0; i < Events.Count; i++)
            {
                var res =
                    $"Event {i} : {Events[Events.Count - i - 1].Summary.ToString()} -- {Events[Events.Count - i - 1].Description}";
                content.Add(res);
            }

            return string.Join("\n", content);
        }

        public string GetAllSequencedThoughts()
        {
            var content = new List<string>();
            for (int i = 0; i < Thoughts.Count; i++)
            {
                var res =
                    $"Thought {i} : {Thoughts[Thoughts.Count - i - 1].Summary.ToString()} -- {Thoughts[Thoughts.Count - i - 1].Description}";
                content.Add(res);
            }

            return string.Join("\n", content);
        }

        public string GetAllSequencedChats()
        {
            var content = new List<string>();
            for (int i = 0; i < Chats.Count; i++)
            {
                var res =
                    $"Chat {i} with {Chats[Chats.Count - i - 1].Object}: {Chats[Chats.Count - i - 1].Description}";
                content.Add(res);
            }

            return string.Join("\n", content);
        }
        
        public List<ConceptNode> RetrieveRelevantEvents(string subject, string predicate, string @object)
        {
            var res = new List<ConceptNode>();
            if (KeywordToEvents.ContainsKey(subject))
            {
                res.AddRange(KeywordToEvents[subject]);
            }
            if (KeywordToEvents.ContainsKey(predicate))
            {
                res.AddRange(KeywordToEvents[predicate]);
            }
            if (KeywordToEvents.ContainsKey(@object))
            {
                res.AddRange(KeywordToEvents[@object]);
            }

            return res;
        }
        
        public List<ConceptNode> RetrieveRelevantThoughts(string subject, string predicate, string @object)
        {
            var res = new List<ConceptNode>();
            if (KeywordToThoughts.ContainsKey(subject))
            {
                res.AddRange(KeywordToThoughts[subject]);
            }
            if (KeywordToThoughts.ContainsKey(predicate))
            {
                res.AddRange(KeywordToThoughts[predicate]);
            }
            if (KeywordToThoughts.ContainsKey(@object))
            {
                res.AddRange(KeywordToThoughts[@object]);
            }

            return res;
        }
    }
    
    [System.Serializable]
    public class ConceptNode
    {
        public string NodeId;
        public int NodeCount;
        public int TypeCount;
        public NodeType Type;
        public int Depth;

        public GameTime CreationTime;
        [CanBeNull] public GameTime ExpirationTime;
        public GameTime LastAccessTime;

        public string Subject;
        public string Predicate;
        public string Object;

        public string Description;
        public string EmbeddingKey;
        public int Poignancy;
        public List<string> Keywords;

        /// <summary>
        /// For thoughts, this is the list of node ids that are referenced by this thought.
        /// For chats, this is the dialogues in the chat.
        /// </summary>
        public Filling Filling;

        public NodeSummary Summary => new NodeSummary
        {
            Subject = Subject,
            Predicate = Predicate,
            Object = Object
        };
    }
    
    public class NodeSummary
    {
        public string Subject;
        public string Predicate;
        public string Object;

        public string ToString()
        {
            return $"({Subject}, {Predicate}, {Object})";
        }
    }
    public enum NodeType
    {
        Thought,
        Event,
        Chat
    }
    
    
    [System.Serializable]
    public abstract class Filling
    {
    }
    
    [System.Serializable]
    public class ChatFilling : Filling
    {
        public List<ChatEntry> Dialogue { get; set; } = new();
        public string EndReason { get; set; } = "";
    }

    [System.Serializable]
    public class ChatEntry
    {
        public string Speaker { get; set; }
        public string Content { get; set; }
    }
    
    [System.Serializable]
    public class EventThoughtFilling : Filling
    {
        public List<string> ReferencedNodeIds { get; set; } = new();
    }
}