///
/// MovingFloor
/// 
/// 이동 방식
/// - Rigidbody2D (Kinematic) + Lerp 기반 이동
/// - 매 프레임 이동한 delta를 계산하여 위에 올라탄 Rigidbody2D 들에게
/// MovePosition으로 동일하게 더함
/// → 자유 이동 중인 플레이어도 자연스럽게 “달라붙어 이동”
/// 
/// 구성 요소
/// - Rigidbody2D (Kinematic)
/// - BoxCollider2D (IsTrigger = true) → 위에 플레이어 감지
/// - MovingFloor 스크립트
/// 	- startCell, endCell: 인스펙터에서 타일 좌표로 설정
/// 
/// 플레이어 쪽
/// - Rigidbody2D (Dynamic) + Collider2D
/// - 발판 위에 있으면 delta 만큼 이동
/// 
/// 
/// 스크립트 사양
/// 
/// Grid 연동
/// - GridObject, GridOccupancy, GridUtil 사용
/// - GridObject.CurrentCell 기준으로 셀 좌표 관리
/// - GridOccupancy로 점유 등록/해제
/// - GridUtil로 Cell↔World 변환
/// 
/// 이동 모드
/// 1) DurationToTarget: 지정 시간(moveDuration) 동안 Lerp 이동
/// 2) SpeedUnitsPerSecond: 초당 거리(moveSpeed)로 등속 이동
/// 
/// 이동 방향
/// 1) Start -> End
/// 2) End -> Start
/// 
/// 기능
/// - 매 프레임 Trigger/OverlapBox로 승객 수집
/// - 엔드 평면 클램프 적용 — 목표 초과 이동 방지
/// - 종료 시 자신 및 승객 GridOccupancy 갱신 + 셀 스냅
/// - 승객 Rigidbody2D 델타 이동 (충돌 시 승객만 정지, 발판은 계속 이동)
/// - 이동 중 플레이어 입력 Lock (PlayerController.SetExternalLock)
/// 
/// 사용 순서
/// 1) GridObject 컴포넌트 부착 (필수)
/// 2) 본체 Collider + ‘탑승 감지용 Trigger Collider’ 추가
/// 3) startCell / endCell 설정 후 TriggerMove() 호출
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

    public enum E_PathDirection
    {
        StartToEnd,
        EndToStart,
    }

    [Header("Grid Path")]
    [Tooltip("시작 셀 (씬 배치 기준)")]
    [SerializeField] private Vector3Int _startCell;
    [Tooltip("도착 셀")]
    [SerializeField] private Vector3Int _endCell;

    [Header("Direction")]
    [Tooltip("Start→End 또는 End→Start 선택")]
    public E_PathDirection _direction = E_PathDirection.StartToEnd;

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

    private Vector3Int _fromCell; // 이번 이동의 시작 셀
    private Vector3Int _toCell;   // 이번 이동의 도착 셀

    private Vector3 _fromWorld;
    private Vector3 _toWorld;
    private Vector3 _prevWorld;

    // 엔드 평면 클램프용
    // 캐시: 이동 축 단위벡터/부호/엔드 평면 스칼라
    private Vector2 _moveDir; // 정규화된 이동방향(2D)
    private float _dirSign;   // +1 / -1
    private float _endScalar;      // 엔드 평면의 축상 좌표(dot(endWorld, moveDir))

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
        // 현재 방향 기준 From/To 결정
        ResolveFromToCells();

        _fromWorld = GridUtil.CellToWorldCenter(_fromCell);
        _toWorld = GridUtil.CellToWorldCenter(_toCell);
        _prevWorld = transform.position;

        _rb.position = transform.position;

        if (_moveOnStart)
        {
            TriggerMoveByInspectorDirection();
        }
    }

    private void FixedUpdate()
    {
        if (!_isMoving) return;

        Vector3 cur = transform.position;
        Vector3 next = ComputeNext(cur);
        Vector3 delta = next - cur;

        // 프레임 중첩 승객 수집
        RefreshPassengersFramewise();

        if (delta != Vector3.zero)
        {
            MovePassengersWithEndClamp(delta);
        }

        transform.position = next;
        _prevWorld = next;

        if (ReachedEnd(next))
        {
            FinishMove();
        }
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
    /// Inspector의 direction 값에 따라 이동 시작
    /// </summary>
    public void TriggerMoveByInspectorDirection()
    {
        if (_isMoving) return;
        ResolveFromToCells();
        BeginMove();
    }

    /// <summary>
    /// 강제로 Start→End 이동
    /// </summary>
    public void TriggerMoveForward()
    {
        if (_isMoving) return;
        _direction = E_PathDirection.StartToEnd;
        ResolveFromToCells();
        BeginMove();
    }

    /// <summary>
    /// 강제로 End→Start 이동
    /// </summary>
    public void TriggerMoveBackward()
    {
        if (_isMoving) return;
        _direction = E_PathDirection.EndToStart;
        ResolveFromToCells();
        BeginMove();
    }

    /// <summary>
    /// 현재 방향을 토글해 이동
    /// </summary>
    public void TriggerMoveToggleDirection()
    {
        if (_isMoving) return;
        _direction = (_direction == E_PathDirection.StartToEnd) ? E_PathDirection.EndToStart : E_PathDirection.StartToEnd;
        ResolveFromToCells();
        BeginMove();
    }

    /// <summary>
    /// 임의의 목표 셀로 이동 시작
    /// </summary>
    public void TriggerMoveTo(Vector3Int targetCell)
    {
        if (_isMoving) return;

        // 현재 위치(셀)를 From으로, targetCell을 To로 설정
        _fromCell = GridUtil.WorldToCell(transform.position);
        _toCell = targetCell;
        _direction = E_PathDirection.StartToEnd; // 방향 표시는 의미 없음
        PreparePathVectors();
        BeginMovePrepared();
    }

    /// <summary>
    /// 강제 정지 + 현 위치 근사 셀로 스냅 + 점유 갱신
    /// </summary>
    public void ForceStopAndSnapToNearestCell()
    {
        _isMoving = false;
        
        var world = transform.position;
        var newCell = GridUtil.WorldToCell(world);
        transform.position = GridUtil.CellToWorldCenter(newCell);

        GridOccupancy.Instance?.TryRegister(_gridObj, newCell);

        ReRegisterPassengersToGrid();
        SetPlayerInputLock(false);
        _passengers.Clear();
    }

    private void ResolveFromToCells()
    {
        if (_direction == E_PathDirection.StartToEnd)
        {
            _fromCell = _startCell;
            _toCell = _endCell;
        }
        else
        {
            _fromCell = _endCell;
            _toCell = _startCell;
        }
        PreparePathVectors();
    }

    private void PreparePathVectors()
    {
        _fromWorld = GridUtil.CellToWorldCenter(_fromCell);
        _toWorld = GridUtil.CellToWorldCenter(_toCell);

        // 이동 축/부호/엔드 평면
        Vector2 v = (Vector2)(_toWorld - _fromWorld);
        _moveDir = v.sqrMagnitude > 0 ? v.normalized : Vector2.right;
        _dirSign = Mathf.Sign(Vector2.Dot((Vector2)(_toWorld - _fromWorld), _moveDir));
        _endScalar = Vector2.Dot((Vector2)_toWorld, _moveDir);
    }

    private void BeginMove()
    {
        GridOccupancy.Instance?.Unregister(_gridObj.CurrentCell);

        // 현재 트랜스폼이 From에 정확히 있지 않을 수 있음 → 현재 위치 기준으로 경로 보정
        _fromWorld = transform.position;
        // _toWorld는 유지
        _prevWorld = _fromWorld;

        _isMoving = true;
        _elapsed = 0f;

        RefreshPassengersInitial();
        SetPlayerInputLock(true);
    }

    // From/To 벡터가 이미 세팅된 경우(TriggerMoveTo 등)
    private void BeginMovePrepared()
    {
        GridOccupancy.Instance?.Unregister(_gridObj.CurrentCell);

        _prevWorld = transform.position;
        _isMoving = true;
        _elapsed = 0f;

        RefreshPassengersInitial();
        SetPlayerInputLock(true);
    }

    private Vector3 ComputeNext(Vector3 cur)
    {
        switch (_moveMode)
        {
            case E_MoveMode.DurationToTarget:
                {
                    _elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(_elapsed / Mathf.Max(0.0001f, _moveDuration));
                    return Vector3.Lerp(_fromWorld, _toWorld, t);
                }
            case E_MoveMode.SpeedUnitsPerSecond:
                {
                    float step = Mathf.Max(0f, _moveSpeed) * Time.deltaTime;
                    return Vector3.MoveTowards(cur, _toWorld, step);
                }
        }
        return cur;
    }

    private bool ReachedEnd(Vector3 next)
    {
        return _moveMode == E_MoveMode.DurationToTarget
            ? _elapsed >= Mathf.Max(0.0001f, _moveDuration)
            : (next - _toWorld).sqrMagnitude <= 1e-8f;
    }

    private void FinishMove()
    {
        _isMoving = false;

        // 1) 자기 자신 스냅
        transform.position = GridUtil.CellToWorldCenter(_toCell);

        // 2) GridObject.Cell 갱신 + 점유 갱신
        var oldCell = _gridObj.CurrentCell;
        GridOccupancy.Instance?.TryRegister(_gridObj, _toCell);

        // 탑승객 재등록
        ReRegisterPassengersToGrid();

        // 3) 외부 시스템(예: 레이저 리캐스트, 경로 동기화 등) 통지 필요 시 여기서 호출
        //LaserWorldEvents.RaiseWorldChanged();

        SetPlayerInputLock(false);
        _passengers.Clear();
    }

    private void MovePassengersWithEndClamp(Vector3 deltaWorld)
    {
        if (_passengers.Count == 0 || deltaWorld == Vector3.zero) return;

        Vector2 delta = (Vector2)deltaWorld;
        float stepS = Vector2.Dot(delta, _moveDir);

        foreach (var rb in _passengers)
        {
            if (rb == null) continue;
            if (((1 << rb.gameObject.layer) & _passengerMask.value) == 0) continue;

            Vector2 origin = rb.position;
            float originS = Vector2.Dot(origin, _moveDir);
            float wantS = originS + stepS;

            // 엔드 평면 클램프(방향에 따라 Max/Min)
            float clampedS = (_dirSign >= 0f) ? Mathf.Min(wantS, _endScalar) : Mathf.Max(wantS, _endScalar);
            Vector2 target = origin + _moveDir * (clampedS - originS);

            if (_usePrecastClamp)
            {
                Vector2 straight = target - origin;
                float dist = straight.magnitude;
                if (dist > 0f)
                {
                    var col = rb.GetComponent<Collider2D>();
                    if (col != null)
                    {
                        var size = col.bounds.size;
                        var hit = Physics2D.BoxCast(origin, size, 0f, straight.normalized,
                            dist, _blockingMask);
                        if (hit.collider != null)
                            target = origin + straight.normalized * hit.distance;
                    }
                }
            }

            rb.MovePosition(target);
        }
    }

    private void RefreshPassengersInitial()
    {
        RefreshPassengers(true);
    }

    private void RefreshPassengersFramewise()
    {
        RefreshPassengers(false);
    }

    private void RefreshPassengers(bool includeExisting)
    {
        if (!includeExisting)
        {
            _passengers.Clear();
        }

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

    private void ReRegisterPassengersToGrid()
    {
        foreach (var rb in _passengers)
        {
            if (rb == null) continue;

            // GridObject가 붙은 오브젝트만 재등록
            var gobj = rb.GetComponent<GridObject>();
            if (gobj == null) continue;

            Vector3Int newCell = GridUtil.WorldToCell(rb.position);
            Vector3Int oldCell = gobj.CurrentCell;

            GridOccupancy.Instance?.Unregister(oldCell);
            GridOccupancy.Instance?.TryRegister(gobj, newCell);
        }
    }
}
