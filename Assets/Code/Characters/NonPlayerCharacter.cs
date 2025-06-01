using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Code.Characters.Memories;
using Code.Characters.Perceive;
using Code.Characters.Plan;
using Code.Characters.Retrieve;
using Code.Managers;
using Code.Tiles;
using Code.OpenaiApi;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Serialization;

namespace Code.Characters
{
    [System.Serializable]
    public class NonPlayerCharacter : Character
    {
        public void OnDestroy()
        {
            Utils.SerializeIntoJson(this.AssociativeMemory, "associative_memory_" + characterName);
        }

        public int vision = 5;

        /// <summary>
        /// Whether this npc is cleaning up after the previous turn it took
        /// Cleaning up shouldn't block another npc from taking turns
        /// </summary>
        private bool _isCleaningUp = false;

        [HideInInspector] public int PerceiveBandwidth => wisdom / 2;

        /// <summary>
        /// Retention is the number of memories that the NPC will go back to when perceiving a new event.
        /// If the event is regarded to already be in the retention memory(first {retention} events),
        /// it will not be added to the memory again.
        /// </summary>
        [HideInInspector]
        public int Retention => wisdom;

        [HideInInspector] public SpatialMemory SpatialMemory;
        [HideInInspector] public AssociativeMemory AssociativeMemory;

        // NPC identity
        public int age;
        public string innateTraits;
        public string learnedTraits;
        public string dailyPlanRequirements;
        public string currently;
        public string lifestyle;
        public string relationships;

        // NPC reflection variables

        public int importanceTrigger = 20;
        public int currentImportanceTrigger = 0;
        // The number of things that npc should reflect on
        // It increases as the npc perceives more events(+1) and as npc is involved in a chat(+2)
        public int reflectionCount = 0;

        // NPC planning
        // Todo: do we actually need this plan?
        // /// <summary>
        // /// NPC's long term planning
        // /// dailyRequirements is a list of requirements that the NPC would like to fulfill today.
        // /// </summary>
        // public DailyRequirement DailyRequirements = new DailyRequirement();
        //
        // /// <summary>
        // /// As the time passes, the tasks will be decomposed into smaller tasks
        // /// Or if the schedules are not fulfilled, the NPC will revise the daily schedule
        // /// </summary>
        // public DailySchedule DailySchedule = new DailySchedule();
        //
        // /// <summary>
        // /// This is the original version of the dailySchedule
        // /// </summary>
        // public DailySchedule DailyScheduleHourly = new DailySchedule();

        public ScheduleEntry NextSchedule;

        // Current action(current schedule)

        /// <summary>
        /// The target address of the current action.
        /// It might be an object -- in this case, the npc wants to interact with the object
        /// It might be the NPC's own position -- in this case, the npc wants to do something at the current position
        /// Or it might be the position of another character -- in this case, the address will update every turn
        /// </summary>
        public Vector2Int? ActionAddress = null;

        /// <summary>
        /// If the ActionAddress should be updated to the position of another character every turn, here is the character
        /// </summary>
        public Character TargetCharacter = null;

        /// <summary>
        /// If the Action target is an object, here is the name of the object
        /// </summary>
        [CanBeNull] public string TargetObject = null;

        /// <summary>
        /// Whether the character should loot the target object
        /// It is only applicable when the target object is relic
        /// </summary>
        public bool ShouldLoot = false;

        /// <summary>
        /// The start time in turn/sequence of the current action
        /// </summary>
        public GameTime ActionStartTime;
        
        /// <summary>
        /// A cache for the current action mode
        /// </summary>
        public ActionMode CurrentActionMode;

        /// <summary>
        /// The meant duration of the action in minutes
        /// </summary>
        public int actionDuration;

        /// <summary>
        /// A string description of the action.
        /// </summary>
        public string actionDescription;

        /// <summary>
        /// The subject, predicate and object of the current action event.
        /// </summary>
        public NodeSummary ActionEventTriple;

        /// <summary>
        /// The recency decay factor for the associative memory.
        /// </summary>
        public float RecencyDecay = 0.99f;

        /// <summary>
        /// The summary of the retrieved nodes about the relationship with the current chatting target
        /// </summary>
        public string RelationshipNodeStatements = "";

