using UnityEngine;

public sealed class FarmSaveProviderV1 : MonoBehaviour, IFarmSaveProviderV1
{
    [SerializeField] private FarmSystem farmSystem; // 있으면 드래그, 없으면 Find

    public FarmSaveData CaptureFarm()
    {
        var data = new FarmSaveData();

        if (TileManager.Instance == null) return data;

        var tiles = TileManager.Instance.GetAllTiles();
        for (int i = 0; i < tiles.Count; i++)
        {
            var t = tiles[i];
            if (t == null) continue;

            data.tiles.Add(new TileSaveData
            {
                x = t.position.x,
                y = t.position.y,
                state = ToSaveState(t.state),
                currentCropId = t.currentCropID,
                isWatered = t.isWatered,
                progressDay = t.progressDay,
                isDry = t.isDry
            });
        }

        data.Normalize();
        return data;
    }

    public void ApplyFarm(FarmSaveData data)
    {
        if (data == null) return;
        data.Normalize();

        if (TileManager.Instance == null) return;

        TileManager.Instance.ClearAllTilesAndVisuals();

        for (int i = 0; i < data.tiles.Count; i++)
        {
            var s = data.tiles[i];
            if (s == null) continue;

            var grid = new Vector2Int(s.x, s.y);
            var world = TileManager.Instance.GetWorldPosFromGrid(grid);
            var t = TileManager.Instance.GetTileData(world);
            if (t == null) continue;

            t.state = ToRuntimeState(s.state);
            t.currentCropID = s.currentCropId;
            t.isWatered = s.isWatered;
            t.progressDay = s.progressDay;
            t.isDry = s.isDry;
        }

        if (farmSystem == null) farmSystem = FindFirstObjectByType<FarmSystem>();

        if (farmSystem != null)
            StartCoroutine(CoRefreshFarmVisualsWhenReady());
    }

    private System.Collections.IEnumerator CoRefreshFarmVisualsWhenReady()
    {
        // CropLoader/TileManager 준비 대기
        while (TileManager.Instance == null ||
               CropLoader.Instance == null ||
               !CropLoader.Instance.IsLoaded)
        {
            yield return null;
        }

        farmSystem.RefreshAllFarmVisualsAfterLoad();
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