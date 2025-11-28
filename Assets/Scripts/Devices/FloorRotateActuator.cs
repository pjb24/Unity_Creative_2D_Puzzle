using UnityEngine;

public class FloorRotateActuator : MonoBehaviour, IActivable
{
    [SerializeField] private RotatingFloor _floor;
    [SerializeField] private bool _returnOnRelease = false;

    private bool _latched;

    public void SetActiveState(bool on)
    {
        if (_returnOnRelease)
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
        else
        {
            // “수용했을 때 회전 후 고정”: 최초 on에서만 회전
            if (on && !_latched)
            {
                _latched = true;
                _floor.TriggerRotateStartToEnd();
            }
        }
    }

    public void ResetLatch()
    {
        _latched = false;
        _floor.TriggerRotateEndToStart();
    }
}
