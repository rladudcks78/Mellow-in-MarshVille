using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[CreateAssetMenu(fileName = "InputReader", menuName = "SO/InputReader")]
public class InputReader : ScriptableObject, PlayerInputAction.IPlayerActions, PlayerInputAction.IUIActions
{
    // --- 이벤트 정의 (기존과 동일) ---
    public event UnityAction<Vector2> MoveEvent;
    public event UnityAction AttackEvent;
    public event UnityAction KickboardEvent;
    public event UnityAction<int> HotbarEvent;
    public event UnityAction ContextClickEvent;
    public event UnityAction skillEvent;              // 우클릭 스킬

    public event UnityAction InventoryEvent;
    public event UnityAction QuestEvent;
    public event UnityAction NoteEvent;
    public event UnityAction CloseEvent;
    public event UnityAction ScrollWheelEvent;

    public event UnityAction<Vector2> PointEvent;
    public event UnityAction LeftClickEvent;
    public event UnityAction RightClickStartedEvent;
    public event UnityAction RightClickCanceledEvent;

    private PlayerInputAction _input; // 실제 입력을 받아오는 클래스

    #region 라이프사이클 관리 (에러 방지 핵심)
    private void OnEnable()
    {
        // 1. _input이 없을 때만 새로 생성합니다.
        if (_input == null)
        {
            _input = new PlayerInputAction();
            _input.Player.SetCallbacks(this);
            _input.UI.SetCallbacks(this);
        }

        // 2. 인스턴스가 확실히 생성된 후 활성화합니다.
        EnablePlayerInput();
    }

    private void OnDisable()
    {
        // 3. 종료 시 _input이 있을 때만 비활성화를 호출하여 Null에러를 방지합니다.
        if (_input != null)
        {
            DisableAllInput();
        }
    }
    #endregion

    #region 조작 모드 전환 (안전장치 추가)
    public void EnablePlayerInput()
    {
        // ?. 연산자는 _input이 null이 아닐 때만 뒤의 함수를 실행합니다.
        _input?.UI.Disable();   // UI입력 끄고
        _input?.Player.Enable(); // 플레이어 입력 켜기
    }

    public void EnableUIInput()
    {
        _input?.Player.Disable(); // 플레이어 입력 끄고
        _input?.UI.Enable();     // UI입력 켜기
    }

    // 에러가 났던 지점: ?.를 사용하여 null 체크를 강제합니다.
    public void DisableAllInput() => _input?.Disable();
    #endregion

    #region 인터페이스 구현부 (입력 값 전달)
    public void OnMove(InputAction.CallbackContext context) => MoveEvent?.Invoke(context.ReadValue<Vector2>());
    public void OnAttack(InputAction.CallbackContext context) { if (context.performed) AttackEvent?.Invoke(); }
    public void OnKickBoard(InputAction.CallbackContext context) { if (context.performed) KickboardEvent?.Invoke(); }
    public void OnContextClick(InputAction.CallbackContext context) 
    {
        if (context.performed)
        {
            ContextClickEvent?.Invoke();
            skillEvent?.Invoke();
        }
    }

    // 핫바 선택 (1~0)
    public void OnHotbar1(InputAction.CallbackContext context) { if (context.performed) HotbarEvent?.Invoke(1); }
    public void OnHotbar2(InputAction.CallbackContext context) { if (context.performed) HotbarEvent?.Invoke(2); }
    public void OnHotbar3(InputAction.CallbackContext context) { if (context.performed) HotbarEvent?.Invoke(3); }
    public void OnHotbar4(InputAction.CallbackContext context) { if (context.performed) HotbarEvent?.Invoke(4); }
    public void OnHotbar5(InputAction.CallbackContext context) { if (context.performed) HotbarEvent?.Invoke(5); }
    public void OnHotbar6(InputAction.CallbackContext context) { if (context.performed) HotbarEvent?.Invoke(6); }
    public void OnHotbar7(InputAction.CallbackContext context) { if (context.performed) HotbarEvent?.Invoke(7); }
    public void OnHotbar8(InputAction.CallbackContext context) { if (context.performed) HotbarEvent?.Invoke(8); }
    public void OnHotbar9(InputAction.CallbackContext context) { if (context.performed) HotbarEvent?.Invoke(9); }
    public void OnHotbar0(InputAction.CallbackContext context) { if (context.performed) HotbarEvent?.Invoke(10); }

    public void OnPoint(InputAction.CallbackContext context) => PointEvent?.Invoke(context.ReadValue<Vector2>());
    public void OnLeftClick(InputAction.CallbackContext context) { if (context.started) LeftClickEvent?.Invoke(); }
    public void OnRightClick(InputAction.CallbackContext context)
    {
        if (context.started) RightClickStartedEvent?.Invoke();
        if (context.canceled) RightClickCanceledEvent?.Invoke();
    }
    public void OnInventory(InputAction.CallbackContext context) { if (context.performed) InventoryEvent?.Invoke(); }
    public void OnQuest(InputAction.CallbackContext context) { if (context.performed) QuestEvent?.Invoke(); }
    public void OnNote(InputAction.CallbackContext context) { if (context.performed) NoteEvent?.Invoke(); }
    public void OnClose(InputAction.CallbackContext context) { if (context.performed) CloseEvent?.Invoke(); }
    public void OnScrollWheel(InputAction.CallbackContext context) { if (context.performed) ScrollWheelEvent?.Invoke(); }
    #endregion
}