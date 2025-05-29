using AI.FSM;
using AI.Sensing.NPC;
using UnityEngine;

namespace AI.NPC.Sensing
{
    public class NPCPerceptionCoordinator : MonoBehaviour
    {
        [SerializeField] private TargetingSensor targetingSensor;
        [SerializeField] private ZoneDetector zoneDetector;
        [SerializeField] private VisionSensor visionSensor;

        private IPerceptionAwareFSM _fsm;
        private Transform _player;

        private void Awake()
        {
            _fsm = GetComponentInParent<IPerceptionAwareFSM>();
            if (_fsm == null)
                Debug.LogError($"{nameof(NPCPerceptionCoordinator)}: FSM interface not found in parent.");
        }

        private void OnEnable()
        {
            zoneDetector.OnPlayerEnter += HandlePlayerEnter;
            zoneDetector.OnPlayerExit += HandlePlayerExit;
            visionSensor.OnVisibilityChanged += HandleVisibilityChanged;
        }

        private void OnDisable()
        {
            zoneDetector.OnPlayerEnter -= HandlePlayerEnter;
            zoneDetector.OnPlayerExit -= HandlePlayerExit;
            visionSensor.OnVisibilityChanged -= HandleVisibilityChanged;
        }

        private void HandlePlayerEnter(Transform player)
        {
            _player = player;
            targetingSensor.SetTarget(player);
            _fsm?.NotifyPlayerInDetectionZone(true, player);
            visionSensor.StartChecking();
        }

        private void HandlePlayerExit()
        {
            targetingSensor.SetTarget(null);
            _fsm?.NotifyPlayerInDetectionZone(false, null);
            visionSensor.StopChecking();
        }

        private void HandleVisibilityChanged(bool visible)
        {
            if (visible)
                _fsm?.ConfirmPlayerVisibilityAndEngage();
            else
                _fsm?.NotifyPlayerLostSight();
        }
    }
}