        /// <summary>
        /// There is only one persistent event: the relic is not in the chest inside the manor house
        /// </summary>
        public bool HasPerceivedMissingEvent = false;
        
        private void Start()
        {
            base.Start();
            // Initialize the NPC's spatial memory
            SpatialMemory = new SpatialMemory();
            // Initialize the NPC's associative memory
            AssociativeMemory = new AssociativeMemory();
        }

        public override IEnumerator TakeTurn()
        {
            // If the npc is cleaning up, wait until it is finished
            yield return new WaitUntil(() => !_isCleaningUp);

            // Check if the NPC is dead
            if (currentHitPoints <= 0)
            {
                Debug.Log("NPC " + gameObject.name + ": " + characterName + " is dead and cannot take a turn.");
                yield break;
            }

            Debug.Log("Taking turn for NPC " + gameObject.name + ": " + characterName);
            
            // The NPC takes the turn here
            // 1. If the importance trigger is reached, then reflect
            if (currentImportanceTrigger >= importanceTrigger)
            {
                var reflectTask = Reflect(); 
                yield return new WaitUntil(() => reflectTask.IsCompleted);
                if (reflectTask.IsFaulted) throw reflectTask.Exception;
                // reset the importance trigger and the reflection count
                currentImportanceTrigger = 0;
                reflectionCount = 0;
            }

            // 2. perceive
            var perceiveTask = Perceive();
            yield return new WaitUntil(() => perceiveTask.IsCompleted);
            if (perceiveTask.IsFaulted) throw perceiveTask.Exception;
            var perceived = perceiveTask.Result;
            
            // 3. retrieve
            var retrieved = Retrieve(perceived);
            
            // 4. plan
            var planTask = Plan(retrieved);
            yield return new WaitUntil(() => planTask.IsCompleted);
            if (planTask.IsFaulted) throw planTask.Exception;

            // 5. Act
            if (ActionAddress != null)
            {
                // Find the path and move the NPC if needed
                var targetAddress = TileManager.Instance.FindPathToAdjacent(position, ActionAddress.Value, speed);
                
                if (targetAddress != position)
                {
                    SetPosition(targetAddress);
                }
                else
                {
                    RemainInPlace();
                }

                if (Utils.IsAdjacent(position, ActionAddress.Value))
                {
                    // If the action address is adjacent to the current position, then the action can be done
                    var actTask = Act();
                    yield return new WaitUntil(() => actTask.IsCompleted);
                    if (actTask.IsFaulted) throw actTask.Exception;
                }
            }


            // Utils.SerializeIntoJson(this.DailySchedule, "daily_schedule" + characterName);


            yield return new WaitForSeconds(0.1f);


            Debug.Log("NPC " + gameObject.name + ": " + characterName + " has finished their turn.");
        }

        /// <summary>
        /// reflect, revise identity, and revise the schedule
        /// </summary>
        /// <returns>whether the schedule is changed</returns>
        private async Task<bool> Reflect()
        {
            var reflectHandler = new ReflectHandler(this);
            await reflectHandler.Reflect();
            
            // Revise the identity to reflect the changes in the NPC's identity
            var oldCurrently = currently;
            var isCurrentlyChanged = await reflectHandler.ReviseIdentity();

            // // Revise the schedule accordingly if the currently is changed
            // if (isCurrentlyChanged)
            // {
            //     return await reflectHandler.ReviseSchedule(oldCurrently);
            // }

            return false;
        }

        private async Task<List<ConceptNode>> Perceive()
        {
            var perceptionHandler = new PerceiveHandler(this);
            return await perceptionHandler.Perceive();
        }

        private Dictionary<ConceptNode, RetrievedEvents> Retrieve(List<ConceptNode> perceived)
        {
            var retrieveHandler = new RetrieveHandler(this);
            return retrieveHandler.Retrieve(perceived);
        }

