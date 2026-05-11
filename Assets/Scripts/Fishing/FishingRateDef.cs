using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class FishingRateDef
{
    public FishArea area;
    public WeatherManager.WeatherType weather;
    public FishTimeWindow timeWindow;
    public FishRarity rarity;
    public int rarityWeight;
}

public class FishingRateTable
{
    private struct Key : IEquatable<Key>
    {
        public FishArea area;
        public WeatherManager.WeatherType weather;
        public FishTimeWindow timeWindow;
        public FishRarity rarity;

        public bool Equals(Key other)
            => area == other.area && weather == other.weather && timeWindow == other.timeWindow && rarity == other.rarity;

        public override bool Equals(object obj) => obj is Key other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + (int)area;
                h = h * 31 + (int)weather;
                h = h * 31 + (int)timeWindow;
                h = h * 31 + (int)rarity;

                return h;
            }
        }
    }

    private readonly Dictionary<Key, int> map = new();

    public void Build(IEnumerable<FishingRateDef> defs)
    {
        map.Clear();
        foreach (var def in defs)
        {
            if (def == null) continue;

            var key = new Key
            {
                area = def.area,
                weather = def.weather,
                timeWindow = def.timeWindow,
                rarity = def.rarity
            };

            map[key] = Mathf.Max(0, def.rarityWeight);
        }
    }

    public int GetWeight(FishArea area, WeatherManager.WeatherType weather, FishTimeWindow timeWindow, FishRarity rarity)
    {
        var keyExact = new Key { area = area, weather = weather, timeWindow = timeWindow, rarity = rarity };
        if (map.TryGetValue(keyExact, out var wExact)) return wExact;

        var keyAny = new Key { area = area, weather = weather, timeWindow = FishTimeWindow.Any, rarity = rarity };
        if (map.TryGetValue(keyAny, out var wAny)) return wAny;

        return 0;
    }
}

