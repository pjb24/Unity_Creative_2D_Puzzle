/// 
/// 발판(바닥)은 그리드 기준으로 이동하지만, 내부적으로는
/// Rigidbody2D(kinematic)로 Lerp.
/// 발판이 한 프레임 동안 이동한 delta를 계산해서,
/// 그 위에 올라탄 Rigidbody2D 들에게 같은 delta를 MovePosition으로 더해줌.
/// 이렇게 하면 플레이어가 자유 이동 중이어도 발판과 함께 “끈적하게” 따라간다.
/// 
/// 
/// MovingFloor 오브젝트:
/// Rigidbody2D (Body Type: Kinematic)
/// BoxCollider2D (IsTrigger=true, 발판 위에 플레이어가 올라탈 수 있게)
/// MovingFloor 스크립트
/// startCell, endCell은 인스펙터에서 타일 좌표로 맞춰서 입력.
/// 
/// 플레이어:
/// 기존 Rigidbody2D(dynamic) + Collider2D.
/// 발판 위에서 자연스럽게 같이 이동한다.
/// 
/// 
/// 발판 자체는 Rigidbody2D(kinematic) + Lerp 이동.
/// 한 프레임 delta를 위에 있는 Rigidbody2D에게 그대로 더해줘서
/// 플레이어가 달라붙은 것처럼 따라가게 만들기.
/// 
/// 
/// MovingFloor (GridObject / GridOccupancy / GridUtil 연동, 이동 모드 선택 지원)
/// - 이동 모드:
///   1) DurationToTarget: 지정 시간(moveDuration) 동안 시작→목표까지 보간(Lerp)
///   2) SpeedUnitsPerSecond: 초당 거리(moveSpeed)로 등속 이동(목표 도달 시 종료)
/// - 종료 시 셀 스냅 + GridOccupancy 점유 갱신
/// - 탑승객(Rigidbody2D) 델타 동기 이동 (충돌 시 탑승객만 정지, 바닥은 계속 이동)
/// - 이동 중 플레이어 입력 Lock (PlayerController.SetExternalLock)
/// - GridObject.Cell을 단일 기준으로 사용, 스냅/점유는 GridOccupancy로 처리
/// 
/// 요구 사항(프로젝트 구성에 맞춰 함수명만 맞추면 그대로 사용 가능):
/// - GridObject
///     .CurrentCell : Vector3Int    // 현재 그리드 셀
/// - GridOccupancy (싱글톤)
///     .Instance
///     .TryRegister(GridObject obj, Vector3Int cell)
///     .Unregister(Vector3Int cell)
/// - GridUtil (정적)
///     .CellToWorldCenter(Vector3Int cell) : Vector3
///     .WorldToCell(Vector3 world)         : Vector3Int
/// 
/// 사용법:
/// 1) GridObject를 같은 오브젝트에 부착(필수)
/// 2) 본체 Collider2D + '탑승 감지용' Trigger Collider2D 추가 권장
/// 3) startCell/endCell 지정 후 TriggerMove() 호출
/// 

using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(GridObject))]
public class MovingFloor : MonoBehaviour
{
    public enum E_MoveMode
    {
        DurationToTarget,     // 지정 시간 내 목표까지 이동 (Lerp)
        SpeedUnitsPerSecond,  // 초당 거리(속도)로 이동 (MoveTowards)
    }

    [Header("Grid Path")]
    [Tooltip("시작 셀 (씬 배치 기준)")]
    [SerializeField] private Vector3Int _startCell;
    [Tooltip("도착 셀")]
    [SerializeField] private Vector3Int _endCell;

    [Header("Move Mode")]
    [SerializeField] private E_MoveMode _moveMode = E_MoveMode.DurationToTarget;
    [Tooltip("[DurationToTarget] 목표까지 걸리는 시간(초)")]
    [ShowIfAny(nameof(_moveMode), E_MoveMode.DurationToTarget)]
    [SerializeField] private float _moveDuration = 1.0f;
    [Tooltip("[SpeedUnitsPerSecond] 초당 이동 거리(유닛/초)")]
    [ShowIfAny(nameof(_moveMode), E_MoveMode.SpeedUnitsPerSecond)]
    [SerializeField] private float _moveSpeed = 3.0f;

