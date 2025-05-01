using UnityEngine;
using Input;
using Utilities;
using System.Collections;

namespace Character
{
    public class CharacterCombat : MonoBehaviour, IAttackable
    {
        [SerializeField] private MonoBehaviour inputProvider; // Must implement IInputSource
        [SerializeField] private float checkInterval = 0.1f;

        private IInputSource _inputSource;
        private Animator _animator;
        private int _animIDAttack;
        private Coroutine _attackRoutine;

        private void Awake()
        {
            _inputSource = inputProvider as IInputSource;
            _animator = GetComponent<Animator>();
            _animIDAttack = Animator.StringToHash("Attack");
        }

        private void OnEnable()
        {
            _attackRoutine = StartCoroutine(AttackCheckRoutine());
        }

        private void OnDisable()
        {
            if (_attackRoutine != null)
                StopCoroutine(_attackRoutine);
        }

        private IEnumerator AttackCheckRoutine()
        {
            while (true)
            {
                if (_inputSource.IsAttacking)
                {
                    _animator.SetBool(_animIDAttack, true);
                    yield return CoroutineUtility.WaitForAnimationToEnd(_animator, "Attack");
                    _animator.SetBool(_animIDAttack, false);
                }

                yield return new WaitForSeconds(checkInterval);
            }
        }

        // Optional: exposed for AI to directly trigger if needed
        public void StartAttack() => _animator.SetBool(_animIDAttack, true);
        public void EndAttack() => _animator.SetBool(_animIDAttack, false);
    }
}