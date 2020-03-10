using System.Collections.Generic;

namespace Sidekick.UI.Leagues.Delve
{
    public class DelveRegion
    {
        public DelveRegion(string name)
        {
            Name = name;
        }

        public string Name { get; set; }

        public List<DelveFossil> Fossils { get; set; } = new List<DelveFossil>();
    }
}
