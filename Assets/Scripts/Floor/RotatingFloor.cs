/// 
/// RotatingFloor (GridObject / GridOccupancy / GridUtil 연동
/// · 회전 모드 선택 · 180° 래핑 이슈 해결 · "시작 각도 ↔ 끝 각도" 양방향 회전)
/// - "Raw 각도 누적"으로 회전 방향 반전(±180 래핑) 문제 제거
/// - 회전 모드:
///   1) DurationToTarget    : 지정 시간(rotateDuration) 동안 angleStep 만큼 보간 회전
///   2) SpeedDegPerSecond   : 초당 rotateSpeedDeg 만큼 등속 회전(목표 각도 도달 시 종료)
/// - 두 가지 트리거 경로 제공:
///   A) 기존 step 기반: TriggerRotate(_clockwise) → _angleStep 만큼 시계/반시계 회전
///   B) 프리셋 기반:   TriggerRotateByPresetDirection() / TriggerRotateStartToEnd() / TriggerRotateEndToStart() / TriggerRotateTogglePreset()
///      - 미리 지정한 _startAnglePresetRaw → _endAnglePresetRaw 또는 그 반대 방향으로 회전
///      - 옵션: 회전 시작 전에 현재 각도를 시작 프리셋으로 스냅(_snapToPresetOnStart)
///      
/// - 회전 중 탑승객(Rigidbody2D) 원운동 동기화(+옵션: 방향 회전)
/// - 충돌 시 탑승객만 멈추고 바닥은 계속 회전(Physics2D에 위임, 선택적으로 선행 클램프 지원)
/// - 종료 시 타일 스냅(GridUtil) 및 점유 갱신(GridOccupancy.TryRegister) + 탑승객 재등록
///
/// 요구 유틸/컴포넌트:
/// - GridObject
///     .CurrentCell : Vector3Int    // 현재 그리드 셀
/// - GridOccupancy (Singleton)
///     .TryRegister(GridObject obj, Vector3Int cell)
///     .Unregister(Vector3Int cell)
/// - GridUtil
///     .CellToWorldCenter(Vector3Int cell) : Vector3
///     .WorldToCell(Vector3 world)         : Vector3Int
///
/// 사용 흐름:
/// - 프리셋 각도 필드(_startAnglePresetRaw, _endAnglePresetRaw)와 방향(_presetDirection)을 Inspector에서 설정
/// - TriggerRotateByPresetDirection() 호출 시, 설정된 방향에 따라 시작/끝 프리셋 사이를 회전
///   필요 시 _snapToPresetOnStart=true로 현재 각도를 시작 프리셋에 맞춘 뒤 회전 시작
///
/// - TriggerRotate(true/false) 호출로 시계/반시계 회전
/// 

using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(GridObject))]
public class RotatingFloor : MonoBehaviour
{
    public enum E_RotateMode
    {
        DurationToTarget,   // _rotateDuration 동안 angleStep 만큼 회전 (보간)
        SpeedDegPerSecond   // 초당 _rotateSpeedDeg 만큼 등속 회전 (목표각 도달 시 완료)
    }

    public enum E_SnapMode
    {
        // 모든 트랜스폼을 회전 종료 후 가장 가까운 셀 중심으로 스냅(45° 등 임의 각도 대응)
        NearestCell,
        // 정확히 90° 배수 회전일 때만, 셀 상대좌표를 정수 회전( -y,x / y,-x )으로 스냅
        RightAngleDiscrete,
    }

    // 프리셋 방향 선택자
    public enum E_PresetDirection
    {
        StartToEnd,
        EndToStart,
    }

    [Header("Grid/Pivot")]
    [Tooltip("피벗이 위치할 셀(부모 GridObject.Cell과 동일하게 두는 것을 권장)")]
    [SerializeField] private Vector3Int _pivotCell;

    [Header("Rotation Mode")]
    [SerializeField] private E_RotateMode _rotateMode = E_RotateMode.DurationToTarget;

    [Header("Rotation - Step Based")]
    [Tooltip("한 번 회전할 각도(예: 90 또는 45)")]
    [SnapTo(90f)]
    [SerializeField] private float _angleStep = 90f;
    
