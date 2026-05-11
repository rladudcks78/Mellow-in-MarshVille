using UnityEngine;
using UnityEngine.Tilemaps;
using System.Collections.Generic;

public class TileHighlighter : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Tilemap _highlightTilemap;
    [SerializeField] private TileBase _validTile;   // 초록색 (유효)
    [SerializeField] private TileBase _invalidTile; // 빨간색 (불가/거리멂)

    [Header("Dependencies")]
    [SerializeField] private TileDistanceChecker _distChecker;
    [SerializeField] private InventorySystem _inventory;

    // 도구 타입 상수
    private const string TOOL_HOE = "hoe";
    private const string TOOL_WATERING_CAN = "wateringCan";
    private const string TOOL_SICKLE = "sickle";

    // [수정] 최적화 변수(_lastMousePos) 삭제: 실시간 반응성을 위해 매 프레임 갱신

    private void Update()
    {
        if (_highlightTilemap == null || _distChecker == null || _inventory == null) return;

        // 1. 도구 들고 있는지 확인
        if (!_inventory.TryGetActive(out _, out _, out ItemDef item) || item == null)
        {
            ClearHighlight();
            return;
        }

        if (!IsFarmingTool(item))
        {
            ClearHighlight();
            return;
        }

        // 2. 위치 및 방향 계산
        Vector3 mouseWorldPos = _distChecker.GetMouseWorldPosition();
        Vector3 playerWorldPos = transform.position;

        // 마우스 그리드 좌표
        Vector3Int mouseGridPos3 = TileManager.Instance.WorldToCell(mouseWorldPos);
        Vector2Int mouseGridPos = new Vector2Int(mouseGridPos3.x, mouseGridPos3.y);

        // 방향 계산 (플레이어 -> 마우스)
        Vector2 dirVec = (mouseWorldPos - playerWorldPos);
        Vector2Int facingDir;

        if (Mathf.Abs(dirVec.x) > Mathf.Abs(dirVec.y))
            facingDir = dirVec.x > 0 ? Vector2Int.right : Vector2Int.left;
        else
            facingDir = dirVec.y > 0 ? Vector2Int.up : Vector2Int.down;

        // [핵심 수정] 
        // 기존: if (mouseGridPos3 == _lastMousePos) return; <- 이 줄을 삭제했습니다.
        // 이유: 마우스가 가만히 있어도 플레이어가 움직이면 '거리'나 '방향'이 바뀔 수 있으므로 계속 그려줘야 합니다.

        DrawHighlight(mouseGridPos, facingDir, item);
    }

    private void DrawHighlight(Vector2Int centerPos, Vector2Int facingDir, ItemDef item)
    {
        _highlightTilemap.ClearAllTiles();

        List<Vector2Int> rangeCells = ToolRangeCalculator.GetToolRangeCells(centerPos, facingDir, item);

        // [중요] 여기서 매 프레임 거리를 다시 체크하므로, 
        // 마우스가 고정되어 있어도 플레이어가 다가오면 true로 바뀝니다.
        bool isInRange = _distChecker.IsInInteractionRange();

        foreach (var cell in rangeCells)
        {
            Vector3Int tilePos = new Vector3Int(cell.x, cell.y, 0);
            Vector3 cellWorldPos = TileManager.Instance.GetWorldPosFromGrid(cell);
            TileData tileData = TileManager.Instance.GetTileData(cellWorldPos);

            // 농사 구역 밖이면 스킵
            if (tileData == null || tileData.state == TileData.TileState.Water) continue;

            // 유효성 체크 (땅 상태 확인)
            bool isValidState = CheckFarmValidity(item, tileData);

            // [최종 색상 결정 로직]
            // 1. 거리가 멀면 -> 무조건 빨강 (_invalidTile)
            // 2. 거리가 가까우면 -> 상태가 유효하면 초록(_validTile), 아니면 빨강(_invalidTile)
            TileBase tileToDraw;

            if (!isInRange)
            {
                tileToDraw = _invalidTile; // 거리 멂 = 빨강
            }
            else
            {
                tileToDraw = isValidState ? _validTile : _invalidTile; // 거리 가까움 = 상태 따라 결정
            }

            _highlightTilemap.SetTile(tilePos, tileToDraw);
        }
    }

    public void ClearHighlight()
    {
        if (_highlightTilemap != null) _highlightTilemap.ClearAllTiles();
    }

    private bool IsFarmingTool(ItemDef item)
    {
        if (item.isSeed) return true;
        if (item.IsTool && !string.IsNullOrEmpty(item.toolType))
        {
            string type = item.toolType.Trim();
            return type.Equals(TOOL_HOE, System.StringComparison.OrdinalIgnoreCase) ||
                   type.Equals(TOOL_WATERING_CAN, System.StringComparison.OrdinalIgnoreCase) ||
                   type.Equals(TOOL_SICKLE, System.StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    private bool CheckFarmValidity(ItemDef item, TileData tile)
    {
        if (tile == null) return false;
        if (item.isSeed) return tile.state == TileData.TileState.Tilled && !tile.HasCrop;

        string type = item.toolType != null ? item.toolType.Trim() : "";
        if (type.Equals(TOOL_HOE, System.StringComparison.OrdinalIgnoreCase))
            return tile.state == TileData.TileState.Dirted || tile.state == TileData.TileState.Dried;
        if (type.Equals(TOOL_WATERING_CAN, System.StringComparison.OrdinalIgnoreCase))
            return (tile.state == TileData.TileState.Tilled || tile.state == TileData.TileState.Seeded) && !tile.isWatered;
        if (type.Equals(TOOL_SICKLE, System.StringComparison.OrdinalIgnoreCase))
            return tile.HasCrop;

        return false;
    }
}