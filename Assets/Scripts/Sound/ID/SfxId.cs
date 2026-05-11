public enum SfxId
{
    None,

    //UI
    UI_Click,           //버튼 클릭 했을 때
    UI_Confirm,         //아이템 버릴 시 경고문 뜰 때

    //Inventory
    Inv_Open,           //인벤토리 열릴 때
    Inv_Close,          //인벤토리 닫을 때
    Inv_Hover,          //아이템 Hover했을 때

    //Storage
    Storage_Open,       //창고 열릴 때
    Storage_Close,      //창고 닫을 때

    //Cooking
    Cook_Open_Steam,    //Cook 모드 들어갈때 Steam, Chop 동시재생해야함, 근데 Chop이 조금 늦게 나오면 좋을듯
    Cook_Open_Chop,
    Cook_Close,         //Cook 모드 나갈 때 (아직 없음)
    Cook_Minigame_Success,  //미니게임 노트 성공했을 때
    Cook_Minigame_fail,     //미니게임 노트 실패했을 때
    Cook_Success,       //요리 성공했을 때 (Fish Success 동일)
    Cook_Fail,          //요리 실패했을 때 (Fish Fail 동일)
    Cook_AddIng,        //재료 하나 넣을 때
    Cook_RemoveOneIng,  //재료 하나 뺄 떄
    Cook_RemoveAllIng,  //재료 전부 뺄 때

    //Fishing
    Fish_Cast,          //낚시 시작할 때
    Fish_Bite,          //물고기 물었을 때
    Fish_MinigameReel,  //미니게임 시작하고 스페이스바 누를 때
    Fish_Success,       //낚시 성공했을 때
    Fish_Fail,          //낚시 실패했을 때

    //Combat
    Atk_Swing,          //공격 시도 했을 때
    Atk_Hit,            //공격 성공 했을 때
    Player_Hurt,        //플레이어가 데미지 입었을 때
    Player_Die,         //플레이어가 죽었을 때
    Enemy_Hurt_01,      //적이 데미지 입었을 떄
    Enemy_Hurt_02,
    Enemy_Hurt_03,
    Enemy_Die_01,       //적이 죽었을 때
    Enemy_Die_02,
    Enemy_Die_03,
    Skill_Cast,         //스킬 시도할 때
    Skill_Hit,          //스킬 맞았을 때
    Skill_FailCooldown, //쿨타임이라서 스킬 안나갈 때

    //Tool
    Tool_Hoe,           //밭 갈 때
    Tool_WateringCan,   //밭에 물 줄 때
    Tool_Sickle,        //낫질할 때
    Plant_Seed,         //씨앗 심을 때

    //Movement
    Footstep_Grass,     //풀 위 걸을 때
    Footstep_Stone,     //돌 위 걸을 때
    Footstep_Wood,      //나무 위 걸을 때
    Door_Enter,         //문을 통해 들어갈 때/ 나올 때

    //Environment
    Weather_Sunny,      //화창할 때 나올만한거 
    Weather_Rainy,      //빗소리
    Floor_Cultivate     //경작 소리


}