    [Tooltip("[DurationToTarget] 회전에 걸리는 시간(초)")]
    [ShowIfAny(nameof(_rotateMode), E_RotateMode.DurationToTarget)]
    [SerializeField] private float _rotateDuration = 0.5f;

    [Tooltip("[SpeedDegPerSecond] 초당 회전 각도(도/초)")]
    [ShowIfAny(nameof(_rotateMode), E_RotateMode.SpeedDegPerSecond)]
    [SerializeField] private float _rotateSpeedDeg = 180f;
    
    [Tooltip("종료 시 타일 스냅 방식")]
    [SerializeField] private E_SnapMode _snapMode = E_SnapMode.NearestCell;
    [Tooltip("step 기반 회전 방향(TriggerRotate 사용 시)")]
    [SerializeField] private bool _clockwise = true;


    [Header("Rotation — Preset Based")]
    [Tooltip("프리셋 시작(raw) 각도(도). 예: 0")]
    [SnapTo(90f)]
    [SerializeField] private float _startAnglePresetRaw = 0f;

    [Tooltip("프리셋 끝(raw) 각도(도). 예: 90")]
    [SnapTo(90f)]
    [SerializeField] private float _endAnglePresetRaw = 90f;

    [Tooltip("프리셋 회전의 기본 방향")]
    [SerializeField] private E_PresetDirection _presetDirection = E_PresetDirection.StartToEnd;

    [Tooltip("프리셋 회전 시작 시 현재 각도를 시작 프리셋 각도로 스냅")]
    [SerializeField] private bool _snapToPresetOnStart = false;


    [Header("Passengers")]
    [Tooltip("탑승객으로 취급할 레이어(비우면 전체)")]
    [SerializeField] private LayerMask _passengerMask = ~0;
    [Tooltip("탑승객 MovePosition 전, 충돌 지점까지만 이동량을 제한(선행 BoxCast)")]
    [SerializeField] private bool _usePrecastClamp = false;
    [Tooltip("선행 클램프 시 차단 레이어")]
    [SerializeField] private LayerMask _blockingMask = ~0;

    [Header("Passenger Orientation")]
    [Tooltip("탑승객의 방향(회전)도 함께 회전시킬지 여부")]
    [SerializeField] private bool _rotatePassengerOrientation = false;

    [Header("Boot")]
    [Tooltip("Awake 시 피벗을 pivotCell 좌표로 스냅")]
    [SerializeField] private bool _snapPivotOnAwake = true;
    [Tooltip("시작 시 회전")]
    [SerializeField] private bool _rotateOnAwake = false;

    [Header("Debug")]
    [SerializeField] private bool _showGizmos = true;
    [SerializeField] private Color _gizmoPivotColor = new Color(1f, 0.8f, 0.15f, 0.9f);
    [SerializeField] private Color _gizmoArcColor = new Color(0.2f, 0.9f, 1f, 0.8f);

    [SerializeField] private Collider2D _triggerCollector; // 탑승 감지용 트리거

    // 내부 상태
    private GridObject _gridObj;     // 부모(피벗)의 GridObject
    private bool _isRotating;
    private float _elapsed;          // DurationToTarget에서 경과 시간

    // *** 핵심: Unity Euler를 쓰지 않고 "raw 각도"를 직접 누적 관리 ***
    private float _rawStartAngle;     // 시작 raw 각
    private float _rawTargetAngle;    // 목표 raw 각
    private float _rawCurrentAngle;   // 현재 raw 각
    private float _rawPrevAngle;      // 이전 프레임 raw 각

    // 탑승객 캐시
    private List<Rigidbody2D> _passengers = new();

