using System;
using System.Collections.Generic;
using UnityEngine;

public class NPCDialogueManager : MonoBehaviour
{
    public static NPCDialogueManager Instance { get; private set; }

    [Header("디버그")]
    [SerializeField] private bool debugMode = false;

    [Header("로더")]
    [SerializeField] private NpcDialogueLoader dialogueLoader;

    [Header("링크 로더")]
    [SerializeField] private NpcDialogueLinkLoader linkLoader;

    [Header("조건 디버그(임시)")]
    [SerializeField] private string debugWeather = "";

    private NpcDialogueDatabase dialogueDb;
    private NpcDialogueLinkDatabase linkDb;

    private bool isReady = false;
    private bool dialogueReady = false;
    private bool linkReady = false;

    public NpcDialogueDef currentNode;
    private int currentNpcId = -1;
    private string currentEventKey = "";

    public bool IsReady => isReady;
    public bool IsInDialogue => currentNode != null;
    public NpcDialogueDatabase DialogueDb => dialogueDb;


    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        if (dialogueLoader == null)
            dialogueLoader = FindAnyObjectByType<NpcDialogueLoader>();

        if (linkLoader == null)
            linkLoader = FindAnyObjectByType<NpcDialogueLinkLoader>();

        if (dialogueLoader != null)
            dialogueLoader.Register(OnDialogueLoaded, OnDialogueFailed);
        else
            Debug.LogWarning("[NPCDialogueManager] NpcDialogueLoader를 찾지 못했습니다.");

