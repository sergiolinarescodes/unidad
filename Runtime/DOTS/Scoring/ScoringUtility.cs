using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace Unidad.Core.DOTS
{
    [BurstCompile]
    public static class ScoringUtility
    {
        /// <summary>
        /// Evaluates a response curve given an input in [0..1]. Returns clamped [0..1].
        /// </summary>
        public static float EvaluateCurve(
            ResponseCurveType curveType, float input,
            float a, float b, float c, float d)
        {
            float result;
            switch (curveType)
            {
                case ResponseCurveType.Linear:
                    result = a * input + b;
                    break;
                case ResponseCurveType.Quadratic:
                    float diff = input - b;
                    result = a * diff * diff + c;
                    break;
                case ResponseCurveType.Logistic:
                    result = 1f / (1f + math.exp(-a * (input - b)));
                    break;
                case ResponseCurveType.Step:
                    result = input >= a ? b : c;
                    break;
                case ResponseCurveType.Exponential:
                    result = a * math.exp(b * input) + c;
                    break;
                case ResponseCurveType.Inverse:
                    result = a / (input + b) + c;
                    break;
                default:
                    result = input;
                    break;
            }
            return math.clamp(result, 0f, 1f);
        }

        /// <summary>
        /// Compensation factor for geometric mean of N considerations.
        /// Prevents actions with many considerations from being penalized.
        /// Uses the Dill/Mark modification factor: score^(1/N).
        /// </summary>
        public static float CompensatedScore(float rawProduct, int considerationCount)
        {
            if (considerationCount <= 1 || rawProduct <= 0f)
                return rawProduct;
            return math.pow(rawProduct, 1f / considerationCount);
        }

        /// <summary>
        /// Returns the index of the action timestamp for a given actionId, or -1.
        /// </summary>
        public static int FindTimestamp(
            in DynamicBuffer<ActionTimestampElement> timestamps, int actionId)
        {
            for (int i = 0; i < timestamps.Length; i++)
            {
                if (timestamps[i].ActionId == actionId)
                    return i;
            }
            return -1;
        }
    }
}