        private async Task Plan(Dictionary<ConceptNode, RetrievedEvents> retrieved)
        {
            var planHandler = new PlanHandler(this);
            if (GameManager.Instance.currentHour == 0 &&
                GameManager.Instance.currentMinute == 0)
            {
                // if it is a new day, then do the long term planning
                await planHandler.LongTermPlan();
            }

            bool shouldReact = false;
            // revise the schedule if the npc would like to react to the events
            if (retrieved.Count > 0)
            {
                shouldReact = await planHandler.React(retrieved);
            }

            // if the npc has finished the previous action and doesn't want to react, plan a new action
            if (!shouldReact && IsActionFinished())
            {
                await planHandler.PlanNext();
            }

            // if the action is finished or the schedule has been changed, determine the next action
            if (shouldReact || IsActionFinished())
            {
                await planHandler.DetermineAction();
            }
            else if (TargetCharacter != null)
            {
                // Update the action address to the target character's position
                ActionAddress = TargetCharacter.position;
            }

            return;
        }

        private async Task Act()
        {
            // Determine which action to take
            // and then take one of the actions
            var actHandler = new ActHandler(this);
            // if the action has already started, then use the original action mode
            var actionMode = ActionStartTime != null ? CurrentActionMode : await actHandler.DetermineActionMode();
            
            Debug.Log($"{characterName} is going to {actionMode.ToString()}: {actionDescription} at {ActionAddress}");

            switch (actionMode)
            {
                case ActionMode.Chat:
                    // Try to start chatting with the target character
                    await actHandler.StartChat();
                    break;
                case ActionMode.Interact:
                    // Interact with the target object
                    actHandler.Interact();
                    break;
                case ActionMode.Attack:
                    // Attack the target character
                    actHandler.Attack();
                    break;
                case ActionMode.Give:
                    // Give the target character something
                    await actHandler.GiveItem();
                    break;
                case ActionMode.Wait:
                    break;
                default:
                    Debug.Log("No action taken.");
                    break;
            }

            // set the action start time
            if (ActionStartTime == null)
            {
                // if the action is not started yet, set the start time and action mode
                ActionStartTime = GameManager.Instance.CurrentGameTime;
                CurrentActionMode = actionMode;
            }
        }

        /// <summary>
        /// Identity Stable Set is a summary of the NPC's identity
        /// </summary>
        /// <returns></returns>
        public string GetIdentityStableSet()
        {
            var tile = TileManager.Instance.GetTile(position);
            string iss = "";
            iss += $"Name: {characterName}\n";
            iss += $"Race: {race}\n";
            iss += $"Age: {age}\n";
            iss += $"Inventory: {Utils.InventoryToString(Inventory)}\n";
            iss += $"Stats: {GetStatsSummary()}\n";
            iss += $"Innate Traits: {innateTraits}\n";
            iss += $"Learned Traits: {learnedTraits}\n";
            iss += $"Currently: {currently}\n";
            iss += $"Lifestyle: {lifestyle}\n";
            iss += $"Relationships: {relationships}\n";
            iss += $"Daily Plan Requirements: {dailyPlanRequirements}\n";
            iss += $"Current Time: {GameManager.Instance.CurrentGameTime.ToRealTime()}\n";
            iss += $"Current Position: {position.x}, {position.y} in {tile.TileSector.Name}\n";
            return iss;
        }

        public override async Task<(string, bool)> ReceiveMessage(string message, Character chattingCharacter,
            List<ChatEntry> history, int sequence)
        {
            while (_isCleaningUp)
            {
                // Wait until the NPC has finished cleaning up
                await Task.Delay(100);
            }

            if (chattingTarget == null)
            {
                // Initialize the chatting if the target is null
                chattingTarget = chattingCharacter;
                ChatEntries = history;
                // Retrieve the relevant nodes about the target from memory
                var retrievedNodes = await RetrieveNodes(chattingCharacter.characterName, 25);
                // Get a summary of the relationship between them
                var nodeDescriptions = Utils.GetSummaryStatements(retrievedNodes);
                var relationshipSummary = await OpenaiApi.OpenaiApi.GetRelationshipSummary(this,
                    nodeDescriptions.ToString(), chattingTarget.characterName);
                var chatRetrievedNodes = await RetrieveNodes(relationshipSummary, 15);
                var chatRetrievedNodesDescriptions = Utils.GetSummaryStatements(chatRetrievedNodes);
                RelationshipNodeStatements = chatRetrievedNodesDescriptions;
            }


            var historyFocalPoint = Utils.ChatEntriesToString(history);
            // Retrieve the relevant nodes about the current chatting history from memory
            var historyNodes = await RetrieveNodes(historyFocalPoint, 15);
            var historyNodesDescription = Utils.GetSummaryStatements(historyNodes);


            // Generate a response 
            var response = await OpenaiApi.OpenaiApi.GenerateChat(
                this, chattingTarget,
                RelationshipNodeStatements + '\n' + historyNodesDescription, sequence,
                Utils.ChatEntriesToString(history));
            // Add the response to the chat history
            history.Add(new ChatEntry
            {
                Speaker = characterName,
                Content = response.Item1
            });
            return response;
        }

