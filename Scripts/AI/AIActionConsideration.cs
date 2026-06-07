using UnityEngine;

namespace DragonWorld.AI.Utility
{
    /// <summary>
    /// Base class for all AI considerations (evaluators).
    /// Considerations return a score between 0.0 and 1.0 based on the context.
    /// </summary>
    public abstract class AIActionConsideration : ScriptableObject
    {
        [Tooltip("Evaluation curve to map the input value to a utility score (0-1).")]
        public AnimationCurve responseCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        /// <summary>
        /// Evaluates the consideration based on the provided context.
        /// </summary>
        /// <param name="ctx">The current AI data context.</param>
        /// <returns>A score between 0.0 (useless) and 1.0 (highly desired).</returns>
        public abstract float Evaluate(AIDataContext ctx);
    }
}
