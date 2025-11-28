using UnityEngine;

public class DeviceOutputBinder : MonoBehaviour
{
    [SerializeField] LaserSwitchDevice _device;
    [SerializeField] MonoBehaviour[] _targets; // IActivatable 구현체(Door, WallToggle 등)

    void Awake()
    {
        _device.OnOutput += on => {
            foreach (var t in _targets)
            {
                if (t is IActivable a)
                {
                    a.SetActiveState(on);
                }
            }
        };
    }
}
