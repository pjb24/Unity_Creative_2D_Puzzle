using UnityEngine;

public class FloorMoveActuator : MonoBehaviour, IActivable
{
    [SerializeField] private MovingFloor _floor;

    public void SetActiveState(bool on)
    {
        // “수용 중에만”: on=이동, off=원점 복귀
        if (on)
        {
            _floor.TriggerMoveForward();
        }
        else
        {
            _floor.TriggerMoveBackward();
        }
    }

    // 필요 시 외부에서 원점으로 리셋
    public void ResetLatch()
    {
        _floor.TriggerMoveBackward();
    }
}
