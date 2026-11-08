using System;
using System.Collections.Generic;
using UnityEngine;

namespace Purrville.Colony
{
    // Owns relationships and event generation, independently of colony rendering and jobs.
    public sealed class CatSocialSystem
    {
        private readonly System.Random random;
        private readonly Dictionary<CatProfile, Dictionary<CatProfile, int>> relations =
            new Dictionary<CatProfile, Dictionary<CatProfile, int>>();
        private readonly List<string> chronicle = new List<string>();
        public IReadOnlyList<string> Chronicle => chronicle;
        public CatSocialSystem(int seed) { random = new System.Random(seed ^ 673291); }

        public int Relationship(CatProfile a, CatProfile b)
        {
            if (a == null || b == null || a == b) return 0;
            return relations.TryGetValue(a, out var map) && map.TryGetValue(b, out int value) ? value : 0;
        }

        private void Adjust(CatProfile a, CatProfile b, int delta)
        {
            if (!relations.TryGetValue(a, out var map))
            {
                map = new Dictionary<CatProfile, int>();
                relations.Add(a, map);
            }
            map[b] = Mathf.Clamp(Relationship(a, b) + delta, -100, 100);
        }

        public string Interact(CatProfile a, CatProfile b)
        {
            if (a == null || b == null || a == b) return null;
            int affinity = Relationship(a, b);
            int temperamentBonus = a.Temperament == CatTemperament.Social ? 20 : 0;
            int grumpyPenalty = a.Temperament == CatTemperament.Grumpy ? 25 : 0;
            int roll = random.Next(100) + affinity / 3 + temperamentBonus - grumpyPenalty;
            bool friendly = roll >= 35;
            int change = friendly ? random.Next(4, 10) : -random.Next(3, 9);
            Adjust(a, b, change);
            Adjust(b, a, change);
            a.Social = Mathf.Min(100f, a.Social + 32f);
            b.Social = Mathf.Min(100f, b.Social + 24f);
            a.Mood = Mathf.Clamp(a.Mood + (friendly ? 5f : -6f), 0f, 100f);
            b.Mood = Mathf.Clamp(b.Mood + (friendly ? 4f : -5f), 0f, 100f);
            string eventText = friendly
                ? a.Name + " y " + b.Name + " pasaron un buen rato juntos."
                : a.Name + " y " + b.Name + " tuvieron una discusion.";
            a.LastMemory = eventText;
            b.LastMemory = eventText;
            a.Friendships = CountFriends(a);
            b.Friendships = CountFriends(b);
            chronicle.Add(eventText);
            if (chronicle.Count > 80) chronicle.RemoveAt(0);
            return eventText;
        }

        private int CountFriends(CatProfile cat)
        {
            int count = 0;
            if (relations.TryGetValue(cat, out var map))
                foreach (var pair in map)
                    if (pair.Value >= 30) count++;
            return count;
        }

        public List<KeyValuePair<CatProfile, int>> KnownRelations(CatProfile cat)
        {
            var result = new List<KeyValuePair<CatProfile, int>>();
            if (relations.TryGetValue(cat, out var map))
                foreach (var pair in map) result.Add(pair);
            result.Sort((a, b) => b.Value.CompareTo(a.Value));
            return result;
        }
    }
}
