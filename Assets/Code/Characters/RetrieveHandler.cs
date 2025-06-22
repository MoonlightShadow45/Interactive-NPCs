using System.Collections.Generic;
using System.Threading.Tasks;
using Code.Characters.Memories;

namespace Code.Characters.Retrieve
{
    public class RetrieveHandler
    {
        private readonly NonPlayerCharacter _npc;

        public RetrieveHandler(NonPlayerCharacter npc)
        {
            _npc = npc;
        }

        /// <summary>
        /// Retrieve the relevant events and thoughts from the associative memory
        /// </summary>
        /// <param name="events"></param>
        /// <returns>A Dictionary of the retrieved events for the events passed in</returns>
        public Dictionary<ConceptNode, RetrievedEvents> Retrieve(List<ConceptNode> events)
        {
            var retrieved = new Dictionary<ConceptNode, RetrievedEvents>();
            foreach (var e in events)
            {
                var relevantEvents = _npc.AssociativeMemory.RetrieveRelevantEvents(e);
                var relevantThoughts = _npc.AssociativeMemory.RetrieveRelevantThoughts(e);
                
                retrieved[e] = new RetrievedEvents
                {
                    OriginalEvent = e,
                    RelevantEvents = relevantEvents,
                    RelevantThoughts = relevantThoughts
                };
            }

            return retrieved;
        }
    }

    public class RetrievedEvents
    {
        public ConceptNode OriginalEvent;
        public List<ConceptNode> RelevantEvents;
        public List<ConceptNode> RelevantThoughts;
        
        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Original Event: at {OriginalEvent.CreationTime.ToRealTime()}, {OriginalEvent.Description}");
            if (RelevantEvents.Count > 0)
            {
                sb.AppendLine("Relevant Events:");
                foreach (var e in RelevantEvents)
                {
                    sb.AppendLine($"- at {e.CreationTime.ToRealTime()}, {e.Description}");
                }
            }

            if (RelevantThoughts.Count > 0)
            {
                sb.AppendLine("Relevant Thoughts:");
                foreach (var t in RelevantThoughts)
                {
                    sb.AppendLine($"- at {t.CreationTime.ToRealTime()}, {t.Description}");
                }
            }

            return sb.ToString();
        }
    }
}