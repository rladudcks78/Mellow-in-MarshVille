using Mono.Cecil;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SpriteResolver : MonoBehaviour
{
    //spritePath -> Sprite 캐시
    private readonly Dictionary<string, Sprite> cache = new();

    //folderPath -> spriteName -> sprite 캐시
    private readonly Dictionary<string, Dictionary<string, Sprite>> folderCache = new();

    //경고 중복 방지
    private readonly HashSet<string> warned = new();

    public Sprite Load(string spritePath)
    {
        if (string.IsNullOrWhiteSpace(spritePath)) return null;

        spritePath = spritePath.Trim().Trim('"').Replace("\\", "/");

        if (cache.TryGetValue(spritePath, out var cached) && cached != null)
            return cached;

        //1)Single Sprite 먼저 시도
        var direct = Resources.Load<Sprite>(spritePath);
        if (direct != null)
        {
            cache[spritePath] = direct;
            return direct;
        }

        //2)Multiple 시도
        int lastSlash = spritePath.LastIndexOf('/');
        if (lastSlash < 0 || lastSlash >= spritePath.Length)
        {
            WarnOnce(spritePath, $"Resources.Load<Sprite> 실패.spritePath 형식 확인 필요: {spritePath}");
            cache[spritePath] = null;
            return null;
        }

        string folderPath = spritePath.Substring(0, lastSlash);
        string spriteName = spritePath.Substring(lastSlash + 1);

        var found = FindInFolder(folderPath, spriteName);

        if (found == null)
            found = FindInFolder(spritePath, spriteName);

        if (found == null)
        {
            WarnOnce(spritePath,
                $"- Single이면: Resources/{spritePath}.png 같은 단일 스프라이트여야 함\n" +
                $"- Multiple이면: '{folderPath}' 폴더에서 sprite name '{spriteName}'를 찾음\n" +
                $"- 스프라이트 실제 이름(Inspector 상단 Name)과 '{spriteName}'가 완전 동일해야 함");
        }
        cache[spritePath] = found;
        return found;
    }

    private Sprite FindInFolder(string folderPath, string spriteName)
    {
        if (string.IsNullOrWhiteSpace(folderPath)) return null;
        folderPath = folderPath.Trim().Trim('"').Replace("\\", "/");

        if (!folderCache.TryGetValue(folderPath, out var map) || map == null)
        {
            map = new Dictionary<string, Sprite>();
            var all = Resources.LoadAll<Sprite>(folderPath);

            //폴더에 없거나 로딩 실패면 빈 map 유지
            for (int i = 0; i < all.Length; i++)
            {
                var s = all[i];
                if (s == null) continue;
                map[s.name] = s;
            }

            folderCache[folderPath] = map;
        }

        return map.TryGetValue(spriteName, out var s2) ? s2 : null;
    }

    private void WarnOnce(string key, string msg)
    {
        if (warned.Contains(key)) return;
        warned.Add(key);
        Debug.LogWarning($"[SpriteResolver] {msg}");
    }

    // 특정 폴더의 모든 스프라이트를 배열로 반환하는 함수 (크롭 성장 이미지용)
    public Sprite[] LoadAllSpritesAtFolder(string folderPath)
    {
        // 1. 경로 정리 (공백 제거, 따옴표 제거, 역슬래시 변환)
        if (string.IsNullOrWhiteSpace(folderPath)) return null;
        folderPath = folderPath.Trim().Trim('"').Replace("\\", "/");

        // 2. 이미 캐싱된 폴더인지 확인 (folderCache 이용)
        if (folderCache.TryGetValue(folderPath, out var map) && map != null)
        {
            // 딕셔너리의 값(Value)들만 뽑아서 배열로 반환 (이름순 정렬 등은 파일명에 따라 결정됨)
            return map.Values.ToArray();
        }

        // 3. 캐시에 없으면 Resources 폴더에서 로드
        Sprite[] sprites = Resources.LoadAll<Sprite>(folderPath);

        // 4. 로드된 데이터가 있으면 캐시에 저장 (다음 요청 때 빠르게 주기 위해)
        if (sprites != null && sprites.Length > 0)
        {
            var newMap = new Dictionary<string, Sprite>();
            foreach (var s in sprites)
            {
                newMap[s.name] = s;
            }
            folderCache[folderPath] = newMap;
        }
        else
        {
            Debug.LogWarning($"[SpriteResolver] 해당 폴더에 이미지가 없습니다: {folderPath}");
        }

        return sprites;
    }
}