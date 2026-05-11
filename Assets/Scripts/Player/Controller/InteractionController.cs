using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어의 상호작용(농사, 도구 사용) 로직을 전담하는 순수 C# 클래스입니다.
/// </summary>
public class InteractionController
{
    private readonly FarmSystem _farmSystem;
    private readonly WaterSystem _waterSystem;
    private readonly CombatSystem _combatSystem;
    private readonly InventorySystem _inventory;
    private readonly SkillSystem _skillSystem;

    //toolType으로 분기 나누기
    private const string TOOL_HOE = "hoe";
    private const string TOOL_WATERING_CAN = "wateringCan";
    private const string TOOL_SICKLE = "sickle";
    private const string TOOL_FISHING_ROD = "fishingRod";

    // 생성자를 통해 외부 시스템(인벤토리)과 설정값(도구 ID)을 주입받습니다.
    public InteractionController(FarmSystem farm, WaterSystem water, CombatSystem combat, InventorySystem inventory, SkillSystem skillSystem)
    {
        _farmSystem = farm;
        _waterSystem = water;
        _combatSystem = combat;
        _inventory = inventory;
        _skillSystem = skillSystem;
    }

    /// <summary>
    /// 현재 아이템에 따라 적절한 상호작용을 수행합니다.
    /// </summary>
    public void ExecuteInteraction(Vector3 mousePos, Vector3 playerPos, Vector2 facingDir)
    {
        //인벤/DB 준비 체크
        if (_inventory == null || !_inventory.IsReady) return;

        //활성 아이템 없으면 채집인데 일단 아무것도 안하는걸로 진행 <<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<나중에 바꿔야함!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
        if (!_inventory.TryGetActive(out _, out _, out ItemDef def) || def == null)
            return;

        Debug.Log($"[아이템 확인] ID: {def.itemId} | 이름: {def.name} | isSeed: {def.isSeed} | IsTool: {def.IsTool}");

        //전투
        if (def.IsWeapon)  
        {
            _combatSystem?.PerformAttack(playerPos, facingDir);
            return;
        }

        //TileManager 필요한 로직 -> 농사 / 낚시
        if (TileManager.Instance == null) return;

        // 1. 마우스 그리드 좌표 
        Vector3Int mouseCell = TileManager.Instance.WorldToCell(mousePos);
        Vector2Int mouseGridPos = new Vector2Int(mouseCell.x, mouseCell.y);

        // 2. 방향 변환 (Vector2 -> Vector2Int)
        // PlayerInteract에서 마우스 방향으로 업데이트된 FacingDir를 받음
        Vector2Int gridFacingDir = Vector2Int.down;
        if (facingDir == Vector2.up) gridFacingDir = Vector2Int.up;
        else if (facingDir == Vector2.down) gridFacingDir = Vector2Int.down;
        else if (facingDir == Vector2.left) gridFacingDir = Vector2Int.left;
        else if (facingDir == Vector2.right) gridFacingDir = Vector2Int.right;

        // 4. 범위 계산 
        List<Vector2Int> targetCells = ToolRangeCalculator.GetToolRangeCells(mouseGridPos, gridFacingDir, def);

        if (def.isSeed)
        {
            _farmSystem?.OnSeedPlanted(mousePos, def.itemId, () =>
            {   
                _inventory.ConsumeAt(_inventory.ActiveSlotIndex, 1);
            });
            return;
        }

        //도구 분기
        if (def.IsTool)
        {
            string tool = def.toolType != null ? def.toolType.Trim() : "";

            if(tool.Equals(TOOL_HOE, System.StringComparison.OrdinalIgnoreCase))
            {
                if (SoundManager.Instance != null) SoundManager.Instance.PlaySfx(SfxId.Tool_Hoe);
                _farmSystem?.OnHoeUsedBatch(targetCells);
                return;
            }

            if(tool.Equals(TOOL_WATERING_CAN, System.StringComparison.OrdinalIgnoreCase))
            {
                if (SoundManager.Instance != null) SoundManager.Instance.PlaySfx(SfxId.Tool_WateringCan);
                _farmSystem?.OnWateringBatch(targetCells);
                return;
            }

            if (tool.Equals(TOOL_SICKLE, System.StringComparison.OrdinalIgnoreCase))
            {
                if (SoundManager.Instance != null) SoundManager.Instance.PlaySfx(SfxId.Tool_Sickle);
                _farmSystem?.OnHarvestBatch(targetCells);
                return;
            }

            if (tool.Equals(TOOL_FISHING_ROD, System.StringComparison.OrdinalIgnoreCase))
            {
                _waterSystem?.TryFishing(playerPos, facingDir);
                return;
            }
        }
    }

    public void ExecuteSkill(Vector3 playerPos, Vector2 facingDir)
    {
        // 1. 인벤토리 준비 확인
        if (_inventory == null || !_inventory.IsReady) return;
        if (_skillSystem == null) return;

        // 2. 현재 손에 든 아이템 확인
        if (!_inventory.TryGetActive(out int slotIndex, out ItemStack stack, out ItemDef def) || def == null) return;

        // 3. 음식 섭취 로직
        if (def.isUsable && !def.IsWeapon && !def.isSeed && !def.IsTool)
        {
            // 1) 아이템 1개 소모
            if (_inventory.ConsumeAt(slotIndex, 1))
            {
                // 2) BuffManager에게 효과 적용 요청
                if (BuffManager.Instance != null)
                {
                    BuffManager.Instance.ConsumeFood(def.itemId, stack.quality);
                }
            }
            return;
        }

        // 4. 기존 무기 스킬 로직
        if (def.IsWeapon)
        {
            _skillSystem.TryUseSkill(playerPos, facingDir, def);
        }
    }
}