    [Header("Passengers")]
    [Tooltip("탑승객으로 취급할 레이어(비우면 전체)")]
    [SerializeField] private LayerMask _passengerMask = ~0;
    [Tooltip("선행 충돌 박스캐스트를 사용할 경우 차단 레이어(선택)")]
    [SerializeField] private LayerMask _blockingMask = ~0;
    [Tooltip("탑승객 MovePosition 전에 BoxCast로 충돌 지점까지만 이동량을 클램프")]
    [SerializeField] private bool _usePrecastClamp = false;
    [Tooltip("탑승 감지용 트리거")]
    [SerializeField] private Collider2D _triggerCollector;

    [Header("Boot")]
    [Tooltip("Start에서 자동 이동 시작")]
    [SerializeField] private bool _moveOnStart = false;

    [Header("Debug")]
    [SerializeField] private bool _showGizmos = true;
    [SerializeField] private Color _gizmoPathColor = new Color(0.2f, 0.9f, 1f, 0.8f);

    private Rigidbody2D _rb;

    private GridObject _gridObj;
    private bool _isMoving;
    private float _elapsed; // 모드 공용 경과 시간
    private Vector3 _startPos;
    private Vector3 _endPos;
    private Vector3 _lastPos;

    private readonly List<Rigidbody2D> _passengers = new List<Rigidbody2D>();

    private void Awake()
    {
        _gridObj = GetComponent<GridObject>();
        if (_gridObj == null)
        {
            Debug.LogError("[MovingFloor] GridObject가 필요합니다.");
            enabled = false;
            return;
        }

        _rb = GetComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
    }

    private void Start()
    {
        _startPos = GridUtil.CellToWorldCenter(_startCell);
        _endPos = GridUtil.CellToWorldCenter(_endCell);
        _lastPos = _startPos;

        _rb.position = _startPos;

        if (_moveOnStart)
        {
            TriggerMove();
        }
    }

