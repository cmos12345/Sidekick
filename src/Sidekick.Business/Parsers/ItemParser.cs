using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Sidekick.Business.Filters;
using Sidekick.Business.Languages;
using Sidekick.Business.Maps;
using Sidekick.Business.Parsers.Models;
using Sidekick.Business.Parsers.Types;
using Sidekick.Business.Tokenizers;
using Sidekick.Business.Tokenizers.ItemName;

namespace Sidekick.Business.Parsers
{
    public class ItemParser : IItemParser
    {
        public readonly string[] PROPERTY_SEPERATOR = new string[] { "--------" };
        public readonly string[] NEWLINE_SEPERATOR = new string[] { Environment.NewLine };
        private readonly ILanguageProvider languageProvider;
        private readonly ILogger logger;
        private readonly IMapService mapService;
        private readonly ITokenizer itemNameTokenizer;

        public ItemParser(ILanguageProvider languageProvider,
            ILogger logger,
            IEnumerable<ITokenizer> tokenizers,
            IMapService mapService)
        {
            this.languageProvider = languageProvider;
            this.logger = logger;
            this.mapService = mapService;
            itemNameTokenizer = tokenizers.OfType<ItemNameTokenizer>().First();
        }

        /// <summary>
        /// Tries to parse an item based on the text that Path of Exile gives on a Ctrl+C action.
        /// There is no recurring logic here so every case has to be handled manually.
        /// </summary>
        public async Task<Item> ParseItem(string itemText)
        {
            await languageProvider.FindAndSetLanguage(itemText);

            try
            {
                var lines = itemText.Split(NEWLINE_SEPERATOR, StringSplitOptions.RemoveEmptyEntries);
                // Every item should start with Rarity in the first line.
                if (!lines[0].StartsWith(languageProvider.Language.DescriptionRarity)) throw new Exception("Probably not an item.");

                var itemProperties = GetItemProperties(lines);

                var rarityString = lines[0].Replace(languageProvider.Language.DescriptionRarity, string.Empty);
                var rarity = GetRarity(rarityString);

                Item item;
                if (itemProperties.IsMap)
                {
                    item = GetMapItem(itemProperties, lines, rarity);
                }
                else if (itemProperties.IsOrgan)
                {
                    item = new OrganItem
                    {
                        Name = lines[1]
                    };
                }
                else
                {
                    item = rarity switch
                    {
                        Rarity.Unique => GetUniqueItem(itemProperties, lines),
                        Rarity.Rare => GetRareItem(itemProperties, lines),
                        Rarity.Magic => throw new Exception("Magic items are not yet supported."),
                        Rarity.Normal => GetNormalItem(itemProperties, lines),
                        Rarity.Currency => GetCurrencyItem(lines[1]),
                        Rarity.Gem => GetGemItem(itemProperties, lines),
                        Rarity.DivinationCard => GetDivinationCardItem(lines[1]),
                        _ => throw new NotImplementedException()
                    };
                }

                if (!string.IsNullOrWhiteSpace(item.Name))
                {
                    item.Name = ParseName(item.Name);
                }

                if (!string.IsNullOrWhiteSpace(item.Type))
                {
                    item.Type = ParseName(item.Type);
                }

                item.Rarity = rarity;
                item.IsCorrupted = itemProperties.IsCorrupted;
                item.IsIdentified = itemProperties.IsIdentified;
                item.ItemText = itemText;
                return item;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Could not parse item.");
                return null;
            }
        }

        private static Item GetDivinationCardItem(string line)
        {
            return new DivinationCardItem()
            {
                Name = line,
                Type = line,
            };
        }

        private Item GetGemItem(ItemProperties itemProperties, string[] lines)
        {
            var item = new GemItem()
            {
                Name = lines[1],        // Need adjustment for Thai Language
                Type = lines[1],        // For Gems the Type has to be set to the Gem Name insead of the name itself
                Level = GetNumberFromString(lines[4]),
                ExperiencePercent = lines.Any(x => x.StartsWith(languageProvider.Language.DescriptionExperience)) ? ParseGemExperiencePercent(lines.Where(y => y.StartsWith(languageProvider.Language.DescriptionExperience)).FirstOrDefault()) : 0, // Some gems have no experience like portal or max ranks
                Quality = itemProperties.HasQuality ? GetNumberFromString(lines.Where(x => x.StartsWith(languageProvider.Language.DescriptionQuality)).FirstOrDefault()) : "0",      // Quality Line Can move for different Gems
                IsVaalVersion = itemProperties.IsCorrupted && lines[3].Contains(languageProvider.Language.KeywordVaal) // check if the gem tags contain Vaal
            };

            // if it's the vaal version, remap to have that name instead
            // Unsure if this works on non arabic lettering (ru/th/kr)
            if (item.IsVaalVersion)
            {
                var vaalName = lines.Where(x => x.Contains(languageProvider.Language.KeywordVaal) && x.Contains(item.Name)).FirstOrDefault(); // this should capture the vaaled name version
                item.Name = vaalName;
                item.Type = vaalName;
            }

            return item;
        }

