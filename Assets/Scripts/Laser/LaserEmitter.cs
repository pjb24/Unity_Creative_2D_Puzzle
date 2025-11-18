///
/// 한 emitter당 LineRenderer 하나.
/// LaserManager가 LineRenderer에 점들을 넣어서 경로를 그린다.
///

using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class LaserEmitter : MonoBehaviour
{
    [SerializeField] private Vector2Int _direction = Vector2Int.right; // 시작 방향 (동,서,남,북 중 하나)
    private LineRenderer _line;

    public Vector2Int Direction => _direction;
    public LineRenderer Line => _line;

    private void Awake()
    {
        _line = GetComponent<LineRenderer>();
        _line.positionCount = 0;
    }

    private void OnEnable()
    {
        if (LaserManager.Instance != null)
            LaserManager.Instance.RegisterEmitter(this);
    }

    private void OnDisable()
    {
        if (LaserManager.Instance != null)
            LaserManager.Instance.UnregisterEmitter(this);
    }
}
