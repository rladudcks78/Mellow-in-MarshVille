using System.Collections.Generic;
using UnityEngine;

public class RecipeDatabase
{
    private List<RecipeDef> _recipes = new List<RecipeDef>();

    // 외부에서 레시피 개수를 확인할 수 있는 프로퍼티
    public int Count => _recipes.Count;

    public IReadOnlyList<RecipeDef> GetAllRecipes() => _recipes;

    /// <summary>
    /// 로더(Loader)가 데이터를 파싱한 후 이 함수를 호출해 DB를 구축합니다.
    /// </summary>
    public void Build(List<RecipeDef> newRecipes)
    {
        _recipes = newRecipes; // 리스트 교체
        Debug.Log($"[RecipeDatabase] DB 구축 완료. 총 레시피 수: {_recipes.Count}");
    }

    /// <summary>
    /// 도마 위의 재료 리스트를 받아 일치하는 레시피를 찾습니다.
    /// </summary>
    /// <param name="inputIngredients">도마 위 재료 ID 리스트</param>
    /// <returns>찾은 레시피 객체 (없으면 null)</returns>
    public RecipeDef FindRecipe(List<int> inputIngredients)
    {
        foreach (var recipe in _recipes)
        {
            // RecipeDef에 있는 IsMatch 함수로 순서/종류 일치 확인
            if (recipe.IsMatch(inputIngredients))
            {
                return recipe; // 찾았으면 반환
            }
        }

        // 끝까지 못 찾았으면 null 반환 (실패 요리 처리용)
        return null;
    }
}