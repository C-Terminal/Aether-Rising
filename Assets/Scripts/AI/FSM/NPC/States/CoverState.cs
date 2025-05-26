using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using System;

namespace AI.FSM.NPC.States
{
    public class CoverState : MonoBehaviour, IState
    {
        [Tooltip("Speed at which NPC moves to cover")]
        [SerializeField] private float npcSpeed = 3.5f;
        [Tooltip("Maximum distance to search for cover")]
        [SerializeField] private float maxCoverSearchDistance = 15f;
        [Tooltip("Minimum distance from player for valid cover")]
        [SerializeField] private float minDistanceFromPlayer = 5f;
        [Tooltip("Layers that count as valid cover")]
        [SerializeField] private LayerMask coverLayers;
        [Tooltip("Time to hide before peeking")]
        [SerializeField] private float timeInCover = 4f;
        [Tooltip("Duration of peek action")]
        [SerializeField] private float peekDuration = 1.5f;
        [Tooltip("Chance to return to normal behavior when health recovers")]
        [Range(0, 1)]
        [SerializeField] private float returnToNormalChance = 0.3f;
        [Tooltip("Chance to peek out and potentially attack")]
        [Range(0, 1)]
        [SerializeField] private float peekChance = 0.7f;
        [Tooltip("Health threshold to consider returning to normal behavior")]
        [SerializeField] private float healthRecoveryThreshold = 60f;
        [Tooltip("Maximum time to stay in cover")]
        [SerializeField] private float maxCoverTime = 20f;

        // Events for external systems like animation/audio
        public event Action OnNpcTakingCover;
        public event Action OnNpcPeeking;

        private FSM.StateMachineNew _stateMachineNew;
        private NavMeshAgent agent;
        private float coverTimer = 0f;
        private float totalCoverTime = 0f;
        private bool inCoverPosition = false;
        private bool isPeeking = false;
        private Vector3 coverPosition;
        private Vector3 coverNormal;
        private Coroutine peekCoroutine;

        void Awake()
        {
            _stateMachineNew = GetComponent<FSM.StateMachineNew>();
            if (_stateMachineNew == null) Debug.LogError($"[CoverState - {gameObject.name}] : NPCStateMachine not found.");
            
            agent = GetComponent<NavMeshAgent>();
            if (agent == null) Debug.LogError($"[CoverState - {gameObject.name}] : No NavMesh Agent found.");
        }

        public void OnStateEnter()
        {
            //TODO: move these to better locations
            OnNpcTakeCover?.Invoke();
            OnNPCSquat?.Invoke();
            Debug.Log("CoverState: Enter");
            
            if (agent != null && agent.enabled)
            {
                agent.speed = npcSpeed;
                agent.isStopped = false;
            }
            
            // Reset state variables
            coverTimer = 0f;
            totalCoverTime = 0f;
            inCoverPosition = false;
            isPeeking = false;
            
            // Try to find cover
            bool foundCover = FindCoverPosition(out coverPosition, out coverNormal);
            if (foundCover)
            {
                
                Debug.Log($"CoverState: Found cover at {coverPosition}");
                MoveToPosition(coverPosition);
                OnNpcTakingCover?.Invoke();
            }
            else
            {
                // If no cover found, fall back to moving away from player
                Debug.Log("CoverState: No cover found, falling back to retreat");
                Retreat();
            }
        }

