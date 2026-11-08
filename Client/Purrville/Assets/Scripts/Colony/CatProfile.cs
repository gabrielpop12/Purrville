using System;

namespace Purrville.Colony
{
    public enum CatTemperament { Curious, Calm, Hardworking, Playful, Grumpy, Social }
    public enum CatProfession { Generalist, Lumberjack, Miner, Forager, Builder }

    [Serializable]
    public sealed class CatProfile
    {
        public string Name;
        public CatTemperament Temperament;
        public CatProfession Profession;
        public float Energy = 100f;
        public float Mood = 75f;
        public int JobsCompleted;

        private static readonly string[] Names = {
            "Mochi", "Miso", "Nube", "Chispa", "Menta", "Pompom", "Luna",
            "Tito", "Mimi", "Canela", "Trufa", "Pixel", "Nori", "Kiwi",
            "Copito", "Yuki", "Churro", "Mora", "Tuna", "Biscuit"
        };

        public static CatProfile Create(System.Random rng, int index)
        {
            return new CatProfile {
                Name = Names[rng.Next(Names.Length)] + (index >= Names.Length ? " " + (index + 1) : ""),
                Temperament = (CatTemperament)rng.Next(Enum.GetValues(typeof(CatTemperament)).Length),
                Profession = (CatProfession)rng.Next(Enum.GetValues(typeof(CatProfession)).Length),
                Mood = rng.Next(60, 96)
            };
        }

        public void Tick(float deltaTime, bool working)
        {
            Energy = Math.Clamp(Energy + (working ? -2.2f : 3.8f) * deltaTime, 0f, 100f);
            Mood = Math.Clamp(Mood + (Energy < 20f ? -1.1f : .15f) * deltaTime, 0f, 100f);
        }

        public bool NeedsRest => Energy < 18f;
        public string Status => NeedsRest ? "Descansando" : Mood < 35f ? "Malhumorado" : "Bien";
    }
}