        /// <summary>
        /// This function is not a task so it will not block other threads
        /// But this character's TakeTurn and ReceiveMessage will be blocked until the cleanup is finished
        /// </summary>
        /// <param name="reason"></param>
        public override async void EndChat(string reason = "")
        {
            // set the isCleaningUp flag to true: now it starts to clean up
            _isCleaningUp = true;

            // Generate a summary of the chat
            var chatSummary = await OpenaiApi.OpenaiApi.SummarizeChat(this, reason);

            // Add the chat to the associative memory,
            List<string> chatNodeIds = new List<string>();
            var currentEvent = new NodeSummary
            {
                Subject = characterName,
                Predicate = "is chatting with",
                Object = chattingTarget.characterName
            };
            // make the keywords of the event
            var keywords = new HashSet<string>();
            keywords.Add(currentEvent.Subject);
            keywords.Add(currentEvent.Object);
            float[] chatEmbedding;
            if (AssociativeMemory.Embeddings.ContainsKey(chatSummary))
            {
                chatEmbedding = AssociativeMemory.Embeddings[chatSummary];
            }
            else
            {
                chatEmbedding = await OpenaiApi.OpenaiApi.GetEmbedding(chatSummary);
            }

            // get the poignancy of the chat
            int chatPoignancyScore =
                await OpenaiApi.OpenaiApi.GetChatPoignancy(this);
            // add the chat node to the associative memory
            var chatNode = AssociativeMemory.AddChat(GameManager.Instance.CurrentGameTime, null,
                currentEvent.Subject, currentEvent.Predicate, currentEvent.Object,
                chatSummary,
                new KeyValuePair<string, float[]>(chatSummary, chatEmbedding),
                chatPoignancyScore,
                keywords.ToList(), new ChatFilling
                {
                    Dialogue = new List<ChatEntry>(ChatEntries),
                    EndReason = reason,
                });
            // add on to the importance trigger
            currentImportanceTrigger += chatPoignancyScore;
            chatNodeIds.Add(chatNode.NodeId);
            reflectionCount += 2;


            // Add a planning thought
            var planningThought = await OpenaiApi.OpenaiApi.GetPlanningThought(this, reason);
            if (planningThought != "None")
            {
                var planningEventTriple = await OpenaiApi.OpenaiApi.GetActionTriple(planningThought);
                var planningKeywords = new HashSet<string>
                {
                    planningEventTriple.subject,
                    planningEventTriple.predicate,
                    planningEventTriple.@object
                };
                var planningPoignancyScore =
                    await OpenaiApi.OpenaiApi.GetThoughtPoignancy(this, planningThought);
                var planningEmbedding = await OpenaiApi.OpenaiApi.GetEmbedding(planningThought);
                AssociativeMemory.AddThought(GameManager.Instance.CurrentGameTime, null,
                    planningEventTriple.subject, planningEventTriple.predicate, planningEventTriple.@object,
                    planningThought,
                    new KeyValuePair<string, float[]>(planningThought, planningEmbedding),
                    planningPoignancyScore,
                    planningKeywords.ToList(), new EventThoughtFilling()
                    {
                        ReferencedNodeIds = chatNodeIds
                    });
            }

            // Add a memo thought
            var memoThought = await OpenaiApi.OpenaiApi.GetMemoThought(this, reason);
            if (memoThought != "None")
            {
                var memoEventTriple = await OpenaiApi.OpenaiApi.GetActionTriple(memoThought);
                var memoKeywords = new HashSet<string>
                {
                    memoEventTriple.subject,
                    memoEventTriple.predicate,
                    memoEventTriple.@object
                };
                var memoPoignancyScore =
                    await OpenaiApi.OpenaiApi.GetThoughtPoignancy(this, memoThought);
                var memoEmbedding = await OpenaiApi.OpenaiApi.GetEmbedding(memoThought);
                AssociativeMemory.AddThought(GameManager.Instance.CurrentGameTime, null,
                    memoEventTriple.subject, memoEventTriple.predicate, memoEventTriple.@object,
                    memoThought,
                    new KeyValuePair<string, float[]>(memoThought, memoEmbedding),
                    memoPoignancyScore,
                    memoKeywords.ToList(), new EventThoughtFilling()
                    {
                        ReferencedNodeIds = chatNodeIds
                    });
            }

            // Clean up the chat-related states
            chattingTarget = null;
            ChatEntries = null;
            RelationshipNodeStatements = "";
            _isCleaningUp = false;
        }

