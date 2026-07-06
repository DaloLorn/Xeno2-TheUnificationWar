using Common.RPG;
using System;
using X2UnificationWar.Abilities;
using Xenonauts.GroundCombat;
using Xenonauts.GroundCombat.Components;

#nullable disable
namespace X2UnificationWar.Components {
    public class FriendlyMesmerizeAbilityDefinition : TriggeredAbilityDefinition
    {
        public int moraleDamage = 50;
        public int uses = 1;

        public FriendlyMesmerizeAbilityDefinition()
            : base(GroundCombatConstants.NAME_ABILITY_MESMERIZE)
        {
        }

        public override AbilityDefinitionBuilder BuildAbilityDefinition()
        {
            return FriendlyMesmerizeAbility.Create(Name, moraleDamage, Math.Max(1, uses));
        }

        public override string ToString()
        {
            return $"{GetType().Name.Replace("AbilityDefinition", "")} [Morale Damage {moraleDamage}, Uses {uses}]";
        }

        protected bool Equals(FriendlyMesmerizeAbilityDefinition other)
        {
            return Equals((GCAbilityDefinition) other) && moraleDamage == other.moraleDamage;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            return false;
            if (this == obj)
            return true;
            return !(obj.GetType() != GetType()) && Equals((FriendlyMesmerizeAbilityDefinition) obj);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode() * 397 ^ moraleDamage;
        }
    }

}