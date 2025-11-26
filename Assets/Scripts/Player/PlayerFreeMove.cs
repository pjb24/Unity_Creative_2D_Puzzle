///
/// 벽은 TilemapCollider2D / BoxCollider2D + Layer Collision으로 막는다.
/// 이제 더 이상 GridOccupancy로 플레이어 이동을 검사하지 않는다.
/// 
/// 
/// 푸시(밀기)는 충돌 기반 + GridOccupancy 혼합
/// 플레이어 콜라이더가 PushableMirror 콜라이더와 닿은 상태에서,
/// 이동 입력 방향 기준으로 “미는 방향 셀” 계산 → TryPush(dir) 호출
/// 
/// 충돌 중인 PushableMirror 목록을 유지하고,
/// 입력 방향이 그쪽일 때만 pushable.TryPush(gridDir) 호출
/// 
/// 
/// MovingFloor / RotatingFloor와의 조화
/// MovingFloor / RotatingFloor는 여전히 GridObject + kinematic Rigidbody2D
/// 퍼즐 로직에서 셀 단위로 이동/회전 후, transform.position/rotation 스냅
/// 플레이어는 그냥 Rigidbody2D 충돌/OnCollisionStay로 “끌려가는” 패턴 사용
/// 
/// MovingFloor 쪽에서 FixedUpdate에 자신의 delta를 기억해두고,
/// 그 delta를 플레이어 Rigidbody2D에 더해주는 패턴도 가능
///

using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerPusher))]
public class PlayerFreeMove : MonoBehaviour
{
    public float moveSpeed = 5f;

    private PlayerPusher _pusher;

    private Rigidbody2D _rb;
    private Vector2 _input;
    private Vector2 _facingDir = Vector2.up;

    private Vector2 _filteredDir;

    private bool _lockMovement = false;

    private void Awake()
    {
        _pusher = GetComponent<PlayerPusher>();
        _rb = GetComponent<Rigidbody2D>();
        gameObject.tag = "Player";
        _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        _rb.gravityScale = 0f;
    }

    private void Update()
    {
        _input.x = Input.GetAxisRaw("Horizontal");
        _input.y = Input.GetAxisRaw("Vertical");
        _input = _input.normalized;

        if (_input.sqrMagnitude > 0.01f)
        {
            _facingDir = _input;
            _pusher.SetFacingDir(Vector2Int.FloorToInt(_facingDir));
        }

        _filteredDir = _pusher.FilterMoveAndTryPush(_input);
    }

    private void FixedUpdate()
    {
        if (_lockMovement)
        {
            return;
        }

        var target = _rb.position + _filteredDir * moveSpeed * Time.fixedDeltaTime;
        _rb.MovePosition(target); // 물리 기반 연속 이동
    }

    public Vector2 FacingDir => _facingDir;

    public void SetExternalLock(bool flag)
    {
        _lockMovement = flag;
    }
}
