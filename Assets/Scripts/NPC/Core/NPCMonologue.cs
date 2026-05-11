using UnityEngine;

public class NPCMonologue : MonoBehaviour
{
    [Header("NPC 정보")]
    [SerializeField] private int npcId = 0;

    [Header("혼자말 설정")]
    [SerializeField] private bool enableMonologue = true;
    [SerializeField] private float monologueIntervalMin = 20f;
    [SerializeField] private float monologueIntervalMax = 40f;
    [SerializeField] private float displayDuration = 3f;

    [Header("플레이어 거리 체크")]
    [SerializeField] private float minPlayerDistance = 5f;

    [Header("말풍선")]
    [SerializeField] private GameObject speechBubble;
    [SerializeField] private TMPro.TextMeshProUGUI bubbleText;

    [Header("디버그")]
    [SerializeField] private bool showDebugInfo = false;

    private float monologueTimer = 0f;
    private bool isShowingBubble = false;
    private bool isVisible = false;  // Manager가 설정

    // 직전 혼자말 반복 방지(간단 버전)
    private int lastMonologueNodeId = -1;

    public int NpcId => npcId;
    public bool IsShowingBubble => isShowingBubble;

    private void Start()
    {
        // 통합 Manager에 등록
        if (NPCManager.Instance != null)
        {
            NPCManager.Instance.RegisterMonologueNpc(this);
        }

        ResetTimer();

        if (speechBubble != null)
            speechBubble.SetActive(false);
    }

    private void OnDestroy()
    {
        if (NPCManager.Instance != null)
        {
            NPCManager.Instance.UnregisterMonologueNpc(this);
        }
    }

    private void Update()
    {
        // 타이머만 매 프레임 업데이트
        if (enableMonologue && isVisible)
        {
            monologueTimer -= Time.deltaTime;
        }
    }

    /// <summary>
    /// Manager가 호출
    /// </summary>
    public void OnManagerCheck()
    {
        if (!enableMonologue || !isVisible) return;

        if (monologueTimer <= 0)
        {
            TryPlayMonologue();
            ResetTimer();
        }
    }

    /// <summary>
    /// Manager가 visibility 설정
    /// </summary>
    public void SetVisible(bool visible)
    {
        isVisible = visible;

        // 카메라 밖으로 나가면 말풍선 숨기기
        if (!visible && isShowingBubble)
        {
            HideBubble();
        }
    }

    private bool CanPlayMonologue()
    {
        if (isShowingBubble) return false;

        if (NPCDialogueManager.Instance != null && NPCDialogueManager.Instance.IsInDialogue)
            return false;

        // Manager의 캐시된 플레이어 위치 사용
        if (NPCManager.Instance != null)
        {
            Vector2 playerPos = NPCManager.Instance.GetPlayerPosition();
            float distance = Vector2.Distance(transform.position, playerPos);

            if (distance < minPlayerDistance)
                return false;
        }

        return true;
    }

    private void TryPlayMonologue()
    {
        if (!CanPlayMonologue()) return;

        var dialogueMgr = NPCDialogueManager.Instance;
        if (dialogueMgr == null || !dialogueMgr.IsReady)
            return;

        // 같은 대사 연속 방지
        NpcDialogueDef monologue = null;

        for (int attempt = 0; attempt < 2; attempt++)
        {
            var picked = dialogueMgr.GetMonologueNode(npcId);
            if (picked == null) break;

            if (picked.nodeId != lastMonologueNodeId)
            {
                monologue = picked;
                break;
            }

            // 같으면 한 번 더 뽑아본다(풀에 1개뿐이면 결국 같을 수 있음)
            monologue = picked;
        }

        if (monologue != null)
        {
            ShowMonologue(monologue.dialogueText, monologue.expressionKey);
            lastMonologueNodeId = monologue.nodeId;

            if (monologue.once)
            {
                var db = dialogueMgr.GetDialogueDb();
                if (db != null)
                    db.MarkNodeAsSeen(monologue.npcId, monologue.nodeId);
            }
        }
    }


    private void ShowMonologue(string text, string expression)
    {
        if (showDebugInfo)
        {
            Debug.Log($"<color=yellow>[NPC {npcId}] \"{text}\"</color>");
        }

        if (speechBubble != null && bubbleText != null)
        {
            bubbleText.text = text;
            speechBubble.SetActive(true);
            isShowingBubble = true;

            CancelInvoke(nameof(HideBubble));
            Invoke(nameof(HideBubble), displayDuration);
        }
    }

    private void HideBubble()
    {
        if (speechBubble != null)
            speechBubble.SetActive(false);

        isShowingBubble = false;
    }

    private void ResetTimer()
    {
        monologueTimer = Random.Range(monologueIntervalMin, monologueIntervalMax);
    }

    public void ForcePlayMonologue()
    {
        if (enableMonologue && !isShowingBubble && isVisible)
        {
            TryPlayMonologue();
        }
    }
}
