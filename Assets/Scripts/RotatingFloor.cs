///
/// 목표:
/// 중심 기준으로 Z축 회전 한 번
/// 호출형 RotateOnce()만 제공
///

using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
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
