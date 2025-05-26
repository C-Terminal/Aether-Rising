using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System;

namespace AI.FSM.NPC.States
{
    public class DeathState : MonoBehaviour, IState
    {
        [Tooltip("Delay before destroying the GameObject after death")]
        [SerializeField] private float destroyDelay = 5f;
        [Tooltip("Enable ragdoll physics on death")]
        [SerializeField] private bool enableRagdoll = true;
        [Tooltip("Drop item chance")]
        [Range(0, 1)]
        [SerializeField] private float dropItemChance = 0.7f;
        [Tooltip("Prefab to spawn as dropped item")]
        [SerializeField] private GameObject itemDropPrefab;
        [Tooltip("Enable death particle effects")]
        [SerializeField] private bool enableDeathEffect = true;
        [Tooltip("Particle system for death effect")]
        [SerializeField] private ParticleSystem deathEffectParticles;
        [Tooltip("Audio clip for death sound")]
        [SerializeField] private AudioClip deathSound;

        // Events for external systems like animation/audio
        public event Action OnNpcDeath;
        //TODO: implement item drops
        public event Action<GameObject> OnNpcItemDrop;

        //TODO: Make this the interface instead so i can use multiple FSMs
        private FSM.StateMachineNew _stateMachineNew;
        private bool isDead = false;
        private AudioSource audioSource;

        void Awake()
        {
            _stateMachineNew = GetComponent<FSM.StateMachineNew>(); 
            if (_stateMachineNew == null) Debug.LogError($"[DeathState - {gameObject.name}] : NPCStateMachine not found.");
            
            audioSource = GetComponent<AudioSource>();
        }

        public void OnStateEnter()
        {
            Debug.Log("DeathState: Enter");
            
            if (isDead) return; // Prevent double execution
            isDead = true;
            
            // Disable all AI components
            DisableAIComponents();
            
            // Trigger death animation/sound
            OnNpcDeath?.Invoke();
            PlayNpcDeadAnim?.Invoke();
            
            // Play death sound if available
            if (audioSource != null && deathSound != null)
            {
                audioSource.clip = deathSound;
                audioSource.Play();
            }
            
            // Play death effect if enabled
            if (enableDeathEffect && deathEffectParticles != null)
            {
                deathEffectParticles.Play();
            }
            
            // Handle ragdoll if enabled
            if (enableRagdoll)
            {
                EnableRagdoll();
            }
            
            // Attempt to drop an item
            TryDropItem();
            
            // Start cleanup process
            StartCoroutine(DeathCleanup());
        }

        public void OnStateUpdate(float deltaTime)
        {
            // Death state is terminal, no updates needed
            // Any ongoing animations or physics will continue automatically
        }

        public void OnStateExit()
        {
            // Death state should never exit, but just in case
            Debug.LogWarning("DeathState: Exit was called, which should not happen!");
        }
        
        private void DisableAIComponents()
        {
            // Disable navigation
            NavMeshAgent agent = GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.isStopped = true;
                agent.enabled = false;
            }
            
            // Disable any colliders to prevent further interactions
            Collider[] colliders = GetComponentsInChildren<Collider>();
            foreach (Collider col in colliders)
            {
                // Skip trigger colliders as they might be used for item pickup
                if (!col.isTrigger)
                {
                    col.enabled = false;
                }
            }
            
            // Disable other AI components if present
            var aiScripts = GetComponents<MonoBehaviour>();
            foreach (var script in aiScripts)
            {
                // Skip this Death state component
                if (script != this && script != _stateMachineNew)
                {
                    script.enabled = false;
                }
            }
        }
        
        private void EnableRagdoll()
        {
            // Get all rigidbodies in children for ragdoll
            Rigidbody[] rigidbodies = GetComponentsInChildren<Rigidbody>();
            foreach (Rigidbody rb in rigidbodies)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
                
                // Apply some random force for dramatic effect
                rb.AddForce(new Vector3(
                    UnityEngine.Random.Range(-1f, 1f), 
                    UnityEngine.Random.Range(0.1f, 0.5f), 
                    UnityEngine.Random.Range(-1f, 1f)) * 3f, 
                    ForceMode.Impulse);
            }
            
            // Disable animator to let physics take over
            Animator animator = GetComponent<Animator>();
            if (animator != null)
            {
                animator.enabled = false;
            }
        }
        
        private void TryDropItem()
        {
            if (itemDropPrefab != null && UnityEngine.Random.value <= dropItemChance)
            {
                // Instantiate the item prefab slightly above the ground
                Vector3 dropPosition = transform.position + Vector3.up * 0.5f;
                GameObject droppedItem = Instantiate(itemDropPrefab, dropPosition, Quaternion.identity);
                
                // Apply small random force to the item if it has a rigidbody
                Rigidbody itemRb = droppedItem.GetComponent<Rigidbody>();
                if (itemRb != null)
                {
                    itemRb.AddForce(new Vector3(
                        UnityEngine.Random.Range(-0.5f, 0.5f),
                        0.2f,
                        UnityEngine.Random.Range(-0.5f, 0.5f)), 
                        ForceMode.Impulse);
                }
                
                Debug.Log($"DeathState: Dropped item {droppedItem.name}");
                OnNpcItemDrop?.Invoke(droppedItem);
            }
        }
        
        private IEnumerator DeathCleanup()
        {
            // Wait for the specified delay
            yield return new WaitForSeconds(destroyDelay);
            
            // Optional: fade out the object
            SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>();
            float fadeTime = 2.0f;
            float elapsed = 0f;
            
            // Store original materials for fading
            Material[] originalMaterials = new Material[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                originalMaterials[i] = renderers[i].material;
                
                // Ensure the material can be transparent
                Color color = renderers[i].material.color;
                renderers[i].material.SetFloat("_Mode", 3); // Fade mode
                renderers[i].material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                renderers[i].material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                renderers[i].material.SetInt("_ZWrite", 0);
                renderers[i].material.DisableKeyword("_ALPHATEST_ON");
                renderers[i].material.EnableKeyword("_ALPHABLEND_ON");
                renderers[i].material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                renderers[i].material.renderQueue = 3000;
            }
            
            // Perform fade out
            while (elapsed < fadeTime)
            {
                float normalizedTime = elapsed / fadeTime;
                float alpha = 1f - normalizedTime;
                
                foreach (SkinnedMeshRenderer renderer in renderers)
                {
                    Color color = renderer.material.color;
                    color.a = alpha;
                    renderer.material.color = color;
                }
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            // Destroy the GameObject
            Destroy(gameObject);
        }

        public event Action PlayNpcDeadAnim;
    }
}