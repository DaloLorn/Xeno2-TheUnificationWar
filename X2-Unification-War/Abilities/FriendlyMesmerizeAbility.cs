using Artitas;
using Artitas.Utils;
using Common;
using Common.Boards;
using Common.FSM.Systems;
using Common.RPG;
using Common.UI.Toasts;
using Common.Util;
using System;
using System.Collections.Generic;
using UnityEngine;
using Xenonauts.Common.Systems;
using Xenonauts.GroundCombat;
using Xenonauts.GroundCombat.Abilities.Shoot;
using Xenonauts.GroundCombat.Animation;
using Xenonauts.GroundCombat.Animation.Acts;
using Xenonauts.GroundCombat.Components;
using Xenonauts.GroundCombat.Events;
using Xenonauts.GroundCombat.Utils;

#nullable disable
namespace X2UnificationWar.Abilities {

  /** A variant of the 7.20.4 MesmerizeAbility with the following changes:
    - No rollRange argument (always rolls 1-100)
    - No filter against the attacker being player-controlled (so defectors can use it)
    - Refined psi conflict formula: d100 * PsiStrength versus Morale * Bravery (was d100-1 versus 2*(Bravery-25) )
      - TODO: Consider tweaking it to use the same formula as Mind Control.
  */ 
  public class FriendlyMesmerizeAbility
  {
    public static AbilityDefinitionBuilder Create(
      string name,
      int moraleDamage,
      int usesPerTurn)
    {
      return new AbilityDefinitionBuilder()
      {
        ID = name,
        Name = name,
        Description = "Do morale damage when attacking inside vision cone of this unit",
        Variables = {
          {
            GroundCombatConstants.KEY_DAMAGE,
            new XRange( moraleDamage,  moraleDamage,  moraleDamage)
          },
          {
            GroundCombatConstants.KEY_USES,
            new XRange(0.0f,  usesPerTurn,  usesPerTurn)
          }
        },
        Metadata = {
          {
            GroundCombatConstants.META_KEY_DESCRIPTION,
            "Do morale damage when attacking inside vision cone of this unit"
          },
          {
            GroundCombatConstants.META_USER_INTERACTIVE,
            false
          },
          {
            GroundCombatConstants.META_ACTIVATE_ON_CREATION,
            true
          }
        },
        CastPrecondition = (ability, unitCaster, rawConflict) => true,
        TargetPrecondition = (ability, unitCaster, targetUnit, rawConflict) => targetUnit.IsCombatantConscious(),
        Effects = {
          EffectDSL<AbilityState>.OnStart( state => state.Source.Abilities().FindAbility(GroundCombatConstants.NAME_ABILITY_MIND_CONTROL_MESMERIZE)),
          EffectDSL<AbilityState>.Any(EffectDSL<AbilityState>.OnEach<MeleeImpactReport, MeleeMissEvent, ShotSequenceFinishedReport>( (state, effect, report) =>
          {
            IRange variable = state.GetVariable(GroundCombatConstants.KEY_USES);
            if ((double) variable.Value <= 0.0)
              return false;
            bool flag = true;
            Entity target;
            Entity attacker;
            switch (report)
            {
              case ShotSequenceFinishedReport _:
                ShotSequenceFinishedReport sequenceFinishedReport = report as ShotSequenceFinishedReport;
                target = sequenceFinishedReport.Conflict.Target;
                attacker = sequenceFinishedReport.Conflict.Attacker;
                if (sequenceFinishedReport.Conflict.IsArc)
                {
                  flag = false;
                  break;
                }
                if (sequenceFinishedReport.Sequence != null)
                {
                  using (List<Shot>.Enumerator enumerator = sequenceFinishedReport.Sequence.GetEnumerator())
                  {
                    while (enumerator.MoveNext())
                    {
                      foreach (Projectile projectile in enumerator.Current.projectiles)
                      {
                        if (!(projectile.ImpactEntity != target))
                        {
                          flag = false;
                          break;
                        }
                      }
                    }
                    break;
                  }
                }
                break;
              case MeleeImpactReport _:
                IImpactReport impactReport = (IImpactReport) report;
                target = impactReport.Conflict.Target;
                attacker = impactReport.Conflict.Attacker;
                flag = target != impactReport.Target;
                break;
              default:
                MeleeMissEvent meleeMissEvent = (MeleeMissEvent) report;
                attacker = meleeMissEvent.Conflict.Attacker;
                target = meleeMissEvent.Conflict.Target;
                flag = true;
                break;
            }
            if (!flag || state.Source != target || attacker.GCCombatantMeta().IsPsionicInvulnerable() || state.Source.IsMemberOfSuppressed() || !SightSystem.IsCombatantVisibleFor(state.Source, attacker, true))
              return false;
            Entity entityPerformingMesmerize = target;
            Entity entityBeingMesmerized = attacker;
            double psiStrength = (double) target.PsionicStrength().Value;
            bool resistedMesmerize =  (Constants.RNG.Next(1, 101) * psiStrength) < ((double) attacker.Bravery().Value * (double) attacker.Morale().Value);
            Action immediateMethod =  () =>
            {
              if (!entityBeingMesmerized.IsCombatantConscious())
                return;
              World world = state.Source.World;
              world.HandleEvent(new AnimatorTriggerCommand(entityPerformingMesmerize, CombatantAnimatorParameters.Bespoke));
              world.HandleEvent(new AnimatorTriggerCommand(entityPerformingMesmerize, CombatantAnimatorParameters.IdleState, false));
            };
            AddressComponent addressComponent = state.Source.Address();
            Action activateMethod =  () =>
            {
              if (!entityBeingMesmerized.IsCombatantConscious())
                return;
              World world = state.Source.World;
              world.HandleEvent(new AnimatorTriggerCommand(entityPerformingMesmerize, CombatantAnimatorParameters.IdleState));
              world.HandleEvent(new AnimatorTriggerCommand(entityPerformingMesmerize, CombatantAnimatorParameters.Bespoke, false));
              GroundCombatCameraCutoffSystem.MoveCameraToCenterOn(world, (Address)  entityBeingMesmerized.Address(), 0.25f);
              XenonautsAudioSystem.PlayOneShot( GroundCombatAudioSystem.MIND_WAR_ATTACK,  GroundCombatConstants.Audio.Materials.MIND_WAR, (Optional<Vector3>) state.Source.Transformation().position);
              if (resistedMesmerize)
              {
                world.QueueEvent(new ShowToastCommand(string.Format((string) GroundCombatTranslation.MESSAGE_MESMERIZE_MORALE_CHECK_FAIL,  entityBeingMesmerized.Name()), GroundCombatConstants.MESMERIZE_TOAST_LIFETIME));
              }
              else
              {
                int num = Constants.RNG.Next(0, state.GetVariable(GroundCombatConstants.KEY_DAMAGE).Value.Round());
                entityBeingMesmerized.TimeUnitsToMinimum();
                entityBeingMesmerized.DeltaMorale( -num);
                world.QueueEvent(new ShowToastCommand(string.Format((string) GroundCombatTranslation.MESSAGE_MESMERIZE_MORALE_CHECK_SUCCEED,  entityBeingMesmerized.Name()), GroundCombatConstants.MESMERIZE_TOAST_LIFETIME));
              }
            };
            int priority = 1;
            EntityActCommand @event = new( new ObservedActParameter(entityBeingMesmerized, immediateMethod, activateMethod,  () => { }, (Optional<Vector3>) addressComponent.value.position, GroundCombatConstants.CameraTracking.MIND_WAR_BEFORE_EVENT, GroundCombatConstants.CameraTracking.MIND_WAR_AFTER_EVENT, "mesmerize_{0}_{1}".Format( state.Source.ID,  state.Target.ID), priority,
            [
              entityPerformingMesmerize
            ]), false);
            state.Source.World.HandleEvent(@event);
            variable.Value =  (variable.Value - 1f).Round();
            return false;
          }), EffectDSL<AbilityState>.OnEach<PostCombatantStunReport, PostCombatantDeathReport>( (state, effect, raw) =>
          {
            IPostCombatantStatusReport combatantStatusReport = (IPostCombatantStatusReport) raw;
            return combatantStatusReport.Actor == state.Source && combatantStatusReport.Status != LifeStatusComponent.Status.Conscious;
          }), EffectDSL<AbilityState>.OnEach<PhaseStepReport<GCPhases.TurnBegin, Steps.End>>( (state, effect, raw) =>
          {
            state.GetVariable(GroundCombatConstants.KEY_USES).ToMaximum();
            return false;
          }))
        }
      };
    }
  }

}