using UnityEngine;

namespace AI.NPC.Sensing.Vision
{
    public interface IVisionSensor
    {
        bool CanSeePlayer(Transform player);
    }
}