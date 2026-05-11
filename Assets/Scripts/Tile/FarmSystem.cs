using UnityEngine;
using System.Collections.Generic;

public class FarmSystem : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private InventorySystem _inventory;

    // [New] CropLoader를 통해 DB에 접근 (싱글톤 프로퍼티 사용)
    private CropDatabase CropDb
    {
        get
        {
            if (CropLoader.Instance != null && CropLoader.Instance.IsLoaded)
                return CropLoader.Instance.CropDb;
            return null;
        }
    }

    private void Awake()
    {
        if (_inventory == null)
            _inventory = FindFirstObjectByType<InventorySystem>();
    }

    private void Start()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnNewDay += OnNewDayPassed;
    }

    private void OnDestroy()
    {
        if (TimeManager.Instance != null)
            TimeManager.Instance.OnNewDay -= OnNewDayPassed;
    }

    // =================================================================================
    // 하루 경과 처리 로직 (Core Logic)
    // =================================================================================
    private void OnNewDayPassed(int day)
    {
        if (CropDb == null) return;

        Debug.Log($"[FarmSystem] {day}일차 아침. 농장 상태 업데이트.");
        List<TileData> allTiles = TileManager.Instance.GetAllTiles();

        foreach (var tile in allTiles)
        {
            if (tile.state == TileData.TileState.Water) continue;
            UpdateTileDailyState(tile);
        }
    }

    private void UpdateTileDailyState(TileData data)
    {
        bool visualUpdateNeeded = false;

        bool isRaining = WeatherManager.Instance != null &&
                         WeatherManager.Instance.CurrentWeather == WeatherManager.WeatherType.Rainy;

        // 1. 비 오는 날 자동 급수
        if (isRaining && (data.state == TileData.TileState.Tilled || data.state == TileData.TileState.Seeded))
        {
            if (!data.isWatered)
            {
                data.isWatered = true;
                visualUpdateNeeded = true;
            }
        }

        // 2. 작물 성장 처리
        if (data.HasCrop && data.state != TileData.TileState.Dried)
        {
            // [수정] CropData 대신 CropDef 사용
            if (CropDb.TryGetByCrop(data.currentCropID, out CropDef crop))
            {
                if (data.isWatered)
                {
                    if (data.progressDay < crop.growDays)
                    {
                        data.progressDay++;
                        // [수정] 성장했으니 이미지 갱신 (UpdateCropVisual 호출)
                        UpdateCropVisual(data, crop);
                    }
                }
                else
                {
                    if (Random.value < 0.8f) // 80% 확률로 시듦
                    {
                        data.state = TileData.TileState.Dried;
                        data.isDry = true;

                        // [수정] 시들었으므로 null을 보내 이미지 제거 (또는 시든 이미지)
                        TileManager.Instance.UpdateCropSprite(data.position, null);
                        visualUpdateNeeded = true;
                    }
                }
            }
        }
        // 3. 빈 땅 복구
        else if (data.state == TileData.TileState.Tilled && !data.HasCrop)
        {
            data.state = TileData.TileState.Dirted;
            visualUpdateNeeded = true;
        }

        // 4. 물 마름 처리
        if (data.isWatered && !isRaining)
        {
            data.isWatered = false;
            visualUpdateNeeded = true;
        }

        if (visualUpdateNeeded)
        {
            TileManager.Instance.RefreshTileVisual(data);
        }
    }

    // [New] 작물 이미지 갱신 헬퍼 (FarmSystem 내부용)
    // 이 함수가 날짜를 계산해서 TileManager에게 '완성된 이미지'만 전달합니다.
    private void UpdateCropVisual(TileData data, CropDef crop)
    {
        if (crop == null) return;

        // CropDef 안에 있는 로직을 이용해 오늘 날짜에 맞는 스프라이트를 가져옴
        Sprite sprite = crop.GetSpriteByDay(data.progressDay);

        // TileManager의 새로운 함수 호출 (Sprite를 직접 전달)
        TileManager.Instance.UpdateCropSprite(data.position, sprite);
    }

    // =================================================================================
    // 인터랙션 로직
    // =================================================================================

    // 씨앗 심기
    public void OnSeedPlanted(Vector3 worldPos, int seedID, System.Action onSuccess)
    {
        TileData data = TileManager.Instance.GetTileData(worldPos);
        if (data == null || data.state != TileData.TileState.Tilled || data.HasCrop) return;

        // DB에서 씨앗 ID로 검색
        if (CropDb != null && CropDb.TryGetBySeed(seedID, out CropDef crop))
        {
            data.currentCropID = crop.cropId;
            data.progressDay = 0;
            data.state = TileData.TileState.Seeded;

            // 심은 직후 0일차 이미지 표시
            UpdateCropVisual(data, crop);
            TileManager.Instance.RefreshTileVisual(data);

            onSuccess?.Invoke();
        }
    }

    // 배치 도구 (호미, 물뿌리개 등)
    public void OnHoeUsedBatch(List<Vector2Int> gridPositions)
    {
        foreach (var pos in gridPositions) ProcessHoeLogic(pos);
    }

    public void OnWateringBatch(List<Vector2Int> gridPositions)
    {
        foreach (var pos in gridPositions) ProcessWateringLogic(pos);
    }

    // 수확하기
    public void OnHarvestBatch(List<Vector2Int> gridPositions)
    {
        foreach (var pos in gridPositions)
        {
            Vector3 worldPos = TileManager.Instance.GetWorldPosFromGrid(pos);
            TileData data = TileManager.Instance.GetTileData(worldPos);

            if (data == null || !data.HasCrop) continue;

            // DB 검색
            if (CropDb != null && CropDb.TryGetByCrop(data.currentCropID, out CropDef crop))
            {
                // 다 자랐는지 확인
                if (data.progressDay >= crop.growDays)
                {
                    int amount = Random.Range(crop.harvestMin, crop.harvestMax + 1);

                    if (_inventory.TryPickup(crop.harvestItemId, amount))
                    {
                        // 수확 성공 시 초기화
                        data.currentCropID = 0;
                        data.progressDay = 0;
                        data.state = TileData.TileState.Tilled; // 수확 후 갈린 땅 유지

                        // [수정] 작물 이미지 제거 (null 전달)
                        TileManager.Instance.UpdateCropSprite(data.position, null);
                    }
                }
            }
        }
    }

    // 내부 로직 (호미)
    private void ProcessHoeLogic(Vector2Int gridPos)
    {
        Vector3 worldPos = TileManager.Instance.GetWorldPosFromGrid(gridPos);
        TileData data = TileManager.Instance.GetTileData(worldPos);

        if (data == null) return;
        if (data.HasCrop && data.state != TileData.TileState.Dried) return;

        // Case 1: 마른 땅 복구
        if (data.state == TileData.TileState.Dried)
        {
            data.state = TileData.TileState.Dirted;
            data.isDry = false;
            data.currentCropID = 0;
            data.progressDay = 0;
            data.isWatered = false;

            TileManager.Instance.RefreshTileVisual(data);
            TileManager.Instance.UpdateCropSprite(data.position, null);
            return;
        }

        // Case 2: 일반 땅 -> 갈린 땅 (개간)
        if (data.state == TileData.TileState.Dirted)
        {
            if (SoundManager.Instance != null) SoundManager.Instance.PlaySfx(SfxId.Floor_Cultivate);

            data.state = TileData.TileState.Tilled;

            // [New] 비 오는 날이면 개간하자마자 물을 줍니다.
            if (WeatherManager.Instance != null &&
                WeatherManager.Instance.CurrentWeather == WeatherManager.WeatherType.Rainy)
            {
                data.isWatered = true; // 즉시 젖음 상태로 변경
            }

            TileManager.Instance.RefreshTileVisual(data);
        }
    }

    // 내부 로직 (물뿌리개)
    private void ProcessWateringLogic(Vector2Int gridPos)
    {
        Vector3 worldPos = TileManager.Instance.GetWorldPosFromGrid(gridPos);
        TileData data = TileManager.Instance.GetTileData(worldPos);

        if (data == null) return;

        if (data.state == TileData.TileState.Tilled || data.state == TileData.TileState.Seeded)
        {
            if (!data.isWatered)
            {
                data.isWatered = true;
                TileManager.Instance.RefreshTileVisual(data);
            }
        }
    }

    public void RefreshAllFarmVisualsAfterLoad()
    {
        if (TileManager.Instance == null) return;

        var allTiles = TileManager.Instance.GetAllTiles();
        for (int i = 0; i < allTiles.Count; i++)
        {
            var tile = allTiles[i];
            if (tile == null) continue;

            //바닥 갱신
            TileManager.Instance.RefreshTileVisual(tile);

            //작물 스프라이트 갱신
            if (tile.HasCrop && tile.state != TileData.TileState.Dried && CropDb != null)
            {
                if (CropDb.TryGetByCrop(tile.currentCropID, out CropDef crop))
                {
                    Sprite sprite = crop.GetSpriteByDay(tile.progressDay);
                    TileManager.Instance.UpdateCropSprite(tile.position, sprite);
                }
                else
                {
                    TileManager.Instance.UpdateCropSprite(tile.position, null);
                }
            }
            else
            {
                TileManager.Instance.UpdateCropSprite(tile.position, null);
            }
        }
    }
}