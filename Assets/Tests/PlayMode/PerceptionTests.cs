using AI.FSM.NPC;
using AI.FSM.NPC.States;
using AI.NPC.Sensing;
using Animation.AnimControllers;
using Characters.NPC;
using Combat.DamageSystem.Health;
using Moq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

namespace Tests.PlayMode
{
    public class PerceptionTests
    {
        private GameObject npcGO;
        private WarriorStateMachine fsm;

        [SetUp]
        public void Setup()
        {
            npcGO = new GameObject("TestNPC");
            fsm = npcGO.AddComponent<WarriorStateMachine>();

            npcGO.AddComponent<NavMeshAgent>();
            npcGO.AddComponent<CharacterAnimator>();
            npcGO.AddComponent<NPCController>();
            npcGO.AddComponent<Health>();
            npcGO.AddComponent<TargetingSensor>();
            npcGO.AddComponent<ZoneDetector>();
            npcGO.AddComponent<VisionSensor>();
            npcGO.AddComponent<NPCPerceptionCoordinator>();
        }

        [Test]
        public void ConfirmPlayerVisibility_TransitionsToChaseState()
        {
            // Arrange
            var mockPlayer = new GameObject("Player");
            mockPlayer.tag = "Player";
            fsm.Player = mockPlayer.transform;

            // Act
            fsm.NotifyPlayerInDetectionZone(true, mockPlayer.transform);
            fsm.ConfirmPlayerVisibilityAndEngage();

            // Assert
            Assert.IsTrue(fsm.HasSpottedPlayer);
            Assert.IsInstanceOf<ChaseState>(fsm.CurrentState); // Must have ChaseState on prefab
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(npcGO);
        }
        
        // [Test]
        // public void ConfirmPlayerVisibility_TransitionsToChaseStateTwo()
        // {
        //     // Arrange
        //     var npcGO = new GameObject("TestNPC");
        //     var fsm = npcGO.AddComponent<WarriorStateMachine>();
        //
        //     // Mocks
        //     var mockVision = new Mock<IVisionSensor>();
        //     mockVision.Setup(v => v.CanSeePlayer(It.IsAny<Transform>())).Returns(true);
        //
        //     var mockTargeting = new Mock<ITargetingSensor>();
        //     var mockZone = new Mock<IZoneDetector>();
        //
        //     fsm.InjectDependencies(mockVision.Object, mockTargeting.Object, mockZone.Object);
        //
        //     var mockPlayer = new GameObject("Player");
        //     mockPlayer.tag = "Player";
        //     fsm.Player = mockPlayer.transform;
        //
        //     // Setup fake state (you’ll need to attach a ChaseState manually if it’s MonoBehaviour)
        //     var chaseState = npcGO.AddComponent<ChaseState>();
        //     fsm.SetStates(new IState[] { chaseState }); // Assuming your FSM supports dynamic state assignment
        //
        //     // Act
        //     fsm.NotifyPlayerInDetectionZone(true, mockPlayer.transform);
        //     fsm.ConfirmPlayerVisibilityAndEngage();
        //
        //     // Assert
        //     Assert.IsTrue(fsm.HasSpottedPlayer);
        //     Assert.IsInstanceOf<ChaseState>(fsm.CurrentState);
        // }

    }
}