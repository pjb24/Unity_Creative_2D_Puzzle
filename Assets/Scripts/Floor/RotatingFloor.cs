///
/// 목표:
/// 중심 기준으로 Z축 회전 한 번
/// 호출형 RotateOnce()만 제공
///
/// 
/// RotatingFloor (GridObject / GridOccupancy / GridUtil 연동 통합 스크립트)
/// - 중심 피벗을 기준으로 일정 시간 동안 angleStep만큼 회전(시계/반시계)
/// - 회전 중 위에 있는 Rigidbody2D 탑승객을 deltaAngle만큼 원운동 MovePosition으로 동기 이동
/// - 충돌 시 탑승객만 멈추고 바닥은 계속 회전(Physics2D에 위임, 선택적으로 선행 클램프 지원)
/// - 종료 시 타일 스냅(GridUtil) 및 점유 갱신(GridOccupancy.TryRegister)
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
/// 사용법:
/// 1) 부모에 본 스크립트 + GridObject 부착 (부모가 피벗)
/// 2) 바닥 타일들은 부모의 자식 트랜스폼(필요 시 각 타일도 GridObject 보유 가능)
/// 3) 탑승 감지용 Trigger Collider2D 하나 배치(부모 또는 자식)
/// 4) TriggerRotate(true/false) 호출로 회전 시작
/// 

using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(GridObject))]
public class RotatingFloor : MonoBehaviour
{
    public float rotateAngle = 90f;      // 한 번에 몇 도 회전할지
    public float rotateDuration = 0.5f;  // 회전 시간
    public bool playOnStart = false;

    private bool _isRotating;

    private void Start()
    {
        if (playOnStart)
            RotateOnce();
    }

    public void RotateOnce()
    {
        if (_isRotating) return;
        StartCoroutine(RotateRoutine());
    }

    private IEnumerator RotateRoutine()
    {
        _isRotating = true;

        float t = 0f;
        float startAngle = transform.eulerAngles.z;
        float targetAngle = startAngle + rotateAngle;

        while (t < rotateDuration)
        {
            t += Time.deltaTime;
            float normalized = Mathf.Clamp01(t / rotateDuration);
            float angle = Mathf.Lerp(startAngle, targetAngle, normalized);
            var euler = transform.eulerAngles;
            euler.z = angle;
            transform.eulerAngles = euler;
            yield return null;
        }

        var finalEuler = transform.eulerAngles;
        finalEuler.z = targetAngle;
        transform.eulerAngles = finalEuler;

        _isRotating = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // MovingFloor랑 동일하게 rider를 자식으로 붙여서 회전만 같이 처리
        other.transform.SetParent(transform);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.transform.parent == transform)
            other.transform.SetParent(null);
    }
}
