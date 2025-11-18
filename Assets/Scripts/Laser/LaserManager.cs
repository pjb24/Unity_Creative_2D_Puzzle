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

using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-5)]
public class LaserManager : MonoBehaviour
{
    public enum E_HitObjectType
    {
        None,
        Wall,
        Mirror,
        Door,
    }

    public static LaserManager Instance { get; private set; }

    [SerializeField] private LayerMask _wallMask;
    [SerializeField] private LayerMask _mirrorMask;
    [SerializeField] private LayerMask _doorMask;
    [SerializeField] private float _maxDistance = 100f;
    [SerializeField] private int _maxBounce = 10;

    [SerializeField] private float _reflectionOffset = 1f;

    private readonly List<LaserEmitter> _emitters = new();
    private bool _isDirty = true;   // 리캐스트 필요 플래그

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
        LaserWorldEvents.OnWorldChanged += MarkDirty;
    }

    private void OnDisable()
    {
        LaserWorldEvents.OnWorldChanged -= MarkDirty;
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
            _emitters.Add(emitter);
        MarkDirty();
    }

    public void UnregisterEmitter(LaserEmitter emitter)
    {
        _emitters.Remove(emitter);
        MarkDirty();
    }

    public void MarkDirty()
    {
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
        }
    }

    private void CalculateLaserPath(LaserEmitter emitter)
    {
        if (emitter.Line == null) return;

        List<Vector3> points = new List<Vector3>();

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

            bool hasWall = hitWall.collider != null;
            bool hasMirror = hitMirror.collider != null;
            bool hasDoor = hitDoor.collider != null;

            // 아무것도 안 맞으면 직선 끝까지 쏘고 종료
            if (!hasWall && !hasMirror && !hasDoor)
            {
                Vector3 endPoint = origin + (Vector3)dir * _maxDistance;
                points.Add(endPoint);

                break;
            }

            // 맞은 것 중 더 가까운 것 선택
            float wallDist = hasWall ? hitWall.distance : float.MaxValue;
            float mirrorDist = hasMirror ? hitMirror.distance : float.MaxValue;
            float doorDist = hasDoor ? hitDoor.distance : float.MaxValue;

            E_HitObjectType closest = E_HitObjectType.None;
            if (wallDist < float.MaxValue)
            {
                closest = E_HitObjectType.Wall;
            }
            if (mirrorDist < wallDist)
            {
                closest = E_HitObjectType.Mirror;
            }
            if (doorDist < mirrorDist)
            {
                closest = E_HitObjectType.Door;
            }

            if (closest == E_HitObjectType.None ||  closest == E_HitObjectType.Door)
            {
                origin = (Vector3)hitDoor.point + (Vector3)dir * _reflectionOffset;
                continue; // 계속 쏨
            }
            else if (closest == E_HitObjectType.Mirror)
            {
                // 먼저 거울에 맞음
                Vector3 hitPos = hitMirror.point;
                points.Add(hitPos);

                // 새로운 방향 계산
                Mirror mirror = hitMirror.collider.GetComponent<Mirror>();
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
                Vector3 hitPos = hitWall.point;
                points.Add(hitPos);
                // 벽에서 레이저 종료

                break;
            }
        }

        // LineRenderer에 포인트 지정
        emitter.Line.positionCount = points.Count;
        emitter.Line.SetPositions(points.ToArray());
    }
}
