using UnityEngine;

public class MergedSceneFinalChecker : MonoBehaviour
{
    [SerializeField] private InputReader _inputReader; //
    [SerializeField] private TileManager _tileManager; //

    void Start()
    {
        Debug.Log("<color=orange><b>[Final Check] 시스템 무결성 검사를 시작합니다...</b></color>");

        // 1. 싱글톤 중복 체크
        TileManager[] managers = FindObjectsByType<TileManager>(FindObjectsSortMode.None);
        if (managers.Length > 1)
        {
            Debug.LogError($"[Critical] 씬에 <b>TileManager가 {managers.Length}개</b>나 있습니다! 하나만 남기고 지우세요.");
        }

        // 2. 인스턴스 일치 여부
        if (TileManager.Instance != _tileManager)
        {
            Debug.LogError("[Critical] 현재 활성화된 Instance가 내가 할당한 매니저가 아닙니다!");
        }

        // 3. 타일맵 할당 상태
        if (_tileManager != null && _tileManager.WorldToCell(Vector3.zero) == null)
        {
            Debug.LogError("[Critical] TileManager에 타일맵이 연결되지 않았습니다!");
        }

        Debug.Log("<color=orange><b>[Final Check] 검사 종료. 에러가 없다면 하드웨어 입력을 확인해야 합니다.</b></color>");
    }
}