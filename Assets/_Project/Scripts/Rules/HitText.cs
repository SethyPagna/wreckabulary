using System;
using System.Globalization;

namespace Wreckabulary.Rules
{
    public static class HitText
    {
        public static string Damage(float amount) => amount > 0f ? ((int)Math.Ceiling(amount)).ToString(CultureInfo.InvariantCulture) : "";

        public static bool IsDamageNumber(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text)
                if (c < '0' || c > '9') return false;
            return true;
        }
    }
}
