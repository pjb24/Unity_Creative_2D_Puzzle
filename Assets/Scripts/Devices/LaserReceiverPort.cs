using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class LaserReceiverPort : MonoBehaviour
{
    [SerializeField] LaserSwitchDevice _device;  // 아래 정책 장치에 연결

    public void LaserEnter()
    {
        _device?.SetInput(true);
    }

    public void LaserExit()
    {
        _device?.SetInput(false);
    }
}
