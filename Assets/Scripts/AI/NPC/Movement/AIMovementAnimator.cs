using Animation.AnimControllers;
using UnityEngine;

namespace AI.NPC.Movement
{
    [RequireComponent(typeof(AIMovementController))]
    public class AIMovementAnimator : MonoBehaviour
    {
        [SerializeField] private CharacterAnimator characterAnimator;
        private AIMovementController _movementController;

        private void Awake()
        {
            _movementController = GetComponent<AIMovementController>();
            if (characterAnimator == null)
                characterAnimator = GetComponent<CharacterAnimator>();
        }

        private void Update()
        {
            if (characterAnimator != null)
                characterAnimator.SetMovementSpeed(_movementController.GetSpeed());
        }

        private void OnDisable()
        {
            if (characterAnimator != null)
                characterAnimator.SetMovementSpeed(0f);
        }
    }
}