using UnityEngine;

namespace Input
{

    [CreateAssetMenu(menuName = "Input/NPC Input Source")]
    public class ScriptableInputSource : ScriptableObject, IInputSource
    {
        [SerializeField] private bool isAttacking;

        public bool IsAttacking => isAttacking;
        
        public void SetAttack(bool value) => isAttacking = value;
    }
    
}