using System;
using System.Collections.Generic;

namespace Purrville.Colony
{
    /// <summary>Deterministic, lightweight emergent social events; no external AI service required.</summary>
    public sealed class ColonyStorySystem
    {
        private readonly Random random;
        private readonly List<string> history = new List<string>();
        public IReadOnlyList<string> History => history;
        public ColonyStorySystem(int seed) { random = new Random(seed ^ 714231); }

        public string TryGenerate(IReadOnlyList<CatProfile> profiles)
        {
            if (profiles == null || profiles.Count < 2) return null;
            int a = random.Next(profiles.Count);
            int b = random.Next(profiles.Count - 1);
            if (b >= a) b++;
            CatProfile first = profiles[a], second = profiles[b];
            string message;
            int eventType = random.Next(4);
            switch (eventType)
            {
                case 0:
                    first.Friendships++;
                    second.Friendships++;
                    first.Mood = Math.Min(100f, first.Mood + 7f);
                    second.Mood = Math.Min(100f, second.Mood + 7f);
                    message = first.Name + " compartio una merienda con " + second.Name + ".";
                    break;
                case 1:
                    first.Mood = Math.Max(0f, first.Mood - 8f);
                    second.Mood = Math.Max(0f, second.Mood - 5f);
                    message = first.Name + " y " + second.Name + " discutieron por una caja de carton.";
                    break;
                case 2:
                    first.Mood = Math.Min(100f, first.Mood + 12f);
                    message = first.Name + " encontro una flor y se la regalo a " + second.Name + ".";
                    break;
                default:
                    first.Mood = Math.Min(100f, first.Mood + 5f);
                    message = first.Name + " conto una historia graciosa a " + second.Name + ".";
                    break;
            }
            first.LastMemory = message;
            second.LastMemory = message;
            history.Add(message);
            if (history.Count > 50) history.RemoveAt(0);
            return message;
        }
    }
}
