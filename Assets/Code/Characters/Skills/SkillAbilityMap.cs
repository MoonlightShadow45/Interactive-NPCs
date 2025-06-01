using System.Collections.Generic;

namespace Code.Characters.Skills
{
    public static class SkillAbilityMap
    {
        public static Dictionary<SkillAbility, string> skillAbilityMap = new ()
        {
            { SkillAbility.Acrobatics, "DEX" },
            { SkillAbility.AnimalHandling, "WIS" },
            { SkillAbility.Arcana, "INT" },
            { SkillAbility.Athletics, "STR" },
            { SkillAbility.Deception, "CHA" },
            { SkillAbility.History, "INT" },
            { SkillAbility.Insight, "WIS" },
            { SkillAbility.Intimidation, "CHA" },
            { SkillAbility.Investigation, "INT" },
            { SkillAbility.Medicine, "WIS" },
            { SkillAbility.Nature, "INT" },
            { SkillAbility.Perception, "WIS" },
            { SkillAbility.Performance, "CHA" },
            { SkillAbility.Persuasion, "CHA" },
            { SkillAbility.Religion, "INT" },
            { SkillAbility.SleightOfHand, "DEX" },
            { SkillAbility.Stealth, "DEX" },
            { SkillAbility.Survival, "WIS" }
        };
        
    }
}