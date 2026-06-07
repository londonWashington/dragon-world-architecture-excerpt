using UnityEngine;

namespace DragonWorld.AI.Utility
{
    /// <summary>
    /// Package of input commands sent from the AI to the Flyer controller.
    /// This abstracts away the concept of "buttons" (Shift, Jump) and provides raw data.
    /// </summary>
    public struct FlyerInputData
    {
        public Vector3 aimPosition;
        
        /// <summary>
        /// Directional steering inputs (x = yaw, y = pitch). Values [-1, 1].
        /// </summary>
        public Vector2 pitchYawInput;
        
        /// <summary>
        /// Roll steering input. Value [-1, 1].
        /// </summary>
        public float rollInput;
        
        /// <summary>
        /// Movement inputs for forward/backward and strafe.
        /// </summary>
        public Vector2 moveInput;
        
        /// <summary>
        /// True if the AI wants to use sharp turn/maneuverability mode.
        /// </summary>
        public bool useSharpTurn;

        /// <summary>
        /// True if the AI wants to flap its wings / sprint / boost.
        /// </summary>
        public bool isFlapping;
        
        /// <summary>
        /// True if the AI wants to gain altitude explicitly (e.g. "Jump" equivalent)
        /// </summary>
        public bool wantsToGainAltitude;

        public bool isGliding;
    }
}
