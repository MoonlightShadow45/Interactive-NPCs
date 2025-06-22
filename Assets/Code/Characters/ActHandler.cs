using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Code.Characters.Memories;
using Code.Managers;
using Code.Tiles;
using UnityEngine;

namespace Code.Characters
{
    public class ActHandler
    {
        private readonly NonPlayerCharacter _npc;

        public ActHandler(NonPlayerCharacter npc)
        {
            _npc = npc;
        }

        public async Task<ActionMode> DetermineActionMode()
        {
            return await OpenaiApi.OpenaiApi.GetActionMode(_npc);
        }

        public void Interact()
        {
            // If the target object is "relic" and the character wants to loot it, release two special events
            // 1. The character loots the relic (at the character's position)
            // 2. The relic is looted (at the relic's position)
            // and loot the relic
            if (_npc.TargetObject == "relic" && _npc.ShouldLoot && TileManager.Instance.GetRelicTile() == _npc.ActionAddress)
            {
                TileEvent lootEvent = new TileEvent
                {
                    Subject = _npc.characterName,
                    Predicate = "loots",
                    Object = _npc.TargetObject,
                    Position = _npc.position,
                    GameTime = GameManager.Instance.CurrentGameTime,
                    Description = $"{_npc.characterName} loots the relic"
                };
                TileManager.Instance.AddEventToTile(_npc.position, lootEvent);

                TileEvent lootedEvent = new TileEvent
                {
                    Subject = _npc.TargetObject,
                    Predicate = "is looted",
                    Object = "",
                    Position = _npc.ActionAddress.Value,
                    GameTime = GameManager.Instance.CurrentGameTime,
                    Description = $"{_npc.TargetObject} is looted"
                };
                TileManager.Instance.AddEventToTile(_npc.ActionAddress.Value, lootedEvent);
                
                // Add the relic to the character's inventory
                _npc.Inventory.Add(new Item
                {
                    Name = "relic",
                    Quantity = 1,
                });
                _npc.RelicSprite.SetActive(true);
                // register the relic is looted
                TileManager.Instance.RegisterRelicLooted();
            }
            
            // Release two events:
            // 1. The character is using the object (at the object's position, if there is a target object)
            // 2. The character is performing the action (at the character's position)
            if (_npc.TargetObject != null)
            {
                System.Diagnostics.Debug.Assert(_npc.ActionAddress != null, "_npc.ActionAddress != null");
                TileEvent objectEvent = new TileEvent
                {
                    Subject = _npc.characterName,
                    Predicate = "is using",
                    Object = _npc.TargetObject,
                    Position = _npc.ActionAddress.Value,
                    GameTime = GameManager.Instance.CurrentGameTime,
                    Description = $"{_npc.characterName} is using {_npc.TargetObject}"
                };
                TileManager.Instance.AddEventToTile(_npc.ActionAddress.Value, objectEvent);
            }

            TileEvent characterEvent = new TileEvent
            {
                Subject = _npc.ActionEventTriple.Subject,
                Predicate = _npc.ActionEventTriple.Predicate,
                Object = _npc.ActionEventTriple.Object,
                Position = _npc.position,
                GameTime = GameManager.Instance.CurrentGameTime,
                Description = _npc.actionDescription
            };
            TileManager.Instance.AddEventToTile(_npc.position, characterEvent);

            Debug.Log($"{_npc.characterName} is interacting: {_npc.actionDescription}");
            
            // If it is an escape action, then mark the escape to true
            if (_npc.TargetObject == "escape point")
            {
                _npc.IsEscaping = true;
            }
        }

