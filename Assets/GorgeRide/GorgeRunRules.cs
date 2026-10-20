using System;
namespace TaiwanGo
{
    public static class GorgeRunRules
    {
        public const float Duration=30,PracticeTime=5;
        static float Clamp01(float x){return Math.Max(0,Math.Min(1,x));}
        public static float TargetSpeed(float elapsed)
        {
            if(elapsed<5)return 14+8*Clamp01(elapsed/5);
            if(elapsed<20)return 22+7*Clamp01((elapsed-5)/15);
            return 29+6*Clamp01((elapsed-20)/10);
        }
        public static float Difficulty(float elapsed){return Clamp01((elapsed-PracticeTime)/(Duration-PracticeTime));}
        public static bool CanSpawnRockfall(float elapsed){return elapsed>=20&&elapsed<Duration-4.5f;}
    }
}
