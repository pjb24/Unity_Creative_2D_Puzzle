using UnityEngine;

public class FloorMoveActuator : MonoBehaviour, IActivable
{
    [SerializeField] private MovingFloor _floor;
    // true=홀드형(놓으면 복귀), false=래치형(유지)
    [SerializeField] private bool _returnOnRelease = false;

    private bool _latched; // 래치 유지 상태

    public void SetActiveState(bool on)
    {
        if (_returnOnRelease)
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
        else
        {
            // “수용했을 때 이동 후 고정”: 최초 on에서만 이동, off는 무시
            if (on && !_latched)
            {
                _latched = true;
                _floor.TriggerMoveForward();
            }
        }
    }

    // 필요 시 외부에서 원점으로 리셋
    public void ResetLatch()
    {
        _latched = false;
        _floor.TriggerMoveBackward();
    }
}
