using System;
using System.ClientModel;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Code.Characters;
using Code.Characters.Memories;
using Code.Characters.Retrieve;
using Code.Managers;
using OpenAI.Chat;
using OpenAI.Embeddings;
using UnityEngine;

namespace Code.OpenaiApi
{
    public static class OpenaiApi
    {
        private static readonly EmbeddingClient EmbeddingClient = new(
            model: "text-embedding-3-small",
            apiKey: "your_api_key"
        );

        private static readonly ChatClient ChatClient = new(
            model: "gpt-4o-mini",
            apiKey:
            "your_api_key"
        );

        private static readonly string CognitiveSystemMessage =
            "You are simulating the mind of a fictional character in a Dungeons & Dragons-style fantasy world.\n\n" +
            "You will be provided with a character description, " +
            "a situation they are experiencing, and a specific task to perform.\n\n" +
            "You must act consistently with the character’s identity, personality, beliefs, and goals.\n\n" +
            "Your responses should:\n" +
            "- Reflect natural, in-character reasoning or emotional expression\n" +
            "- Be grounded in the character's memories, knowledge, and current situation\n" +
            "- Avoid generic, out-of-character, or AI-like language\n" +
            "- Avoid adding meta-commentary or formatting unless asked\n\n" +
            "When the prompt asks for a cognitive process (e.g., perception, memory recall, or emotional reaction), " +
            "simulate only internal reasoning — do not roleplay or output actions unless explicitly requested.\n\n" +
            "When the prompt asks for a spoken message or decision, you must roleplay as the character " +
            "and output their next utterance or decision directly.\n";

        public static async Task<float[]> GetEmbedding(string text)
        {
            var embedding = await EmbeddingClient.GenerateEmbeddingAsync(text);
            return embedding.Value.ToFloats().ToArray();
        }

        public static async Task<int> GetEventPoignancy(NonPlayerCharacter character, string eventDescription)
        {
            var prompt = new
            {
                agent = character.characterName,
                iss = character.GetIdentityStableSet(),
                @event = eventDescription,
            };
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(CognitiveSystemMessage),
                new UserChatMessage(
                    TemplateManager.Instance.Render("poignancy_event", prompt)
                )
            };
            var response = await CallWithRetryAsync(() => ChatClient.CompleteChatAsync(messages), Int32.Parse);
            return response;
        }

