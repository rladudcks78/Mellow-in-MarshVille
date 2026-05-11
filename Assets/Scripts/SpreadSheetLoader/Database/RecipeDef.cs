using System;
using System.Collections.Generic;
using UnityEngine;

// [직렬화 가능] 인스펙터에서 확인 및 저장이 가능하도록 설정
[Serializable]
public class RecipeDef
{
    // --- 기본 정보 (시트 A, B, C열) ---
    public int recipeId;             // RecipeID: 레시피 고유 번호
    public int resultItemId;         // ResultItemID: 완성된 요리 아이템 ID
    public string recipeName;        // RecipeName: 레시피 이름 (UI 표시용)

    // --- 재료 정보 (시트 D ~ H열) ---
    // Ingredient1 ~ 5를 리스트 하나로 관리합니다. (순서가 중요함)
    public List<int> ingredients = new List<int>();

    // --- 품질 및 보너스 (시트 I, J열) ---
    public float sloppyPenalty;      // SloppyPenalty: 엉성한 요리 시 패널티 (기본 0.5)
    public float perfectBonus;       // PerfectBonus: 완벽한 요리 시 보너스 (기본 1.5)

    // --- 설명 및 힌트 (시트 K, L열) ---
    public string description;       // Description: 레시피 설명
    public string discoveryHint;     // DiscoveryHint: 미발견 시 보여줄 힌트 텍스트

    // --- 가격 정보 (시트 M, N열) ---
    // 레시피 자체(종이)의 가격일 수도 있고, 기획 의도에 따라 다르게 쓰일 수 있음
    public int buyPrice;             // buyPrice: 구매 가격
    public int sellPrice;            // sellPrice: 판매 가격

    // UI 표시용 이미지 경로
    public string spritePath;

    /// <summary>
    /// 입력받은 재료 리스트와 이 레시피의 재료가 (순서 포함) 정확히 일치하는지 검사
    /// </summary>
    /// <param name="inputIngredients">도마 위에 올라온 재료 ID 리스트</param>
    /// <returns>일치하면 true, 아니면 false</returns>
    public bool IsMatch(List<int> inputIngredients)
    {
        // 1. 재료의 개수가 다르면 실패
        if (inputIngredients.Count != ingredients.Count) return false;

        // 2. 0번부터 끝까지 순서대로 비교
        for (int i = 0; i < ingredients.Count; i++)
        {
            // 하나라도 틀리면 불일치로 판정
            if (ingredients[i] != inputIngredients[i])
                return false;
        }

        // 3. 모든 검사를 통과했으므로 일치
        return true;
    }
}