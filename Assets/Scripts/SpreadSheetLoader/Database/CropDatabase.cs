using System.Collections.Generic;
using UnityEngine;

public class CropDatabase
{
    // Key: 씨앗 아이템 ID, Value: 작물 데이터
    // -> "이 씨앗(8001)을 심으면 무슨 작물이 되나요?"를 찾을 때 사용
    private readonly Dictionary<int, CropDef> _bySeedId = new Dictionary<int, CropDef>();

    // Key: 작물 ID, Value: 작물 데이터
    // -> "작물(1)의 정보를 알려주세요"를 찾을 때 사용
    private readonly Dictionary<int, CropDef> _byCropId = new Dictionary<int, CropDef>();

    // 전체 데이터 개수 반환 (디버깅용)
    public int Count => _byCropId.Count;

    /// <summary>
    /// 로더가 파싱한 리스트를 받아서 검색용 딕셔너리(Dictionary)를 구축합니다.
    /// </summary>
    public void Build(List<CropDef> cropList)
    {
        // 기존 데이터를 비웁니다 (중복 로드 방지)
        _bySeedId.Clear();
        _byCropId.Clear();

        foreach (var crop in cropList)
        {
            if (crop == null) continue;

            // 1. 씨앗 ID로 검색 가능하게 등록
            if (!_bySeedId.ContainsKey(crop.seedItemId))
                _bySeedId.Add(crop.seedItemId, crop);

            // 2. 작물 ID로 검색 가능하게 등록
            if (!_byCropId.ContainsKey(crop.cropId))
                _byCropId.Add(crop.cropId, crop);
        }

        Debug.Log($"[CropDatabase] DB 구축 완료. 등록된 작물: {cropList.Count}개");
    }

    // 씨앗 ID로 작물 찾기 (실패 시 false 반환, 데이터는 out으로 전달)
    public bool TryGetBySeed(int seedId, out CropDef def) => _bySeedId.TryGetValue(seedId, out def);

    // 작물 ID로 작물 찾기
    public bool TryGetByCrop(int cropId, out CropDef def) => _byCropId.TryGetValue(cropId, out def);
}