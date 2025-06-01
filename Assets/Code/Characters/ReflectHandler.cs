using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Code.Characters.Memories;
using Code.Managers;
using UnityEngine;

namespace Code.Characters
{
    public class ReflectHandler
    {
        private readonly NonPlayerCharacter _npc;

        public ReflectHandler(NonPlayerCharacter npc)
        {
            _npc = npc;
        }

        public async Task Reflect()
        {
            // Generate focal-points about the recently accessed events and thoughts
            var nodes = new List<ConceptNode>();
            nodes.AddRange(_npc.AssociativeMemory.Events);
            nodes.AddRange(_npc.AssociativeMemory.Thoughts);
            nodes.Sort((x, y) => x.LastAccessTime.IsNewerThan(y.LastAccessTime) ? -1 : 1);
            nodes = nodes.Take(_npc.reflectionCount).ToList();
            var statements = Utils.GetSummaryStatements(nodes);
            var focalPoints = await OpenaiApi.OpenaiApi.GenerateFocalPoints(_npc, statements, 3);

            // Retrieve the nodes from the focal points; 
            // For each of the focal points, generate one thought and add it into the associative memory
            foreach (var focalPoint in focalPoints)
            {
                var retrievedNodes = await _npc.RetrieveNodes(focalPoint, 30);
                var retrievedStatements = Utils.GetSummaryStatements(retrievedNodes);
                var insights = await OpenaiApi.OpenaiApi.GenerateInsights(_npc, retrievedStatements, 2);
                
                foreach (var (thought, evidence) in insights)
                {
                    var evidenceNodeIds = evidence.Select(index => retrievedNodes[index].NodeId).ToList();
                    
                    var eventTriple = await OpenaiApi.OpenaiApi.GetActionTriple(thought);
                    var keywords = new HashSet<string>
                    {
                        eventTriple.subject,
                        eventTriple.predicate,
                        eventTriple.@object
                    };
                    var memoPoignancyScore =
                        await OpenaiApi.OpenaiApi.GetThoughtPoignancy(_npc, thought);
                    var memoEmbedding = await OpenaiApi.OpenaiApi.GetEmbedding(thought);
                    _npc.AssociativeMemory.AddThought(GameManager.Instance.CurrentGameTime, null,
                        eventTriple.subject, eventTriple.predicate, eventTriple.@object,
                        thought,
                        new KeyValuePair<string, float[]>(thought, memoEmbedding),
                        memoPoignancyScore,
                        keywords.ToList(), new EventThoughtFilling()
                        {
                            ReferencedNodeIds = evidenceNodeIds
                        });
                }
            }
        }

        /// <summary>
        /// The important thing to be changed: currently
        /// Todo: DailyPlanRequirements should also be changed if it is more than one day
        /// </summary>
        /// <returns>bool - whether the currently is changed</returns>
        public async Task<bool> ReviseIdentity()
        {
            // Retrieve
            var retrievedPlans = await _npc.RetrieveNodes($"{_npc.characterName}'s plan for the day", 15);
            var retrievedEvents = await _npc.RetrieveNodes($"Important recent events for {_npc.characterName}'s life", 15);
            var statements = Utils.GetSummaryStatements(retrievedPlans);
            statements += '\n' + Utils.GetSummaryStatements(retrievedEvents);
            
            // generate a plan note and a thought note
            var planNote = await OpenaiApi.OpenaiApi.GeneratePlanNote(_npc, statements);
            var thoughtNote = await OpenaiApi.OpenaiApi.GenerateThoughtNote(_npc, statements);
            
            Debug.Log($"Plan note: {planNote}");
            Debug.Log($"Thought note: {thoughtNote}");
            
            // generate a new currently
            var newCurrently = await OpenaiApi.OpenaiApi.GenerateCurrently(_npc, planNote, thoughtNote);
            if (newCurrently != null)
            {
                _npc.currently = newCurrently;
                return true;
            }

            return false;
        }

        // public async Task<bool> ReviseSchedule(string oldCurrently)
        // {
        //     var newSchedule = await OpenaiApi.OpenaiApi.GetScheduleAfterReflection(_npc, oldCurrently);
        //     
        //     // If the new schedule is null, it means the npc doesn't want to react
        //     if (newSchedule == null)
        //     {
        //         return false;
        //     }
        //     // If it is not null, update the daily schedule
        //     else
        //     {
        //         _npc.DailySchedule.ChangeScheduleFrom(newSchedule, GameManager.Instance.currentHour, GameManager.Instance.currentMinute);
        //         return true;
        //     }
        // }
    }
}