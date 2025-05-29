using UnityEngine;

namespace AI.FSM
{
    public interface IPerceptionAwareFSM
    {
        void NotifyPlayerInDetectionZone(bool inZone, Transform player);
        void NotifyPlayerLostSight();
        void ConfirmPlayerVisibilityAndEngage();
    }
}