using Input;
using UnityEngine;

namespace GameInput
{
    public class NPCInputProvider : MonoBehaviour, IInputSource
    {
        [SerializeField] private ScriptableInputSource inputSource;

        public IInputSource InputSource => inputSource;
        public bool IsAttacking { get; }
    }
}