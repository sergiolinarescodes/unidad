using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Unidad.Core.DOTS
{
    [BurstCompile]
    public static class SocialUtility
    {
        public static int FindRelationship(in DynamicBuffer<RelationshipElement> relationships, int targetAgentId)
        {
            for (int i = 0; i < relationships.Length; i++)
            {
                if (relationships[i].TargetAgentId == targetAgentId)
                    return i;
            }
            return -1;
        }

        public static float GetTrust(in DynamicBuffer<RelationshipElement> relationships, int targetAgentId)
        {
            int idx = FindRelationship(in relationships, targetAgentId);
            return idx >= 0 ? relationships[idx].Trust : 0f;
        }

        public static void ModifyTrust(ref DynamicBuffer<RelationshipElement> relationships,
            int targetAgentId, float delta, double currentTime)
        {
            int idx = FindRelationship(in relationships, targetAgentId);
            if (idx >= 0)
            {
                var rel = relationships[idx];
                rel.Trust = math.clamp(rel.Trust + delta, -1f, 1f);
                rel.Familiarity = math.clamp(rel.Familiarity + 0.1f, 0f, 1f);
                rel.InteractionCount++;
                rel.LastInteractionTime = currentTime;
                relationships[idx] = rel;
            }
            else
            {
                relationships.Add(new RelationshipElement
                {
                    TargetAgentId = targetAgentId,
                    Trust = math.clamp(delta, -1f, 1f),
                    Familiarity = 0.1f,
                    InteractionCount = 1,
                    LastInteractionTime = currentTime
                });
            }
        }
    }
}
