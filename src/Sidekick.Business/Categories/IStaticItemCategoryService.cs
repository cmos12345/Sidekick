using Sidekick.Business.Apis.Poe.Models;
using System.Collections.Generic;

namespace Sidekick.Business.Categories
{
    public interface IStaticItemCategoryService
    {
        List<StaticItemCategory> Categories { get; }
        Dictionary<string, string> Lookup { get; }
        Dictionary<string, string> CurrencyUrls { get; }
    }
}
