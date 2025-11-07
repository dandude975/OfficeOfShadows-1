namespace OOS.Shared
{
    public static class GameFlags
    {
        public static bool DevMode { get; set; } = false;
        public static bool RealismMode { get; set; } = false; // keep OFF unless the user opts in
        public static bool AccessibilityNoJumps { get; set; } = false;
    }
}
