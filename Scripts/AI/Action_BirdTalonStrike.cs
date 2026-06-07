using UnityEngine;
using DragonWorld.AI.Utility;

namespace DragonWorld.Bird
{
    [CreateAssetMenu(fileName = "Action_BirdTalonStrike", menuName = "DragonWorld/AI/Actions/Bird Talon Strike")]
    public class Action_BirdTalonStrike : DragonAIAction
    {
        [Tooltip("The range within which the bird considers the talon strike valid.")]
        public float validStrikeRange = 20f;

        [Header("Debug")]
        [Tooltip("If true, the bird will constantly try to execute this attack ignoring other conditions.")]
        public bool forceAttackDebug = false;
        
        public override float Evaluate(AIDataContext ctx)
        {
            if (forceAttackDebug) return 1f;

            // Do not spam attack if debug is off and no considerations are set
            if (considerations == null || considerations.Length == 0) return 0f;

            return base.Evaluate(ctx);
        }

        public override void Execute(AIDataContext ctx)
        {
            if (ctx.target != null && ctx.combatActor != null && ctx.flyer != null)
            {
                // Execute the melee strike (Talon Attack in BirdController)
                
                // Get precise aim position if pilot is available
                Vector3 strikeTarget = ctx.target.position;
                var pilot = ctx.flyer.RootTransform.GetComponent<DragonAIPilot>();
                if (pilot != null)
                {
                    strikeTarget = pilot.GetBestMeleeTarget(ctx.target);
                }
                
                ctx.combatActor.ExecuteMeleeAttack(strikeTarget);
            }
        }
    }
}
