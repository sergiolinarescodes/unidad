using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Unidad.Core.DOTS
{
    /// <summary>
    /// Static helpers for action-to-navigation bridging. Used by ActionBridgeSystem
    /// and available to game code that overrides specific ActionTypes.
    /// </summary>
    [BurstCompile]
    public static class ActionBridgeUtility
    {
        public static int FindMapping(
            in DynamicBuffer<ActionTargetMappingElement> mappings, int actionType)
        {
            for (int i = 0; i < mappings.Length; i++)
            {
                if (mappings[i].ActionType == actionType)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Find the nearest POI of a given type from the agent's known POIs.
        /// Returns true if found, with position, entity, and distance populated.
        /// </summary>
        public static bool FindNearestPOI(
            in DynamicBuffer<KnownPOIElement> knownPOIs, int poiType, float3 agentPos,
            out float3 bestPos, out Entity bestEntity, out float bestDist)
        {
            bestPos = agentPos;
            bestEntity = Entity.Null;
            bestDist = float.MaxValue;

            for (int i = 0; i < knownPOIs.Length; i++)
            {
                if (knownPOIs[i].POIType != poiType) continue;

                float dist = math.distance(agentPos, knownPOIs[i].Position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestPos = knownPOIs[i].Position;
                    bestEntity = knownPOIs[i].POIEntity;
                }
            }

            return bestEntity != Entity.Null;
        }

        /// <summary>
        /// Clear the command queue and enqueue a single Wait command.
        /// </summary>
        public static void EnqueueWaitCommand(EntityManager em, Entity entity, float duration)
        {
            if (!em.HasBuffer<CommandEntry>(entity) || !em.HasComponent<CommandQueueData>(entity))
                return;

            var qd = em.GetComponentData<CommandQueueData>(entity);
            qd.CurrentIndex = 0;
            qd.IsPaused = false;
            em.SetComponentData(entity, qd);

            var commands = em.GetBuffer<CommandEntry>(entity);
            commands.Clear();
            commands.Add(new CommandEntry
            {
                Type = CommandType.Wait,
                Status = CommandStatus.Pending,
                Duration = duration,
                Elapsed = 0f
            });
        }

        /// <summary>
        /// Claim a POI: increment CurrentUsers and store the claim on the agent.
        /// </summary>
        public static void ClaimPOI(EntityManager em, Entity agent, Entity poiEntity)
        {
            if (!em.HasComponent<POIClaim>(agent)) return;
            if (poiEntity == Entity.Null || !em.HasComponent<PointOfInterest>(poiEntity)) return;

            var poi = em.GetComponentData<PointOfInterest>(poiEntity);
            if (poi.CurrentUsers < poi.Capacity)
            {
                poi.CurrentUsers++;
                em.SetComponentData(poiEntity, poi);
                em.SetComponentData(agent, new POIClaim { POIEntity = poiEntity, POIType = poi.POIType });
            }
        }

        /// <summary>
        /// Release a POI claim: decrement CurrentUsers and clear the claim.
        /// </summary>
        public static void ReleasePOI(EntityManager em, Entity agent)
        {
            if (!em.HasComponent<POIClaim>(agent)) return;

            var claim = em.GetComponentData<POIClaim>(agent);
            if (claim.POIEntity == Entity.Null) return;
            if (!em.Exists(claim.POIEntity) || !em.HasComponent<PointOfInterest>(claim.POIEntity)) return;

            var poi = em.GetComponentData<PointOfInterest>(claim.POIEntity);
            poi.CurrentUsers = math.max(0, poi.CurrentUsers - 1);
            em.SetComponentData(claim.POIEntity, poi);
            em.SetComponentData(agent, new POIClaim { POIEntity = Entity.Null, POIType = 0 });
        }
    }
}
