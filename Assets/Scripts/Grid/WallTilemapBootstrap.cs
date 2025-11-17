///
/// 벽 Tilemap 오브젝트에 부착.
/// 
/// TilemapCollider2D는 제거하거나, Collider가 붙어 있어도 Player와는 충돌 안 하는 레이어로 둔다.
///

using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(Tilemap))]
public class WallTilemapBootstrap : MonoBehaviour
{
    private void Awake()
    {
        var tilemap = GetComponent<Tilemap>();
        // GridOccupancy.Instance.InitWallsFromTilemap(tilemap);
        GridOccupancy.Instance.AddWallsFromTilemap(tilemap);
    }
}
