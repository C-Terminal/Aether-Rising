using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Combat.DamageSystem.Health
{
    public class Health : MonoBehaviour, IHealth
    {
        [SerializeField] private int maxHealth = 100;
        public bool IsDead { get; private set; }


        private void Awake()
        {
            // animator = GetComponent<Animator>(); // If Health was directly triggering animations
        }

        private void Start()
        {
            CurrentHealth = maxHealth;
            IsDead = false;
        }

        // Update method for testing was present in original (Listing 3-3)
        // For production, this would typically be removed.
        private void Update()
        {
// #if UNITY_EDITOR // Only include test code in editor
//             // Using new Input System for test
//             var myMouse = Mouse.current;
//             if (myMouse != null && myMouse.rightButton.wasPressedThisFrame) // Changed to right button to avoid conflict
//                 if (gameObject.CompareTag("NPC") || gameObject.CompareTag("Player")) // Allow testing on Player too
//                 {
//                     // Simulate this object being shot by something else
//                     Debug.Log($"Test: Simulating 10 damage to {gameObject.name}");
//                     TakeDamage(10);
//                 }
// #endif
        }
        // private Animator animator; // Animator reference was in the provided script but not used by Health directly

        public int CurrentHealth { get; private set; }

        public void TakeDamage(int amount, string attackerTag)
        {
            if (IsDead) return;

            int finalDamage = amount;
            // ... (damage scaling logic here)

            CurrentHealth -= finalDamage;
            CurrentHealth = Mathf.Max(CurrentHealth, 0);

            if (CurrentHealth == 0)
                Die();
            else
                OnHealthDepleted?.Invoke(gameObject.tag, attackerTag); // Pass victim and attacker tags
        }

        public void Die()
        {
            if (IsDead) return; // Don't die twice
            IsDead = true;
            Debug.Log($"{gameObject.name} has died.");

            if (gameObject.CompareTag("Player"))
                OnPlayerDeath?.Invoke();
            // Handle player death logic here (e.g., show death screen)
            else // Assuming non-player is an NPC
                OnNpcDeath?.Invoke(); // Listened for by NPCStateMachine

            // Death animations are typically handled by the Actions script based on state changes,
            // rather than directly in Health.
        }

        public bool IsAlive => !IsDead;

        // Event triggered when health changes but NPC is not dead yet.
        // The string parameter was used in the original to pass the tag of the damaged object.
        public event Action<string, string> OnHealthDepleted; // (victimTag, attackerTag)
        public event Action OnPlayerDeath; // Specific event if this component is on the player
        public event Action OnNpcDeath; // Specific event if this component is on an NPC
    }
}