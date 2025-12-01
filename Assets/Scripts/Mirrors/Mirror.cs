///
/// Collider2D에는 Mirror 레이어(_mirrorMask)를 할당.
///

using UnityEngine;

public enum E_MirrorShapeType
{
    Slash,      // '/' 모양
    BackSlash,  // '\' 모양
    Vertical,   // '｜' 모양
    Horizontal, // '―' 모양
}

public class Mirror : MonoBehaviour
{
    [Header("Mirror Settings")]
    [SerializeField] private E_MirrorShapeType _shapeType = E_MirrorShapeType.Slash;
    public E_MirrorShapeType ShapeType => _shapeType;

    #region 레이저 반사

    // 4방향 (Right, Left, Up, Down) 기준 반사
    public Vector2Int Reflect(Vector2Int inDir)
    {
        // 정규화: -1,0,1 중 하나
        inDir = new Vector2Int(
            Mathf.Clamp(inDir.x, -1, 1),
            Mathf.Clamp(inDir.y, -1, 1)
        );

        switch (_shapeType)
        {
            case E_MirrorShapeType.Slash:
            default:
                {
                    // '/' 반사 규칙
                    // Right -> Up, Up -> Right, Left -> Down, Down -> Left
                    if (inDir == Vector2Int.right) return Vector2Int.up;
                    if (inDir == Vector2Int.up) return Vector2Int.right;
                    if (inDir == Vector2Int.left) return Vector2Int.down;
                    if (inDir == Vector2Int.down) return Vector2Int.left;

                    break;
                }
            case E_MirrorShapeType.BackSlash:
                {
                    // '\' 반사 규칙
                    // Right -> Down, Down -> Right, Left -> Up, Up -> Left
                    if (inDir == Vector2Int.right) return Vector2Int.down;
                    if (inDir == Vector2Int.down) return Vector2Int.right;
                    if (inDir == Vector2Int.left) return Vector2Int.up;
                    if (inDir == Vector2Int.up) return Vector2Int.left;

                    break;
                }
            case E_MirrorShapeType.Vertical:
                {
                    // '｜' 반사 규칙
                    // 좌/우 → 반대, 상/하 → 그대로
                    if (inDir == Vector2Int.right) return Vector2Int.left;
                    if (inDir == Vector2Int.left) return Vector2Int.right;

                    break;
                }
            case E_MirrorShapeType.Horizontal:
                {
                    // '―' 반사 규칙
                    // 상/하 → 반대, 좌/우 → 그대로
                    if (inDir == Vector2Int.up) return Vector2Int.down;
                    if (inDir == Vector2Int.down) return Vector2Int.up;

                    break;
                }
        }

        // 예외: 4방향이 아니면 방향 유지
        return inDir;
    }

    #endregion

    #region 45° 회전 Slash(/) → Horizontal(―) → BackSlash(\) → Vertical(｜) → Slash(/) …

    public void Rotate45()
    {
        switch (_shapeType)
        {
            case E_MirrorShapeType.Slash:
                {
                    _shapeType = E_MirrorShapeType.Horizontal;
                    break;
                }
            case E_MirrorShapeType.Horizontal:
                {
                    _shapeType = E_MirrorShapeType.BackSlash;
                    break;
                }
            case E_MirrorShapeType.BackSlash:
                {
                    _shapeType = E_MirrorShapeType.Vertical;
                    break;
                }
            case E_MirrorShapeType.Vertical:
                {
                    _shapeType = E_MirrorShapeType.Slash;
                    break;
                }
        }

        // 비주얼은 Z축 기준 45도 회전으로 처리 (필요하면 조정)
        var rot = transform.rotation.eulerAngles;
        rot.z -= 45f;
        transform.rotation = Quaternion.Euler(rot);

        // 레이저 재계산 요청
        LaserWorldEvents.RaiseWorldChanged();
    }

    #endregion
}