        private Item GetCurrencyItem(string line)
        {
            return new CurrencyItem()
            {
                Name = line,
                Type = line
            };
        }

        private Item GetNormalItem(ItemProperties itemProperties, string[] lines)
        {
            if (lines.Any(c => c.StartsWith(languageProvider.Language.DescriptionItemLevel))) // Equippable Item
            {
                var item = new EquippableItem()
                {
                    Type = lines[1].Replace(languageProvider.Language.PrefixSuperior, string.Empty).Trim(),
                    Name = lines[1].Replace(languageProvider.Language.PrefixSuperior, string.Empty).Trim(),
                    ItemLevel = GetNumberFromString(lines.Where(c => c.StartsWith(languageProvider.Language.DescriptionItemLevel)).FirstOrDefault()),
                };

                if (itemProperties.HasNote)
                {
                    item.Influence = GetInfluenceType(lines[lines.Length - 3]);
                }
                else
                {
                    item.Influence = GetInfluenceType(lines.LastOrDefault());
                }

                var links = GetLinkCount(lines.Where(c => c.StartsWith(languageProvider.Language.DescriptionSockets)).FirstOrDefault());

                if (links >= 5)
                {
                    item.Links = new SocketFilterOption()
                    {
                        Min = links,
                        Max = links,
                    };
                }

                return item;
            }
            else if (lines.Any(c => c.Contains(languageProvider.Language.KeywordProphecy))) // Prophecy
            {
                return new ProphecyItem()
                {
                    Name = lines[1],
                    Type = lines[1],
                };
            }
            else // Fragment
            {
                return new FragmentItem()
                {
                    Name = lines[1],
                    Type = lines[1],
                };
            }
        }

        private Item GetRareItem(ItemProperties itemProperties, string[] lines)
        {
            Item item = new EquippableItem()
            {
                Name = lines[1],
                Type = itemProperties.IsIdentified ? lines[2] : lines[1],
                ItemLevel = GetNumberFromString(lines.Where(c => c.StartsWith(languageProvider.Language.DescriptionItemLevel)).FirstOrDefault()),
            };
            var links = GetLinkCount(lines.Where(c => c.StartsWith(languageProvider.Language.DescriptionSockets)).FirstOrDefault());

            if (links >= 5)
            {
                ((EquippableItem)item).Links = new SocketFilterOption()
                {
                    Min = links,
                    Max = links,
                };
            }

            if (itemProperties.HasNote)
            {
                ((EquippableItem)item).Influence = GetInfluenceType(lines[lines.Length - 3]);
            }
            else
            {
                ((EquippableItem)item).Influence = GetInfluenceType(lines.LastOrDefault());
            }

            return item;
        }

        private Item GetUniqueItem(ItemProperties itemProperties, string[] lines)
        {
            var item = new EquippableItem
            {
                Name = lines[1],
                Type = itemProperties.IsIdentified ? lines[2] : lines[1]
            };
            var links = GetLinkCount(lines.Where(c => c.StartsWith(languageProvider.Language.DescriptionSockets)).FirstOrDefault());

            if (links >= 5)
            {
                item.Links = new SocketFilterOption()
                {
                    Min = links,
                    Max = links,
                };
            }

            return item;
        }

        private Item GetMapItem(ItemProperties itemProperties, string[] lines, Rarity rarity)
        {
            var item = new MapItem()
            {
                ItemQuantity = GetNumberFromString(lines.Where(c => c.StartsWith(languageProvider.Language.DescriptionItemQuantity)).FirstOrDefault()),
                ItemRarity = GetNumberFromString(lines.Where(c => c.StartsWith(languageProvider.Language.DescriptionItemRarity)).FirstOrDefault()),
                MonsterPackSize = GetNumberFromString(lines.Where(c => c.StartsWith(languageProvider.Language.DescriptionMonsterPackSize)).FirstOrDefault()),
                MapTier = GetNumberFromString(lines.Where(c => c.StartsWith(languageProvider.Language.DescriptionMapTier)).FirstOrDefault()),
                Rarity = rarity,
            };

            if (rarity == Rarity.Normal)
            {
                item.Name = lines[1].Replace(languageProvider.Language.PrefixSuperior, string.Empty).Trim();
                item.Type = lines[1].Replace(languageProvider.Language.PrefixSuperior, string.Empty).Replace(languageProvider.Language.PrefixBlighted, string.Empty).Trim();
            }
            else if (rarity == Rarity.Magic)        // Extract only map name
            {
                item.Name = languageProvider.Language.PrefixBlighted + " " + mapService.MapNames.Where(c => lines[1].Contains(c)).FirstOrDefault();
                item.Type = mapService.MapNames.Where(c => lines[1].Contains(c)).FirstOrDefault();     // Search map name from statics
            }
            else if (rarity == Rarity.Rare)
            {
                item.Name = lines[2].Trim();
                item.Type = lines[2].Replace(languageProvider.Language.PrefixBlighted, string.Empty).Trim();
            }
            else if (rarity == Rarity.Unique)
            {
                if (!itemProperties.IsIdentified)
                {
                    item.Name = lines[1].Replace(languageProvider.Language.PrefixSuperior, string.Empty).Trim();
                }
                else
                {
                    item.Name = lines[1];
                }
            }

            item.IsBlight = itemProperties.IsBlighted.ToString();
            return item;
        }