        if (linkLoader != null)
            linkLoader.Register(OnLinksLoaded, OnLinksFailed);
        else
            Debug.LogWarning("[NPCDialogueManager] NpcDialogueLinkLoader를 찾지 못했습니다.");
    }

    private void OnDestroy()
    {
        if (dialogueLoader != null)
            dialogueLoader.Unregister(OnDialogueLoaded, OnDialogueFailed);

        if (linkLoader != null)
            linkLoader.Unregister(OnLinksLoaded, OnLinksFailed);
    }

    private void OnDialogueLoaded(NpcDialogueDatabase db)
    {
        dialogueDb = db;
        dialogueDb.FinalizeEntryNodes();
        dialogueReady = true;
        TrySetReady();
    }

    private void OnDialogueFailed(string error)
    {
        Debug.LogError($"[NPCDialogueManager] 대화 로드 실패: {error}");
        dialogueReady = false;
        isReady = false;
    }

    private void OnLinksLoaded(NpcDialogueLinkDatabase db)
    {
        linkDb = db;
        linkReady = true;
        TrySetReady();
    }

    private void OnLinksFailed(string error)
    {
        Debug.LogError($"[NPCDialogueManager] 링크 로드 실패: {error}");
        linkReady = false;
        isReady = false;
    }

    private void TrySetReady()
    {
        isReady = dialogueReady && linkReady;
        if (isReady) Debug.Log("[NPCDialogueManager] 대화+링크 시스템 준비 완료");
    }

    public NpcDialogueDatabase GetDialogueDb() => dialogueDb;

    private QuestStateCsv GetQuestState(int questId)
    {
        if (questId <= 0) return null;
        if (QuestManagerCsv.Instance == null) return null;
        if (!QuestManagerCsv.Instance.IsReady()) return null;

        var st = QuestManagerCsv.Instance.GetState(questId);
        if (st != null) return st;

        if (QuestManagerCsv.Instance.IsRewardClaimed(questId))
        {
            var done = new QuestStateCsv(questId);
            done.objectiveCompleted = true;
            done.rewardClaimed = true;
            return done;
        }

        return null;
    }

    public NpcDialogueDef StartDialogue(int npcId)
    {
        if (!isReady || dialogueDb == null)
            return null;

        currentNpcId = npcId;

        int affectionStage = GetCurrentAffectionStage(npcId);
        string timeOfDay = Norm(GetCurrentTimeOfDay());
        string weather = Norm(GetCurrentWeather());
        string eventKey = Norm(GetCurrentEvent());

        SetEventContext(eventKey);

        currentNode = dialogueDb.GetEntryNode(npcId, affectionStage, timeOfDay, weather, eventKey);

        // 폴백: 컨텍스트 조건 때문에 못 찾았으면 조건 제거 후 재시도
        if (currentNode == null && (!string.IsNullOrEmpty(timeOfDay) || !string.IsNullOrEmpty(weather) || !string.IsNullOrEmpty(eventKey)))
        {
            currentNode = dialogueDb.GetEntryNode(npcId, affectionStage, "", "", "");
        }

        if (currentNode == null)
        {
            if (debugMode)
                Debug.LogWarning($"[NPCDialogueManager] Entry node missing. npcId={npcId}, stage={affectionStage}, time='{timeOfDay}', weather='{weather}', event='{eventKey}'");
            return null;
        }

        Debug.Log($"[StartDialogue] nodeId={currentNode?.nodeId}, speaker='{currentNode?.speaker}', IsRouter={currentNode?.IsRouterNode}");

        if (currentNode.once)
            dialogueDb.MarkNodeAsSeen(currentNode.npcId, currentNode.nodeId);

        if (currentNode.IsRouterNode)
        {
            currentNode = AdvanceDialogue();
            if (currentNode != null)
            {
                if (currentNode.once) dialogueDb.MarkNodeAsSeen(currentNode.npcId, currentNode.nodeId);
            }
            return currentNode;
        }

        if (debugMode)
            Debug.Log($"[Dialogue] 시작: NPC {npcId}, node {currentNode.nodeId}, \"{currentNode.dialogueText}\"");

        return currentNode;
    }

    public NpcDialogueDef GetMonologueNode(int npcId)
    {
        if (!IsReady || dialogueDb == null) return null;
        if (npcId <= 0) return null;

        int affectionStage = GetCurrentAffectionStage(npcId);
        string timeOfDay = GetCurrentTimeOfDay();
        string weather = GetCurrentWeather();
        string eventKey = GetCurrentEvent();

        return dialogueDb.GetMonologueNode(npcId, affectionStage, timeOfDay, weather, eventKey);
    }

    public NpcDialogueDef AdvanceDialogue()
    {
        if (currentNode == null)
        {
            Debug.Log("노드 설정 실패");
            EndDialogue();
            return null;
        }

        if (linkDb == null)
        {
            Debug.Log("링크 DB 설정 실패");
            EndDialogue();
            return null;
        }

        int affectionStage = GetCurrentAffectionStage(currentNpcId);
        string timeOfDay = GetCurrentTimeOfDay();
        string weather = GetCurrentWeather();
        string eventKey = GetCurrentEvent();

        Func<int, QuestStateCsv> getQuestState = questId => {
            return GetQuestState(questId);  // 인스턴스 메서드 호출
        };

        if (linkDb.TryPickNextNodeId(
                currentNpcId,
                currentNode.nodeId,
                affectionStage,
                timeOfDay,
                weather,
                eventKey,
                getQuestState,
                out var nextId))
        {
            currentNode = dialogueDb.GetNode(currentNpcId, nextId);

            // 폴백: 컨텍스트 조건 때문에 못 찾았으면 조건 제거 후 재시도
            if (currentNode == null && (!string.IsNullOrEmpty(timeOfDay) || !string.IsNullOrEmpty(weather) || !string.IsNullOrEmpty(eventKey)))
            {
                currentNode = dialogueDb.GetEntryNode(currentNpcId, affectionStage, "", "", "");
            }

            if (currentNode != null && currentNode.once)
                dialogueDb.MarkNodeAsSeen(currentNode.npcId, currentNode.nodeId);

            if (currentNode != null && currentNode.IsRouterNode)
                return AdvanceDialogue();

            if (debugMode && currentNode != null)
                Debug.Log($"[Dialogue] 링크 진행: toNodeId={nextId}, text=\"{currentNode.dialogueText}\"");

            return currentNode;
        }

        if (nextId == 0 || currentNode == null)
        {  // null 시 fallback
           // 라우터 실패 시 종료
            Debug.Log("걍 실패");
            EndDialogue();
            return null;
        }
        Debug.Log($"[Advance] 링크 실패: nextId={nextId}");
        EndDialogue();
        return null;
    }

    public NpcDialogueDef SelectChoice(int choiceNodeId)
    {
        currentNode = dialogueDb.GetNode(currentNpcId, choiceNodeId);

        if (currentNode != null && currentNode.once)
            dialogueDb.MarkNodeAsSeen(currentNode.npcId, currentNode.nodeId);

        if (debugMode && currentNode != null)
            Debug.Log($"[Dialogue] 선택: node {currentNode.nodeId}, \"{currentNode.dialogueText}\"");

        return currentNode;
    }

    public List<NpcDialogueDef> GetChoices(int choiceGroupId)
    {
        if (dialogueDb == null)
            return new List<NpcDialogueDef>();

        int affectionStage = GetCurrentAffectionStage(currentNpcId);
        string timeOfDay = GetCurrentTimeOfDay();
        string weather = GetCurrentWeather();
        string eventKey = GetCurrentEvent();

        return dialogueDb.GetChoiceGroup(currentNpcId, choiceGroupId, affectionStage, timeOfDay, weather, eventKey);
    }

    // =====  systemKey로 시스템 문구 노드 조회 =====
    public NpcDialogueDef GetSystemNode(int npcId, string systemKey)
    {
        if (!IsReady || dialogueDb == null) return null;
        if (npcId <= 0)
        {
            return null;
        }

        int affectionStage = GetCurrentAffectionStage(npcId);
        string timeOfDay = GetCurrentTimeOfDay() ?? "";
        string weather = GetCurrentWeather() ?? "";
        string eventKey = GetCurrentEvent() ?? "";

        var node = dialogueDb.GetSystemNode(npcId, systemKey, affectionStage, timeOfDay, weather, eventKey);

        // 2차 폴백: 컨텍스트 제거
        if (node == null && (!string.IsNullOrEmpty(timeOfDay) || !string.IsNullOrEmpty(weather) || !string.IsNullOrEmpty(eventKey)))
        {
            node = dialogueDb.GetSystemNode(npcId, systemKey, affectionStage, "", "", "");
        }

        // 3차 폴백: stage도 제거 (최소 조건)
        if (node == null)
        {
            node = dialogueDb.GetSystemNode(npcId, systemKey, 0, "", "", "");
        }

        if (node == null) return null;

        currentNpcId = npcId;
        currentNode = node;

        if (node.once)
            dialogueDb.MarkNodeAsSeen(node.npcId, node.nodeId);

        if (node.IsRouterNode)
            return AdvanceDialogue();

        return node;
    }

    public void EndDialogue()
    {
        currentNode = null;
        currentNpcId = -1;
    }

    public NpcDialogueDef GetCurrentNode() => currentNode;

    public int GetCurrentAffectionStage(int npcId)
    {
        if (RelationshipManager.Instance != null)
            return RelationshipManager.Instance.GetAffectionStage10(npcId);
        return 0;
    }

    public string GetCurrentTimeOfDay()
    {
        if (TimeManager.Instance == null) return "";
        return Norm(TimeManager.Instance.GetTimeOfDay());
    }

    public string GetCurrentWeather()
    {
        if (WeatherManager.Instance != null)
        {
            return WeatherManager.Instance.CurrentWeather switch
            {
                WeatherManager.WeatherType.Rainy => "rainy",
                WeatherManager.WeatherType.Sunny => "sunny",
                _ => "sunny"
            };
        }
        return debugWeather ?? "sunny";
    }


    public string GetCurrentEvent() => currentEventKey;

    public void SetEventContext(string eventKey)
    {
        currentEventKey = eventKey ?? "";
        Debug.Log($"[Mgr] SetEventContext '{currentEventKey}'");
    }


    // Flow에서 호출하는 nodeId 조회용(현재 NPC 기준)
    public NpcDialogueDef GetNodeById(int nodeId)
    {
        if (!IsReady || dialogueDb == null) return null;
        if (nodeId <= 0) return null;
        if (currentNpcId <= 0) return null;

        return dialogueDb.GetNode(currentNpcId, nodeId);
    }

    private static string Norm(string s)
    {
        return string.IsNullOrWhiteSpace(s) ? "" : s.Trim().ToLowerInvariant();
    }

}
