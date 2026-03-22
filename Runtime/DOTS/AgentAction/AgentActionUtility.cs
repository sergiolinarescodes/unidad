using Unity.Burst;
using Unity.Entities;

namespace Unidad.Core.DOTS
{
    [BurstCompile]
    public static class AgentActionUtility
    {
        /// <summary>
        /// Apply a single action effect. This is the central switch that handles all effect types.
        /// Each new module that adds an effect type adds a case here.
        /// </summary>
        public static void ApplyEffect(
            in ActionEffectElement effect,
            ref DynamicBuffer<ResourceElement> resources,
            ref DynamicBuffer<ResourceChangeRecord> resourceChanges,
            in DynamicBuffer<ResourceMaxModifier> maxMods,
            in DynamicBuffer<ResourceMinModifier> minMods,
            ref StateMachineData stateMachine)
        {
            switch (effect.EffectType)
            {
                case ActionEffectType.AddToResource:
                    ResourceUtility.Add(
                        ref resources, ref resourceChanges,
                        in maxMods, in minMods,
                        effect.TargetResourceId, effect.Value);
                    break;

                case ActionEffectType.SetResource:
                    ResourceUtility.Set(
                        ref resources, ref resourceChanges,
                        in maxMods, in minMods,
                        effect.TargetResourceId, effect.Value);
                    break;

                case ActionEffectType.TriggerState:
                    stateMachine.TransitionRequested = true;
                    stateMachine.RequestedState = (int)effect.Value;
                    break;

                // SetNeedUrgency: directly set urgency on NeedElement — handled by caller
                // if NeedElement buffer is available.
                case ActionEffectType.SetNeedUrgency:
                    break;

                // SpawnTimer: requires EntityManager access — handled by caller system.
                case ActionEffectType.SpawnTimer:
                    break;

                // Game-specific ActionEffectTypes (100+) are handled by external systems.
                // The ActionEffectElement buffer persists on the agent until the next action starts,
                // giving game systems time to process custom effects on ActionCompleted.
                default:
                    break;
            }
        }

        /// <summary>
        /// Apply all effects in the buffer.
        /// </summary>
        public static void ApplyAllEffects(
            in DynamicBuffer<ActionEffectElement> effects,
            ref DynamicBuffer<ResourceElement> resources,
            ref DynamicBuffer<ResourceChangeRecord> resourceChanges,
            in DynamicBuffer<ResourceMaxModifier> maxMods,
            in DynamicBuffer<ResourceMinModifier> minMods,
            ref StateMachineData stateMachine)
        {
            for (int i = 0; i < effects.Length; i++)
            {
                var effect = effects[i];
                ApplyEffect(in effect, ref resources, ref resourceChanges,
                    in maxMods, in minMods, ref stateMachine);
            }
        }
    }
}