        public bool IsActionFinished()
        {
            if (ActionAddress == null)
            {
                return true;
            }

            // if the action is not even started, return false
            if (ActionStartTime == null)
            {
                return false;
            }

            var endTime = new GameTime
            {
                eventTurn = ActionStartTime.eventTurn +
                            Mathf.FloorToInt(actionDuration / GameManager.Instance.turnTime),
                eventSequence = ActionStartTime.eventSequence
            };

            if (endTime.IsNewerThan(GameManager.Instance.currentTurn, GameManager.Instance.currentSequence))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Get the address of the target object from the spatial memory.
        /// If there are multiple objects with the same name, return one with the shortest distance
        /// </summary>
        /// <param name="targetObject"></param>
        /// <returns></returns>
        public Vector2Int GetAddressForSectorObject(string targetSector, string targetObject)
        {
            // if the target object is empty, return the current position
            if (targetObject.ToLower() == "none")
            {
                return position;
            }

            var address = SpatialMemory.FindClosestObjectByPath(targetSector, targetObject, position);
            if (address.HasValue)
            {
                return address.Value;
            }

            // if the target object is not found, return the current position
            Debug.Log($"Target object {targetObject} not found in the spatial memory.");
            return position;
        }

        /// <summary>
        /// Add a new action for the NPC to perform
        /// Also clear the current action
        /// </summary>
        /// <param name="address"></param>
        /// <param name="duration"></param>
        /// <param name="description"></param>
        /// <param name="actionEventTriple"></param>
        /// <param name="targetCharacter"></param>
        /// <param name="targetObject"></param>
        public void AddNewAction(Vector2Int address, int duration, string description, NodeSummary actionEventTriple,
            Character targetCharacter, string targetObject, bool shouldLoot)
        {
            Debug.Log($"{characterName} is planning on {description} at {address} for {duration} minutes");
            
            ActionAddress = address;
            actionDuration = duration;
            actionDescription = description;
            ActionEventTriple = actionEventTriple;
            TargetCharacter = targetCharacter;
            TargetObject = targetObject == "None" ? null : targetObject;
            ShouldLoot = shouldLoot;

            // Clear the current action
            ActionStartTime = null;
            CurrentActionMode = ActionMode.Wait;
        }

        /// <summary>
        /// Retrieve at most {cnt} thought and event nodes from the associative memory,
        /// according to the facal point provided
        /// </summary>
        /// <param name="focalPoint">A string facal point</param>
        /// <param name="cnt"></param>
        /// <returns></returns>
        public async Task<List<ConceptNode>> RetrieveNodes(string focalPoint, int cnt = 30)
        {
            // Get all thought and event nodes
            var nodes = AssociativeMemory.GetThoughtsAndEvents();
            // Sort the nodes by the last access time (newest first)
            nodes.Sort((x, y) => x.LastAccessTime.IsNewerThan(y.LastAccessTime) ? -1 : 1);
            // Rate the nodes by three parameters
            var recency = new List<float>();
            var importance = new List<float>();
            var relevance = new List<float>();
            var focalEmbedding = await OpenaiApi.OpenaiApi.GetEmbedding(focalPoint);
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                var similarity =
                    Utils.CosineSimilarity(focalEmbedding, AssociativeMemory.Embeddings[node.EmbeddingKey]);

                recency.Add((float)Math.Pow(RecencyDecay, i));
                importance.Add(node.Poignancy);
                relevance.Add(similarity);
            }

            // Normalize the three parameters
            var recencyNorm = Utils.Normalize(recency);
            var importanceNorm = Utils.Normalize(importance);
            var relevanceNorm = Utils.Normalize(relevance);

            // Calculate the final score
            var scores = new List<float>();
            var weightedRecency = 0.5f;
            var weightedRelevance = 3f;
            var weightedImportance = 2f;
            for (var i = 0; i < nodes.Count; i++)
            {
                var score = recencyNorm[i] * weightedRecency + relevanceNorm[i] * weightedRelevance +
                            importanceNorm[i] * weightedImportance;
                scores.Add(score);
            }

            // Sort the first {cnt} nodes by the final score
            var resNodes = nodes
                .Select((node, index) => new { Node = node, Score = scores[index] })
                .OrderByDescending(x => x.Score)
                .Take(cnt)
                .Select(x => x.Node)
                .ToList();
            
            // update the last access time of the nodes
            foreach (var node in resNodes)
            {
                node.LastAccessTime = GameManager.Instance.CurrentGameTime;
            }
            
            return resNodes;
        }

