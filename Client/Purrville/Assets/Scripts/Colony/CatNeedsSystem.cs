using UnityEngine;

namespace Purrville.Colony
{
    // Pure rules for satisfying needs; movement and inventories stay in ColonyPrototype.
    public static class CatNeedsSystem
    {
        public static bool WantsFood(CatProfile cat) => cat.NeedsFood;
        public static bool WantsRest(CatProfile cat) => cat.NeedsRest;
        public static bool WantsCompany(CatProfile cat) => cat.NeedsSocial;
        public static bool WantsPlay(CatProfile cat) => cat.NeedsFun;

        public static void Eat(CatProfile cat)
        {
            cat.Hunger = Mathf.Min(100f, cat.Hunger + 65f);
            cat.Mood = Mathf.Min(100f, cat.Mood + 4f);
            cat.LastMemory = "Comio en el almacen.";
        }

        public static bool Rest(CatProfile cat, float deltaTime, bool hasHome)
        {
            cat.Energy = Mathf.Min(100f, cat.Energy + deltaTime * 9f);
            if (cat.Energy < 85f) return false;
            cat.LastMemory = hasHome ? "Descanso en su casa." : "Descanso junto al almacen.";
            return true;
        }

        public static bool Play(CatProfile cat, float deltaTime)
        {
            cat.Fun = Mathf.Min(100f, cat.Fun + deltaTime * 12f);
            if (cat.Fun < 85f) return false;
            cat.LastMemory = "Se tomo un rato para jugar.";
            return true;
        }
    }
}
