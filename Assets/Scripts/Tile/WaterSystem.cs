using UnityEngine;

public class WaterSystem : MonoBehaviour
{
    [Header("Ref")]
    [SerializeField] private FishingSystem fishingSystem; // 인스펙터에서 플레이어의 FishingSystem을 연결하세요.

    [Header("Check Layer")]
    [SerializeField] private float checkDistance = 1.5f;
    [SerializeField] private LayerMask waterLayerMask;
    [SerializeField] private LayerMask zoneLayerMask; // "FishingZone" 레이어 설정

    /// <summary>
    /// 외부(Player)에서 이 함수를 호출하면, 타일과 구역을 체크하고 낚시를 실행시킵니다.
    /// </summary>
    public void TryFishing(Vector3 playerPos, Vector2 facingDir)
    {
        if(fishingSystem == null)
        {
            Debug.LogWarning("[WaterSystem] FishingSystem ref가 없음");
            return;
        }

        Vector2 dir = facingDir.sqrMagnitude > 0.001f ? facingDir.normalized : Vector2.down;
        Vector2 checkPos2D = (Vector2)playerPos + dir * checkDistance;

        //Water레이어인지 체크
        var waterHit = Physics2D.OverlapPoint(checkPos2D, waterLayerMask);
        if(waterHit == null)
        {
            Debug.Log("[WaterSystem] 낚시 불가 (Water 레이어가 아님)");
            return;
        }

        //fishingZone 찾기
        FishingZone zone = null;

        if (!waterHit.TryGetComponent(out zone))
        {
            var hits = Physics2D.OverlapPointAll(checkPos2D, zoneLayerMask);
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i] != null && hits[i].TryGetComponent(out zone))
                    break;
            }
        }

        if (zone == null)
        {
            Debug.LogWarning("[WaterSystem] Water는 맞는데 FishingZone이 없음");
            return;
        }

        //FishingZone 데이터 전달
        fishingSystem.StartFishingMiniGame(zone.areaType, zone.biomeID);


        //// 1. 확인 지점 계산
        //Vector3 checkPos = playerPos + (Vector3)facingDir * 1.5f;

        //// 2. 타일 확인 (물인가?)
        //TileData tile = TileManager.Instance.GetTileData(checkPos);

        //if (tile != null && tile.state == TileData.TileState.Water)
        //{
        //    // 3. [추가된 부분] 구역(Zone) 확인 (FishingZone 레이어 검사)
        //    FishArea currentArea = FishArea.River; // 기본값
        //    int currentBiome = 1;                  // 기본값

        //    Collider2D hit = Physics2D.OverlapPoint(checkPos, zoneLayerMask);
        //    if (hit != null && hit.TryGetComponent<FishingZone>(out var zone))
        //    {
        //        currentArea = zone.areaType;
        //        currentBiome = zone.biomeID;
        //        Debug.Log($"[WaterSystem] 낚시터 확인: {currentArea} / Biome {currentBiome}");
        //    }
        //    else
        //    {
        //        Debug.LogWarning("[WaterSystem] 물은 있는데 FishingZone이 없습니다. 기본값(강/봄)으로 진행합니다.");
        //    }

        //    // 4. FishingSystem에게 "이 조건으로 낚시 시작해!" 라고 명령
        //    if (fishingSystem != null)
        //    {
        //        // 인자값(currentArea, currentBiome)을 전달하도록 수정했습니다.
        //        fishingSystem.StartFishingMiniGame(currentArea, currentBiome);
        //    }
        //}
        //else
        //{
        //    Debug.Log("[WaterSystem] 낚시 불가 (물 타일 아님)");
        //}
    }
}