        public static async Task<int> GetChatPoignancy(NonPlayerCharacter character)
        {
            var prompt = new
            {
                agent = character.characterName,
                iss = character.GetIdentityStableSet(),
                conversation = Utils.ChatEntriesToString(character.ChatEntries),
            };
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(CognitiveSystemMessage),
                new UserChatMessage(
                    TemplateManager.Instance.Render("poignancy_chat", prompt)
                )
            };
            var response = await CallWithRetryAsync(() => ChatClient.CompleteChatAsync(messages), Int32.Parse);
            return response;
        }

        public static async Task<int> GetThoughtPoignancy(NonPlayerCharacter character, string thought)
        {
            var prompt = new
            {
                agent = character.characterName,
                iss = character.GetIdentityStableSet(),
                thought = thought
            };
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(CognitiveSystemMessage),
                new UserChatMessage(
                    TemplateManager.Instance.Render("poignancy_thought", prompt)
                )
            };
            var response = await CallWithRetryAsync(() => ChatClient.CompleteChatAsync(messages), Int32.Parse);
            return response;
        }

        public static async Task<int> GetWakeupTime(NonPlayerCharacter character)
        {
            var prompt = new
            {
                agent = character.characterName,
                iss = character.GetIdentityStableSet(),
            };
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(CognitiveSystemMessage),
                new UserChatMessage(
                    TemplateManager.Instance.Render("wakeup_time", prompt)
                )
            };
            var response = await CallWithRetryAsync(() => ChatClient.CompleteChatAsync(messages), Int32.Parse);
            return response;
        }

        public static async Task<DailyRequirement> GetFirstDailyPlan(NonPlayerCharacter character, int wakeupTime)
        {
            var prompt = new
            {
                agent = character.characterName,
                iss = character.GetIdentityStableSet(),
                wakeup_time = wakeupTime,
            };
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(CognitiveSystemMessage),
                new UserChatMessage(
                    TemplateManager.Instance.Render("daily_planning", prompt)
                )
            };
            var response = await CallWithRetryAsync(() => ChatClient.CompleteChatAsync(messages), text =>
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                return JsonSerializer.Deserialize<DailyRequirement>(text, options);
            });
            return response;
        }

        // public static async Task<DailySchedule> GetHourlySchedule(NonPlayerCharacter character, int wakeupTime)
        // {
        //     var prompt = new
        //     {
        //         agent = character.characterName,
        //         iss = character.GetIdentityStableSet(),
        //         daily_plan = character.DailyRequirements.ToString(),
        //         wakeup_hour = wakeupTime,
        //     };
        //     var messages = new List<ChatMessage>
        //     {
        //         new SystemChatMessage(CognitiveSystemMessage),
        //         new UserChatMessage(
        //             TemplateManager.Instance.Render("daily_schedule", prompt)
        //         )
        //     };
        //     var response = await CallWithRetryAsync(() => ChatClient.CompleteChatAsync(messages), text =>
        //     {
        //         var options = new JsonSerializerOptions
        //         {
        //             PropertyNameCaseInsensitive = true
        //         };
        //         var returnerdPlan =
        //             JsonSerializer.Deserialize<DailyRequirement>(text, options);
        //         return returnerdPlan.ToDailySchedule();
        //     });
        //     return response;
        // }

        // public static async Task<List<ScheduleEntry>> GetDecomposedSchedule(NonPlayerCharacter character,
        //     ScheduleEntry schedule)
        // {
        //     var prompt = new
        //     {
        //         agent = character.characterName,
        //         iss = character.GetIdentityStableSet(),
        //         next_schedules = character.DailySchedule.GetNextSchedulesString(GameManager.Instance.currentHour,
        //             GameManager.Instance.currentMinute),
        //         activity = schedule.Activity,
        //         duration = schedule.DurationInMinutes,
        //     };
        //     var messages = new List<ChatMessage>
        //     {
        //         new SystemChatMessage(CognitiveSystemMessage),
        //         new UserChatMessage(
        //             TemplateManager.Instance.Render("task_decomp", prompt)
        //         )
        //     };
        //     var response = await CallWithRetryAsync(() => ChatClient.CompleteChatAsync(messages), text =>
        //     {
        //         var options = new JsonSerializerOptions
        //         {
        //             PropertyNameCaseInsensitive = true
        //         };
        //         return JsonSerializer.Deserialize<List<ScheduleEntry>>(text, options);
        //     });
        //     return response;
        // }

        public static async Task<string> GetTargetCharacter(NonPlayerCharacter character, ScheduleEntry schedule)
        {
            var prompt = new
            {
                agent = character.characterName,
                iss = character.GetIdentityStableSet(),
                action = schedule.Activity,
            };
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(CognitiveSystemMessage),
                new UserChatMessage(
                    TemplateManager.Instance.Render("target_character", prompt)
                )
            };
            var response = await ChatClient.CompleteChatAsync(messages);
            if (response.Value.Content.Count > 0)
            {
                return response.Value.Content[0].Text;
            }
            else
            {
                throw new Exception("Failed to parse the response: " + response.Value.Content);
            }
        }

        public static async Task<string> GetTargetSector(NonPlayerCharacter character, ScheduleEntry schedule)
        {
            var prompt = new
            {
                agent = character.characterName,
                iss = character.GetIdentityStableSet(),
                action = schedule.Activity,
                sectors = character.SpatialMemory.GetAllKnownSectors()
            };
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(CognitiveSystemMessage),
                new UserChatMessage(
                    TemplateManager.Instance.Render("target_sector", prompt)
                )
            };
            var response = await ChatClient.CompleteChatAsync(messages);
            if (response.Value.Content.Count > 0)
            {
                return response.Value.Content[0].Text;
            }
            else
            {
                throw new Exception("Failed to parse the response: " + response.Value.Content);
            }
        }

        public static async Task<(string, bool)> GetTargetObject(NonPlayerCharacter character, ScheduleEntry schedule,
            string targetSector)
        {
            var prompt = new
            {
                agent = character.characterName,
                iss = character.GetIdentityStableSet(),
                action = schedule.Activity,
                sector = targetSector,
                objects = character.SpatialMemory.GetAllKnownObjectsInSector(targetSector)
            };
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(CognitiveSystemMessage),
                new UserChatMessage(
                    TemplateManager.Instance.Render("target_object", prompt)
                )
            };
            var response = await CallWithRetryAsync(() => ChatClient.CompleteChatAsync(messages), text =>
            {
                var texts = text.Split(",");
                if (texts.Length != 2)
                {
                    throw new FormatException(
                        "Input must contain exactly 2 comma-separated elements: objectName, isLoot.");
                }
                var objectName = texts[0].Trim();
                if (bool.TryParse(texts[1].Trim(), out var isLoot))
                {
                    return (objectName, isLoot);
                }
                else
                {
                    throw new FormatException(
                        "The second element must be a boolean value indicating whether the object should be looted.");
                }
            });
            return response;
        }

        public static async Task<(string subject, string predicate, string @object)> GetActionTriple(
            NonPlayerCharacter character, ScheduleEntry schedule)
        {
            var prompt = new
            {
                agent = character.characterName,
                action = schedule.Activity,
            };
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(CognitiveSystemMessage),
                new UserChatMessage(
                    TemplateManager.Instance.Render("action_triple", prompt)
                )
            };
            var response = await CallWithRetryAsync(() => ChatClient.CompleteChatAsync(messages), text =>
            {
                // if there are multiple lines, take the first one
                var lines = text.Split('\n');
                var parts = lines[0].Split("||");
                if (parts.Length < 3)
                {
                    throw new FormatException(
                        "Input must contain at least 3 comma-separated elements: subject, predicate, object.");
                }

                return (
                    parts[0].Trim(),
                    parts[1].Trim(),
                    parts[2].Trim() == "none" ? "" : parts[2].Trim()
                );
            });
            return response;
        }

        public static async Task<(string subject, string predicate, string @object)> GetActionTriple(string sentence)
        {
            var prompt = new
            {
                sentence = sentence
            };
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(CognitiveSystemMessage),
                new UserChatMessage(
                    TemplateManager.Instance.Render("sentence_triple", prompt)
                )
            };
            var response = await CallWithRetryAsync(() => ChatClient.CompleteChatAsync(messages), text =>
            {
                // if there are multiple lines, take the first one
                var lines = text.Split('\n');
                var parts = lines[0].Split("||");
                if (parts.Length < 3)
                {
                    throw new FormatException(
                        "Input must contain at least 3 comma-separated elements: subject, predicate, object.");
                }

                return (
                    parts[0].Trim(),
                    parts[1].Trim(),
                    parts[2].Trim() == "none" ? "" : parts[2].Trim()
                );
            });
            return response;
        }

        // public static async Task<List<ScheduleEntry>> GetScheduleAfterReaction(NonPlayerCharacter character,
        //     string events)
        // {
        //     var prompt = new
        //     {
        //         agent = character.characterName,
        //         iss = character.GetIdentityStableSet(),
        //         events = events,
        //         next_schedules = character.DailySchedule.GetNextSchedulesString(GameManager.Instance.currentHour,
        //             GameManager.Instance.currentMinute),
        //     };
        //     var messages = new List<ChatMessage>
        //     {
        //         new SystemChatMessage(CognitiveSystemMessage),
        //         new UserChatMessage(
        //             TemplateManager.Instance.Render("schedule_after_reaction_v2", prompt)
        //         )
        //     };
        //     var response = await CallWithRetryAsync(() => ChatClient.CompleteChatAsync(messages), text =>
        //     {
        //         try
        //         {
        //             var options = new JsonSerializerOptions
        //             {
        //                 PropertyNameCaseInsensitive = true
        //             };
        //             return JsonSerializer.Deserialize<List<ScheduleEntry>>(text, options);
        //         }
        //         catch (JsonException)
        //         {
        //             // If it is not such a json, examine whether it is "none"
        //             if (text == "none")
        //             {
        //                 return null;
        //             }
        //
        //             Debug.LogError("Failed to deserialize the decomposed schedule: " + text);
        //             throw new Exception("Failed to parse the response: " + text);
        //         }
        //     });
        //     return response;
        // }
        
        public static async Task<ScheduleEntry> GetInterruptingReactionSchedule(NonPlayerCharacter character,
            string events)
        {
            var prompt = new
            {
                agent = character.characterName,
                iss = character.GetIdentityStableSet(),
                events = events,
                current_action = character.actionDescription,
            };
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(CognitiveSystemMessage),
                new UserChatMessage(
                    TemplateManager.Instance.Render("interrupting_reaction_v2", prompt)
                )
            };
            var response = await CallWithRetryAsync(() => ChatClient.CompleteChatAsync(messages), text =>
            {
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    return JsonSerializer.Deserialize<ScheduleEntry>(text, options);
                }
                catch (JsonException)
                {
                    // If it is not such a json, examine whether it is "none"
                    if (text == "none")
                    {
                        return null;
                    }
        
                    Debug.LogError("Failed to deserialize the schedule: " + text);
                    throw new Exception("Failed to parse the response: " + text);
                }
            });
            return response;
        }
        
        public static async Task<ScheduleEntry> GetReactionSchedule(NonPlayerCharacter character,
            string events)
        {
            var prompt = new
            {
                agent = character.characterName,
                iss = character.GetIdentityStableSet(),
                events = events,
            };
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(CognitiveSystemMessage),
                new UserChatMessage(
                    TemplateManager.Instance.Render("reaction_v2", prompt)
                )
            };
            var response = await CallWithRetryAsync(() => ChatClient.CompleteChatAsync(messages), text =>
            {
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };
                    return JsonSerializer.Deserialize<ScheduleEntry>(text, options);
                }
                catch (JsonException)
                {
                    // If it is not such a json, examine whether it is "none"
                    if (text == "none")
                    {
                        return null;
                    }
        
                    Debug.LogError("Failed to deserialize the schedule: " + text);
                    throw new Exception("Failed to parse the response: " + text);
                }
            });
            return response;
        }

        public static async Task<ActionMode> GetActionMode(NonPlayerCharacter character)
        {
            if (character.TargetObject != null)
            {
                var prompt = new
                {
                    agent = character.characterName,
                    iss = character.GetIdentityStableSet(),
                    action = character.actionDescription,
                    target = character.TargetObject,
                };
                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(CognitiveSystemMessage),
                    new UserChatMessage(
                        TemplateManager.Instance.Render("action_mode_object", prompt)
                    )
                };
                var response = await CallWithRetryAsync(() => ChatClient.CompleteChatAsync(messages), text =>
                {
                    var mode = text;
                    if (mode == "Interact")
                    {
                        return ActionMode.Interact;
                    }
                    else if (mode == "Wait")
                    {
                        return ActionMode.Wait;
                    }

                    throw new FormatException("Failed to parse the action mode: " + text);
                });
                return response;
            }
            else if (character.TargetCharacter != null)
            {
                var retrieve = await character.RetrieveNodes(character.TargetCharacter.characterName, 30);
                var statements = Utils.GetSummaryStatements(retrieve);
                var prompt = new
                {
                    agent = character.characterName,
                    iss = character.GetIdentityStableSet(),
                    action = character.actionDescription,
                    target = character.TargetCharacter.characterName,
                    statements
                };
                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(CognitiveSystemMessage),
                    new UserChatMessage(
                        TemplateManager.Instance.Render("action_mode_character", prompt)
                    )
                };
                var response = await CallWithRetryAsync(() => ChatClient.CompleteChatAsync(messages), text =>
                {
                    var mode = text;
                    if (mode == "Attack")
                    {
                        return ActionMode.Attack;
                    }
                    else if (mode == "Chat")
                    {
                        return ActionMode.Chat;
                    }
                    else if (mode == "Give")
                    {
                        return ActionMode.Give;
                    }
                    else if (mode == "Wait")
                    {
                        return ActionMode.Wait;
                    }

                    throw new FormatException("Failed to parse the action mode: " + text);
                });
                return response;
            }
            // If there is no target, the action is to wait by default
            else return ActionMode.Wait;
        }

        public static async Task<string> GetRelationshipSummary(NonPlayerCharacter character, string statements,
            string target)
        {
            var prompt = new
            {
                agent = character.characterName,
                iss = character.GetIdentityStableSet(),
                statements = statements,
            };
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(CognitiveSystemMessage),
                new UserChatMessage(
                    TemplateManager.Instance.Render("relationship_summary", prompt)
                )
            };
            var response = await ChatClient.CompleteChatAsync(messages);
            if (response.Value.Content.Count > 0)
            {
                return response.Value.Content[0].Text;
            }
            else
            {
                throw new Exception("Failed to parse the response: " + response.Value.Content);
            }
        }

        /// <summary>
        /// Generate the next chat for the NPC
        /// </summary>
        /// <param name="character">the speaker</param>
        /// <param name="targetCharacter">the other character in the chat</param>
        /// <param name="chatRetrievedNodesDescriptions">the things in the speaker's head</param>
        /// <param name="sequence">the sequence of the current chat(starting from 0)</param>
        /// <param name="history">the history of the current chat</param>
        /// <returns>(string: the next chat; bool: whether npc wants to end the chat)</returns>
        public static async Task<(string, bool)> GenerateChat(NonPlayerCharacter character, Character targetCharacter,
            string chatRetrievedNodesDescriptions, int sequence, string history)
        {
            var prompt = new
            {
                agent = character.characterName,
                iss = character.GetIdentityStableSet(),
                target = targetCharacter.characterName,
                statements = chatRetrievedNodesDescriptions,
                chat_history = history,
                sequence = Math.Ceiling(sequence / 2f)
            };
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(CognitiveSystemMessage),
                new UserChatMessage(
                    TemplateManager.Instance.Render(sequence == 0 ? "generate_chat_start" : "generate_chat", prompt)
                )
            };
            var response = await CallWithRetryAsync(() => ChatClient.CompleteChatAsync(messages), text =>
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var res = JsonSerializer.Deserialize<AgentResponse>(text, options);
                return (res.Message, res.End);
            });
            return response;
        }

        [System.Serializable]
        private class AgentResponse
        {
            public string Message { get; set; } = "";
            public bool End { get; set; }
        }

        public static async Task<string> SummarizeChat(NonPlayerCharacter character, string endReason)
        {
            var prompt = new
            {
                agent = character.characterName,
                iss = character.GetIdentityStableSet(),
                chat_history = Utils.ChatEntriesToString(character.ChatEntries) + '\n' + endReason,
                target = character.chattingTarget.characterName,
            };
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(CognitiveSystemMessage),
                new UserChatMessage(
                    TemplateManager.Instance.Render("chat_summary", prompt)
                )
            };
            var response = await ChatClient.CompleteChatAsync(messages);
            if (response.Value.Content.Count > 0)
            {
                return response.Value.Content[0].Text;
            }
            else
            {
                throw new Exception("Failed to parse the response: " + response.Value.Content);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="character"></param>
        /// <param name="endReason"></param>
        /// <returns>"None" - nothing to remember; else - the sentence</returns>
        /// <exception cref="Exception"></exception>
        public static async Task<string> GetPlanningThought(NonPlayerCharacter character, string endReason)
        {
            var prompt = new
            {
                agent = character.characterName,
                iss = character.GetIdentityStableSet(),
                chat_history = Utils.ChatEntriesToString(character.ChatEntries) + '\n' + endReason,
                target = character.chattingTarget.characterName,
            };
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(CognitiveSystemMessage),
                new UserChatMessage(
                    TemplateManager.Instance.Render("planning_thought", prompt)
                )
            };
            var response = await ChatClient.CompleteChatAsync(messages);
            if (response.Value.Content.Count > 0)
            {
                return response.Value.Content[0].Text;
            }
            else
            {
                throw new Exception("Failed to parse the response: " + response.Value.Content);
            }
        }

        public static async Task<string> GetMemoThought(NonPlayerCharacter character, string endReason)
        {
            var prompt = new
            {
                agent = character.characterName,
                iss = character.GetIdentityStableSet(),
                chat_history = Utils.ChatEntriesToString(character.ChatEntries) + '\n' + endReason,
                target = character.chattingTarget.characterName,
            };
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(CognitiveSystemMessage),
                new UserChatMessage(
                    TemplateManager.Instance.Render("memo_thought", prompt)
                )
            };
            var response = await ChatClient.CompleteChatAsync(messages);
            if (response.Value.Content.Count > 0)
            {
                return response.Value.Content[0].Text;
            }
            else
            {
                throw new Exception("Failed to parse the response: " + response.Value.Content);
            }
        }

        public static async Task<string[]> GenerateFocalPoints(NonPlayerCharacter character, string statements,
            int count)
        {
            var prompt = new
            {
                agent = character.characterName,
                iss = character.GetIdentityStableSet(),
                statements = statements,
                count = count,
            };
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(CognitiveSystemMessage),
                new UserChatMessage(
                    TemplateManager.Instance.Render("generate_focal_points", prompt)
                )
            };
            var response = await CallWithRetryAsync(() => ChatClient.CompleteChatAsync(messages), text =>
            {
                var parts = text.Split("||");
                if (parts.Length != count)
                {
                    throw new FormatException(
                        $"Input must contain exactly {count} '||'-separated questions.");
                }

                return parts.Select(p => p.Trim()).ToArray();
            });
            return response;
        }
        
        public static async Task<ScheduleEntry> GeneratePlanning(NonPlayerCharacter character, string statements = "Nothing really important yet")
        {
            var prompt = new
            {
                agent = character.characterName,
                iss = character.GetIdentityStableSet(),
                statements = statements,
            };
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(CognitiveSystemMessage),
                new UserChatMessage(
                    TemplateManager.Instance.Render("generate_planning_v2", prompt)
                )
            };
            var response = await CallWithRetryAsync(() => ChatClient.CompleteChatAsync(messages), text =>
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                return JsonSerializer.Deserialize<ScheduleEntry>(text, options);
            });
            return response;
        }

        public static async Task<List<(string thought, int[] evidence)>> GenerateInsights(NonPlayerCharacter character,
            string statements, int count)
        {
            var prompt = new
            {
                agent = character.characterName,
                iss = character.GetIdentityStableSet(),
                statements = statements,
                count = count,
            };
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(CognitiveSystemMessage),
                new UserChatMessage(
                    TemplateManager.Instance.Render("generate_insights", prompt)
                )
            };
            var response = await CallWithRetryAsync(() => ChatClient.CompleteChatAsync(messages), text =>
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var insights = JsonSerializer.Deserialize<List<AgentInsight>>(text, options);
                var result = new List<(string thought, int[] evidence)>();
                    
                if (insights != null)
                {
                    foreach (var insight in insights)
                    {
                        result.Add((insight.Thought, insight.Evidence ?? Array.Empty<int>()));
                    }
                }

                return result;
            });
            return response;
        }

        [System.Serializable]
        private class AgentInsight
        {
            public string Thought { get; set; }
            public int[] Evidence { get; set; }
        }

        public static async Task<string> GeneratePlanNote(NonPlayerCharacter character, string statements)
        {
            var prompt = new
            {
                agent = character.characterName,
                iss = character.GetIdentityStableSet(),
                statements = statements,
            };
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(CognitiveSystemMessage),
                new UserChatMessage(
                    TemplateManager.Instance.Render("plan_note", prompt)
                )
            };
            var response = await ChatClient.CompleteChatAsync(messages);
            if (response.Value.Content.Count > 0)
            {
                return response.Value.Content[0].Text;
            }
            else
            {
                throw new Exception("Failed to parse the response: " + response.Value.Content);
            }
        }
        
        public static async Task<string> GenerateThoughtNote(NonPlayerCharacter character, string statements)
        {
            var prompt = new
            {
                agent = character.characterName,
                iss = character.GetIdentityStableSet(),
                statements = statements,
            };
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(CognitiveSystemMessage),
                new UserChatMessage(
                    TemplateManager.Instance.Render("thought_note", prompt)
                )
            };
            var response = await ChatClient.CompleteChatAsync(messages);
            if (response.Value.Content.Count > 0)
            {
                return response.Value.Content[0].Text;
            }
            else
            {
                throw new Exception("Failed to parse the response: " + response.Value.Content);
            }
        }

        public static async Task<string> GenerateCurrently(NonPlayerCharacter character, string planNote, string thoughtNote)
        {
            var prompt = new
            {
                agent = character.characterName,
                currently = character.currently,
                current_time = GameManager.Instance.CurrentGameTime.ToRealTime(),
                plan_note = planNote,
                thought_note = thoughtNote,
            };
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(CognitiveSystemMessage),
                new UserChatMessage(
                    TemplateManager.Instance.Render("generate_currently", prompt)
                )
            };
            var response = await ChatClient.CompleteChatAsync(messages);
            if (response.Value.Content.Count > 0)
            {
                if (response.Value.Content[0].Text == "None")
                {
                    return null;
                }
                return response.Value.Content[0].Text;
            }
            else
            {
                throw new Exception("Failed to parse the response: " + response.Value.Content);
            }
        }

        // public static async Task<List<ScheduleEntry>> GetScheduleAfterReflection(NonPlayerCharacter character, string oldCurrently)
        // {
        //     var prompt = new
        //     {
        //         agent = character.characterName,
        //         iss = character.GetIdentityStableSet(),
        //         old_currently = oldCurrently,
        //         next_schedules = character.DailySchedule.GetNextSchedulesString(GameManager.Instance.currentHour,
        //             GameManager.Instance.currentMinute),
        //     };
        //     var messages = new List<ChatMessage>
        //     {
        //         new SystemChatMessage(CognitiveSystemMessage),
        //         new UserChatMessage(
        //             TemplateManager.Instance.Render("schedule_after_reflection", prompt)
        //         )
        //     };
        //     var response = await CallWithRetryAsync(() => ChatClient.CompleteChatAsync(messages), text =>
        //     {
        //         try
        //         {
        //             var options = new JsonSerializerOptions
        //             {
        //                 PropertyNameCaseInsensitive = true
        //             };
        //             return JsonSerializer.Deserialize<List<ScheduleEntry>>(text, options);
        //         }
        //         catch (JsonException)
        //         {
        //             // If it is not such a json, examine whether it is "none"
        //             if (text == "none")
        //             {
        //                 return null;
        //             }
        //
        //             Debug.LogError("Failed to deserialize the decomposed schedule: " + text);
        //             throw new Exception("Failed to parse the response: " + text);
        //         }
        //     });
        //     return response;
        // }
        
        public static async Task<(Item, string)> GetTradeItemAmount(NonPlayerCharacter character, Character target, string statements)
        {
            var prompt = new
            {
                agent = character.characterName,
                iss = character.GetIdentityStableSet(),
                target = target.characterName,
                statements = statements,
                inventory = Utils.InventoryToString(character.Inventory)
            };
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(CognitiveSystemMessage),
                new UserChatMessage(
                    TemplateManager.Instance.Render("trade_item_amount", prompt)
                )
            };
            var response = await CallWithRetryAsync(() => ChatClient.CompleteChatAsync(messages), text =>
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var itemMessage = JsonSerializer.Deserialize<ItemMessage>(text, options);
                    
                var item = new Item
                {
                    Name = itemMessage.Name,
                    Quantity = itemMessage.Quantity
                };
                    
                return (item, itemMessage.Message);
            });
            return response;
        }

        [System.Serializable]
        private class ItemMessage
        {
            public string Name { get; set; }
            public int Quantity { get; set; }
            public string Message { get; set; }
        }
        
        private static async Task<T> CallWithRetryAsync<T>(
            Func<Task<ClientResult<ChatCompletion>>> callFunc,
            Func<string, T> parseFunc,
            int maxRetries = 2)
        {
            int attempt = 0;
            while (true)
            {
                attempt++;
                var response = await callFunc();
                var content = response.Value.Content?.FirstOrDefault()?.Text;
                try
                {
                    return parseFunc(content);
                }
                catch (Exception ex) when (ex is JsonException || ex is FormatException)
                {
                    if (attempt > maxRetries)
                    {
                        Debug.LogError($"Failed to parse the response after {attempt} attempts: {ex.Message} from {response.Value.Id}");
                        throw;
                    }
                    Debug.LogWarning($"Attempt {attempt} failed: {ex.Message} from {response.Value.Id}. Retrying...");
                }
            }
        }
    }
}