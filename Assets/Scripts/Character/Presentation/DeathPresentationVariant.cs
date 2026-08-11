


namespace Character.Presentation
{
    public enum DeathPresentationVariant : byte
    {
        Default = 0,
        Executed = 1,
    }


    public static class DeathPresentationVariantExtensions
    {
        public static string ToDebugString(
            this DeathPresentationVariant variant)
        {
            return variant switch
            {
                DeathPresentationVariant.Default => "Default",
                DeathPresentationVariant.Executed => "Executed",
                _ => $"Unknown({(byte)variant})",
            };
        }
    }
}
