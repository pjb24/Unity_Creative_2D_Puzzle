///
/// GridObject: 모든 퍼즐 오브젝트 공통 베이스
/// 
/// - OnEnable 시 가장 가까운 타일 중심으로 스냅 + (차단 오브젝트면) 점유 등록
/// - BlocksMovement=false이면 점유 등록 안 함 + Collider2D를 Trigger로 전환(옵션)
/// 

using UnityEngine;

public class GridObject : MonoBehaviour
{
    [Header("Editor")]
    [SerializeField] private bool _snapEveryFrameInEditor = true;

    [Header("Blocking")]
    [Tooltip("true: 셀 점유(이동 차단). false: 점유하지 않음(비충돌).")]
    public bool BlocksMovement = true;

    [SerializeField, Tooltip("BlocksMovement=false일 때 Collider2D를 isTrigger=true로 전환")]
    private bool _setColliderTriggerWhenNonBlocking = true;

    // 상태
    public Vector3Int CurrentCell { get; set; }

    private Collider2D[] _colliders;
    private Collider2D _collider;

    private void Awake()
    {
        _collider = GetComponent<Collider2D>();
        _colliders = GetComponentsInChildren<Collider2D>(includeInactive: true);
    }

    private void OnEnable()
    {
        // 1) 스냅
        GridUtil.SnapTransformToGrid(transform);

        var cell = GridUtil.WorldToCell(transform.position);
        
        // 2) 점유 등록(Blocking일 때만)
        var occ = GridOccupancy.Instance;
        if (occ != null && BlocksMovement)
        {
            // 실패 시(이미 점유/벽) 가까운 인접 셀 탐색 같은 로직은 필요 시 확장
            occ.TryRegister(this, cell);
        }

        // 3) Collider 모드 적용(Non-Blocking이면 Trigger화)
        ApplyColliderMode();
    }

    private void OnDisable()
    {
        var occ = GridOccupancy.Instance;
        if (occ != null && BlocksMovement)
        {
            occ.Unregister(CurrentCell);
        }
    }

    /// <summary>
    /// 런타임에서 동적으로 차단/비차단 전환
    /// - 차단→비차단: 점유 해제 + Collider Trigger 전환
    /// - 비차단→차단: Collider 복구 + 점유 등록 시도
    /// </summary>
    public void SetBlocking(bool block)
    {
        if (BlocksMovement == block) return;

        var occ = GridOccupancy.Instance;

        if (!block)
        {
            // 차단 해제: 점유 해제
            if (occ != null) occ.Unregister(CurrentCell);
            BlocksMovement = false;
            ApplyColliderMode();
        }
        else
        {
            // 차단 활성: 현재 셀 기준 재등록
            BlocksMovement = true;
            ApplyColliderMode();

            var cell = GridUtil.WorldToCell(transform.position);

            if (occ != null)
            {
                // 목적 셀이 막혀있다면 호출측에서 위치 조정 후 다시 SetBlocking(true) 호출하는 패턴 권장
                if (!occ.TryRegister(this, cell))
                {
                    Debug.LogWarning($"[GridObject] Register failed at {cell} (blocked). Move object to a free cell then SetBlocking(true) again.", this);
                    BlocksMovement = false;           // 실패 시 상태 롤백
                    ApplyColliderMode();              // 다시 Non-Blocking 모드 적용
                }
            }
        }
    }

    /// <summary>
    /// 현재 Transform 위치를 기준으로 셀/스냅/등록 갱신 (필요 시 수동 호출)
    /// </summary>
    public void RefreshRegistration()
    {
        GridUtil.SnapTransformToGrid(transform);

        var cell = GridUtil.WorldToCell(transform.position);

        var occ = GridOccupancy.Instance;
        if (occ == null) return;

        if (BlocksMovement)
        {
            // 이미 등록된 경우 이동, 아니면 등록 시도
            if (!occ.TryGetCellOf(this, out var prev))
            {
                occ.TryRegister(this, cell);
            }
            else if (prev != cell)
            {
                occ.TryMove(this, cell);
            }
        }
        // Non-Blocking이면 점유 테이블 갱신 불필요
    }

    private void ApplyColliderMode()
    {
        if (_colliders == null || _colliders.Length == 0) return;

        if (!BlocksMovement && _setColliderTriggerWhenNonBlocking)
        {
            foreach (var c in _colliders)
            {
                if (c == null) continue;
                c.isTrigger = true;
                // 충돌 자체를 완전히 끄고 싶으면 enabled=false 도 가능
                // c.enabled = false;
            }

            if (_collider != null)
            {
                _collider.isTrigger = true;
            }
        }
        else
        {
            foreach (var c in _colliders)
            {
                if (c == null) continue;
                c.isTrigger = false;
                // c.enabled = true;
            }

            if (_collider != null)
            {
                _collider.isTrigger = false;
            }
        }
    }

#if UNITY_EDITOR
    // 씬에서 손으로 움직일 때 자동으로 스냅하고 싶으면
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            if (_snapEveryFrameInEditor)
            {
                GridUtil.SnapTransformToGrid(transform);
                Debug.Log("[OnValidate] GridSnap Active, Object Name: " + gameObject.name);
            }

            // 에디터에서도 모드 즉시 반영
            if (_colliders == null || _colliders.Length == 0)
            {
                _colliders = GetComponentsInChildren<Collider2D>(includeInactive: true);
            }
            ApplyColliderMode();
        }
    }
#endif
}
