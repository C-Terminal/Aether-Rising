using AI.FSM.NPC.States;
using Combat.DamageSystem.Health;
using UnityEngine;

// Required for Action

// Forward declare (or assume existence of) state and health components and their events
// Example:
// public class IdleState : MonoBehaviour { public event Action OnNpcIdle; ... }
// public class PatrolState : MonoBehaviour { public event Action OnNpcPatrol; ... }
// public class ChaseState : MonoBehaviour { public event Action OnNpcChase; ... }
// public class AttackState : MonoBehaviour { public event Action OnNpcAttack; ... } // Note: Attack animation might be handled by NPCController now
// public class WanderState : MonoBehaviour { public event Action OnNpcWander; ... }
// public class CoverState : MonoBehaviour { public event Action OnNpcTakeCover; public event Action OnNPCSquat; ...}
// public class HitState : MonoBehaviour { public event Action PlayNpcHitAnim; ...}
// public class DeathState : MonoBehaviour { public event Action PlayNpcDeadAnim; ...}
// public class Health : MonoBehaviour { public event Action OnNpcDeath; public event Action<int> OnNpcDamaged; ...} // Assuming OnNpcDamaged passes damage amount or some indicator


namespace Characters.NPC
{
    [RequireComponent(typeof(Animator))]
    public class Actions : MonoBehaviour
    {
        private Animator _animator;
        private Health _health;

        // References to state components (optional, if subscribing directly)
        // Alternatively, events could be routed through NPCStateMachine
        private IdleState _idleState;
        private PatrolState _patrolState;
        private ChaseState _chaseState;
        private AttackState _attackState; // Consider if attack animation is now driven by NPCController
        private WanderState _wanderState;
        private CoverState _coverState;
        private HitState _hitState;
        private DeathState _deathState;


        const int COUNT_OF_DAMAGE_ANIMATIONS = 3; // As per original text
        int _lastDamageAnimationId = -1;

        void Awake()
        {
            _animator = GetComponent<Animator>();
            _health = GetComponent<Health>(); // Assumes Health component is on the same GameObject

            // Get references to state components if they are on the same GameObject
            _idleState = GetComponent<IdleState>();
            _patrolState = GetComponent<PatrolState>();
            _chaseState = GetComponent<ChaseState>();
            _attackState = GetComponent<AttackState>();
            _wanderState = GetComponent<WanderState>();
            _coverState = GetComponent<CoverState>();
            _hitState = GetComponent<HitState>();
            _deathState = GetComponent<DeathState>();

            if (_animator == null) Debug.LogError("Actions: Animator component not found!", this);
            if (_health == null) Debug.LogWarning("Actions: Health component not found. Damage/Death animations might not work.", this);
        }

        void OnEnable()
        {
            // Subscribe to events from Health component
            if (_health != null)
            {
                _health.OnNpcDeath += HandleDeathAnimation; // Assuming Health script has OnNpcDeath
                // Assuming Health script has an event like OnNpcDamaged (string tag was in original, let's use a simpler one for now or make it generic)
                // For this example, I'll assume a new event `OnNpcTookDamage` on Health script that PlayNpcHitAnim can subscribe to.
                // Or, that HitState invokes its own PlayNpcHitAnim which this script subscribes to.
                // The original text had HitState.PlayNpcHitAnim += Damage;
            }

            // Subscribe to events from State components
            if (_idleState != null) _idleState.OnNpcIdle += HandleIdleAnimation;
            if (_patrolState != null) _patrolState.OnNpcPatrol += HandlePatrolAnimation; // Walk
            if (_chaseState != null) _chaseState.OnNpcChase += HandleChaseAnimation;   // Run
            if (_attackState != null)
            {
                // If NPCController now handles attack animation via CharacterAnimator,
                // this subscription might change or be removed.
                // For now, keeping it as per original FSM design for aiming.
                _attackState.OnNpcAttack += HandleAttackAiming;
            }
            if (_wanderState != null) _wanderState.OnNpcWander += HandlePatrolAnimation; // Walk (reusing patrol anim for wander)
            if (_coverState != null)
            {
                _coverState.OnNpcTakeCover += HandleCrouchingRunAnimation;
                _coverState.OnNPCSquat += HandleSquatAnimation;
            }
            if (_hitState != null) _hitState.PlayNpcHitAnim += HandleDamageAnimation; // From HitState
            if (_deathState != null) _deathState.PlayNpcDeadAnim += HandleDeathAnimation; // From DeathState
        }