    private void FixedUpdate()
    {
        if (!_isMoving) return;

        Vector3 cur = transform.position;
        Vector3 next = cur;

        switch (_moveMode)
        {
            case E_MoveMode.DurationToTarget:
                {
                    _elapsed += Time.fixedDeltaTime;
                    float duration = Mathf.Max(0.0001f, _moveDuration);
                    float t = Mathf.Clamp01(_elapsed / duration);

                    next = Vector3.Lerp(_startPos, _endPos, t);
                    break;
                }
            case E_MoveMode.SpeedUnitsPerSecond:
                {
                    float step = Mathf.Max(0f, _moveSpeed) * Time.fixedDeltaTime;
                    next = Vector3.MoveTowards(cur, _endPos, step);
                    break;
                }
        }

        Vector3 delta = next - cur;
        if (delta != Vector3.zero)
        {
            MovePassengers(delta);
            transform.position = next;
            _lastPos = next;
        }

        bool arrived =
            (_moveMode == E_MoveMode.DurationToTarget && _elapsed >= Mathf.Max(0.0001f, _moveDuration))
            ||
            (_moveMode == E_MoveMode.SpeedUnitsPerSecond && (next - _endPos).sqrMagnitude <= 0.000001f);

        if (arrived)
            FinishMove();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryAddPassenger(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        TryRemovePassenger(other);
    }

    private void OnDrawGizmosSelected()
    {
        if (!_showGizmos) return;

        Vector3 a = GridUtil.CellToWorldCenter(_startCell);
        Vector3 b = GridUtil.CellToWorldCenter(_endCell);

        Gizmos.color = _gizmoPathColor;
        Gizmos.DrawSphere(a, 0.06f);
        Gizmos.DrawSphere(b, 0.06f);
        Gizmos.DrawLine(a, b);
    }

    /// <summary>
    /// endCell로 1회 이동
    /// </summary>
    public void TriggerMove()
    {
        if (_isMoving) return;

        GridOccupancy.Instance?.Unregister(_gridObj.CurrentCell);

        _isMoving = true;
        _elapsed = 0f;

        _startPos = transform.position;
        _endPos = GridUtil.CellToWorldCenter(_endCell);
        _lastPos = _startPos;

        RefreshPassengers();
        SetPlayerInputLock(true);
    }

    /// <summary>
    /// 임의의 목표 셀로 이동 시작
    /// </summary>
    public void TriggerMoveTo(Vector3Int targetCell)
    {
        if (_isMoving) return;

        _endCell = targetCell;
        TriggerMove();
    }

    /// <summary>
    /// 강제 정지 + 현 위치 근사 셀로 스냅 + 점유 갱신
    /// </summary>
    public void ForceStopAndSnapToNearestCell()
    {
        var world = transform.position;
        var newCell = GridUtil.WorldToCell(world);

        _isMoving = false;
        transform.position = GridUtil.CellToWorldCenter(newCell);

        var oldCell = _gridObj.CurrentCell;
        GridOccupancy.Instance?.TryRegister(_gridObj, newCell);

        SetPlayerInputLock(false);
        _passengers.Clear();
    }

    private void FinishMove()
    {
        _isMoving = false;

        // 1) 자기 자신 스냅
        transform.position = GridUtil.CellToWorldCenter(_endCell);

        // 2) GridObject.Cell 갱신 + 점유 갱신
        var oldCell = _gridObj.CurrentCell;
        GridOccupancy.Instance?.TryRegister(_gridObj, _endCell);

        // 3) 외부 시스템(예: 레이저 리캐스트, 경로 동기화 등) 통지 필요 시 여기서 호출
        LaserWorldEvents.RaiseWorldChanged();

        SetPlayerInputLock(false);
        _passengers.Clear();
    }

    private void MovePassengers(Vector3 delta)
    {
        if (delta == Vector3.zero || _passengers.Count == 0) return;

        foreach (var rb in _passengers)
        {
            if (rb == null) continue;
            if (((1 << rb.gameObject.layer) & _passengerMask.value) == 0) continue;

            Vector2 origin = rb.position;
            Vector2 target = origin + (Vector2)delta;

            if (_usePrecastClamp)
            {
                var col = rb.GetComponent<Collider2D>();
                if (col != null)
                {
                    var size = col.bounds.size;
                    var hit = Physics2D.BoxCast(origin, size, 0f, ((Vector2)delta).normalized,
                                                delta.magnitude, _blockingMask);
                    if (hit.collider != null)
                        target = origin + ((Vector2)delta).normalized * hit.distance;
                }
            }

            rb.MovePosition(target);
        }
    }

    private void RefreshPassengers()
    {
        _passengers.Clear();

        if (_triggerCollector != null)
        {
            // 트리거 내 겹치는 콜라이더 수집
            var results = new List<Collider2D>();
            var filter = new ContactFilter2D
            {
                useTriggers = true,
                useLayerMask = true,
                layerMask = _passengerMask
            };
            int count = _triggerCollector.Overlap(filter, results);
            for (int i = 0; i < count; i++)
            {
                TryAddPassenger(results[i]);
            }
        }
        else
        {
            // 트리거 없을 때: 본체 콜라이더 상면 기준 OverlapBox 대체 수집
            var col = GetComponent<Collider2D>();
            if (col != null)
            {
                Bounds b = col.bounds;
                Vector2 size = new(b.size.x * 0.98f, b.size.y * 0.5f);
                Vector2 center = new(b.center.x, b.max.y - size.y * 0.5f);
                var hits = Physics2D.OverlapBoxAll(center, size, 0f, _passengerMask);
                foreach (var h in hits)
                {
                    TryAddPassenger(h);
                }
            }
        }
    }

    private void TryAddPassenger(Collider2D other)
    {
        if (other == null || !other.isActiveAndEnabled) return;
        if (((1 << other.gameObject.layer) & _passengerMask.value) == 0) return;

        var rb = other.attachedRigidbody;
        if (rb == null) return;

        if (!_passengers.Contains(rb))
        {
            _passengers.Add(rb);
        }
    }

    private void TryRemovePassenger(Collider2D other)
    {
        if (other == null) return;

        var rb = other.attachedRigidbody;
        if (rb == null) return;

        _passengers.Remove(rb);

        // 플레이어가 내렸다면 입력 해제(안전장치)
        var player = rb.GetComponent<PlayerFreeMove>();
        if (player != null)
        {
            player.SetExternalLock(false);
        }
    }

    private void SetPlayerInputLock(bool lockOn)
    {
        foreach (var rb in _passengers)
        {
            if (rb == null) continue;

            var player = rb.GetComponent<PlayerFreeMove>();
            if (player != null)
            {
                player.SetExternalLock(lockOn);
            }
        }
    }
}
