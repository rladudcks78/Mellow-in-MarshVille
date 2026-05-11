// TileManager.cs
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Tilemaps;

public class TileManager : MonoBehaviour
{
    public static TileManager Instance { get; private set; }

    [Header("Tilemaps")]
    [SerializeField] private Tilemap _farmGroundTilemap; // 땅 타일맵 (갈린 땅 등 표시)
    [SerializeField] private Tilemap _cropTilemap;       // 작물 타일맵 (식물 표시)
    [SerializeField] private Tilemap _waterTilemap;      // 물 타일맵 (물 여부 판단용)

    [Header("Visual Resources")]
    [SerializeField] private TileBase _tilledTileVisual;     // 마른 갈린 땅 이미지
    [SerializeField] private TileBase _wetTilledTileVisual;  // 젖은 갈린 땅 이미지
    [SerializeField] private TileBase _dirtTileVisual;       // 일반 땅 이미지

    // 좌표(Grid)별 타일 데이터 저장소 (Key: x,y 좌표)
    private readonly Dictionary<Vector2Int, TileData> _tileDataDict = new Dictionary<Vector2Int, TileData>();

    // Crop Tile 캐시(스프라이트마다 Tile 인스턴스 재사용)
    private readonly Dictionary<Sprite, Tile> _cropTileCache = new Dictionary<Sprite, Tile>(128);

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    // 저장된 모든 타일 데이터를 리스트로 반환
    public List<TileData> GetAllTiles() => _tileDataDict.Values.ToList();

    /// <summary>
    /// 월드 좌표 기반 타일 데이터 가져오기.
    /// 없으면 타일맵을 확인하고 새로 생성(Lazy Init).
    /// </summary>
    public TileData GetTileData(Vector3 worldPos)
    {
        if (_farmGroundTilemap == null) return null;

        Vector3Int cellPos = _farmGroundTilemap.WorldToCell(worldPos);
        Vector2Int key = new Vector2Int(cellPos.x, cellPos.y);

        if (_tileDataDict.TryGetValue(key, out var exist))
            return exist;

        TileData newData = new TileData(key);
        bool isValid = false;

        // 물 타일인지 확인
        if (_waterTilemap != null && _waterTilemap.HasTile(_waterTilemap.WorldToCell(worldPos)))
        {
            newData.state = TileData.TileState.Water;
            isValid = true;
        }
        // 농사 구역인지 확인
        else if (_farmGroundTilemap.HasTile(cellPos))
        {
            newData.state = TileData.TileState.Dirted;
            isValid = true;
        }

        if (isValid)
        {
            _tileDataDict.Add(key, newData);
            return newData;
        }

        return null;
    }

    // =========================
    // View (그리기)
    // =========================

    public void RefreshTileVisual(TileData data)
    {
        if (data == null) return;
        if (_farmGroundTilemap == null) return;
        if (data.state == TileData.TileState.Water) return;

        Vector3Int pos = new Vector3Int(data.position.x, data.position.y, 0);
        TileBase targetTile = _dirtTileVisual;

        if (data.state == TileData.TileState.Tilled || data.state == TileData.TileState.Seeded)
        {
            TileBase wetTile = _wetTilledTileVisual != null ? _wetTilledTileVisual : _tilledTileVisual;
            targetTile = data.isWatered ? wetTile : _tilledTileVisual;
        }

        _farmGroundTilemap.SetTile(pos, targetTile);
    }

    /// <summary>
    /// 작물 이미지 갱신 (Sprite -> Tile 캐시 사용)
    /// </summary>
    public void UpdateCropSprite(Vector2Int gridPos, Sprite sprite)
    {
        if (_cropTilemap == null) return;

        Vector3Int pos = new Vector3Int(gridPos.x, gridPos.y, 0);

        if (sprite == null)
        {
            _cropTilemap.SetTile(pos, null);
            return;
        }

        if (!_cropTileCache.TryGetValue(sprite, out var tile) || tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.hideFlags = HideFlags.DontSave;
            _cropTileCache[sprite] = tile;
        }

        _cropTilemap.SetTile(pos, tile);
    }

    // =========================
    // Utility
    // =========================

