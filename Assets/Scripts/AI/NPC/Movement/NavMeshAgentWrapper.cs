using UnityEngine;
using UnityEngine.AI;

namespace AI.NPC.Movement
{
    public class NavMeshAgentWrapper : INavAgent
    {
        private readonly NavMeshAgent agent;

        public NavMeshAgentWrapper(NavMeshAgent agent)
        {
            this.agent = agent;
        }

        public void SetDestination(Vector3 destination) => agent.SetDestination(destination);

        public float RemainingDistance => agent.remainingDistance;

        public bool IsPathPending => agent.pathPending;
    }
}