        public void OnStateUpdate(float deltaTime)
        {
            // Update timers
            coverTimer += deltaTime;
            totalCoverTime += deltaTime;
            
            // Check if total time in cover state has exceeded maximum
            if (totalCoverTime > maxCoverTime)
            {
                Debug.Log("CoverState: Max cover time exceeded, returning to normal behavior");
                ReturnToNormalBehavior();
                return;
            }
            
            // Check if health has recovered enough to potentially return to normal
            if (_stateMachineNew.NpcHealth != null && 
                _stateMachineNew.NpcHealth.CurrentHealth > healthRecoveryThreshold &&
                !isPeeking && inCoverPosition)
            {
                if (UnityEngine.Random.value < returnToNormalChance * (coverTimer / timeInCover))
                {
                    Debug.Log("CoverState: Health recovered, returning to normal behavior");
                    ReturnToNormalBehavior();
                    return;
                }
            }
            
            // Check if we've reached cover position
            if (!inCoverPosition && agent != null && agent.enabled && 
                !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance)
            {
                Debug.Log("CoverState: Reached cover position");
                inCoverPosition = true;
                
                // Orient to face away from player (behind cover)
                if (_stateMachineNew.Player != null)
                {
                    OrientBehindCover();
                }
            }
            
            // If in cover and not peeking, see if it's time to peek
            if (inCoverPosition && !isPeeking && coverTimer >= timeInCover)
            {
                if (UnityEngine.Random.value < peekChance)
                {
                    Debug.Log("CoverState: Peeking from cover");
                    StartPeeking();
                }
                else
                {
                    // Reset cover timer but stay in cover
                    coverTimer = 0f;
                }
            }
            
            // If player gets too close to cover, find new cover
            if (inCoverPosition && _stateMachineNew.Player != null && 
                Vector3.Distance(transform.position, _stateMachineNew.Player.position) < minDistanceFromPlayer)
            {
                Debug.Log("CoverState: Player too close, finding new cover");
                bool foundNewCover = FindCoverPosition(out Vector3 newCoverPos, out Vector3 newCoverNormal);
                if (foundNewCover)
                {
                    coverPosition = newCoverPos;
                    coverNormal = newCoverNormal;
                    inCoverPosition = false;
                    coverTimer = 0f;
                    MoveToPosition(coverPosition);
                }
                else
                {
                    // If no new cover found, fight or flee
                    if (_stateMachineNew.NpcHealth.CurrentHealth > healthRecoveryThreshold * 0.7f)
                    {
                        Debug.Log("CoverState: No new cover available and health OK, engaging player");
                        EngagePlayer();
                    }
                    else
                    {
                        Debug.Log("CoverState: No new cover available and low health, retreating");
                        Retreat();
                    }
                }
            }
        }

        public void OnStateExit()
        {
            Debug.Log("CoverState: Exit");
            
            // Clean up peek coroutine if running
            if (peekCoroutine != null)
            {
                StopCoroutine(peekCoroutine);
                peekCoroutine = null;
            }
            
            isPeeking = false;
        }
        
        private bool FindCoverPosition(out Vector3 bestCoverPos, out Vector3 coverNormal)
        {
            bestCoverPos = transform.position;
            coverNormal = Vector3.zero;
            
            if (_stateMachineNew.Player == null) return false;
            
            // Start with a failed result
            bool foundCover = false;
            float bestCoverScore = 0f;
            
            // Get player position for reference
            Vector3 playerPos = _stateMachineNew.Player.position;
            
            // Create a list of potential cover points to check
            List<Vector3> potentialCoverPoints = GeneratePotentialCoverPoints();
            
            foreach (Vector3 point in potentialCoverPoints)
            {
                // Check if point is on NavMesh
                NavMeshHit navHit;
                if (!NavMesh.SamplePosition(point, out navHit, 2f, NavMesh.AllAreas))
                    continue;
                
                // Cast ray from point to player to check for obstacles
                Vector3 dirToPlayer = playerPos - navHit.position;
                float distToPlayer = dirToPlayer.magnitude;
                dirToPlayer.Normalize();
                
                RaycastHit hit;
                if (Physics.Raycast(navHit.position, dirToPlayer, out hit, distToPlayer, coverLayers))
                {
                    // Calculate a cover score based on distance from player and cover quality
                    float coverScore = EvaluateCoverQuality(navHit.position, hit.point, playerPos);
                    
                    if (coverScore > bestCoverScore)
                    {
                        bestCoverScore = coverScore;
                        bestCoverPos = navHit.position;
                        coverNormal = hit.normal;
                        foundCover = true;
                    }
                }
            }
            
            return foundCover;
        }
        
        private List<Vector3> GeneratePotentialCoverPoints()
        {
            List<Vector3> points = new List<Vector3>();
            
            // Get obstacles in the environment that could be cover
            Collider[] potentialCover = Physics.OverlapSphere(transform.position, maxCoverSearchDistance, coverLayers);
            
            foreach (Collider col in potentialCover)
            {
                // Skip small objects
                if (col.bounds.size.magnitude < 1f) continue;
                
                // Sample points around the edges of objects
                Vector3 center = col.bounds.center;
                Vector3 extents = col.bounds.extents;
                
                // Generate points at the corners and edges
                for (int x = -1; x <= 1; x += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 cornerPos = center + new Vector3(extents.x * x, 0, extents.z * z);
                        points.Add(cornerPos);
                        
                        // Add extra points along edges
                        points.Add(center + new Vector3(extents.x * x, 0, 0));
                        points.Add(center + new Vector3(0, 0, extents.z * z));
                    }
                }
            }
            
