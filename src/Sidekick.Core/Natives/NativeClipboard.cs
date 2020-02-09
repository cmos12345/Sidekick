using System.Threading.Tasks;

namespace Sidekick.Core.Natives
{
    public class NativeClipboard : INativeClipboard
    {
        public async Task<string> GetText()
        {
            return await TextCopy.Clipboard.GetTextAsync();
        }

        public async Task SetText(string text)
        {
            if (!string.IsNullOrWhiteSpace(text))
            {
                await TextCopy.Clipboard.SetTextAsync(text);
            }
        }
    }
}
