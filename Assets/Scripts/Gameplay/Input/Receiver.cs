using UnityEngine;

namespace Gameplay.Input
{
    public class Receiver : MonoBehaviour
    {
        public void OnJump()
        {
            Debug.Log($"[SendMessage/BroadcastMessage] {gameObject.name} received OnJump");
        }

        public void OnAttack()
        {
            Debug.Log($"[BroadcastMessage] {gameObject.name} received OnAttack");
        }

        public void CustomUnityEventResponse()
        {
            Debug.Log($"[UnityEvent] {gameObject.name} handled UnityEvent!");
        }
    }
}