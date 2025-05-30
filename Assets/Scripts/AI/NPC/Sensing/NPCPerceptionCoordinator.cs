using AI.FSM;
using AI.FSM.NPC;
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
        private bool _playerCurrentlyVisible = false;

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
            
 
            visionSensor.VisibilityEvaluator = () => targetingSensor.CurrentTarget != null && targetingSensor.HasLineOfSight();
            
            _fsm?.NotifyPlayerInDetectionZone(true, player);
            visionSensor.StartChecking();
        }

        private void HandlePlayerExit()
        {
            targetingSensor.SetTarget(null);
            visionSensor.VisibilityEvaluator = null; // Clear the evaluator
            _fsm?.NotifyPlayerInDetectionZone(false, null);
            visionSensor.StopChecking();
        }

        private void HandleVisibilityChanged(bool visible)
        {

            _playerCurrentlyVisible = visible;
            
            if (visible)
            {
                NPCManager.Instance.RegisterInRangeNpc(_fsm as WarriorStateMachine);
                _fsm?.ConfirmPlayerVisibilityAndEngage();
            }

            else
            {
                NPCManager.Instance.UnregisterOutOfRangeNpc(_fsm as WarriorStateMachine);
                _fsm?.NotifyPlayerLostSight();
            }
                
        }
        public bool IsPlayerCurrentlyVisible() => _playerCurrentlyVisible;
    }
}