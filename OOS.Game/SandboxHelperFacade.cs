using OOS.Shared;

namespace OOS.Game
{
    /// <summary>
    /// Facade so legacy calls to SandboxHelper (in OOS.Game) still work.
    /// Forwards to OOS.Shared.SandboxHelper.
    /// </summary>
    public static class SandboxHelper
    {
        public static string EnsureSandboxFolder() => OOS.Shared.SandboxHelper.EnsureSandboxFolder();
        public static void OpenSandboxFolder() => OOS.Shared.SandboxHelper.OpenSandboxFolder();
    }
}
