///
/// 목표:
/// StartCell → EndCell 까지 일정 시간 동안 한 번 움직이는 정도
/// Rider(위에 있는 오브젝트)는 일단 부모로 붙여서 같이 움직이게만 처리 (충돌·지능형은 나중)
/// 
/// 최소 기능만 있다: 이동 + 위에 올라온 애들 같이 데려가기
/// 타일/Occupancy 연동(이동 후 셀 스냅)은 나중에 퍼즐 로직 쪽에서 확장하면 된다.
/// 
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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class MovingFloor : MonoBehaviour
{
    public Vector3Int startCell;
    public Vector3Int endCell;
    public float moveDuration = 1.0f;
    public bool loop = true;          // 왕복 반복 여부
    public bool playOnStart = false;

    private Rigidbody2D _rb;
    private Vector3 _startPos;
    private Vector3 _endPos;

    private Vector3 _lastPos;
    private bool _isMoving;

    private readonly List<Rigidbody2D> _riders = new List<Rigidbody2D>();

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.bodyType = RigidbodyType2D.Kinematic;
    }

    private void Start()
    {
        // 에디터에서 cell 값만 세팅하고, 실제 위치는 여기서 스냅
        // transform.position = GridUtil.CellToWorldCenter(startCell);

        _startPos = GridUtil.CellToWorldCenter(startCell);
        _endPos = GridUtil.CellToWorldCenter(endCell);

        _rb.position = _startPos;
        _lastPos = _startPos;

        if (playOnStart)
        {
            StartMove();
        }
    }

    public void StartMove()
    {
        if (_isMoving) return;
        //StartCoroutine(MoveRoutine());
        StartCoroutine(MoveLoop());
    }

    private IEnumerator MoveLoop()
    {
        _isMoving = true;

        while (true)
        {
            // A -> B
            yield return MoveOnce(_startPos, _endPos);

            if (!loop) break;

            // B -> A
            yield return MoveOnce(_endPos, _startPos);
        }

        _isMoving = false;
    }

    private IEnumerator MoveOnce(Vector3 from, Vector3 to)
    {
        float t = 0f;
        _lastPos = from;
        _rb.position = from;

        // FixedUpdate 타이밍에 맞추는게 더 자연스러움
        var wait = new WaitForFixedUpdate();

        while (t < moveDuration)
        {
            t += Time.fixedDeltaTime;
            float normalized = Mathf.Clamp01(t / moveDuration);

            Vector3 newPos = Vector3.Lerp(from, to, normalized);
            Vector3 delta = newPos - _lastPos;

            // 발판 이동
            _rb.MovePosition(newPos);

            // 위에 올라탄 라이더들 이동
            for (int i = 0; i < _riders.Count; i++)
            {
                var rider = _riders[i];
                if (rider == null) continue;

                Vector2 riderPos = rider.position;
                rider.MovePosition(riderPos + (Vector2)delta);
            }

            _lastPos = newPos;

            yield return wait;
        }

        // 마지막 위치 스냅
        _rb.position = to;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        var rb = collision.rigidbody;
        if (rb == null) return;

        // 태울 대상을 필터링하고 싶으면 태그/레이어 조건 추가
        if (!_riders.Contains(rb))
        {
            _riders.Add(rb);
        }
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        var rb = collision.rigidbody;
        if (rb == null) return;

        _riders.Remove(rb);
    }

    private IEnumerator MoveRoutine()
    {
        _isMoving = true;

        Vector3 startPos = GridUtil.CellToWorldCenter(startCell);
        Vector3 endPos = GridUtil.CellToWorldCenter(endCell);

        float t = 0f;
        while (t < moveDuration)
        {
            t += Time.deltaTime;
            float normalized = Mathf.Clamp01(t / moveDuration);
            transform.position = Vector3.Lerp(startPos, endPos, normalized);
            yield return null;
        }

        transform.position = endPos;

        // 필요하면 여기서 start/end swap 가능
        // (왕복 플랫포머로 쓰고 싶으면)
        // var tmp = startCell; startCell = endCell; endCell = tmp;

        _isMoving = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 간단: 위에 올라온 오브젝트를 자식으로 붙여서 같이 이동시키기
        other.transform.SetParent(transform);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // 벗어나면 부모 해제
        if (other.transform.parent == transform)
            other.transform.SetParent(null);
    }
}
