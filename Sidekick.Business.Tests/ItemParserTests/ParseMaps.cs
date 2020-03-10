using NUnit.Framework;
using System.Collections.Generic;
using Sidekick.Business.Maps;
using FluentAssertions;
using Sidekick.Business.Parsers.Models;
using Sidekick.Business.Parsers;
using System.Threading.Tasks;
using FluentAssertions.Execution;

namespace Sidekick.Business.Tests.ItemParserTests
{
    public class ParseMaps : TestContext<ItemParser>
    {
        [OneTimeSetUp]
        public void Setup()
        {
            GetMockFor<IMapService>()
                .Setup(o => o.MapNames)
                .Returns(new HashSet<string> { "Beach" });
        }

        [Test]
        public async Task ParseNormalMap()
        {
            var actual = await Subject.ParseItem(ExampleItems.NormalMap) as MapItem;

            using (new AssertionScope())
            {
                actual.Name.Should().Be("Beach Map");
                actual.MapTier.Should().Be("1");
            }
        }

        [Test]
        public async Task ParseMagicMap()
        {
            var actual = await Subject.ParseItem(ExampleItems.MagicMap) as MapItem;

            using(new AssertionScope())
            {
                // Probably incorrect behavior, right now it always appends blighted prefix for magic maps
                actual.Name.Should().Be("Blighted Beach");
                actual.Type.Should().Be("Beach");
                actual.MapTier.Should().Be("1");
            }
        }

        [Test]
        public async Task ParseUniqueMap()
        {
            var actual = await Subject.ParseItem(ExampleItems.UniqueMap) as MapItem;

            using (new AssertionScope())
            {
                actual.Name.Should().Be("Maelström of Chaos");
                actual.MapTier.Should().Be("5");
                actual.ItemQuantity.Should().Be("69");
                actual.ItemRarity.Should().Be("356");
            }
        }
    }
}
