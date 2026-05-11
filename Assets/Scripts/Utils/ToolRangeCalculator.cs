using UnityEngine;
using System.Collections.Generic;

public static class ToolRangeCalculator
{
    // 내부에서만 쓰는 캐싱용 리스트 (외부 노출 금지)
    private static List<Vector2Int> _cachedResults = new List<Vector2Int>();

    /// <summary>
    /// 마우스 위치(originPos)를 시작점으로 하여, 바라보는 방향(facingDir)으로 뻗어나가는 범위를 계산합니다.
    /// </summary>
    public static List<Vector2Int> GetRange(Vector2Int originPos, Vector2Int facingDir, int width, int height)
    {
        _cachedResults.Clear();

        // CSV 오류 등으로 0이 들어오는 것을 방지
        if (width <= 0) width = 1;
        if (height <= 0) height = 1;

        // 좌우 대칭을 위한 시작점 계산 (중앙 정렬)
        int wStart = -(width / 2);

        for (int h = 0; h < height; h++)
        {
            for (int w = 0; w < width; w++)
            {
                int currentW = wStart + w;
                Vector2Int targetPos = originPos; // 기준점

                // 방향에 따른 회전 로직 (스왑 및 부호 변경)
                if (facingDir == Vector2Int.up)
                    targetPos += new Vector2Int(currentW, h);
                else if (facingDir == Vector2Int.down)
                    targetPos += new Vector2Int(currentW, -h);
                else if (facingDir == Vector2Int.right)
                    targetPos += new Vector2Int(h, currentW);
                else if (facingDir == Vector2Int.left)
                    targetPos += new Vector2Int(-h, currentW);

                _cachedResults.Add(targetPos);
            }
        }

        // [중요] 참조(Reference) 덮어쓰기 에러를 막기 위해 새로운 리스트로 깊은 복사(Deep Copy)하여 반환!
        return new List<Vector2Int>(_cachedResults);
    }

    public static List<Vector2Int> GetToolRangeCells(Vector2Int originPos, Vector2Int facingDir, ItemDef item)
    {
        int w = 1, h = 1;

        if (item == null || !item.IsTool)
        {
            //Debug.LogWarning("[ToolRange] 도구가 아니거나 아이템 정보가 없습니다. 기본 1x1을 반환합니다.");
            return GetRange(originPos, facingDir, 1, 1);
        }

        switch (item.tier)
        {
            case 0: case 1: w = item.areaW_S; h = item.areaH_S; break;
            case 2: w = item.areaW_M; h = item.areaH_M; break;
            case 3: w = item.areaW_L; h = item.areaH_L; break;
            default: w = item.areaW_S; h = item.areaH_S; break;
        }

        //Debug.Log($"<color=cyan>[ToolRange] {item.name}(티어:{item.tier}) 범위 계산 -> 너비(W):{w}, 높이(H):{h}</color>");

        return GetRange(originPos, facingDir, w, h);
    }
}