using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace Sidekick.Core.Natives
{
    public interface INativeProcess
    {
        Mutex Mutex { get; set; }
        bool IsPathOfExileInFocus { get; }
        bool IsSidekickInFocus { get; }
        Task CheckPermission();
        float ActiveWindowDpi { get; }
        Rectangle GetScreenDimensions();
        string ClientLogPath { get; }
        bool Is64bitProcess { get; }
    }
}
