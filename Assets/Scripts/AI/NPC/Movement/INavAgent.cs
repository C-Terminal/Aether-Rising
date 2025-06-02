using UnityEngine;

namespace AI.NPC.Movement
{
    public interface INavAgent
    {
        float RemainingDistance { get; }
        bool IsPathPending { get; }
        void SetDestination(Vector3 destination);
    }
}