        public void Attack()
        {
            // Perform the attack action first
            var attackCheck = _npc.GetAttackCheck();
            if (attackCheck > _npc.TargetCharacter.armorClass)
            {
                var damage = _npc.GetDamageRoll();
                _npc.TargetCharacter.currentHitPoints -= damage;
                if (_npc.TargetCharacter is PlayerCharacter)
                {
                    GameManager.Instance.UpdateHealthText(_npc.TargetCharacter.currentHitPoints);
                }

                Debug.Log($"{_npc.characterName} attacks {_npc.TargetCharacter.characterName} for {damage} damage");
            }
            else
            {
                Debug.Log($"{_npc.characterName} misses the attack on {_npc.TargetCharacter.characterName}");
            }

            // Release the attack event
            TileEvent attackEvent = new TileEvent
            {
                Subject = _npc.characterName,
                Predicate = "is attacking",
                Object = _npc.TargetCharacter.characterName,
                Position = _npc.position,
                GameTime = GameManager.Instance.CurrentGameTime,
                Description = $"{_npc.characterName} is attacking {_npc.TargetCharacter.characterName}"
            };
            TileManager.Instance.AddEventToTile(_npc.position, attackEvent);

            // Release the attacked or death event
            if (_npc.TargetCharacter.currentHitPoints <= 0)
            {
                TileEvent deathEvent = new TileEvent
                {
                    Subject = _npc.TargetCharacter.characterName,
                    Predicate = "is killed",
                    Object = "",
                    Position = _npc.TargetCharacter.position,
                    GameTime = GameManager.Instance.CurrentGameTime,
                    Description = $"{_npc.TargetCharacter.characterName} is killed"
                };
                TileManager.Instance.AddEventToTile(_npc.TargetCharacter.position, deathEvent);

                // Register the character's death with the TileManager
                TileManager.Instance.RegisterCharacterDeath(_npc.TargetCharacter);
            }
            else
            {
                TileEvent underAttackEvent = new TileEvent
                {
                    Subject = _npc.TargetCharacter.characterName,
                    Predicate = "is under attack",
                    Object = "",
                    Position = _npc.TargetCharacter.position,
                    GameTime = GameManager.Instance.CurrentGameTime,
                    Description = $"{_npc.TargetCharacter.characterName} is under attack"
                };
                TileManager.Instance.AddEventToTile(_npc.TargetCharacter.position, underAttackEvent);
            }
        }

        public async Task StartChat()
        {
            // Retrieve the relevant nodes about the target from memory
            var retrievedNodes = await _npc.RetrieveNodes(_npc.TargetCharacter.characterName, 25);
            // Get a summary of the relationship between them
            var nodeDescriptions = Utils.GetSummaryStatements(retrievedNodes);
            var relationshipSummary = await OpenaiApi.OpenaiApi.GetRelationshipSummary(_npc,
                nodeDescriptions.ToString(), _npc.TargetCharacter.characterName);

            var chatRetrievedNodes = await _npc.RetrieveNodes(relationshipSummary, 15);
            var chatRetrievedNodesDescriptions = Utils.GetSummaryStatements(chatRetrievedNodes);
            _npc.RelationshipNodeStatements = chatRetrievedNodesDescriptions;

            // Generate the first chat
            var sequence = 1;
            var chatHistory = new List<ChatEntry>();
            var (firstChat, _) = await OpenaiApi.OpenaiApi.GenerateChat(_npc, _npc.TargetCharacter,
                chatRetrievedNodesDescriptions, sequence, "");

            // Initialize the chat
            chatHistory.Add(new ChatEntry
            {
                Speaker = _npc.characterName,
                Content = firstChat
            });
            _npc.chattingTarget = _npc.TargetCharacter;
            _npc.ChatEntries = chatHistory;
            sequence++;
            var nextCharacter = _npc.chattingTarget;
            Character prevCharacter = _npc;
            var prevChat = firstChat;

            // Take turns to chat
            while (sequence <= 10)
            {
                var (message, end) =
                    await nextCharacter.ReceiveMessage(prevChat, prevCharacter, chatHistory, sequence);
                prevChat = message;
                (nextCharacter, prevCharacter) = (prevCharacter, nextCharacter);
                if (end)
                {
                    break;
                }

                sequence++;
            }

            // Get the end chat reason
            string endReason = "";
            if (sequence > 10)
            {
                endReason = "The chat ends due to the message limitation in one chat.";
            }
            else
            {
                endReason = $"{prevCharacter.characterName} is not going to talk further";
            }

            // Release the chat event(only one event)
            TileEvent chatEvent = new TileEvent
            {
                Subject = _npc.characterName,
                Predicate = "is talking to",
                Object = _npc.chattingTarget.characterName,
                Position = _npc.position,
                GameTime = GameManager.Instance.CurrentGameTime,
                Description = $"{_npc.characterName} is talking to {_npc.chattingTarget.characterName}",
            };
            TileManager.Instance.AddEventToTile(_npc.position, chatEvent);

            // Log the chat
            var chatLog = new StringBuilder();
            foreach (var entry in _npc.ChatEntries)
            {
                chatLog.AppendLine($"{entry.Speaker}: {entry.Content}");
            }
            Debug.Log($"{_npc.characterName} is chatting with {_npc.chattingTarget.characterName}: {chatLog}");
            
            // Chat clean up
            // If it is not the player that manually ended the chat, then call the chattingTarget's EndChat
            if (_npc.chattingTarget is not PlayerCharacter)
            {
                _npc.chattingTarget.EndChat(endReason);
            }
            else if (sequence % 2 != 0)
            {
                // This means: it is not the other character that ended the chat
                _npc.chattingTarget.EndChat(endReason);
            }

            _npc.EndChat(endReason);
        }

