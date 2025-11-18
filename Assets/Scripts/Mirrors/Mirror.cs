///
/// Collider2D에는 Mirror 레이어(_mirrorMask)를 할당.
///

using UnityEngine;

public enum MirrorType
{
    None,
    Slash,      // '/' 모양
    BackSlash   // '\' 모양
}

public class Mirror : MonoBehaviour
{
    [SerializeField] private MirrorType _type = MirrorType.None;

    public MirrorType Type => _type;

    // 4방향 (Right, Left, Up, Down) 기준 반사
    public Vector2Int Reflect(Vector2Int inDir)
    {
        // 정규화: -1,0,1 중 하나
        inDir = new Vector2Int(
            Mathf.Clamp(inDir.x, -1, 1),
            Mathf.Clamp(inDir.y, -1, 1)
        );

        if (_type == MirrorType.Slash)
        {
            // '/' 반사 규칙
            // Right -> Up, Up -> Right, Left -> Down, Down -> Left
            if (inDir == Vector2Int.right) return Vector2Int.up;
            if (inDir == Vector2Int.up) return Vector2Int.right;
            if (inDir == Vector2Int.left) return Vector2Int.down;
            if (inDir == Vector2Int.down) return Vector2Int.left;
        }
        else
        {
            // '\' 반사 규칙
            // Right -> Down, Down -> Right, Left -> Up, Up -> Left
            if (inDir == Vector2Int.right) return Vector2Int.down;
            if (inDir == Vector2Int.down) return Vector2Int.right;
            if (inDir == Vector2Int.left) return Vector2Int.up;
            if (inDir == Vector2Int.up) return Vector2Int.left;
        }

        // 예외: 4방향이 아니면 방향 유지
        return inDir;
    }
}
