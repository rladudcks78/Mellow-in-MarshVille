using UnityEngine;
using System.Collections.Generic;

public class AnimController
{
    private readonly Animator _anim;
    private readonly List<string> _dirHistory = new List<string>();

    private readonly Dictionary<string, int> _dirHashes = new Dictionary<string, int>
    {
        { "Up",    Animator.StringToHash("IsMove_Up") },
        { "Down",  Animator.StringToHash("IsMove_Down") },
        { "Left",  Animator.StringToHash("IsMove_Left") },
        { "Right", Animator.StringToHash("IsMove_Right") }
    };

    // [추가] 기절 애니메이션 Trigger 파라미터 ID
    // private readonly int _passOutHash = Animator.StringToHash("PassOut");

    public AnimController(Animator anim) => _anim = anim;

    public void UpdateAnimation(Vector2 input)
    {
        ManageDir(input.y > 0, "Up");
        ManageDir(input.y < 0, "Down");
        ManageDir(input.x > 0, "Right");
        ManageDir(input.x < 0, "Left");

        ResetMoveParameters();

        if (_dirHistory.Count > 0)
        {
            string primaryDir = _dirHistory[0];
            _anim.SetBool(_dirHashes[primaryDir], true);
        }
    }

    // [추가] 기절 트리거 실행 함수
    public void TriggerPassOut()
    {
        // 1. 이동 관련 파라미터 모두 초기화 
        ResetMoveParameters();
        _dirHistory.Clear();

        // 2. 기절 트리거 발동
        // _anim.SetTrigger(_passOutHash); 

        Debug.Log("[AnimController] 기절 애니메이션 명령 전달됨 (Trigger: PassOut)");
    }

    private void ManageDir(bool pressed, string dirName)
    {
        if (pressed && !_dirHistory.Contains(dirName))
            _dirHistory.Add(dirName);
        else if (!pressed && _dirHistory.Contains(dirName))
            _dirHistory.Remove(dirName);
    }

    private void ResetMoveParameters()
    {
        foreach (var hash in _dirHashes.Values)
        {
            _anim.SetBool(hash, false);
        }
    }
}