        public override async Task ReceiveItem(Item item, string message, Character sender)
        {
            await base.ReceiveItem(item, message, sender);
            
            // Generate memo thought
            var memoThought = "I have received " + item.Name + "*" + item.Quantity + " from " +
                              sender.characterName + " and message: " + message;
            var memoEventTriple = await OpenaiApi.OpenaiApi.GetActionTriple(memoThought);
            var memoKeywords = new HashSet<string>
            {
                memoEventTriple.subject,
                memoEventTriple.predicate,
                memoEventTriple.@object
            };
            var memoPoignancyScore =
                await OpenaiApi.OpenaiApi.GetThoughtPoignancy(this, memoThought);
            var memoEmbedding = await OpenaiApi.OpenaiApi.GetEmbedding(memoThought);
            AssociativeMemory.AddThought(GameManager.Instance.CurrentGameTime, null,
                memoEventTriple.subject, memoEventTriple.predicate, memoEventTriple.@object,
                memoThought,
                new KeyValuePair<string, float[]>(memoThought, memoEmbedding),
                memoPoignancyScore,
                memoKeywords.ToList(), new EventThoughtFilling()
                {
                });
        }
    }

    [System.Serializable]
    public class DailyRequirement
    {
        public List<RequirementEntry> Plan { get; set; }

        public DailySchedule ToDailySchedule()
        {
            var schedule = new List<ScheduleEntry>();
            // add the sleeping activity
            var sleepingActivity = "sleeping";
            var sleepingDuration = Plan[0].Hour * 60;
            schedule.Add(new ScheduleEntry
            {
                DurationInMinutes = sleepingDuration,
                Activity = sleepingActivity
            });
            // add the other activities
            for (int i = 0; i < Plan.Count; i++)
            {
                int nextHour = i == Plan.Count - 1 ? 24 : Plan[i + 1].Hour;
                var activity = Plan[i].Activity;
                var duration = (nextHour - Plan[i].Hour) * 60;
                schedule.Add(new ScheduleEntry
                {
                    DurationInMinutes = duration,
                    Activity = activity
                });
            }

            return new DailySchedule
            {
                Schedule = schedule
            };
        }

        public override string ToString()
        {
            if (Plan == null || Plan.Count == 0)
            {
                return "No tasks planned for today.";
            }

            var sb = new StringBuilder();
            foreach (var entry in Plan.OrderBy(p => p.Hour))
            {
                sb.AppendLine($"- At hour {entry.Hour}: {entry.Activity}");
            }

            return sb.ToString().TrimEnd();
        }
    }

    [System.Serializable]
    public class RequirementEntry
    {
        public int Hour { get; set; }
        public string Activity { get; set; }
    }

    [System.Serializable]
    public class DailySchedule
    {
        public List<ScheduleEntry> Schedule { get; set; }

        public DailySchedule(DailySchedule schedule)
        {
            Schedule = new List<ScheduleEntry>();
            foreach (var entry in schedule.Schedule)
            {
                Schedule.Add(new ScheduleEntry
                {
                    DurationInMinutes = entry.DurationInMinutes,
                    Activity = entry.Activity
                });
            }
        }

