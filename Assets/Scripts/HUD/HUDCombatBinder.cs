using UnityEngine;

public class HUDCombatBinder : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private HUDRoot hud;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private SkillSystem skillSystem;

    [Header("HP Display")]
    [SerializeField] private bool hpUseCeil = true; // float -> int 변환 방식

    private void Awake()
    {
        if (hud == null) hud = FindAnyObjectByType<HUDRoot>();
        if (playerHealth == null) playerHealth =  FindAnyObjectByType<PlayerHealth>();
        if (skillSystem == null) skillSystem = FindAnyObjectByType<SkillSystem>();
    }

    private void OnEnable()
    {
        if (playerHealth != null)
            playerHealth.OnHpChanged += HandleHpChanged;

        if (skillSystem != null)
            skillSystem.OnSkillCooldownStarted += HandleSkillCooldownStarted;

        if (hud != null && playerHealth != null)            
            HandleHpChanged(playerHealth.CurrentHP, playerHealth.MaxHP);
     
    }

    private void OnDisable()
    {
        if (playerHealth != null)
            playerHealth.OnHpChanged -= HandleHpChanged;

        if (skillSystem != null)
            skillSystem.OnSkillCooldownStarted -= HandleSkillCooldownStarted;
    }

    private void HandleHpChanged(float cur, float max)
    {
        if (hud == null) return;

        int curI = hpUseCeil ? Mathf.CeilToInt(cur) : Mathf.RoundToInt(cur);
        int maxI = hpUseCeil ? Mathf.CeilToInt(max) : Mathf.RoundToInt(max);

        hud.SetHealth(curI, maxI);
    }

    private void HandleSkillCooldownStarted(float cooldownSec)
    {
        if (hud == null) return;
        hud.TriggerSkillCooldown(cooldownSec);
    }
}