        void OnDisable()
        {
            // Unsubscribe from events
            if (_health != null)
            {
                _health.OnNpcDeath -= HandleDeathAnimation;
            }

            if (_idleState != null) _idleState.OnNpcIdle -= HandleIdleAnimation;
            if (_patrolState != null) _patrolState.OnNpcPatrol -= HandlePatrolAnimation;
            if (_chaseState != null) _chaseState.OnNpcChase -= HandleChaseAnimation;
            if (_attackState != null) _attackState.OnNpcAttack -= HandleAttackAiming;
            if (_wanderState != null) _wanderState.OnNpcWander -= HandlePatrolAnimation;
            if (_coverState != null)
            {
                _coverState.OnNpcTakeCover -= HandleCrouchingRunAnimation;
                _coverState.OnNPCSquat -= HandleSquatAnimation;
            }
            if (_hitState != null) _hitState.PlayNpcHitAnim -= HandleDamageAnimation;
            if (_deathState != null) _deathState.PlayNpcDeadAnim -= HandleDeathAnimation;
        }

        // --- Animation Handling Methods ---

        private void HandleIdleAnimation()
        {
            if (_animator == null) return;
            _animator.SetBool("Aiming", false);
            _animator.SetBool("Squat", false);
            _animator.SetFloat("Speed", 0f);
            Debug.Log("Actions: Idle Animation Triggered");
        }

        private void HandlePatrolAnimation() // Used for Patrol and Wander
        {
            if (_animator == null) return;
            _animator.SetBool("Aiming", false);
            _animator.SetBool("Squat", false);
            _animator.SetFloat("Speed", 0.5f); // Example speed for walk
            Debug.Log("Actions: Patrol/Wander Animation Triggered");
        }

        private void HandleChaseAnimation() // Run
        {
            if (_animator == null) return;
            _animator.SetBool("Aiming", false);
            _animator.SetBool("Squat", false);
            _animator.SetFloat("Speed", 1.0f); // Example speed for run (original used 0.7f)
            Debug.Log("Actions: Chase Animation Triggered");
        }

        private void HandleCrouchingRunAnimation()
        {
            if (_animator == null) return;
            _animator.SetBool("Squat", true);
            _animator.SetFloat("Speed", 0.7f); // Example speed for crouching run
            Debug.Log("Actions: Crouching Run Animation Triggered");
        }

        // This method might conflict/be superseded by the new NPCController's attack sequence.
        // It was originally used to set aiming and trigger a one-shot attack.
        // If NPCController handles attack animation, this might only set "Aiming".
        private void HandleAttackAiming()
        {
            if (_animator == null) return;
            _animator.SetBool("Aiming", true);
            _animator.SetBool("Squat", false);
            _animator.SetFloat("Speed", 0f);
            // Original also did: _animator.SetTrigger("Attack");
            // This might not be needed if NPCController's CharacterAnimator.SetAttack(true/false) is used.
            // For now, let's keep it to reflect the original FSM's intent for Actions.cs.
            // If your new NPCController manages the SetAttack(bool) on CharacterAnimator,
            // then the FSM's AttackState might not invoke OnNpcAttack, or this handler does less.
            _animator.SetTrigger("Attack"); // Assuming a trigger-based attack animation
            Debug.Log("Actions: Attack Aiming / Trigger Animation Triggered");
        }

        private void HandleDeathAnimation()
        {
            if (_animator == null) return;
            _animator.SetFloat("Speed", 0f);
            _animator.SetBool("Aiming", false);
            _animator.SetBool("Squat", false);
            _animator.SetTrigger("Death"); // Assuming "Death" is a trigger parameter
            Debug.Log("Actions: Death Animation Triggered");
        }

        private void HandleDamageAnimation()
        {
            if (_animator == null || (_health != null && _health.IsDead)) return; // Don't play if dead

            // Ensure not already in a death animation state
            if (_animator.GetCurrentAnimatorStateInfo(0).IsName("Death")) return; // Adjust "Death" if your state name is different

            int damageAnimId = UnityEngine.Random.Range(0, COUNT_OF_DAMAGE_ANIMATIONS);
            if (COUNT_OF_DAMAGE_ANIMATIONS > 1)
            {
                while (damageAnimId == _lastDamageAnimationId)
                {
                    damageAnimId = UnityEngine.Random.Range(0, COUNT_OF_DAMAGE_ANIMATIONS);
                }
            }
            _lastDamageAnimationId = damageAnimId;

            _animator.SetInteger("DamageID", damageAnimId); // Assuming "DamageID" is an integer parameter
            _animator.SetTrigger("Damage"); // Assuming "Damage" is a trigger parameter
            Debug.Log($"Actions: Damage Animation Triggered (ID: {damageAnimId})");
        }

        public void HandleSquatAnimation()
        {
            if (_animator == null) return;
            _animator.SetBool("Squat", true);
            _animator.SetBool("Aiming", false);
            _animator.SetFloat("Speed", 0f);
            Debug.Log("Actions: Squat Animation Triggered");
        }

        // Optional: A generic method if states pass animation parameters directly
        // public void SetAnimationParameters(float speed, bool isAiming, bool isSquatting) { ... }
    }
}