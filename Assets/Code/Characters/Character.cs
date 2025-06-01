using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Code.Characters.Memories;
using Code.Characters.Skills;
using Code.Tiles;
using UnityEngine;
using UnityEngine.Serialization;

namespace Code.Characters
{
    public abstract class Character : MonoBehaviour
    {
        public string characterName;

        public string race;
        // base stats
        public int strength;
        public int dexterity;
        public int constitution;
        public int intelligence;
        public int wisdom;
        public int charisma;
        
        public string GetStatsSummary()
        {
            return $"STR: {strength}, DEX: {dexterity}, CON: {constitution}, INT: {intelligence}, WIS: {wisdom}, CHA: {charisma}";
        }
    
        // derived stats
        public int armorClass;
        public int maxHitPoints;
        [HideInInspector] public int currentHitPoints;
        public int speed;
        public int attackBonus;
        // eg: 1d8
        public int attackDamageBase;
        // eg: +1
        public int attackDamageBonus;
    
        public SkillProficiency[] proficientSkills;
        
        
        [HideInInspector] public Vector2Int position;

        public Character chattingTarget;
        
        /// <summary>
        /// The chatting content of the current chat, including the speaker and the content
        /// </summary>
        public List<ChatEntry> ChatEntries;

        public List<Item> Inventory;

        public GameObject RelicSprite;

        public bool IsEscaping = false;
        
        protected void Start()
        {
            // Initialize current hit points to max hit points
            currentHitPoints = maxHitPoints;

            position = GetPosition();
            
            // Register the character's initial position with the TileManager
            TileManager.Instance.RegisterCharacterMovement(this, position, position);
        }

        private Vector2Int GetPosition()
        {
            return new Vector2Int()
            {
                x = Mathf.FloorToInt(transform.position.x),
                y = Mathf.FloorToInt(transform.position.y)
            };
        }

        protected void SetPosition(Vector2Int newPosition)
        {
            Debug.Log($"{characterName} is moving from {position} to {newPosition}");
            
            TileManager.Instance.RegisterCharacterMovement(this, position, newPosition);
            position = newPosition;
            transform.position = new Vector3(newPosition.x + 0.5f, newPosition.y + 0.5f, transform.position.z);
        }

        protected void RemainInPlace()
        {
            TileManager.Instance.RemainInPlace(this, position);
        }

        private int GetModifier(int score)
        {
            return Mathf.FloorToInt((score - 10) / 2f);
        }
        
        private int GetAbilityScore(string ability)
        {
            switch (ability.ToUpper())
            {
                case "STR": return strength;
                case "DEX": return dexterity;
                case "CON": return constitution;
                case "INT": return intelligence;
                case "WIS": return wisdom;
                case "CHA": return charisma;
                default: return 0;
            }
        }
        
        private int GetSkillBonus(SkillAbility skill)
        {
            if (!SkillAbilityMap.skillAbilityMap.ContainsKey(skill))
            {
                Debug.LogWarning($"Unknown skill: {skill}");
                return 0;
            }

            string ability = SkillAbilityMap.skillAbilityMap[skill];
            int baseMod = GetModifier(GetAbilityScore(ability));
            
            int proficiencyBonus = proficientSkills.FirstOrDefault(x => x.skillName == skill).bonus;

            return baseMod + proficiencyBonus;
        }

        public int GetAttackCheck()
        {
            // roll a 1d20
            int roll = UnityEngine.Random.Range(1, 21);
            if (roll == 1)
            {
                return int.MinValue;
            }

            if (roll == 20)
            {
                return int.MaxValue;
            }
            return roll + attackBonus;
        }
        
        public int GetDamageRoll()
        {
            // roll a 1d{attackDamageBase} + {attackDamageBonus}
            int roll = UnityEngine.Random.Range(1, attackDamageBase + 1);
            return roll + attackDamageBonus;
        }

        public abstract IEnumerator TakeTurn();
        
        /// <summary>
        /// When the character receives a message, this function is called.
        /// It is also called when a conversation is started by another character.
        /// </summary>
        /// <param name="message">the newest message from the sender</param>
        /// <param name="chattingCharacter">The other target of the chat</param>
        /// <param name="history">the history of the current chat</param>
        /// <param name="sequence">the sequence of the chat(chat stops after sequence 10)</param>
        /// <returns></returns>
        public abstract Task<(string, bool)> ReceiveMessage(string message, Character chattingCharacter, List<ChatEntry> history, int sequence);

        /// <summary>
        /// Clean up the chat state and perform necessary memorization.
        /// </summary>
        public abstract void EndChat(string reason = "");

        public virtual Task ReceiveItem(Item item, string message, Character sender)
        {
            Debug.Log($"{characterName} received {item.Name}*{item.Quantity} from {sender.characterName} with message: {message}");
            // if the item is not in the inventory, add it
            if (Inventory.All(x => x.Name != item.Name))
            {
                Inventory.Add(item);
                if (item.Name == "relic")
                {
                    this.RelicSprite.SetActive(true);
                }
            }
            else
            {
                // if the item is already in the inventory, increase the quantity
                var existingItem = Inventory.FirstOrDefault(x => x.Name == item.Name);
                if (existingItem != null)
                {
                    existingItem.Quantity += item.Quantity;
                    return Task.FromResult(true);
                }
            }

            return Task.FromResult(true);
        }
    }

    [System.Serializable]
    public class Item
    {
        public string Name;
        public int Quantity;
    }
}

