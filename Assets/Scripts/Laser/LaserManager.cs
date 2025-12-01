///
/// LaserManager : 모든 emitter 경로 계산.
/// 
/// 그리드 기반 퍼즐이니까, 셀 단위로 레이캐스트하는 게 안정적.
/// 
/// Dirty 플래그 세우고 Update에서 한 번에 리캐스트하는 구조.
/// 
/// 
/// _wallMask : 벽, 퍼즐에 사용되는 Block 레이어들
/// _mirrorMask : 거울들
/// 문(door)은 레이저 충돌 대상에서 빼면 → 자동으로 통과
/// 문도 맞추고 싶으면 Door용 Layer + Raycast, Hit 시 “그냥 지나감” 처리만 하고 계속 진행.
/// 
/// 히트 목록 비교 방식으로 LaserReceiverPort에 LaserEnter/Exit 호출
///

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-40)]
public class LaserManager : MonoBehaviour
{
    public enum E_HitObjectType
    {
        None,
        Wall,
        Mirror,
        Door,
        Device,
    }

    public static LaserManager Instance { get; private set; }

    [SerializeField] private LayerMask _wallMask;
    [SerializeField] private LayerMask _mirrorMask;
    [SerializeField] private LayerMask _doorMask;
    [SerializeField] private LayerMask _deviceMask;
    [SerializeField] private float _maxDistance = 100f;
    [SerializeField] private int _maxBounce = 10;

    [SerializeField] private float _reflectionOffset = 0.5f;

    private readonly List<LaserEmitter> _emitters = new();
    private bool _isDirty = true;   // 리캐스트 필요 플래그

    // emitter별 이전 히트 포트 집합(히트 목록 비교용)
    private readonly Dictionary<LaserEmitter, HashSet<LaserReceiverPort>> _prevReceiversPerEmitter = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnEnable()
    {
        //LaserWorldEvents.OnWorldChanged += MarkDirty;
        LaserWorldEvents.OnWorldChanged += StartMarkDirtyCoroutine;
    }

    private void OnDisable()
    {
        //LaserWorldEvents.OnWorldChanged -= MarkDirty;
        LaserWorldEvents.OnWorldChanged -= StartMarkDirtyCoroutine;
    }

    private void Update()
    {
        if (!_isDirty) return;

        RecalculateAll();
        _isDirty = false;
    }

    public void RegisterEmitter(LaserEmitter emitter)
    {
        if (!_emitters.Contains(emitter))
        {
            _emitters.Add(emitter);
        }

        if (!_prevReceiversPerEmitter.ContainsKey(emitter))
        {
            _prevReceiversPerEmitter[emitter] = new HashSet<LaserReceiverPort>();
        }

        MarkDirty();
    }

    public void UnregisterEmitter(LaserEmitter emitter)
    {
        // 기존에 히트 중이던 포트들에 Exit 전달
        if (emitter != null && _prevReceiversPerEmitter.TryGetValue(emitter, out var prev))
        {
            foreach (var r in prev)
            {
                r?.LaserExit();
            }
            _prevReceiversPerEmitter.Remove(emitter);
        }

        _emitters.Remove(emitter);

        MarkDirty();
    }

    public void StartMarkDirtyCoroutine()
    {
        StartCoroutine(MarkDirtyCoroutine());
    }

    public void MarkDirty()
    {
        _isDirty = true;
    }

    private IEnumerator MarkDirtyCoroutine()
    {
        yield return new WaitForFixedUpdate();

        _isDirty = true;
    }

    private void RecalculateAll()
    {
        foreach (var emitter in _emitters)
        {
            if (emitter != null && emitter.isActiveAndEnabled)
            {
                CalculateLaserPath(emitter);
            }
            else
            {
                // 비활성/누락된 경우에도 안전하게 Exit 처리
                if (emitter != null && _prevReceiversPerEmitter.TryGetValue(emitter, out var prev))
                {
                    foreach (var r in prev)
                    {
                        r?.LaserExit();
                    }
                    _prevReceiversPerEmitter[emitter].Clear();
                }
            }
        }
    }

