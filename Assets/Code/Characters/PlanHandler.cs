using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Code.Characters.Memories;
using Code.Characters.Retrieve;
using Code.Managers;
using Code.Tiles;
using UnityEngine;

namespace Code.Characters.Plan
{
    public class PlanHandler
    {
        private readonly NonPlayerCharacter _npc;

        public PlanHandler(NonPlayerCharacter npc)
        {
            _npc = npc;
        }

        /// <summary>
        /// Do the long term planning at the beginning of a day
        /// </summary>
        public async Task LongTermPlan()
        {
            // Generate wake up time
            int wakeUpTime = await OpenaiApi.OpenaiApi.GetWakeupTime(_npc);
            // Set the first action to sleeping
            _npc.AddNewAction(
                _npc.position, (wakeUpTime - 0) * 60, $"{_npc.characterName} is sleeping", new NodeSummary
                {
                    Subject = _npc.characterName,
                    Predicate = "is sleeping",
                    Object = null
                }, null, null, false);

            // // If it is the first day, generate the first daily plan
            // if (GameManager.Instance.currentTurn == 1)
            // {
            //     _npc.DailyRequirements = await OpenaiApi.OpenaiApi.GetFirstDailyPlan(_npc, wakeUpTime);
            // }
            //
            // // Generate the hourly schedule based on the daily requirements
            // _npc.DailyScheduleHourly = await OpenaiApi.OpenaiApi.GetHourlySchedule(_npc, wakeUpTime);
            // // Set the origin daily schedule to a copy of the hourly schedule
            // _npc.DailySchedule = new DailySchedule(_npc.DailyScheduleHourly);

            // // Add the daily requirements to the memory
            // string thought = $"This is {_npc.characterName}'s plan for {GameManager.Instance.CurrentDay}:\n";
            // thought += _npc.DailyRequirements.ToString();
            // var embedding = await OpenaiApi.OpenaiApi.GetEmbedding(thought);
            // List<string> keywords = new List<string>();
            // keywords.Add("plan");
            // // TODO: currently it doesn't expire
            // _npc.AssociativeMemory.AddThought(GameManager.Instance.CurrentGameTime, null,
            //     _npc.characterName, "plan", $"day {GameManager.Instance.CurrentDay}", thought,
            //     new KeyValuePair<string, float[]>(thought, embedding), 5, keywords, new());
        }

        public async Task DetermineAction()
        {
            // Decompose the next schedule
            // await DecomposeSchedule(GameManager.Instance.currentHour, GameManager.Instance.currentMinute);
            // await DecomposeSchedule(GameManager.Instance.currentHour, GameManager.Instance.currentMinute + 60);
            
            // // Get the next schedule
            // var nextScheduleIndex = _npc.DailySchedule.GetCurrentIndex(GameManager.Instance.currentHour, GameManager.Instance.currentMinute);
            // var nextSchedule = _npc.DailySchedule.Schedule[nextScheduleIndex];
            
            var nextSchedule = _npc.NextSchedule;
            
            var targetCharacterName = await OpenaiApi.OpenaiApi.GetTargetCharacter(_npc, nextSchedule);
            string targetObject = null;
            bool shouldLoot = false;
            Character targetCharacter = null;
            Vector2Int address;
            if (targetCharacterName == "None")
            {
                var targetSector = await OpenaiApi.OpenaiApi.GetTargetSector(_npc, nextSchedule);
                (targetObject, shouldLoot) = await OpenaiApi.OpenaiApi.GetTargetObject(_npc, nextSchedule, targetSector);

                address = _npc.GetAddressForSectorObject(targetSector, targetObject);
            }
            else
            {
                targetCharacter = GameManager.Instance.FindCharacter(targetCharacterName);
                address = targetCharacter ? targetCharacter.position : _npc.position;
            }
            
            var (subject, predicate, @object) = await OpenaiApi.OpenaiApi.GetActionTriple(_npc, nextSchedule);
            var actionEventTriple = new NodeSummary
            {
                Subject = subject,
                Predicate = predicate,
                Object = @object,
            };
            
            _npc.AddNewAction(address, nextSchedule.DurationInMinutes, nextSchedule.Activity, actionEventTriple, targetCharacter, targetObject, shouldLoot);
        }

        // public async Task DecomposeSchedule(int hour, int minute)
        // {
        //     var currentScheduleIndex = _npc.DailySchedule.GetCurrentIndex(hour, minute);
        //     // always skip the first and last schedule -- we assume that it is sleeping
        //     if (currentScheduleIndex != 0 && currentScheduleIndex != _npc.DailySchedule.Schedule.Count - 1)
        //     {
        //         // Decompose the schedule if it is equal to or more than 60 minutes
        //         if (_npc.DailySchedule.Schedule[currentScheduleIndex].DurationInMinutes >= 60)
        //         {
        //             var schedule = _npc.DailySchedule.Schedule[currentScheduleIndex];
        //             var decomposedSchedules = await OpenaiApi.OpenaiApi.GetDecomposedSchedule(_npc, schedule);
        //             // Add the decomposed schedules to the daily schedule, replacing the original schedule entry
        //             _npc.DailySchedule.Schedule.RemoveAt(currentScheduleIndex);
        //             _npc.DailySchedule.Schedule.InsertRange(currentScheduleIndex, decomposedSchedules);
        //         }
        //     }
        // }
        
        public async Task<bool> React(Dictionary<ConceptNode, RetrievedEvents> retrievedEvents)
        {
            // Delete the events whose subject is the same as the npc
            var events = retrievedEvents.Values
                .Where(x => x.OriginalEvent.Subject != _npc.characterName)
                .ToList();

            // If there are no events to react to, return
            if (events.Count == 0)
            {
                return false;
            }
            
            // If there are at least one event, prompt Chatgpt to ask whether and how to react
            // Make a list of the descriptions of the retrieved events
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < events.Count; i++)
            {
                sb.AppendLine($"Event {i}:");
                sb.AppendLine(events[i].ToString());
                sb.AppendLine();
            }
            var eventDescriptions = sb.ToString();

            ScheduleEntry newSchedule;
            if (!_npc.IsActionFinished())
            {
                newSchedule = await OpenaiApi.OpenaiApi.GetInterruptingReactionSchedule(_npc, eventDescriptions);
            }
            else
            {
                newSchedule = await OpenaiApi.OpenaiApi.GetReactionSchedule(_npc, eventDescriptions);
            }


            // If the new schedule is null, it means the npc doesn't want to react
            if (newSchedule == null)
            {
                return false;
            }
            // If it is not null, update the daily schedule
            else
            {
                // _npc.DailySchedule.ChangeScheduleFrom(newSchedule, GameManager.Instance.currentHour, GameManager.Instance.currentMinute);
                
                // Add the new schedule to the schedule
                _npc.NextSchedule = newSchedule;
                return true;
            }
        }
        
        public async Task PlanNext()
        {
            // Generate the planning
            ScheduleEntry nextSchedule;
            var nodes = new List<ConceptNode>();
            nodes.AddRange(_npc.AssociativeMemory.Thoughts);
            if (nodes.Count > 0)
            {
                nodes.Sort((x, y) => x.LastAccessTime.IsNewerThan(y.LastAccessTime) ? -1 : 1);
                nodes = nodes.Take(30).ToList();
                var statements = Utils.GetSummaryStatements(nodes);
                nextSchedule = await OpenaiApi.OpenaiApi.GeneratePlanning(_npc, statements);
            }
            else
            {
                nextSchedule = await OpenaiApi.OpenaiApi.GeneratePlanning(_npc);
            }
            
            _npc.NextSchedule = nextSchedule;
        }
    }
}