            // Add extra sample points in a radius around NPC
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI * 2f / 16;
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                points.Add(transform.position + direction * maxCoverSearchDistance * 0.7f);
            }
            
            return points;
        }
        
        private float EvaluateCoverQuality(Vector3 agentPos, Vector3 coverPos, Vector3 playerPos)
        {
            float score = 0f;
            
            // Distance from player (further is better, up to a point)
            float distFromPlayer = Vector3.Distance(agentPos, playerPos);
            score += Mathf.Clamp(distFromPlayer / 5f, 1f, 3f);
            
            // Distance from current position (closer is better)
            float distFromSelf = Vector3.Distance(agentPos, transform.position);
            score += (maxCoverSearchDistance - distFromSelf) / maxCoverSearchDistance * 2f;
            
            // Cover thickness (thicker is better)
            RaycastHit[] hits = Physics.RaycastAll(playerPos, (coverPos - playerPos).normalized, maxCoverSearchDistance, coverLayers);
            score += hits.Length * 0.5f;
            
            // Check if position is valid (not too close to player)
            if (distFromPlayer < minDistanceFromPlayer)
            {
                score = 0f; // Invalid cover
            }
            
            return score;
        }
        
        private void MoveToPosition(Vector3 position)
        {
            if (agent != null && agent.enabled)
            {
                agent.SetDestination(position);
            }
        }
        
        private void OrientBehindCover()
        {
            if (_stateMachineNew.Player == null) return;
            
            // Orient to face away from player, using cover normal as reference
            Vector3 toPlayer = _stateMachineNew.Player.position - transform.position;
            toPlayer.y = 0;
            
            // If we have a valid cover normal, use it for orientation
            if (coverNormal != Vector3.zero)
            {
                coverNormal.y = 0;
                Quaternion targetRotation = Quaternion.LookRotation(-coverNormal.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 0.5f);
            }
            else
            {
                // Otherwise just face away from player
                Quaternion targetRotation = Quaternion.LookRotation(-toPlayer.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 0.5f);
            }
        }
        
        private void StartPeeking()
        {
            if (peekCoroutine != null)
            {
                StopCoroutine(peekCoroutine);
            }
            
            peekCoroutine = StartCoroutine(PeekFromCover());
        }
        
        private IEnumerator PeekFromCover()
        {
            isPeeking = true;
            
            // Notify listeners for animation/audio cues
            OnNpcPeeking?.Invoke();
            
            // Turn to face the player for peeking
            if (_stateMachineNew.Player != null)
            {
                Vector3 toPlayer = _stateMachineNew.Player.position - transform.position;
                toPlayer.y = 0;
                Quaternion peekRotation = Quaternion.LookRotation(toPlayer.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, peekRotation, 0.8f);
            }
            
            // Wait for peek duration
            yield return new WaitForSeconds(peekDuration);
            
            // Based on situation, decide next action
            // Check if player is visible/attackable during peek
            if (_stateMachineNew.IsPlayerVisible())
            {
                // Player visible, decide whether to attack or stay in cover
                float healthPercentage = 0f;
                if (_stateMachineNew.NpcHealth != null)
                {
                    healthPercentage = _stateMachineNew.NpcHealth.CurrentHealth / 100f; // Assuming max health is 100
                }
                
                // If player is attackable and NPC has moderate health, engage
                if (_stateMachineNew.IsPlayerAttackable() && healthPercentage > 0.4f)
                {
                    Debug.Log("CoverState: Player visible during peek, engaging");
                    EngagePlayer();
                }
                else
                {
                    // Return to cover position
                    Debug.Log("CoverState: Returning to cover after peek");
                    OrientBehindCover();
                    isPeeking = false;
                    coverTimer = 0f; // Reset cover timer
                }
            }
            else
            {
                // Player not visible, return to cover
                Debug.Log("CoverState: Player not visible during peek, staying in cover");
                OrientBehindCover();
                isPeeking = false;
                coverTimer = 0f; // Reset cover timer
            }
            
            peekCoroutine = null;
        }
        
        private void ReturnToNormalBehavior()
        {
            Debug.Log("CoverState: Returning to normal behavior");
            
            // Find the appropriate state to transition to
            IState nextState = null;
            
            // If player is visible, go to chase state
            if (_stateMachineNew.IsPlayerVisible())
            {
                nextState = _stateMachineNew.states.Find(s => s.GetType().Name == "ChaseState");
                if (nextState == null)
                {
                    nextState = _stateMachineNew.states.Find(s => s.GetType().Name == "IdleState");
                }
            }
            else
            {
                // If player not visible, go to patrol or idle state
                nextState = _stateMachineNew.states.Find(s => s.GetType().Name == "PatrolState");
                if (nextState == null)
                {
                    nextState = _stateMachineNew.states.Find(s => s.GetType().Name == "IdleState");
                }
            }
            
            if (nextState != null)
            {
                _stateMachineNew.SwitchState(nextState);
            }
            else
            {
                Debug.LogWarning("CoverState: No valid state to transition to!");
            }
        }
        
        private void Retreat()
        {
            Debug.Log("CoverState: Retreating from player");
            
            if (_stateMachineNew.Player == null || agent == null) return;
            
            // Find a direction away from the player
            Vector3 dirAwayFromPlayer = transform.position - _stateMachineNew.Player.position;
            dirAwayFromPlayer.y = 0; // Keep it on horizontal plane
            dirAwayFromPlayer = dirAwayFromPlayer.normalized;
            
            // Find a point in that direction to retreat to
            Vector3 retreatPoint = transform.position + dirAwayFromPlayer * maxCoverSearchDistance;
            
            // Check if the point is on the NavMesh
            NavMeshHit navHit;
            if (NavMesh.SamplePosition(retreatPoint, out navHit, maxCoverSearchDistance, NavMesh.AllAreas))
            {
                Debug.Log($"CoverState: Retreating to {navHit.position}");
                MoveToPosition(navHit.position);
                inCoverPosition = false;
                coverTimer = 0f;
            }
            else
            {
                // If we couldn't find a valid retreat point, try to find any cover
                Debug.Log("CoverState: Couldn't find valid retreat point, searching for any cover");
                bool foundCover = FindCoverPosition(out Vector3 newCoverPos, out Vector3 newCoverNormal);
                if (foundCover)
                {
                    coverPosition = newCoverPos;
                    coverNormal = newCoverNormal;
                    MoveToPosition(coverPosition);
                    inCoverPosition = false;
                    coverTimer = 0f;
                }
                else
                {
                    // Last resort - try to engage or find a random point
                    if (_stateMachineNew.NpcHealth.CurrentHealth > healthRecoveryThreshold * 0.4f)
                    {
                        EngagePlayer();
                    }
                    else
                    {
                        // Find any random point on the NavMesh
                        Vector3 randomPoint = transform.position + UnityEngine.Random.insideUnitSphere * maxCoverSearchDistance;
                        if (NavMesh.SamplePosition(randomPoint, out navHit, maxCoverSearchDistance, NavMesh.AllAreas))
                        {
                            MoveToPosition(navHit.position);
                            inCoverPosition = false;
                            coverTimer = 0f;
                        }
                    }
                }
            }
        }
        
        private void EngagePlayer()
        {
            Debug.Log("CoverState: Engaging player");
            
            // Find and switch to attack or chase state
            IState attackState = _stateMachineNew.states.Find(s => s.GetType().Name == "AttackState");
            if (attackState != null)
            {
                _stateMachineNew.SwitchState(attackState);
                return;
            }
            
            IState chaseState = _stateMachineNew.states.Find(s => s.GetType().Name == "ChaseState");
            if (chaseState != null)
            {
                _stateMachineNew.SwitchState(chaseState);
                return;
            }
            
            // If no attack or chase state found, default to idle
            IState idleState = _stateMachineNew.states.Find(s => s.GetType().Name == "IdleState");
            if (idleState != null)
            {
                _stateMachineNew.SwitchState(idleState);
            }
            else
            {
                Debug.LogWarning("CoverState: No valid state to transition to for engaging player!");
            }
        }

        public event Action OnNpcTakeCover;
        public event Action OnNPCSquat;
    }
}