        public async Task GiveItem()
        {
            // If the target npc is dead, do not give item
            if (_npc.TargetCharacter.currentHitPoints <= 0)
            {
                Debug.LogWarning($"{_npc.characterName} cannot give item to {_npc.TargetCharacter.characterName} because they are dead.");
                return;
            }
            
            // Like chat, we need to generate what to give, and a message

            // Retrieve the relevant nodes about the target from memory
            var retrievedNodes = await _npc.RetrieveNodes(_npc.TargetCharacter.characterName, 25);
            // Get a summary of the relationship between them
            var nodeDescriptions = Utils.GetSummaryStatements(retrievedNodes);
            var relationshipSummary = await OpenaiApi.OpenaiApi.GetRelationshipSummary(_npc,
                nodeDescriptions.ToString(), _npc.TargetCharacter.characterName);

            var chatRetrievedNodes = await _npc.RetrieveNodes(relationshipSummary, 15);
            var chatRetrievedNodesDescriptions = Utils.GetSummaryStatements(chatRetrievedNodes);
            _npc.RelationshipNodeStatements = chatRetrievedNodesDescriptions;

            // Generate what to give
            // Todo: here it is assumed that the item & quantity is valid
            var (item, message) =
                await OpenaiApi.OpenaiApi.GetTradeItemAmount(_npc, _npc.TargetCharacter,
                    chatRetrievedNodesDescriptions);

            // give the item to the target character
            await _npc.TargetCharacter.ReceiveItem(item, message, _npc);
            
            // remove the item from the giver's inventory
            var itemInInventory = _npc.Inventory.FirstOrDefault(x => x.Name == item.Name);
            if (itemInInventory != null)
            {
                itemInInventory.Quantity -= item.Quantity;
                if (itemInInventory.Quantity <= 0)
                {
                    _npc.Inventory.Remove(itemInInventory);
                }
                if (item.Name == "relic")
                {
                    _npc.RelicSprite.SetActive(false);
                }
            }

            // Release the give item event
            TileEvent giveItemEvent = new TileEvent
            {
                Subject = _npc.characterName,
                Predicate = "gives",
                Object = item.Name,
                Position = _npc.position,
                GameTime = GameManager.Instance.CurrentGameTime,
                Description = $"{_npc.characterName} is giving {item.Name} to {_npc.TargetCharacter.characterName}"
            };
            TileManager.Instance.AddEventToTile(_npc.position, giveItemEvent);
            
            // Log the give item 
            var giveItemLog = new StringBuilder();
            giveItemLog.AppendLine($"{_npc.characterName} gives {item.Name}*{item.Quantity} to {_npc.TargetCharacter.characterName}");
            giveItemLog.AppendLine($"and says: {message}");
            Debug.Log(giveItemLog);
            
            // Add a memo thought
            var memoThought = "I have given " + item.Name + "*" + item.Quantity + " to " +
                              _npc.TargetCharacter.characterName + " and said: " + message;
            var memoEventTriple = await OpenaiApi.OpenaiApi.GetActionTriple(memoThought);
            var memoKeywords = new HashSet<string>
            {
                memoEventTriple.subject,
                memoEventTriple.predicate,
                memoEventTriple.@object
            };
            var memoPoignancyScore =
                await OpenaiApi.OpenaiApi.GetThoughtPoignancy(_npc, memoThought);
            var memoEmbedding = await OpenaiApi.OpenaiApi.GetEmbedding(memoThought);
            _npc.AssociativeMemory.AddThought(GameManager.Instance.CurrentGameTime, null,
                memoEventTriple.subject, memoEventTriple.predicate, memoEventTriple.@object,
                memoThought,
                new KeyValuePair<string, float[]>(memoThought, memoEmbedding),
                memoPoignancyScore,
                memoKeywords.ToList(), new EventThoughtFilling()
                {
                });
        }
    }
}