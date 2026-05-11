using UnityEngine;

/// <summary>
/// 농사 구역(FarmGround) 내의 개별 타일 정보를 저장하는 클래스입니다. 
/// </summary>
[System.Serializable]
public class TileData
{
    public Vector2Int position;    // 타일의 격자 좌표 
    public TileState state;        // 현재 타일의 상태 (땅, 갈린 땅 등) 

    [Header("Crop Info")]
    public int currentCropID;            // 심어진 작물 ID
    public bool isWatered;        // 오늘 물을 주었는지 여부 
    public int progressDay;       // 현재 성장 단계에서의 경과 일수 
    public bool isDry;            // 물을 안 주어 말라버린 상태인지 여부

    /// <summary>
    /// 타일 상태
    /// </summary>
    public enum TileState
    {
        Dirted,           // 일반 땅 상태
        Tilled,           // 호미로 갈린 상태 (씨앗 심기 가능)
        Seeded,           // 씨앗이 심어진 상태 
        Dried,            // 물을 주지 않아 말라버린 상태 
        Water             // 물
    }

    public TileData(Vector2Int pos)
    {
        position = pos;
        state = TileState.Dirted; // FarmGround 위에 생성되면 기본 '땅' 상태입니다.
        currentCropID = 0;
        progressDay = 0;
        isWatered = false;
        isDry = false;
    }

    // 작물이 심어져 있는지 확인
    public bool HasCrop => currentCropID != 0;
}