using AI.NPC.Sensing;
using UnityEngine;
using UnityEngine.UI;

public class NPCDebugUI : MonoBehaviour
{
    [Header("Toggle References")]
    [SerializeField] private Toggle visionToggle;
    [SerializeField] private Toggle zoneToggle;

    private VisionSensor[] _visionSensors;
    private ZoneDetector[] _zoneDetectors;

    private void Start()
    {
        _visionSensors = FindObjectsOfType<VisionSensor>();
        _zoneDetectors = FindObjectsOfType<ZoneDetector>();

        if (visionToggle != null)
            visionToggle.onValueChanged.AddListener(OnVisionToggleChanged);

        if (zoneToggle != null)
            zoneToggle.onValueChanged.AddListener(OnZoneToggleChanged);
    }

    private void OnVisionToggleChanged(bool enabled)
    {
        foreach (var sensor in _visionSensors)
        {
            if (enabled)
                sensor.StartChecking();
            else
                sensor.StopChecking();
        }
    }

    private void OnZoneToggleChanged(bool enabled)
    {
        foreach (var detector in _zoneDetectors)
        {
            detector.enabled = enabled;
        }
    }
}