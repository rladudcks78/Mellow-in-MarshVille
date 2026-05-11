using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class CookingMiniGame : MonoBehaviour
{
    public enum CookingQuality { Sloppy, Normal, Perfect }

    [Header("UI 연결")]
    [SerializeField] private RectTransform knifeCursor;   // 빨간색 칼 (이동함)
    [SerializeField] private RectTransform barArea;       // 배경 바 (트랙)
    [SerializeField] private RectTransform targetParent;  // 그림자(노트)들이 생성될 부모
    [SerializeField] private GameObject targetPrefab;     // 그림자 프리팹 (파란색 바)

    [Header("게임 밸런스 설정")]
    [SerializeField] private float cursorSpeed = 600f;    // 칼 이동 속도
    [SerializeField] private float judgeWidth = 20f;      // 판정 범위 
    [SerializeField] private float minNoteSpacing = 100f; // 노트 사이의 최소 거리 
    [SerializeField] private float startPadding = 500f;   // 시작시 안전거리

    // 내부 상태 변수
    private List<RectTransform> _hittableTargets = new List<RectTransform>(); // 판정 대기 중인 노트들
    private System.Action<CookingQuality> _onComplete;

    // 결과 집계용
    private int _totalNotes;
    private int _hitCount;
    private int _perfectCount;

    private Coroutine _gameRoutine;

    // 외부에서 요리 시작 시 호출
    public void StartGame(int ingredientCount, System.Action<CookingQuality> onComplete)
    {
        _onComplete = onComplete;
        _totalNotes = ingredientCount;
        _hitCount = 0;
        _perfectCount = 0;

        gameObject.SetActive(true);

        // 1. 재료 개수만큼 그림자 배치 (거리 계산 포함)
        SpawnShadows(ingredientCount);

        // 2. 게임 루프 시작 (코루틴)
        if (_gameRoutine != null) StopCoroutine(_gameRoutine);
        _gameRoutine = StartCoroutine(Co_RunSinglePassGame());
    }

    // 한 번의 주행으로 끝나는 게임 로직
    private IEnumerator Co_RunSinglePassGame()
    {
        float halfBarWidth = barArea.rect.width / 2;
        float startX = -halfBarWidth; // 왼쪽 끝
        float endX = halfBarWidth;    // 오른쪽 끝

        // 칼 초기화 
        knifeCursor.anchoredPosition = new Vector2(startX, 0);
        knifeCursor.gameObject.SetActive(true);

        // 칼이 오른쪽 끝에 닿을 때까지 이동
        while (knifeCursor.anchoredPosition.x < endX)
        {
            // 시간 정지(TimeScale=0) 상태에서도 움직이도록 unscaledDeltaTime 사용
            float moveAmount = cursorSpeed * Time.unscaledDeltaTime;
            knifeCursor.anchoredPosition += new Vector2(moveAmount, 0);

            float currentKnifeX = knifeCursor.anchoredPosition.x;

            // [Miss 체크] 맨 앞 노트가 칼보다 뒤쳐졌는지 확인
            if (_hittableTargets.Count > 0)
            {
                RectTransform firstTarget = _hittableTargets[0];
                // 칼이 노트보다 '판정범위'만큼 더 오른쪽으로 갔다면 -> 놓친 것!
                if (currentKnifeX > firstTarget.anchoredPosition.x + judgeWidth)
                {
                    HandleMiss(firstTarget);       // 흐리게 처리
                    _hittableTargets.RemoveAt(0);  // 리스트에서 제외
                }
            }

            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                TryHitTarget(currentKnifeX);
            }

            yield return null; // 다음 프레임까지 대기
        }

        // 주행 끝 -> 종료 처리
        knifeCursor.gameObject.SetActive(false);
        FinishGame();
    }

    // 판정 로직
    private void TryHitTarget(float knifeX)
    {
        if (_hittableTargets.Count == 0) return;

        // 가장 가까운 노트 찾기
        RectTransform bestTarget = null;
        float minDiff = float.MaxValue;
        int targetIndex = -1;

        for (int i = 0; i < _hittableTargets.Count; i++)
        {
            float diff = Mathf.Abs(knifeX - _hittableTargets[i].anchoredPosition.x);

            // 판정 범위(judgeWidth) 안에 들어왔는가?
            if (diff <= judgeWidth)
            {
                if (diff < minDiff)
                {
                    minDiff = diff;
                    bestTarget = _hittableTargets[i];
                    targetIndex = i;
                }
            }
        }

        // 성공!
        if (bestTarget != null)
        {
            // 퍼펙트 판정 (정중앙 30% 이내)
            if (minDiff <= judgeWidth * 0.3f)
            {
                Debug.Log("Perfect!");
                _perfectCount++;
            }
            else
            {
                Debug.Log("Good!");
            }
            _hitCount++;

            // 맞춘 노트는 즉시 제거 
            bestTarget.gameObject.SetActive(false);
            _hittableTargets.Remove(bestTarget);

            //SFX 재생
            //SoundManager.Instance.PlaySfx(SfxId.Cook_Minigame_Success);
        }
    }

    // 놓친 노트 처리
    private void HandleMiss(RectTransform target)
    {
        // 그냥 놔두거나, 반투명하게 만들어서 "놓쳤음" 표시
        Image img = target.GetComponent<Image>();
        if (img != null)
        {
            Color c = img.color;
            c.a = 0.3f; // 흐리게 만듦
            img.color = c;
        }

        //SFX 재생
        //SoundManager.Instance.PlaySfx(SfxId.Cook_Minigame_fail);
    }

    // 그림자 배치 로직 (겹침 방지)
    private void SpawnShadows(int count)
    {
        // 기존 것들 삭제
        foreach (Transform child in targetParent) Destroy(child.gameObject);
        _hittableTargets.Clear();

        float barWidth = barArea.rect.width;
        float halfWidth = barWidth / 2f;

        // 배치 가능한 X좌표 범위 설정
        float endPadding = judgeWidth * 2;

        float minX = -halfWidth + startPadding;
        float maxX = halfWidth - endPadding;

        // 위치들을 저장할 리스트
        List<float> positions = new List<float>();

        for (int i = 0; i < count; i++)
        {
            float finalX = 0;
            bool foundSpot = false;

            // 랜덤 위치를 30번 시도해서, 기존 노트들과 minNoteSpacing 이상 떨어진 곳 찾기
            for (int tryCount = 0; tryCount < 30; tryCount++)
            {
                float candidate = Random.Range(minX, maxX);
                bool overlap = false;

                foreach (float existingPos in positions)
                {
                    // 거리가 너무 가까우면 실패
                    if (Mathf.Abs(candidate - existingPos) < minNoteSpacing)
                    {
                        overlap = true;
                        break;
                    }
                }

                if (!overlap)
                {
                    finalX = candidate;
                    foundSpot = true;
                    break;
                }
            }

            // 만약 30번 시도해도 자리가 없으면? (너무 좁거나 재료가 많음)
            // 어쩔 수 없이 그냥 랜덤으로 넣지만, 실제 게임에선 minNoteSpacing을 조절하면 됨
            if (!foundSpot) finalX = Random.Range(minX, maxX);

            positions.Add(finalX);
        }

        // [중요] 왼쪽부터 순서대로 오도록 정렬 (오름차순)
        positions.Sort();

        // 정렬된 위치에 실제로 생성
        foreach (float xPos in positions)
        {
            GameObject obj = Instantiate(targetPrefab, targetParent);
            RectTransform rt = obj.GetComponent<RectTransform>();

            // 위치 잡기
            rt.anchoredPosition = new Vector2(xPos, 0);

            // 초기화 (투명도 등)
            Image img = obj.GetComponent<Image>();
            if (img != null)
            {
                Color c = img.color;
                c.a = 1f;
                img.color = c;
            }

            _hittableTargets.Add(rt);
        }
    }

    private void FinishGame()
    {
        _gameRoutine = null;

        // gameObject.SetActive(false); 

        // 결과 계산
        CookingQuality quality = CookingQuality.Normal;

        // 절반 넘게 실패하면 엉성함
        int missCount = _totalNotes - _hitCount;
        if (missCount > _totalNotes / 2)
        {
            quality = CookingQuality.Sloppy;
        }
        else if (_perfectCount == _totalNotes)
        {
            quality = CookingQuality.Perfect;
        }

        Debug.Log($"게임 종료! 품질: {quality} (성공:{_hitCount}/{_totalNotes})");
        _onComplete?.Invoke(quality);
    }

    public void ForceStop()
    {
        if (_gameRoutine != null) StopCoroutine(_gameRoutine);
        gameObject.SetActive(false);
    }
}