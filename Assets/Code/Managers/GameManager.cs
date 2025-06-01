using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Code.Characters;
using Code.Tiles;
using TMPro;
using UnityEngine.Serialization;

namespace Code.Managers
{
    /// <summary>
    /// This class manages the turns and the time in the game
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance;

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

        // The time in the game
        [HideInInspector] public int currentHour = 0;
        [HideInInspector] public int currentMinute = 0;
        [HideInInspector] public int currentTurn = 1;
        public int CurrentDay => Mathf.FloorToInt(currentTurn * turnTime / 60f / 24f) + 1;
        public GameTime CurrentGameTime => new GameTime
        {
            eventTurn = currentTurn,
            eventSequence = currentSequence
        };

        // the game starts at 00:00 AM, but time will be quickly advanced to startHour:startMinute before the player takes control
        public int startHour = 3;
        public int startMinute = 0;

        // Time in minutes for each turn
        public float turnTime = 10f;

        private List<Character> _characters = new List<Character>();
        public int currentSequence = 0;
        
        // References
        private TMP_Text _currentTimeTMP;
        private TMP_Text _currentHealthTMP;
        private TMP_Text _inventoryTMP;
        private TMP_Text _receiveItemTMP;
        private GameTime _receiveItemTextExpirationTime = new GameTime
        {
            eventTurn = 0,
            eventSequence = 0
        };

        private void Start()
        {
            // Initialize the game time
            currentHour = 0;
            currentMinute = 0;
            
            // Find the current time text object
            _currentTimeTMP = GameObject.Find("CurrentTimeTMP").GetComponent<TMP_Text>(); ;

            // Find the inventory text object
            _inventoryTMP = GameObject.Find("InventoryTMP").GetComponent<TMP_Text>();
            _inventoryTMP.text = Utils.InventoryToString(PlayerCharacter.Instance.Inventory);
            
            _receiveItemTMP = GameObject.Find("ReceiveItemTMP").GetComponent<TMP_Text>();
            _receiveItemTMP.text = "";
            
            // Find the current health text object
            _currentHealthTMP = GameObject.Find("CurrentHealthTMP").GetComponent<TMP_Text>();

            // Add all characters in the scene to the list
            var characters = FindObjectsOfType<Character>();
            foreach (var character in characters)
            {
                _characters.Add(character);
            }

            // sorting the characters by their dexterity
            _characters.Sort((x, y) => y.dexterity.CompareTo(x.dexterity));

            // Start the first turn
            StartCoroutine(StartTurn());
        }
        
        // Move the camera with the arrow keys
        private void Update()
        {
            if (!PlayerCharacter.Instance.ChatInputText.isFocused)
            {
                if (Input.GetKeyDown(KeyCode.UpArrow))
                {
                    Camera.main.transform.position += new Vector3(0, 1, 0);
                }
                if (Input.GetKeyDown(KeyCode.DownArrow))
                {
                    Camera.main.transform.position += new Vector3(0, -1, 0);
                }
                if (Input.GetKeyDown(KeyCode.LeftArrow))
                {
                    Camera.main.transform.position += new Vector3(-1, 0, 0);
                }
                if (Input.GetKeyDown(KeyCode.RightArrow))
                {
                    Camera.main.transform.position += new Vector3(1, 0, 0);
                }
            }
        }

        private IEnumerator StartTurn()
        {
            // wait for one frame
            yield return new WaitForEndOfFrame();
            while (true)
            {
                // The player can't move before the start time
                if (currentHour < startHour || (currentHour == startHour && currentMinute < startMinute))
                {
                    if (_characters[currentSequence] is PlayerCharacter)
                    {
                        currentSequence++;
                        if (currentSequence >= _characters.Count)
                        {
                            currentSequence = 0;
                            UpdateTime();
                        }

                        continue;
                    }
                }
                
                yield return _characters[currentSequence]
                    .StartCoroutine(_characters[currentSequence].TakeTurn());
                
                // Check if the current character is escaping
                if (_characters[currentSequence].IsEscaping)
                {
                    // If the character is escaping, remove them from the list
                    Debug.Log($"{_characters[currentSequence].characterName} has escaped.");
                    _characters.RemoveAt(currentSequence);
                }
                
                currentSequence++;
                if (currentSequence >= _characters.Count)
                {
                    currentSequence = 0;
                    UpdateTime();
                }
                
                // Check if the item receive text should be hidden
                if (_receiveItemTextExpirationTime.eventTurn == currentTurn && _receiveItemTextExpirationTime.eventSequence == currentSequence)
                {
                    _receiveItemTMP.text = "";
                }
            }
        }

        private void UpdateTime()
        {
            // Update the game time
            currentMinute += (int)turnTime;
            if (currentMinute >= 60)
            {
                currentHour += currentMinute / 60;
                currentMinute = currentMinute % 60;
            }

            if (currentHour >= 24)
            {
                currentHour = currentHour % 24;
            }
            
            Debug.Log($"Current time: {currentHour:D2}:{currentMinute:D2}");
            _currentTimeTMP.text = $"Current time: {currentHour:D2}:{currentMinute:D2}";
            currentTurn++;
        }

        public string GetRealTime(int turn)
        {
            // Calculate the real time based on the turn number
            int realHour = (turn * (int)turnTime) / 60;
            int realMinute = (turn * (int)turnTime) % 60;

            if (realMinute >= 60)
            {
                realHour += realMinute / 60;
                realMinute = realMinute % 60;
            }

            if (realHour >= 24)
            {
                realHour = realHour % 24;
            }

            return $"{realHour:D2}:{realMinute:D2}";
        }

        public Character FindCharacter(string characterName)
        {
            Character c = _characters.FirstOrDefault(i => i?.characterName == characterName);
            if (c == null)
            {
                Debug.LogError($"Character with name '{characterName}' not found.");
                return null;
            }
            return c;
        }

        public void PlayerReceiveItem(Item item, string message, Character sender)
        {
            // Show the message
            string text = $"{sender.characterName} gives you {item.Name}*{item.Quantity} and says:";
            text += $"\n{message}";
            _receiveItemTMP.text = text;
            _receiveItemTextExpirationTime = new GameTime
            {
                eventTurn = currentTurn + 1,
                eventSequence = currentSequence 
            };
            
            UpdateInventoryText();
        }
        

        public void UpdateHealthText(int targetCharacterCurrentHitPoints)
        {
            // Update the health text
            _currentHealthTMP.text = $"Current health: {targetCharacterCurrentHitPoints}/{PlayerCharacter.Instance.maxHitPoints}";
            if (targetCharacterCurrentHitPoints <= 0)
            {
                _currentHealthTMP.text = "You are dead";
            }
        }
        
        public void UpdateInventoryText()
        {
            // Update the inventory text
            _inventoryTMP.text = Utils.InventoryToString(PlayerCharacter.Instance.Inventory);
        }
    }
}