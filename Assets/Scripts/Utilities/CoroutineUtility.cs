using System.Collections;
using UnityEngine;

namespace Utilities
{
    public static class CoroutineUtility
    {
        public static IEnumerator WaitForAnimationToEnd(Animator animator, string animationName) {
            
            //waint until animator transitions to target state
            yield return new WaitUntil(() =>
                animator.GetCurrentAnimatorStateInfo(0).IsName(animationName));

            // Wait until animation nears end
            yield return new WaitUntil(() =>
                animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 0.99f);
        }
    }
}