        public DailySchedule(List<ScheduleEntry> schedule)
        {
            Schedule = schedule;
        }

        public DailySchedule()
        {
            Schedule = new List<ScheduleEntry>();
        }

        /// <summary>
        /// Get the current index of the schedule based on the current time (hour and minute).
        /// The retuened index is the index of the schedule entry that is currently active
        /// If the previous schedule is finished, it will return the next one
        /// </summary>
        /// <param name="hour"></param>
        /// <param name="minute"></param>
        /// <returns></returns>
        public int GetCurrentIndex(int hour, int minute)
        {
            int minutesPassed = hour * 60 + minute;
            for (int i = 0; i < Schedule.Count; i++)
            {
                var entry = Schedule[i];
                if (minutesPassed < entry.DurationInMinutes)
                {
                    return i;
                }

                minutesPassed -= entry.DurationInMinutes;
            }

            return Schedule.Count - 1; // Return the last index if no match is found
        }

        public override string ToString()
        {
            if (Schedule == null || Schedule.Count == 0)
            {
                return "No schedule for today.";
            }

            var sb = new StringBuilder();
            var passedMinutes = 0;
            foreach (var entry in Schedule)
            {
                var startTime = passedMinutes;
                var endTime = passedMinutes + entry.DurationInMinutes;
                sb.AppendLine(
                    $"- From {TimeSpan.FromMinutes(startTime):hh\\:mm} to {TimeSpan.FromMinutes(endTime):hh\\:mm}: {entry.Activity}");
                passedMinutes += entry.DurationInMinutes;
            }

            return sb.ToString().TrimEnd();
        }

        public string GetNextSchedulesString(int instanceCurrentHour, int instanceCurrentMinute)
        {
            var currentMinutes = instanceCurrentHour * 60 + instanceCurrentMinute;
            var passedMinutes = 0;
            var sb = new StringBuilder();
            foreach (var entry in Schedule)
            {
                var startTime = passedMinutes;
                var endTime = passedMinutes + entry.DurationInMinutes;
                passedMinutes = endTime;
                // Skip the schedules that are already finished before or equal to the current time
                if (passedMinutes < currentMinutes)
                {
                    continue;
                }

                // If the schedule has already started, change the start time to the current time
                if (startTime < currentMinutes)
                {
                    startTime = currentMinutes;
                }

                sb.AppendLine(
                    $"- From {TimeSpan.FromMinutes(startTime):hh\\:mm} to {TimeSpan.FromMinutes(endTime):hh\\:mm}: {entry.Activity}");
            }

            if (sb.Length > 0)
            {
                return sb.ToString().TrimEnd();
            }
            else
            {
                return "No more schedules for today.";
            }
        }

        public void ChangeScheduleFrom(List<ScheduleEntry> newSchedule, int currentHour, int currentMinute)
        {
            // Get the current index of the schedule
            var currentScheduleIndex = GetCurrentIndex(currentHour, currentMinute);

            // If the current item has started but unfinished, change the duration of this item
            var currentMinutes = currentHour * 60 + currentMinute;
            var passedMinutes = 0;
            for (int i = 0; i < currentScheduleIndex; i++)
            {
                passedMinutes += Schedule[i].DurationInMinutes;
            }

            if (passedMinutes < currentMinutes &&
                passedMinutes + Schedule[currentScheduleIndex].DurationInMinutes > currentMinutes)
            {
                var currentSchedule = Schedule[currentScheduleIndex];
                var newDuration = currentMinutes - passedMinutes;
                Schedule[currentScheduleIndex] = new ScheduleEntry
                {
                    DurationInMinutes = newDuration,
                    Activity = currentSchedule.Activity
                };
            }
            else
                // if the current item has not started yet, mark it to be removed
            {
                currentScheduleIndex--;
            }

            // Remove the schedule after the current index and append the new schedule
            Schedule.RemoveRange(currentScheduleIndex + 1, Schedule.Count - currentScheduleIndex - 1);
            Schedule.AddRange(newSchedule);
        }
    }

    [System.Serializable]
    public class ScheduleEntry
    {
        public int DurationInMinutes { get; set; }
        public string Activity { get; set; }
    }

    public enum ActionMode
    {
        Chat,
        Interact,
        Attack,
        Give,
        Wait
    }
}