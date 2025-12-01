using UnityEngine;

public class FloorRotateActuator : MonoBehaviour, IActivable
{
    [SerializeField] private RotatingFloor _floor;

    public void SetActiveState(bool on)
    {
        // “수용 중에만”: on=회전, off=원래 각도로 복귀
        if (on)
        {
            _floor.TriggerRotateStartToEnd();
        }
        else
        {
            _floor.TriggerRotateEndToStart();
        }
    }

    public void ResetLatch()
    {
        _floor.TriggerRotateEndToStart();
    }
}
