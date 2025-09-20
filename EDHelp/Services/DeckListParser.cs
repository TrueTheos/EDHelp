using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EDHelp.Models;

namespace EDHelp.Services;

public class DecklistParser
    {
        private static readonly Regex DecklistLineRegex = new(@"^(\d+)x?\s+(.+)$", RegexOptions.Compiled);
        private static readonly Regex CommanderRegex = new(@"^(?:1x?\s+)?(.+?)(?:\s*//.*)?$", RegexOptions.Compiled);

        public async Task<Deck> ParseDecklistAsync(string filePath)
        {
            var lines = await File.ReadAllLinesAsync(filePath);
            var deck = new Deck
            {
                Name = Path.GetFileNameWithoutExtension(filePath)
            };

            bool inCommanderSection = false;
            bool inMainSection = false;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                
                if (string.IsNullOrEmpty(trimmedLine) || trimmedLine.StartsWith("//"))
                    continue;

                // Check for section headers
                if (trimmedLine.ToLower().Contains("commander"))
                {
                    inCommanderSection = true;
                    inMainSection = false;
                    continue;
                }
                else if (trimmedLine.ToLower().Contains("deck") || trimmedLine.ToLower().Contains("main"))
                {
                    inCommanderSection = false;
                    inMainSection = true;
                    continue;
                }

                // Parse commander
                if (inCommanderSection)
                {
                    var commanderMatch = CommanderRegex.Match(trimmedLine);
                    if (commanderMatch.Success)
                    {
                        var cardName = commanderMatch.Groups[1].Value.Trim();
                        // TODO: Fetch card from database/API
                        deck.commander = new Card { name = cardName, isCommander = true };
                    }
                }
                // Parse main deck cards
                else if (inMainSection || (!inCommanderSection && !inMainSection))
                {
                    var match = DecklistLineRegex.Match(trimmedLine);
                    if (match.Success)
                    {
                        var quantity = int.Parse(match.Groups[1].Value);
                        var cardName = match.Groups[2].Value.Trim();
                        
                        // TODO: Fetch card from database/API
                        var card = new Card { name = cardName };
                        deck.cards.Add(new DeckCard { card = card, quantity = quantity });
                    }
                }
            }

            return deck;
        }
    }