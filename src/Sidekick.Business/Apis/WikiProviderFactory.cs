using Sidekick.Business.Apis.PoeDb;
using Sidekick.Business.Apis.PoeWiki;
using Sidekick.Business.Parsers.Models;
using Sidekick.Core.Settings;

namespace Sidekick.Business.Apis
{
    public class WikiProviderFactory: IWikiProvider
    {
        private readonly SidekickSettings settings;
        private readonly IPoeWikiClient poeWikiClient;
        private readonly IPoeDbClient poeDbClient;

        public WikiProviderFactory(SidekickSettings settings, IPoeWikiClient poeWikiClient, IPoeDbClient poeDbClient)
        {
            this.settings = settings;
            this.poeWikiClient = poeWikiClient;
            this.poeDbClient = poeDbClient;
        }

        public void Open(Item item)
        {
            GetCurrentProvider().Open(item);
        }

        private IWikiProvider GetCurrentProvider()
        {
            return settings.Wiki_Preferred == WikiSetting.PoeDb
                ? (IWikiProvider) poeDbClient
                : poeWikiClient;
        }
    }
}
