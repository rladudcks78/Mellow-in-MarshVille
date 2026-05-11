using UnityEngine;
using System.Collections.Generic;

public class GamePauseManager : MonoBehaviour
{
    public static GamePauseManager Instance;

    public enum Modal
    {
        Inventory,
        Storage,
        Cooking,
        Option,
        Dialogue,
        FishingMiniGame,
        Note,
        Quest
    }

    [Header("Refs")]
    [SerializeField] private InputReader inputReader;

    [Header("Behavior")]
    [SerializeField] private bool pauseWorldWhileAnyModal = true;

    //현재 켜져있는 모달들
    private readonly HashSet<Modal> activeModals = new();

    public bool IsAnyModalOpen => activeModals.Count > 0;
    public bool IsWorldPaused => pauseWorldWhileAnyModal && IsAnyModalOpen;


    private void Awake()
    {
        if(Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        Apply();
    }

    public void OnDestroy()
    {
        if(Instance == this)
        {
            Time.timeScale = 1f;
        }
    }

    public void Enter(Modal modal)
    {
        if (activeModals.Add(modal))
            Apply();
    }

    public void Exit(Modal modal)
    {
        if (activeModals.Remove(modal))
            Apply();
    }

    public bool Contains(Modal modal) => activeModals.Contains(modal);

    private void Apply()
    {
        //월드 정지
        if (pauseWorldWhileAnyModal)
            Time.timeScale = IsAnyModalOpen ? 0f : 1f;

        //입력 모드 전환
        if(inputReader != null)
        {
            if (IsAnyModalOpen) inputReader.EnableUIInput();
            else inputReader.EnablePlayerInput();
        }
    }
}
