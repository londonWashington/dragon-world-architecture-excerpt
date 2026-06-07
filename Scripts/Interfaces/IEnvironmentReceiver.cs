using UnityEngine;

namespace DragonWorld.AI.Utility
{
    /// <summary>
    /// Implemented by actors that react to environmental and weather phenomena (Tornado, Wind, Water).
    /// </summary>
    public interface IEnvironmentReceiver
    {
        bool IsUnderWater { get; }
        void SetUnderwaterState(bool isUnderwater);
        void PlayWaterDrops();
        
        void ApplyTornadoBuffeting(float intensity);
        void AddExternalWind(Vector3 velocity, float turbulence);
        void PlayStruggleVocalization();
        void SetContinuousWaterDrops(float rate);
    }
}