using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Code.Characters.Memories;
using Code.Managers;
using Code.Tiles;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Code.Characters
{
    public class PlayerCharacter : Character
    {
        public static PlayerCharacter Instance { get; private set; }
        
        private bool _isTurnOver = true;

        private int _chattingSequence = 1;

        private string _endChatReason = "";
        
        private bool _isInPassiveChat = false;
        private bool _endPassiveChat = false;
        private string _passiveChatResponse = "";
        
        private TaskCompletionSource<(string, bool)> _passiveChatCompletion;

        // references
        private Button _endTurnButton;
        private Button _moveButton;
        private Button _skipMoveButton;
        private Button _chatButton;
        private Button _attackButton;
        private Button _sendItemButton;
        private Button _endChatButton;
        private Button _endPassiveChatButton;
        private Button _sendMessageButton;
        private Button _sendPassiveMessageButton;
        private GameObject _chatInput;
        public TMP_InputField ChatInputText;
        private Button _confirmSendItemButton;
        private GameObject _sendItemInput;
        public TMP_InputField SendItemInputText;
        private GameObject _chatHistory;
        private TMP_Text _chatHistoryText;
        private ScrollRect _chatHistiryScrollRect;
        
        private Character _sendItemTarget;

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
        }

        private void Start()
        {
            base.Start();
            _endTurnButton = GameObject.Find("EndTurnButton").GetComponent<Button>();
            _moveButton = GameObject.Find("MoveButton").GetComponent<Button>();
            _skipMoveButton = GameObject.Find("SkipMoveButton").GetComponent<Button>();
            _chatButton = GameObject.Find("ChatButton").GetComponent<Button>();
            _attackButton = GameObject.Find("AttackButton").GetComponent<Button>();
            _sendItemButton = GameObject.Find("SendItemButton").GetComponent<Button>();
            _endChatButton = GameObject.Find("EndChatButton").GetComponent<Button>();
            _endPassiveChatButton = GameObject.Find("EndPassiveChatButton").GetComponent<Button>();
            _sendMessageButton = GameObject.Find("SendMessageButton").GetComponent<Button>();
            _sendPassiveMessageButton = GameObject.Find("SendPassiveMessageButton").GetComponent<Button>();
            _chatInput = GameObject.Find("ChatInput");
            ChatInputText = _chatInput.GetComponent<TMP_InputField>();
            _sendItemInput = GameObject.Find("SendItemInput");
            SendItemInputText = _sendItemInput.GetComponent<TMP_InputField>();
            _confirmSendItemButton = GameObject.Find("ConfirmSendItemButton").GetComponent<Button>();
            _chatHistory = GameObject.Find("ChatHistory");
            _chatHistoryText = GameObject.Find("ChatHistoryText").GetComponent<TMP_Text>();
            _chatHistiryScrollRect = _chatHistory.GetComponent<ScrollRect>();

            _endTurnButton.onClick.AddListener(EndTurn);

            _moveButton.onClick.AddListener(() => StartCoroutine(Move()));

            _skipMoveButton.onClick.AddListener(() =>
            {
                DisableAllUIs();
                _endTurnButton.gameObject.SetActive(true);
                _chatButton.gameObject.SetActive(true);
                _attackButton.gameObject.SetActive(true);
                _sendItemButton.gameObject.SetActive(true);
                RemainInPlace();
            });

            _chatButton.onClick.AddListener(() => StartCoroutine(Chat()));
            
            _attackButton.onClick.AddListener(() => StartCoroutine(Attack()));
            
            _sendItemButton.onClick.AddListener(() => StartCoroutine(SendItem()));
            
            _confirmSendItemButton.onClick.AddListener(() => StartCoroutine(ConfirmSendItem()));

            _endChatButton.onClick.AddListener(EndChatButtonClicked);
            _endPassiveChatButton.onClick.AddListener(EndPassiveChatButtonClicked);

            _sendMessageButton.onClick.AddListener(SendMessage);
            _sendPassiveMessageButton.onClick.AddListener(SendPassiveMessage);

            DisableAllUIs();
        }

        private IEnumerator ConfirmSendItem()
        {
            var texts = SendItemInputText.text.Split(',');
            var itemName = texts[0].Trim();
            var message = texts[2].Trim();
            
            var item = Inventory.Find(i => i.Name == itemName);

            if (texts.Length == 3 && item != null && Int32.TryParse(texts[1].Trim(), out var num) && num > 0 && num <= item.Quantity)
            {
                var itemToSend = new Item
                {
                    Name = item.Name,
                    Quantity = num
                };
                DisableAllUIs();
                var receiveItemTask = _sendItemTarget.ReceiveItem(itemToSend, message, this); 
                yield return new WaitUntil(() => receiveItemTask.IsCompleted);
                item.Quantity -= num;
                if (item.Quantity <= 0)
                {
                    Inventory.Remove(item);
                }
                GameManager.Instance.UpdateInventoryText();
                // If this is the relic, disable the sprite
                if (item.Name == "relic")
                {
                    RelicSprite.SetActive(false);
                }
                
                // Release the give item event
                TileEvent giveItemEvent = new TileEvent
                {
                    Subject = characterName,
                    Predicate = "gives",
                    Object = item.Name,
                    Position = _sendItemTarget.position,
                    GameTime = GameManager.Instance.CurrentGameTime,
                    Description = $"{characterName} is giving {item.Name} to {_sendItemTarget.characterName}"
                };
                TileManager.Instance.AddEventToTile(position, giveItemEvent);
                
                // Update UI
                SendItemInputText.text = "";
                _sendItemTarget = null;
                _endTurnButton.gameObject.SetActive(true);
            }
            else
            {
                Debug.LogWarning("Invalid input");
            }
        }

        private void SendPassiveMessage()
        {
            var message = ChatInputText.text;
            if (string.IsNullOrEmpty(message))
            {
                Debug.Log("Message is empty");
                return;
            }
            
            _chatHistoryText.text += $"{characterName}(player):\n {message}\n\n";
            ChatInputText.text = "";
            ChatEntries.Add(new ChatEntry
            {
                Speaker = characterName,
                Content = message
            });
            
            _sendPassiveMessageButton.gameObject.SetActive(false);
            _endPassiveChatButton.gameObject.SetActive(false);
            StartCoroutine(ScrollToBottomNextFrame());
            
            _passiveChatCompletion?.TrySetResult((message, false));
        }

        private void EndPassiveChatButtonClicked()
        {
            DisableAllUIs();
            ClearAllChat();
            
            // Clear the chat states
            chattingTarget = null;
            _isInPassiveChat = false;
            ChatEntries = null;
            
            // End the passive chat
            _passiveChatCompletion?.TrySetResult(("", true));
        }

        private void DisableAllUIs()
        {
            _endTurnButton.gameObject.SetActive(false);
            _moveButton.gameObject.SetActive(false);
            _skipMoveButton.gameObject.SetActive(false);
            _chatButton.gameObject.SetActive(false);
            _endChatButton.gameObject.SetActive(false);
            _sendMessageButton.gameObject.SetActive(false);
            _chatInput.SetActive(false);
            _chatHistory.SetActive(false);
            _endPassiveChatButton.gameObject.SetActive(false);
            _sendPassiveMessageButton.gameObject.SetActive(false);
            _attackButton.gameObject.SetActive(false);
            _sendItemButton.gameObject.SetActive(false);
            _confirmSendItemButton.gameObject.SetActive(false);
            _sendItemInput.SetActive(false);
        }

        public override IEnumerator TakeTurn()
        {
            Debug.Log("Taking turn for player character: " + characterName);
            _endTurnButton.gameObject.SetActive(true);
            _moveButton.gameObject.SetActive(true);
            _skipMoveButton.gameObject.SetActive(true);

            // Wait for the player to click on the end turn button
            _isTurnOver = false;
            yield return new WaitUntil(() => _isTurnOver);
            
            // Check if the player is adjacent to the escape tile
            var escapeTile = TileManager.Instance.GetEscapeTile();
            if (escapeTile != Vector2Int.zero && IsAdjacent(escapeTile))
            {
                // If the player is adjacent to the escape tile, mark them as escaping
                IsEscaping = true;
            }
            
            // Check if the player is adjacent to the relic tile
            var relicTile = TileManager.Instance.GetRelicTile();
            if (relicTile.HasValue && IsAdjacent(relicTile.Value))
            {
                // Loot the relic
                var item = new Item
                {
                    Name = "relic",
                    Quantity = 1
                };
                Inventory.Add(item);
                RelicSprite.SetActive(true);
                TileManager.Instance.RegisterRelicLooted();
                
                // release the loot event
                TileEvent lootEvent = new TileEvent
                {
                    Subject = characterName,
                    Predicate = "loots",
                    Object = "relic",
                    Position = position,
                    GameTime = GameManager.Instance.CurrentGameTime,
                    Description = $"{characterName} loots the relic"
                };
                TileManager.Instance.AddEventToTile(position, lootEvent);
                
                // release the relic taken event
                TileEvent relicTakenEvent = new TileEvent
                {
                    Subject = "relic",
                    Predicate = "is taken",
                    Object = "",
                    Position = relicTile.Value,
                    GameTime = GameManager.Instance.CurrentGameTime,
                    Description = $"The relic is taken"
                };
                TileManager.Instance.AddEventToTile(relicTile.Value, relicTakenEvent);
                
            }

            Debug.Log("Player character " + characterName + " has finished their turn.");
        }

        private void EndTurn()
        {
            _isTurnOver = true;
            DisableAllUIs();
        }

        private IEnumerator Move()
        {
            DisableAllUIs();

            yield return new WaitUntil(() => Input.GetMouseButtonDown(0));
            var mouseTile = GetMouseTile();
            // Move the character towards the tile position
            var targetTile = TileManager.Instance.FindPath(position, mouseTile, speed, out _);
            if (targetTile.HasValue)
            {
                // Move the character to the target tile
                SetPosition(targetTile.Value);
                _endTurnButton.gameObject.SetActive(true);
                _chatButton.gameObject.SetActive(true);
                _attackButton.gameObject.SetActive(true);
                _sendItemButton.gameObject.SetActive(true);
            }
            else
            {
                Debug.Log("No path found");
                _moveButton.gameObject.SetActive(true);
                _skipMoveButton.gameObject.SetActive(true);
                _endTurnButton.gameObject.SetActive(true);
            }
        }

        private IEnumerator Chat()
        {
            DisableAllUIs();
            yield return new WaitUntil(() => Input.GetMouseButtonDown(0));
            var mouseTile = GetMouseTile();
            if (!IsAdjacent(mouseTile))
            {
                Debug.Log("Not adjacent to the target tile");
                _endTurnButton.gameObject.SetActive(true);
                _chatButton.gameObject.SetActive(true);
                _attackButton.gameObject.SetActive(true);
                _sendItemButton.gameObject.SetActive(true);
            }
            else
            {
                var targetCharacter = TileManager.Instance.GetCharacterAtPosition(mouseTile);
                if (targetCharacter == null || targetCharacter == this)
                {
                    Debug.Log("No available character in the target tile");
                    _endTurnButton.gameObject.SetActive(true);
                    _chatButton.gameObject.SetActive(true);
                    _attackButton.gameObject.SetActive(true);
                    _sendItemButton.gameObject.SetActive(true);
                }
                else
                {
                    StartChat(targetCharacter);
                }
            }
        }

        private Vector2Int GetMouseTile()
        {
            // Get the mouse position in world space
            Vector3 mousePosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            // Get the tile position according to the mouse position
            Vector2Int mouseTile = new Vector2Int(Mathf.FloorToInt(mousePosition.x), Mathf.FloorToInt(mousePosition.y));
            return mouseTile;
        }

        private bool IsAdjacent(Vector2Int targetPosition)
        {
            return (Mathf.Abs(position.x - targetPosition.x) <= 1 && Mathf.Abs(position.y - targetPosition.y) <= 1);
        }

        private void StartChat(Character targetCharacter)
        {
            Debug.Log("Starting chat with " + targetCharacter.gameObject.name);
            chattingTarget = targetCharacter;
            _chatHistory.SetActive(true);

            _chatInput.SetActive(true);
            SelectChatInput();

            _sendMessageButton.gameObject.SetActive(true);
            _endChatButton.gameObject.SetActive(true);
        }

        private void SelectChatInput()
        {
            ChatInputText.ActivateInputField();
            ChatInputText.Select();
        }

        private async void SendMessage()
        {
            if (chattingTarget == null)
            {
                Debug.Log("No chatting target");
                return;
            }

            var message = ChatInputText.text;
            if (string.IsNullOrEmpty(message))
            {
                Debug.Log("Message is empty");
                return;
            }
            
            if (ChatEntries == null)
            {
                // this means the chat is new
                ChatEntries = new List<ChatEntry>();
            }

            _chatHistoryText.text += $"{characterName}(player):\n {message}\n\n";
            ChatInputText.text = "";
            
            _sendMessageButton.gameObject.SetActive(false);
            _endChatButton.gameObject.SetActive(false);
            StartCoroutine(ScrollToBottomNextFrame());
            _chattingSequence++;
            // update the chat history
            ChatEntries.Add(new ChatEntry
            {
                Speaker = characterName,
                Content = message
            });
            // send the message to the target character and get the response
            var (response, end) = await chattingTarget.ReceiveMessage(message, this, ChatEntries, _chattingSequence);
            if (response == "")
            {
                _chatHistoryText.text += $"{chattingTarget.characterName} remains silence\n\n";
            }
            else
            {
                _chatHistoryText.text += $"{chattingTarget.characterName}:\n {response}\n\n";
            }
            
            _chattingSequence++;
            if (!end && _chattingSequence <= 10)
            {
                _sendMessageButton.gameObject.SetActive(true);
                SelectChatInput();
                _endChatButton.gameObject.SetActive(true);
            } else if (end)
            {
                EndChat($"{chattingTarget.characterName} ends the chat");
            } else if (_chattingSequence > 10)
            {
                EndChat($"The chat ends due to the message limitation in one chat.");
            }
            
            StartCoroutine(ScrollToBottomNextFrame());
        }

        private IEnumerator ScrollToBottomNextFrame()
        {
            yield return null;
            _chatHistiryScrollRect.verticalNormalizedPosition = 0f;
        }

        private async void EndChatButtonClicked()
        {
            ClearAllChat();
            DisableAllUIs();
            
            if (!_isTurnOver)
            {
                // If it is in the player turn
                if (_endChatReason == "")
                {
                    // If the end chat reason is empty in the player's turn, 
                    // it means the player ends the chat
                    _endChatReason = $"{characterName} ends the chat";
                }
                
                if (ChatEntries != null && ChatEntries.Count > 0 && chattingTarget != null)
                {
                    // Only notify the npc if the chat history is not empty
                    chattingTarget.EndChat(_endChatReason);
                }

                // Show the end turn button
                _endTurnButton.gameObject.SetActive(true);
            }
            
            // register the chat event
            TileEvent chatEvent = new TileEvent
            {
                Subject = characterName,
                Predicate = "is talking to",
                Object = chattingTarget.characterName,
                Position = position,
                GameTime = GameManager.Instance.CurrentGameTime,
                Description = $"{characterName} is talking to {chattingTarget.characterName}"
            };
            TileManager.Instance.AddEventToTile(position, chatEvent);
            
            // Clear the chat state
            chattingTarget = null;
            _chattingSequence = 1;
            _endChatReason = "";
            ChatEntries = null;
        }

        private void ClearAllChat()
        {
            _chatHistoryText.text = "";
            ChatInputText.text = "";
        }

        public override Task<(string, bool)> ReceiveMessage(string message, Character chattingCharacter, List<ChatEntry> history, int sequence)
        {
            if (!_isInPassiveChat)
            {
                // if the player is not in passive chat, display the history and input ui, and initialize the chat
                _isInPassiveChat = true;
                chattingTarget = chattingCharacter;
                ChatEntries = history;
                _chatHistory.SetActive(true);
                _chatInput.SetActive(true);
            }
            // add the message to the chat history
            _chatHistoryText.text += $"{chattingCharacter.characterName}:\n {message}\n\n";
            
            // display the send message button and end chat button for passive chat
            _sendPassiveMessageButton.gameObject.SetActive(true);
            _endPassiveChatButton.gameObject.SetActive(true);
            
            _passiveChatCompletion = new TaskCompletionSource<(string, bool)>();
            return _passiveChatCompletion.Task;
        }

        public override void EndChat(string reason = "")
        {
            if (!_isInPassiveChat)
            {
                _endChatButton.gameObject.SetActive(true);
                _chatHistoryText.text += reason + "\n";
                StartCoroutine(ScrollToBottomNextFrame());
                _endChatReason = reason;
            }

            else
            {
                // If it is in passive chat, then the player didn't end the chat manually by calling this function
                _endPassiveChatButton.gameObject.SetActive(true);
                _chatHistoryText.text += reason + "\n";
                StartCoroutine(ScrollToBottomNextFrame());
            }
        }

        public override async Task ReceiveItem(Item item, string message, Character sender)
        {
            // Call the original function to add the item
            await base.ReceiveItem(item, message, sender);
            
            // Update the Inventory UI and display the message
            GameManager.Instance.PlayerReceiveItem(item, message, sender);

        }
        
        private IEnumerator Attack()
        {
            DisableAllUIs();
            yield return new WaitUntil(() => Input.GetMouseButtonDown(0));
            var mouseTile = GetMouseTile();
            if (!IsAdjacent(mouseTile))
            {
                Debug.Log("Not adjacent to the target tile");
                _endTurnButton.gameObject.SetActive(true);
                _chatButton.gameObject.SetActive(true);
                _attackButton.gameObject.SetActive(true);
                _sendItemButton.gameObject.SetActive(true);
            }
            else
            {
                var targetCharacter = TileManager.Instance.GetCharacterAtPosition(mouseTile);
                if (targetCharacter == null || targetCharacter == this)
                {
                    Debug.Log("No available character in the target tile");
                    _endTurnButton.gameObject.SetActive(true);
                    _chatButton.gameObject.SetActive(true);
                    _attackButton.gameObject.SetActive(true);
                    _sendItemButton.gameObject.SetActive(true);
                }
                else
                {
                    // Attack the target character
                    int attackCheck = GetAttackCheck();
                    if (attackCheck >= targetCharacter.armorClass)
                    {
                        // Attack hits
                        int damage = GetDamageRoll();
                        targetCharacter.currentHitPoints -= damage;
                        Debug.Log($"{characterName} attacks {targetCharacter.characterName} and deals {damage} damage."); 
                    }
                    else
                    {
                        // Attack misses
                        Debug.Log($"{characterName} attacks {targetCharacter.characterName} but misses.");
                    }
                    
                    // Release the attack event
                    TileEvent attackEvent = new TileEvent
                    {
                        Subject = characterName,
                        Predicate = "is attacking",
                        Object = targetCharacter.characterName,
                        Position = position,
                        GameTime = GameManager.Instance.CurrentGameTime,
                        Description = $"{characterName} is attacking {targetCharacter.characterName}"
                    };
                    TileManager.Instance.AddEventToTile(position, attackEvent);
                    
                    // Release the attacked or death event
                    if (targetCharacter.currentHitPoints <= 0)
                    {
                        TileEvent deathEvent = new TileEvent
                        {
                            Subject = targetCharacter.characterName,
                            Predicate = "is killed",
                            Object = "",
                            Position = targetCharacter.position,
                            GameTime = GameManager.Instance.CurrentGameTime,
                            Description = $"{targetCharacter.characterName} is killed"
                        };
                        TileManager.Instance.AddEventToTile(targetCharacter.position, deathEvent);

                        // Register the character's death with the TileManager
                        TileManager.Instance.RegisterCharacterDeath(targetCharacter);
                    }
                    else
                    {
                        TileEvent underAttackEvent = new TileEvent
                        {
                            Subject = targetCharacter.characterName,
                            Predicate = "is under attack",
                            Object = "",
                            Position = targetCharacter.position,
                            GameTime = GameManager.Instance.CurrentGameTime,
                            Description = $"{targetCharacter.characterName} is under attack"
                        };
                        TileManager.Instance.AddEventToTile(targetCharacter.position, underAttackEvent);
                    }
                    _endTurnButton.gameObject.SetActive(true);
                }
            }
        }
        
        private IEnumerator SendItem()
        {
            DisableAllUIs();
            yield return new WaitUntil(() => Input.GetMouseButtonDown(0));
            var mouseTile = GetMouseTile();
            if (!IsAdjacent(mouseTile))
            {
                Debug.Log("Not adjacent to the target tile");
                _endTurnButton.gameObject.SetActive(true);
                _chatButton.gameObject.SetActive(true);
                _attackButton.gameObject.SetActive(true);
                _sendItemButton.gameObject.SetActive(true);
            }
            else
            {
                var targetCharacter = TileManager.Instance.GetCharacterAtPosition(mouseTile);
                if (targetCharacter == null || targetCharacter == this)
                {
                    Debug.Log("No available character in the target tile");
                    _endTurnButton.gameObject.SetActive(true);
                    _chatButton.gameObject.SetActive(true);
                    _attackButton.gameObject.SetActive(true);
                    _sendItemButton.gameObject.SetActive(true);
                }
                else
                {
                    _sendItemTarget = targetCharacter;
                    _endTurnButton.gameObject.SetActive(true);
                    _sendItemInput.SetActive(true);
                    _confirmSendItemButton.gameObject.SetActive(true);
                }
            }
        }
    }
}