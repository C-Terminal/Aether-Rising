using UnityEngine;

namespace Input
{
    public class NPCInputProvider : MonoBehaviour, IInputSource
    {
        [SerializeField] private ScriptableInputSource inputSource;

        public IInputSource InputSource => inputSource;
        public bool IsAttacking { get; }
    }
}