    public Vector3Int WorldToCell(Vector3 worldPos)
    {
        if (_farmGroundTilemap == null) return default;
        return _farmGroundTilemap.WorldToCell(worldPos);
    }

    public Vector3 GetWorldPosFromGrid(Vector2Int gridPos)
    {
        if (_farmGroundTilemap == null) return default;

        Vector3 cellWorldPos = _farmGroundTilemap.CellToWorld(new Vector3Int(gridPos.x, gridPos.y, 0));
        return cellWorldPos + _farmGroundTilemap.cellSize * 0.5f;
    }

    /// <summary>
    /// 로드 누적 방지용: 기존 dict + 비주얼(ground/crop) 초기화
    /// </summary>
    public void ClearAllTilesAndVisuals()
    {
        if (_farmGroundTilemap != null)
        {
            foreach (var td in _tileDataDict.Values)
            {
                if (td == null) continue;
                if (td.state == TileData.TileState.Water) continue;

                var pos = new Vector3Int(td.position.x, td.position.y, 0);
                _farmGroundTilemap.SetTile(pos, _dirtTileVisual);
            }
        }

        if (_cropTilemap != null)
            _cropTilemap.ClearAllTiles();

        _tileDataDict.Clear();

        // 캐시는 유지해도 되지만, 혹시 런타임 중 스프라이트가 자주 바뀌면 커질 수 있으니 원하면 비움.
        // _cropTileCache.Clear();
    }

    // --------------------------------------------------------------------
    // ⚠아래 CaptureSave/ApplySave는 "FarmSaveProviderV1로만 저장/로드"면 필요없음.
    // 유지하고 싶으면 enum 캐스팅 대신 switch 매핑(Provider와 동일)로 바꾸는게 안전.
    // --------------------------------------------------------------------
    public FarmSaveData CaptureSave()
    {
        var save = new FarmSaveData();

        foreach (var td in _tileDataDict.Values)
        {
            if (td == null) continue;

            save.tiles.Add(new TileSaveData
            {
                x = td.position.x,
                y = td.position.y,
                state = ToSaveState(td.state),
                currentCropId = td.currentCropID,
                isWatered = td.isWatered,
                progressDay = td.progressDay,
                isDry = td.isDry
            });
        }

        save.Normalize();
        return save;
    }

    public void ApplySave(FarmSaveData data)
    {
        if (data == null) return;
        data.Normalize();

        _tileDataDict.Clear();
        if (data.tiles == null) return;

        for (int i = 0; i < data.tiles.Count; i++)
        {
            var t = data.tiles[i];
            if (t == null) continue;

            var pos = new Vector2Int(t.x, t.y);
            var td = new TileData(pos);

            td.state = ToRuntimeState(t.state);
            td.currentCropID = t.currentCropId;
            td.isWatered = t.isWatered;
            td.progressDay = t.progressDay;
            td.isDry = t.isDry;

            _tileDataDict[pos] = td;
        }
    }

    private static TileSaveData.TileStateV1 ToSaveState(TileData.TileState s)
    {
        return s switch
        {
            TileData.TileState.Dirted => TileSaveData.TileStateV1.Dirted,
            TileData.TileState.Tilled => TileSaveData.TileStateV1.Tilled,
            TileData.TileState.Seeded => TileSaveData.TileStateV1.Seeded,
            TileData.TileState.Dried => TileSaveData.TileStateV1.Dried,
            TileData.TileState.Water => TileSaveData.TileStateV1.Water,
            _ => TileSaveData.TileStateV1.Dirted
        };
    }

    private static TileData.TileState ToRuntimeState(TileSaveData.TileStateV1 s)
    {
        return s switch
        {
            TileSaveData.TileStateV1.Dirted => TileData.TileState.Dirted,
            TileSaveData.TileStateV1.Tilled => TileData.TileState.Tilled,
            TileSaveData.TileStateV1.Seeded => TileData.TileState.Seeded,
            TileSaveData.TileStateV1.Dried => TileData.TileState.Dried,
            TileSaveData.TileStateV1.Water => TileData.TileState.Water,
            _ => TileData.TileState.Dirted
        };
    }
}