    private void CalculateLaserPath(LaserEmitter emitter)
    {
        if (emitter.Line == null) return;

        List<Vector3> points = new List<Vector3>();
        // 이번 프레임(또는 이번 재계산)의 히트 포트 집합
        HashSet<LaserReceiverPort> newReceivers = new HashSet<LaserReceiverPort>();

        Vector3 origin = emitter.transform.position;
        Vector2Int dirInt = emitter.Direction;
        Vector2 dir = dirInt; // Vector2Int -> Vector2
        points.Add(origin);

        int bounceCount = 0;

        while (bounceCount < _maxBounce)
        {
            RaycastHit2D hitWall = Physics2D.Raycast(origin, dir, _maxDistance, _wallMask);
            RaycastHit2D hitMirror = Physics2D.Raycast(origin, dir, _maxDistance, _mirrorMask);
            RaycastHit2D hitDoor = Physics2D.Raycast(origin, dir, _maxDistance, _doorMask);
            RaycastHit2D hitDevice = Physics2D.Raycast(origin, dir, _maxDistance, _deviceMask);

            bool hasWall = hitWall.collider != null;
            bool hasMirror = hitMirror.collider != null;
            bool hasDoor = hitDoor.collider != null;
            bool hasDevice = hitDevice.collider != null;

            // 아무것도 안 맞으면 직선 끝까지 쏘고 종료
            if (!hasWall && !hasMirror && !hasDoor && !hasDevice)
            {
                Vector3 endPoint = origin + (Vector3)dir * _maxDistance;
                points.Add(endPoint);

                break;
            }

            // 맞은 것 중 더 가까운 것 선택
            float wallDist = hasWall ? hitWall.distance : float.MaxValue;
            float mirrorDist = hasMirror ? hitMirror.distance : float.MaxValue;
            float doorDist = hasDoor ? hitDoor.distance : float.MaxValue;
            float deviceDist = hasDevice ? hitDevice.distance : float.MaxValue;

            // 가장 가까운 후보 및 그 히트 정보 보관
            E_HitObjectType closest = E_HitObjectType.None;
            RaycastHit2D closestHit = new RaycastHit2D();
            float closestDist = float.MaxValue;

            // 벽
            if (wallDist < closestDist)
            {
                closest = E_HitObjectType.Wall;
                closestHit = hitWall;
                closestDist = wallDist;
            }
            // 거울
            if (mirrorDist < closestDist)
            {
                closest = E_HitObjectType.Mirror;
                closestHit = hitMirror;
                closestDist = mirrorDist;
            }
            // 문
            if (doorDist < closestDist)
            {
                closest = E_HitObjectType.Door;
                closestHit = hitDoor;
                closestDist = doorDist;
            }
            // 장치
            if (deviceDist < closestDist)
            {
                closest = E_HitObjectType.Device;
                closestHit = hitDevice;
                closestDist = deviceDist;
            }

            // 히트 대상에 장착된 LaserReceiverPort 수집(문/거울/벽 어느 쪽이든 수집 시도)
            if (closest != E_HitObjectType.None && closestHit.collider != null)
            {
                var receiver = closestHit.collider.GetComponentInParent<LaserReceiverPort>();
                if (receiver != null)
                {
                    newReceivers.Add(receiver);
                }
            }

            if (closest == E_HitObjectType.None)
            {
                // 안전망: 없으면 직선으로 마감
                Vector3 endPoint = origin + (Vector3)dir * _maxDistance;
                points.Add(endPoint);
                break;
            }
            else if (closest == E_HitObjectType.Door)
            {
                // 문은 "맞추되" 통과. 포인트는 그려도 되고 생략 가능.
                // 포인트 살짝 포함해 디버그 가시성 확보
                Vector3 hitPos = closestHit.point + (dir * _reflectionOffset);
                points.Add(hitPos);

                origin = hitPos + (Vector3)dir * _reflectionOffset;
                continue; // 계속 쏨
            }
            else if (closest == E_HitObjectType.Mirror)
            {
                // 거울에 먼저 맞음
                Vector3 hitPos = closestHit.point + (dir * _reflectionOffset);
                points.Add(hitPos);

                // 새로운 방향 계산
                Mirror mirror = closestHit.collider.GetComponent<Mirror>();
                if (mirror == null)
                {
                    // Mirror 컴포넌트 없으면 막힌 걸로 취급
                    break;
                }

                dirInt = mirror.Reflect(dirInt);
                dir = dirInt;

                // 반사 지점에서 조금 떼고 다시 쏨(자기 자신 다시 맞는 것 방지)
                origin = hitPos + (Vector3)dir * _reflectionOffset;
                bounceCount++;
                continue;
            }
            else if (closest == E_HitObjectType.Wall)
            {
                // 벽에 먼저 맞음
                Vector3 hitPos = closestHit.point;
                points.Add(hitPos);
                // 벽에서 레이저 종료

                break;
            }
            else if (closest == E_HitObjectType.Device)
            {
                // 장치는 "맞추되" 통과. 포인트는 그려도 되고 생략 가능.
                // 포인트 살짝 포함해 디버그 가시성 확보
                Vector3 hitPos = closestHit.point + (dir * _reflectionOffset);
                points.Add(hitPos);

                origin = hitPos + (Vector3)dir * _reflectionOffset;
                continue; // 계속 쏨
            }
        }

        // LineRenderer에 포인트 지정
        emitter.Line.positionCount = points.Count;
        emitter.Line.SetPositions(points.ToArray());

        // === 히트 목록 비교로 Enter/Exit 호출 ===
        if (!_prevReceiversPerEmitter.TryGetValue(emitter, out var prevReceivers))
        {
            prevReceivers = new HashSet<LaserReceiverPort>();
            _prevReceiversPerEmitter[emitter] = prevReceivers;
        }

        // EXIT: 이전엔 있었는데 이번엔 없는 포트
        foreach (var prev in prevReceivers)
        {
            if (prev == null) continue; // 파괴 방어
            if (!newReceivers.Contains(prev))
            {
                prev.LaserExit();
            }
        }

        // ENTER: 이번에 새로 들어온 포트
        foreach (var curr in newReceivers)
        {
            if (curr == null) continue;
            if (!prevReceivers.Contains(curr))
            {
                curr.LaserEnter();
            }
        }

        // 상태 교체(참조 복사 대신 내용 교체로 GC 감소)
        prevReceivers.Clear();
        foreach (var r in newReceivers)
        {
            prevReceivers.Add(r);
        }
    }
}
