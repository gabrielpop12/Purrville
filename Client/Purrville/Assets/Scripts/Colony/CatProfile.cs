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
        public float Hunger = 85f;
        public float Fun = 80f;
        public float Social = 80f;
        public string HomeName = "Sin hogar";
        public int JobsCompleted;
        public int Friendships;
        public string LastMemory = "Llego a Purrville";
        private bool resting;
        // 0 disables a job; 1 is highest priority; 4 is lowest.
        public int WoodPriority = 2, StonePriority = 2, FoodPriority = 2, BuildPriority = 2;
        public int GetPriority(CatProfession job)
        {
            switch (job)
            {
                case CatProfession.Lumberjack: return WoodPriority;
                case CatProfession.Miner: return StonePriority;
                case CatProfession.Forager: return FoodPriority;
                case CatProfession.Builder: return BuildPriority;
                default: return 0;
            }
        }
        public void SetPriority(CatProfession job, int priority)
        {
            priority = Math.Max(0, Math.Min(4, priority));
            switch (job)
            {
                case CatProfession.Lumberjack: WoodPriority = priority; break;
                case CatProfession.Miner: StonePriority = priority; break;
                case CatProfession.Forager: FoodPriority = priority; break;
                case CatProfession.Builder: BuildPriority = priority; break;
            }
        }


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
            if (Energy < 18f) resting = true;
            if (Energy > 80f) resting = false;
            Hunger = Math.Clamp(Hunger - 0.42f * deltaTime, 0f, 100f);
            Fun = Math.Clamp(Fun - (working ? .18f : .04f) * deltaTime, 0f, 100f);
            Social = Math.Clamp(Social - .09f * deltaTime, 0f, 100f);
            Mood = Math.Clamp(Mood + (Energy < 20f || Hunger < 20f ? -1.1f : .15f) * deltaTime, 0f, 100f);
        }

        public bool NeedsRest => resting;
        public bool NeedsFood => Hunger < 35f;
        public bool NeedsFun => Fun < 30f;
        public bool NeedsSocial => Social < 30f;
        public string Status => NeedsFood ? "Hambriento" : NeedsRest ? "Cansado" : NeedsSocial ? "Solitario" : Mood < 35f ? "Malhumorado" : "Bien";
    }
}
