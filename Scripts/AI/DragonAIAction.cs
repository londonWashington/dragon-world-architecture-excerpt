using UnityEngine;

namespace DragonWorld.AI.Utility
{
    /// <summary>
    /// Base class for all Dragon AI Actions.
    /// Actions are evaluated by the Utility Brain and executed if they score highest.
    /// </summary>
    public abstract class DragonAIAction : ScriptableObject
    {
        [Tooltip("List of considerations to evaluate how useful this action is right now.")]
        public AIActionConsideration[] considerations;

        [Tooltip("Base score/weight for this action.")]
        public float baseWeight = 1.0f;

        /// <summary>
        /// Evaluates the total score of this action based on all its considerations.
        /// </summary>
        /// <param name="ctx">The AI data context snapshot.</param>
        /// <returns>A combined utility score (0 to 1).</returns>
        public virtual float Evaluate(AIDataContext ctx)
        {
            if (considerations == null || considerations.Length == 0)
                return baseWeight;

            float score = 1f;

            foreach (var cons in considerations)
            {
                if (cons == null) continue;

                float considerationScore = Mathf.Clamp01(cons.Evaluate(ctx));
                score *= considerationScore; // Multiply or average based on design, multiplying is standard

                // Early exit if score drops to 0 (veto)
                if (score == 0)
                    return 0;
            }

            // Apply a compensation factor if using multiplication with many considerations
            // To prevent scores from becoming too small
            float modificationFactor = 1f - (1f / considerations.Length);
            float makeupValue = (1f - score) * modificationFactor;
            
            return (score + (makeupValue * score)) * baseWeight;
        }

        /// <summary>
        /// Executes the logic of this action when it is selected by the brain.
        /// </summary>
        /// <param name="ctx">The AI data context snapshot.</param>
        public abstract void Execute(AIDataContext ctx);

        /// <summary>
        /// Called when the Brain switches away from this action to a different one.
        /// Useful for cleanup (e.g. stopping firing).
        /// </summary>
        /// <param name="ctx">The AI data context snapshot.</param>
        public virtual void OnActionExited(AIDataContext ctx) {}
    }
}