    private void Awake()
    {
        _gridObj = GetComponent<GridObject>();
        if (_gridObj == null)
        {
            Debug.LogError("[RotatingFloor] GridObject가 필요합니다.");
            enabled = false;

            return;
        }

        if (_snapPivotOnAwake)
        {
            var world = GridUtil.CellToWorldCenter(_pivotCell);
            transform.position = world;

            var oldCell = _gridObj.CurrentCell;
            GridOccupancy.Instance?.Unregister(oldCell);
            GridOccupancy.Instance?.TryRegister(_gridObj, _pivotCell);
        }

        // 초기 raw 각도는 현재 EulerZ로 세팅(표시각 → raw로 초기화만)
        _rawCurrentAngle = transform.eulerAngles.z;
        _rawPrevAngle = _rawCurrentAngle;
        _rawStartAngle = _rawCurrentAngle;
        _rawTargetAngle = _rawCurrentAngle;

        if (_rotateOnAwake)
        {
            // step 기반 회전 시작
            TriggerRotate(_clockwise);
        }
    }

    private void FixedUpdate()
    {
        if (!_isRotating) return;

        float nextRaw = _rawCurrentAngle;

        if (_rotateMode == E_RotateMode.DurationToTarget)
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / Mathf.Max(0.0001f, _rotateDuration));
            // raw 각도 보간 (Unity Euler 래핑 사용 안 함)
            nextRaw = Mathf.Lerp(_rawStartAngle, _rawTargetAngle, t);
        }
        else // SpeedDegPerSecond
        {
            float step = Mathf.Max(0f, _rotateSpeedDeg) * Time.fixedDeltaTime;
            nextRaw = Mathf.MoveTowards(_rawCurrentAngle, _rawTargetAngle, step);
        }

        float deltaAngleDeg = nextRaw - _rawPrevAngle; // 부호(방향) 안정
        if (Mathf.Abs(deltaAngleDeg) > 0.0001f)
        {
            RotatePassengers(deltaAngleDeg);
            // transform에는 "표시각"으로만 반영
            transform.rotation = Quaternion.Euler(0, 0, NormalizeToEuler(nextRaw));
            _rawPrevAngle = nextRaw;
            _rawCurrentAngle = nextRaw;
        }

        bool arrived =
            (_rotateMode == E_RotateMode.DurationToTarget && _elapsed >= Mathf.Max(0.0001f, _rotateDuration))
            ||
            (_rotateMode == E_RotateMode.SpeedDegPerSecond && ApproximatelyRelative(_rawCurrentAngle, _rawTargetAngle));

        if (arrived)
        {
            FinishRotate();
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

        // 피벗 표시
        Vector3 pivotW = GridUtil.CellToWorldCenter(_pivotCell);
        Gizmos.color = _gizmoPivotColor;
        Gizmos.DrawSphere(pivotW, 0.08f);

        // 회전 아크 표시(간략)
        Gizmos.color = _gizmoArcColor;
        Vector3 r = Vector3.right * 0.7f;
        int seg = 16;
        float a0 = 0f;
        float a1 = Mathf.Sign(_angleStep) * Mathf.Abs(_angleStep) * Mathf.Deg2Rad;
        Vector3 prev = pivotW + (Quaternion.Euler(0, 0, a0 * Mathf.Rad2Deg) * r);
        for (int i = 1; i <= seg; i++)
        {
            float a = Mathf.Lerp(a0, a1, i / (float)seg);
            Vector3 p = pivotW + (Quaternion.Euler(0, 0, a * Mathf.Rad2Deg) * r);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }

    /// <summary>
    /// angleStep만큼 회전 시작 (clockwise=true면 시계 방향)
    /// </summary>
    public void TriggerRotate(bool clockwise)
    {
        if (_isRotating) return;

        float dir = clockwise ? -1f : 1f; // 2D에서 시계가 -Z
        float delta = _angleStep * dir;

        BeginRotateRaw(_rawCurrentAngle, _rawCurrentAngle + delta, false);
    }

    /// <summary>
    /// 절대 목표(raw) 각도로 회전 시작. (표시각 아님: 누적 각 기준)
    /// 예: 현재 270, targetRaw=450(=표시 90) 같은 방식으로 사용 가능.
    /// </summary>
    public void TriggerRotateToAbsoluteRaw(float targetAngleRaw)
    {
        if (_isRotating) return;

        BeginRotateRaw(_rawCurrentAngle, targetAngleRaw, false);
    }

    /// <summary>
    /// Inspector의 _presetDirection에 따라 프리셋 시작↔끝 각도로 회전
    /// </summary>
    public void TriggerRotateByPresetDirection()
    {
        if (_isRotating) return;

        if (_presetDirection == E_PresetDirection.StartToEnd)
        {
            TriggerRotateStartToEnd();
        }
        else
        {
            TriggerRotateEndToStart();
        }
    }

    /// <summary>
    /// 프리셋: 시작(raw) → 끝(raw) 회전
    /// </summary>
    public void TriggerRotateStartToEnd()
    {
        if (_isRotating) return;

        // 시작 프리셋으로 스냅 후 회전할지 여부
        BeginRotateRaw(_startAnglePresetRaw, _endAnglePresetRaw, _snapToPresetOnStart);
    }

    /// <summary>
    /// 프리셋: 끝(raw) → 시작(raw) 회전
    /// </summary>
    public void TriggerRotateEndToStart()
    {
        if (_isRotating) return;

        BeginRotateRaw(_endAnglePresetRaw, _startAnglePresetRaw, _snapToPresetOnStart);
    }

    /// <summary>
    /// 프리셋 방향 토글 후 회전
    /// </summary>
    public void TriggerRotateTogglePreset()
    {
        if (_isRotating) return;

        _presetDirection = (_presetDirection == E_PresetDirection.StartToEnd)
            ? E_PresetDirection.EndToStart
            : E_PresetDirection.StartToEnd;

        TriggerRotateByPresetDirection();
    }

    // 공통 시작 루틴: raw 각도 from → to로 회전. 필요 시 현재 각도를 from으로 스냅
    private void BeginRotateRaw(float fromRaw, float toRaw, bool snapCurrentToFrom)
    {
        _isRotating = true;
        _elapsed = 0f;

        _rawStartAngle = fromRaw;
        _rawTargetAngle = toRaw;

        if (snapCurrentToFrom)
        {
            _rawCurrentAngle = fromRaw;
            _rawPrevAngle = fromRaw;
            transform.rotation = Quaternion.Euler(0, 0, NormalizeToEuler(_rawCurrentAngle));
        }
        else
        {
            // 스냅을 하지 않으면 "현재"에서부터 toRaw까지 회전(보간 기준은 fromRaw~toRaw)
            _rawPrevAngle = _rawCurrentAngle;
        }

        RefreshPassengers();
        SetPlayerInputLock(true);
    }

    /// <summary>
    /// 강제 중단 후 현 각도로 스냅/점유 갱신
    /// </summary>
    public void ForceStopAndSnap()
    {
        _isRotating = false;
        transform.rotation = Quaternion.Euler(0, 0, NormalizeToEuler(_rawCurrentAngle));
        SnapTilesToGrid();
        UpdatePivotOccupancy();

        ReRegisterPassengersToGrid();

        SetPlayerInputLock(false);
        _passengers.Clear();
    }

    private void FinishRotate()
    {
        _isRotating = false;

        // raw → 표시각 반영
        _rawCurrentAngle = _rawTargetAngle;
        transform.rotation = Quaternion.Euler(0, 0, NormalizeToEuler(_rawCurrentAngle));

        SnapTilesToGrid();
        UpdatePivotOccupancy();
        
        // 탑승객 재등록
        ReRegisterPassengersToGrid();

        SetPlayerInputLock(false);
        _passengers.Clear();
    }

    private void UpdatePivotOccupancy()
    {
        // 피벗(부모) 셀을 재평가하여 점유 갱신
        var newCell = GridUtil.WorldToCell(transform.position);
        var oldCell = _gridObj.CurrentCell;
        GridOccupancy.Instance?.Unregister(oldCell);
        GridOccupancy.Instance?.TryRegister(_gridObj, newCell);

        // 필요 시: 레이저/경로 리캐스트 통지
        //LaserWorldEvents.RaiseWorldChanged();
    }

    private void RotatePassengers(float deltaAngleDeg)
    {
        if (_passengers.Count == 0) return;

        float rad = deltaAngleDeg * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        Vector2 pivot = transform.position;

        foreach (var rb in _passengers)
        {
            if (rb == null) continue;
            if (((1 << rb.gameObject.layer) & _passengerMask.value) == 0) continue;

            Vector2 origin = rb.position;
            Vector2 dir = origin - pivot;

            // 회전 목표 좌표
            Vector2 rotated = new Vector2(
                dir.x * cos - dir.y * sin,
                dir.x * sin + dir.y * cos
            );
            Vector2 target = pivot + rotated;

            if (_usePrecastClamp)
            {
                // 소각도 프레임 단위라 직선 BoxCast로 근사(충분히 안정적)
                Vector2 straight = target - origin;
                float dist = straight.magnitude;
                if (dist > 0f)
                {
                    var col = rb.GetComponent<Collider2D>();
                    if (col != null)
                    {
                        var size = col.bounds.size;
                        var hit = Physics2D.BoxCast(origin, size, 0f, straight.normalized, dist, _blockingMask);
                        if (hit.collider != null)
                        {
                            target = origin + straight.normalized * hit.distance;
                        }
                    }
                }
            }

            rb.MovePosition(target);

            // 2) 방향 동기 회전(옵션)
            if (_rotatePassengerOrientation)
            {
                rb.MoveRotation(rb.rotation + deltaAngleDeg);
            }
        }
    }

    private void SnapTilesToGrid()
    {
        // 피벗 위치를 셀 중심으로 스냅
        var pivotCellNow = GridUtil.WorldToCell(transform.position);
        transform.position = GridUtil.CellToWorldCenter(pivotCellNow);

        // 모든 자식을 스냅 모드에 따라 정리
        if (_snapMode == E_SnapMode.RightAngleDiscrete && IsRightAngleDelta(_rawTargetAngle - _rawStartAngle))
        {
            // 90° 배수 회전: 자식 셀 상대좌표를 정수 회전
            int quarterTurns = Mod4(Mathf.RoundToInt((_rawTargetAngle - _rawStartAngle) / 90f));
            foreach (Transform child in transform)
            {
                Vector3Int childCell = GridUtil.WorldToCell(child.position);
                Vector3Int local = childCell - pivotCellNow;

                Vector3Int rotatedLocal = local;
                for (int i = 0; i < quarterTurns; i++)
                {
                    // 반시계 90도: (x, y) -> (-y, x)
                    rotatedLocal = new Vector3Int(-rotatedLocal.y, rotatedLocal.x, 0);
                }

                Vector3Int newCell = pivotCellNow + rotatedLocal;
                child.position = GridUtil.CellToWorldCenter(newCell);
            }
        }
        else
        {
            // 근사 스냅: 각 자식을 가장 가까운 셀 중심으로 스냅
            foreach (Transform child in transform)
            {
                Vector3Int cell = GridUtil.WorldToCell(child.position);
                child.position = GridUtil.CellToWorldCenter(cell);
            }
        }
    }

    private static bool IsRightAngleDelta(float rawDelta)
    {
        // rawDelta가 90° 배수인지 허용오차 내에서 검사
        float q = rawDelta / 90f;
        return Mathf.Abs(q - Mathf.Round(q)) < 0.01f;
    }

    private static int Mod4(int v)
    {
        v %= 4;
        if (v < 0)
        {
            v += 4;
        }

        return v;
    }

    private static float NormalizeToEuler(float a)
    {
        // 표시용 Euler 변환: 0~360 → -180~180
        float ang = Mathf.Repeat(a, 360f);
        if (ang > 180f)
        {
            ang -= 360f;
        }

        return ang;
    }

    private void RefreshPassengers()
    {
        _passengers.Clear();

        if (_triggerCollector != null)
        {
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
            // 트리거가 없다면, 부모 본체 콜라이더 상면 부근에서 OverlapBox로 대체 수집
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

    private bool ApproximatelyRelative(float a, float b, float tolerance = 0.0001f)
    {
        return Mathf.Abs(a - b) <= tolerance * Mathf.Max(1f, Mathf.Max(Mathf.Abs(a), Mathf.Abs(b)));
    }
}
