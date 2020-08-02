namespace UniMob
{
    internal static class Utils
    {
        public static bool SetBool(ref bool field, bool value)
        {
            if (field == value)
                return false;

            field = value;
            return true;
        }
    }
}