        private Rarity GetRarity(string rarityString)
        {
            var rarity = Rarity.Unknown;
            if (rarityString == languageProvider.Language.RarityNormal)
            {
                rarity = Rarity.Normal;
            }
            else if (rarityString == languageProvider.Language.RarityMagic)
            {
                rarity = Rarity.Magic;
            }
            else if (rarityString == languageProvider.Language.RarityRare)
            {
                rarity = Rarity.Rare;
            }
            else if (rarityString == languageProvider.Language.RarityUnique)
            {
                rarity = Rarity.Unique;
            }
            else if (rarityString == languageProvider.Language.RarityCurrency)
            {
                rarity = Rarity.Currency;
            }
            else if (rarityString == languageProvider.Language.RarityGem)
            {
                rarity = Rarity.Gem;
            }
            else if (rarityString == languageProvider.Language.RarityDivinationCard)
            {
                rarity = Rarity.DivinationCard;
            }

            return rarity;
        }

        private ItemProperties GetItemProperties(string[] lines)
        {
            var properties = new ItemProperties
            {
                IsIdentified = true
            };

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line == languageProvider.Language.DescriptionUnidentified)
                {
                    properties.IsIdentified = false;
                }
                else if (line.Contains(languageProvider.Language.DescriptionQuality))
                {
                    properties.HasQuality = true;
                }
                else if (line == languageProvider.Language.DescriptionCorrupted)
                {
                    properties.IsCorrupted = true;
                }
                else if (line.Contains(languageProvider.Language.DescriptionMapTier))
                {
                    properties.IsMap = true;
                }
                else if (line.Contains(languageProvider.Language.PrefixBlighted))
                {
                    properties.IsBlighted = true;
                }
                else if (line.Contains(languageProvider.Language.DescriptionOrgan))
                {
                    properties.IsOrgan = true;
                }
                else if (i == lines.Length - 1 && line.Contains("Note"))
                {
                    properties.HasNote = true;
                }
            }

            return properties;
        }

        private string ParseName(string name)
        {
            var langs = new List<string>();
            var tokens = itemNameTokenizer.Tokenize(name);
            var output = "";

            foreach (var token in tokens.Select(x => x as ItemNameToken))
            {
                if (token.TokenType == ItemNameTokenType.Set)
                {
                    langs.Add(token.Match.Match.Groups["LANG"].Value);
                }
                else if (token.TokenType == ItemNameTokenType.Name)
                {
                    output += token.Match.Match.Value;
                }
                else if (token.TokenType == ItemNameTokenType.If)
                {
                    var lang = token.Match.Match.Groups["LANG"].Value;
                    if (langs.Contains(lang))
                        output += token.Match.Match.Groups["NAME"].Value;
                }
            }

            return output;
        }

        private string GetNumberFromString(string input)
        {
            if (string.IsNullOrEmpty(input))     // Default return 0
            {
                return "0";
            }

            return new string(input.Where(c => char.IsDigit(c)).ToArray());
        }

        private int ParseGemExperiencePercent(string input)
        {
            // trim leading prefix if any
            if (input.Contains(languageProvider.Language.DescriptionExperience))
                input = input.Replace(languageProvider.Language.DescriptionExperience, "");
            var split = input.Split('/');

            int percent;
            if (split.Length == 2)
            {
                int.TryParse(split[0], out var current);
                int.TryParse(split[1], out var max);

                percent = (int)(((float)current / max) * 100.0f);
                percent = (percent < 100) ? percent : 100;
            }
            else
            {
                throw new Exception("unable to parse gem experience from input string: " + input);
            }

            return percent;
        }

        private int GetLinkCount(string input)
        {
            if (input == null || !input.StartsWith(languageProvider.Language.DescriptionSockets))
            {
                return 0;
            }

            var values = new List<int>();

            if (!string.IsNullOrEmpty(input))
            {
                foreach (var fragment in input.Split(' '))
                {
                    values.Add(fragment.Count(c => c == '-') == 0 ? 0 : fragment.Count(c => c == '-') + 1);
                }

                return values.Max();
            }
            else
            {
                return 0;
            }
        }

        private InfluenceType GetInfluenceType(string input)
        {
            if (input.Contains(languageProvider.Language.InfluenceShaper))
            {
                return InfluenceType.Shaper;
            }
            else if (input.Contains(languageProvider.Language.InfluenceElder))
            {
                return InfluenceType.Elder;
            }
            else if (input.Contains(languageProvider.Language.InfluenceCrusader))
            {
                return InfluenceType.Crusader;
            }
            else if (input.Contains(languageProvider.Language.InfluenceHunter))
            {
                return InfluenceType.Hunter;
            }
            else if (input.Contains(languageProvider.Language.InfluenceRedeemer))
            {
                return InfluenceType.Redeemer;
            }
            else if (input.Contains(languageProvider.Language.InfluenceWarlord))
            {
                return InfluenceType.Warlord;
            }

            return InfluenceType.None;
        }
    }
}
