using UnityEngine;
using System;

public enum E_LaserPolicy { OnEnterLatch, WhileHeld }  // 1회 래치 / 누르고 있는 동안만

public class LaserSwitchDevice : MonoBehaviour
{
    [SerializeField] private E_LaserPolicy _policy = E_LaserPolicy.OnEnterLatch;
    private bool _isHeld;
    private bool _latched;

    public event Action<bool> OnOutput; // true = 활성

    public void SetInput(bool on)
    {
        _isHeld = on;
        switch (_policy)
        {
            case E_LaserPolicy.WhileHeld:
                {
                    OnOutput?.Invoke(_isHeld);
                    break;
                }
            case E_LaserPolicy.OnEnterLatch:
                {
                    if (on)
                    {
                        _latched = true;
                        OnOutput?.Invoke(true);
                    }
                    // off일 때는 유지(Latch). 필요 시 해제 로직 별도 트리거 추가.
                    break;
                }
        }
    }

    // 필요 시 별도 인터랙션으로 래치 해제
    public void ResetLatch()
    {
        _latched = false;
        if (_policy == E_LaserPolicy.OnEnterLatch)
        {
            OnOutput?.Invoke(false);
        }
    }
}
