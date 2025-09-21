using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;
using EDHelp.Models;
using Newtonsoft.Json;
using System.Threading;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using System.Linq;
using System.IO;

namespace EDHelp.Services;

public class MoxfieldService : IDisposable
{
    private ChromeDriver _driver;
    private readonly ChromeOptions _options;
    private bool _isInitialized = false;

    public MoxfieldService()
    {
        _options = new ChromeOptions();
        Console.WriteLine("MoxfieldService initialized.");
        SetupChromeOptions();
    }

    private void SetupChromeOptions()
    {
        Console.WriteLine("Setting up Chrome options...");
        // Essential anti-detection options
        _options.AddArgument("--disable-blink-features=AutomationControlled");
        _options.AddExcludedArgument("enable-automation");
        _options.AddAdditionalOption("useAutomationExtension", false);
        
        // Performance and stealth options
        _options.AddArgument("--no-sandbox");
        _options.AddArgument("--disable-web-security");
        _options.AddArgument("--disable-features=VizDisplayCompositor");
        _options.AddArgument("--disable-extensions");
        _options.AddArgument("--disable-plugins");
        _options.AddArgument("--disable-images");
        _options.AddArgument("--disable-gpu");
        _options.AddArgument("--disable-dev-shm-usage");
        _options.AddArgument("--disable-software-rasterizer");
        
        // Window size and headless
        _options.AddArgument("--headless=new");
        _options.AddArgument("--window-size=1920,1080");
        
        // Random user agent
        var userAgents = new[]
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
        };
        var randomUserAgent = userAgents[new Random().Next(userAgents.Length)];
        _options.AddArgument($"--user-agent={randomUserAgent}");
        Console.WriteLine($"  -> User-Agent: {randomUserAgent}");
        
        // Additional prefs to avoid detection
        _options.AddUserProfilePreference("profile.default_content_setting_values.notifications", 2);
        _options.AddUserProfilePreference("profile.default_content_settings.popups", 0);
        _options.AddUserProfilePreference("profile.managed_default_content_settings.images", 2);
        
        // Suppress all console output and logging
        _options.AddArgument("--log-level=3");
        _options.AddArgument("--silent");
        _options.AddArgument("--disable-logging");
        _options.AddArgument("--disable-gpu-logging");
        _options.AddArgument("--disable-extensions-http-throttling");
        _options.AddArgument("--disable-extensions-except");
        _options.AddArgument("--disable-background-timer-throttling");
        _options.AddArgument("--disable-renderer-backgrounding");
        _options.AddArgument("--disable-backgrounding-occluded-windows");
        _options.AddArgument("--disable-component-extensions-with-background-pages");
        
        // Suppress Chrome logging to console
        _options.AddUserProfilePreference("profile.default_content_setting_values.media_stream", 2);
        
        // Set log level to suppress warnings
        _options.SetLoggingPreference(LogType.Browser, LogLevel.Off);
        _options.SetLoggingPreference(LogType.Client, LogLevel.Off);
        _options.SetLoggingPreference(LogType.Driver, LogLevel.Off);
        _options.SetLoggingPreference(LogType.Performance, LogLevel.Off);
        _options.SetLoggingPreference(LogType.Profiler, LogLevel.Off);
        _options.SetLoggingPreference(LogType.Server, LogLevel.Off);
    }

    private async Task InitializeDriver()
    {
        if (_isInitialized)
        {
            Console.WriteLine("Driver already initialized. Skipping.");
            return;
        }

        try
        {
            Console.WriteLine("Initializing ChromeDriver...");
            // Suppress ChromeDriver service console output
            var service = ChromeDriverService.CreateDefaultService();
            service.HideCommandPromptWindow = true;
            service.SuppressInitialDiagnosticInformation = true;
            
            // Initialize the driver with service
            _driver = new ChromeDriver(service, _options);
            
            // Set timeouts
            _driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(30);
            _driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
            
            // Execute scripts to hide automation
            await HideAutomationSignals();
            
            // Navigate to main site first to establish session and pass Cloudflare
            Console.WriteLine("Navigating to Moxfield home page to pass Cloudflare...");
            _driver.Navigate().GoToUrl("https://www.moxfield.com/");
            
            // Wait for Cloudflare challenge to complete
            await WaitForCloudflareChallenge();
            
            _isInitialized = true;
            Console.WriteLine("ChromeDriver initialized successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing ChromeDriver: {ex.Message}");
            throw;
        }
    }

    private async Task HideAutomationSignals()
    {
        Console.WriteLine("Hiding automation signals...");
        // Execute scripts to hide webdriver properties
        var scripts = new[]
        {
            "Object.defineProperty(navigator, 'webdriver', {get: () => undefined})",
            "Object.defineProperty(navigator, 'plugins', {get: () => [1, 2, 3, 4, 5]})",
            "Object.defineProperty(navigator, 'languages', {get: () => ['en-US', 'en']})",
            "window.chrome = { runtime: {} }",
            "Object.defineProperty(navigator, 'permissions', {get: () => ({query: () => Promise.resolve({state: 'granted'})})})"
        };

        foreach (var script in scripts)
        {
            try
            {
                _driver.ExecuteScript(script);
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  -> Script failed: {script}");
            }
        }
        Console.WriteLine("Finished hiding automation signals.");
    }

    private async Task WaitForCloudflareChallenge()
    {
        Console.WriteLine("Waiting for Cloudflare challenge to pass...");
        var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(30));
        var maxRetries = 5;
        var retryCount = 0;

        while (retryCount < maxRetries)
        {
            try
            {
                await Task.Delay(2000);
                
                var currentUrl = _driver.Url;
                var pageTitle = _driver.Title;
                
                var challengeIndicators = new[]
                {
                    "Just a moment",
                    "Checking your browser",
                    "DDoS protection",
                    "cf-browser-verification"
                };
                
                var pageSource = _driver.PageSource;
                var hasChallenge = challengeIndicators.Any(indicator => 
                    pageTitle.Contains(indicator, StringComparison.OrdinalIgnoreCase) ||
                    pageSource.Contains(indicator, StringComparison.OrdinalIgnoreCase));
                
                if (!hasChallenge && (currentUrl.Contains("moxfield.com") || pageTitle.Contains("Moxfield")))
                {
                    Console.WriteLine("Cloudflare challenge passed successfully!");
                    return;
                }
                
                if (hasChallenge)
                {
                    Console.WriteLine($"Cloudflare challenge detected. Retrying in 5s. Retry count: {retryCount + 1}");
                    await Task.Delay(5000);
                }
                
                retryCount++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during Cloudflare wait: {ex.Message}. Retrying...");
                retryCount++;
                await Task.Delay(3000);
            }
        }
        
        Console.WriteLine("Cloudflare challenge check timed out or failed. Proceeding anyway.");
    }

    public async Task<List<MoxfieldDeckSearchResult>> SearchDecksForCommander(string cardName)
    {
        Console.WriteLine($"Starting search for decks with commander: {cardName}");
        try
        {
            await InitializeDriver();
            
            // Step 1: Search for the card
            var cardId = await SearchForCard(cardName);
            if (string.IsNullOrEmpty(cardId))
            {
                Console.WriteLine($"Card ID not found for '{cardName}'. Aborting search.");
                return new List<MoxfieldDeckSearchResult>();
            }
            
            // Step 2: Search for decks using this commander
            return await SearchForDecks(cardId, cardName);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SearchDecksForCommander: {ex.Message}");
            return new List<MoxfieldDeckSearchResult>();
        }
    }

    public async Task<List<List<string>>> ExportTopDecksForCommander(string cardName, int topCount = 5)
    {
        Console.WriteLine($"Starting export for top {topCount} decks with commander: {cardName}");
        try
        {
            await InitializeDriver();
        
            // Step 1: Search for the card
            var cardId = await SearchForCard(cardName);
            if (string.IsNullOrEmpty(cardId))
            {
                Console.WriteLine($"Card ID not found for '{cardName}'. Aborting export.");
                return new List<List<string>>();
            }
        
            // Step 2: Get deck search results
            var deckResults = await SearchForDecks(cardId, cardName);
            if (deckResults.Count == 0)
            {
                Console.WriteLine("No decks found for this commander.");
                return new List<List<string>>();
            }
        
            // Step 3: Export top decks and extract card names
            var topDecks = deckResults.Take(topCount).ToList();
            var exportedDecks = new List<List<string>>();
        
            Console.WriteLine($"Found {deckResults.Count} decks. Attempting to export the top {topDecks.Count}...");
            foreach (var deck in topDecks)
            {
                Console.WriteLine($"  -> Exporting deck '{deck.name}' (ID: {ExtractDeckIdFromUrl(deck.link)})");
                var deckCardNames = await ExportDeckCardNames(deck);
                if (deckCardNames != null && deckCardNames.Count > 0)
                {
                    exportedDecks.Add(deckCardNames);
                    Console.WriteLine($"  -> Export successful for '{deck.name}' - {deckCardNames.Count} cards extracted.");
                }
            
                // Add delay between exports to avoid rate limiting
                await Task.Delay(new Random().Next(1000, 2000));
            }
        
            Console.WriteLine($"Finished exporting {exportedDecks.Count} decks.");
            return exportedDecks;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ExportTopDecksForCommander: {ex.Message}");
            return new List<List<string>>();
        }
    }
    
    private async Task<List<string>> ExportDeckCardNames(MoxfieldDeckSearchResult deck)
    {
        try
        {
            var deckId = ExtractDeckIdFromUrl(deck.link);
            if (string.IsNullOrEmpty(deckId))
            {
                Console.WriteLine($"Could not extract Deck ID from URL: {deck.link}");
                return null;
            }
        
            // Use the v3 API endpoint as shown in your example
            string exportUrl = $"https://api2.moxfield.com/v3/decks/all/{deckId}";
        
            Console.WriteLine($"  -> Navigating to export URL: {exportUrl}");
            await Task.Delay(new Random().Next(500, 1000));
        
            _driver.Navigate().GoToUrl(exportUrl);
        
            var jsonContent = await ExtractJsonFromPage();
        
            if (string.IsNullOrEmpty(jsonContent))
            {
                Console.WriteLine("  -> Export content was empty or could not be extracted.");
                return null;
            }
        
            // Parse the JSON and extract card names
            var cardNames = ExtractCardNamesFromDeckJson(jsonContent);
        
            Console.WriteLine($"  -> Extracted {cardNames.Count} card names from deck.");
            return cardNames;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  -> Error exporting deck '{deck.name}': {ex.Message}");
            return null;
        }
    }
    
    private List<string> ExtractCardNamesFromDeckJson(string jsonContent)
    {
        var cardNames = new List<string>();
    
        try
        {
            dynamic deckData = JsonConvert.DeserializeObject(jsonContent);
        
            if (deckData?.boards?.mainboard?.cards != null)
            {
                var cards = deckData.boards.mainboard.cards;
            
                foreach (var cardEntry in cards)
                {
                    var cardInfo = cardEntry.Value;
                
                    if (cardInfo?.card?.name != null)
                    {
                        string cardName = cardInfo.card.name.ToString();
                    
                        int quantity = 1;
                        if (cardInfo.quantity != null)
                        {
                            int.TryParse(cardInfo.quantity.ToString(), out quantity);
                        }
                    
                        for (int i = 0; i < quantity; i++)
                        {
                            cardNames.Add(cardName);
                        }
                    }
                }
            }
        
            Console.WriteLine($"  -> Successfully extracted {cardNames.Count} card names from JSON.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  -> Error parsing deck JSON: {ex.Message}");
        }
    
        return cardNames;
    }

    private string ExtractDeckIdFromUrl(string publicUrl)
    {
        try
        {
            if (string.IsNullOrEmpty(publicUrl))
                return null;
            
            var uri = new Uri(publicUrl);
            var segments = uri.Segments;
            
            for (int i = 0; i < segments.Length - 1; i++)
            {
                if (segments[i].Trim('/').Equals("decks", StringComparison.OrdinalIgnoreCase))
                {
                    var deckId = segments[i + 1].Trim('/');
                    Console.WriteLine($"  -> Extracted Deck ID: {deckId} from URL: {publicUrl}");
                    return deckId;
                }
            }
            
            Console.WriteLine($"  -> Could not extract Deck ID from URL: {publicUrl}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  -> Error extracting Deck ID: {ex.Message}");
            return null;
        }
    }

    private async Task<string> SearchForCard(string cardName)
    {
        try
        {
            Console.WriteLine($"Searching for card ID for '{cardName}'...");
            await Task.Delay(new Random().Next(1000, 2000));
            
            string encodedCardName = HttpUtility.UrlEncode(cardName);
            string cardSearchUrl = $"https://api2.moxfield.com/v2/cards/search?q={encodedCardName}&page=1";
            
            Console.WriteLine($"  -> Navigating to card search URL: {cardSearchUrl}");
            _driver.Navigate().GoToUrl(cardSearchUrl);
            
            var jsonContent = await ExtractJsonFromPage();
            
            if (string.IsNullOrEmpty(jsonContent))
            {
                Console.WriteLine("  -> No JSON content found on card search page.");
                return null;
            }
            
            var cardSearchResult = JsonConvert.DeserializeObject<MoxfieldCardSearchResponse>(jsonContent);
            
            if (cardSearchResult?.data == null || cardSearchResult.data.Count == 0)
            {
                Console.WriteLine($"  -> Card search API returned no results for '{cardName}'.");
                return null;
            }
            
            var foundCard = cardSearchResult.data[0];
            
            Console.WriteLine($"  -> Found card '{foundCard.name}' with ID: {foundCard.id}");
            return foundCard.id;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SearchForCard: {ex.Message}");
            return null;
        }
    }

    private async Task<List<MoxfieldDeckSearchResult>> SearchForDecks(string cardId, string cardName)
    {
        try
        {
            Console.WriteLine($"Searching for decks with Card ID: {cardId} ({cardName})");
            await Task.Delay(new Random().Next(1500, 2500));
            
            string deckSearchUrl = $"https://api2.moxfield.com/v2/decks/search?pageNumber=1&pageSize=64&sortType=views&sortDirection=descending&fmt=commander&commanderCardId={cardId}";
            
            Console.WriteLine($"  -> Navigating to deck search URL: {deckSearchUrl}");
            _driver.Navigate().GoToUrl(deckSearchUrl);
            
            var jsonContent = await ExtractJsonFromPage();
            
            if (string.IsNullOrEmpty(jsonContent))
            {
                Console.WriteLine("  -> No JSON content found on deck search page.");
                return new List<MoxfieldDeckSearchResult>();
            }
            
            var deckSearchResult = JsonConvert.DeserializeObject<MoxfieldDeckSearchResponse>(jsonContent);
            
            var results = new List<MoxfieldDeckSearchResult>();
            
            if (deckSearchResult?.data != null)
            {
                Console.WriteLine($"  -> Found {deckSearchResult.data.Count} decks.");
                foreach (var deck in deckSearchResult.data)
                {
                    results.Add(new MoxfieldDeckSearchResult
                    {
                        name = deck.name,
                        views = deck.viewCount,
                        likes = deck.likeCount,
                        link = deck.publicUrl
                    });
                }
            }
            
            return results;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SearchForDecks: {ex.Message}");
            return new List<MoxfieldDeckSearchResult>();
        }
    }

    private async Task<string> ExtractJsonFromPage()
    {
        try
        {
            try
            {
                var preElement = _driver.FindElement(By.TagName("pre"));
                if (preElement != null && !string.IsNullOrEmpty(preElement.Text))
                {
                    Console.WriteLine("  -> JSON content extracted from <pre> tag.");
                    return preElement.Text;
                }
            }
            catch { }
            
            var pageSource = _driver.PageSource;
            if (pageSource.Contains("{") && pageSource.Contains("}"))
            {
                var startIndex = pageSource.IndexOf("{");
                var lastIndex = pageSource.LastIndexOf("}");
                
                if (startIndex >= 0 && lastIndex > startIndex)
                {
                    var potentialJson = pageSource.Substring(startIndex, lastIndex - startIndex + 1);
                    try
                    {
                        JsonConvert.DeserializeObject(potentialJson);
                        Console.WriteLine("  -> JSON content extracted from page source.");
                        return potentialJson;
                    }
                    catch { }
                }
            }
            
            try
            {
                var jsonData = _driver.ExecuteScript("return document.body.innerText || document.body.textContent;");
                if (jsonData != null)
                {
                    var jsonString = jsonData.ToString();
                    if (jsonString.Trim().StartsWith("{") && jsonString.Trim().EndsWith("}"))
                    {
                        Console.WriteLine("  -> JSON content extracted via JavaScript.");
                        return jsonString;
                    }
                }
            }
            catch { }
            
            Console.WriteLine("  -> Failed to extract JSON content.");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ExtractJsonFromPage: {ex.Message}");
            return null;
        }
    }

    public async Task<List<MoxfieldDeckSearchResult>> SearchDecksViaWebInterface(string cardName)
    {
        try
        {
            Console.WriteLine($"Starting web interface search for '{cardName}'.");
            await InitializeDriver();
            
            _driver.Navigate().GoToUrl("https://www.moxfield.com/decks/browse");
            Console.WriteLine("Navigated to Moxfield deck browse page.");
            
            var searchInput = _driver.FindElement(By.CssSelector("input[placeholder*='Search']"));
            searchInput.Clear();
            searchInput.SendKeys(cardName);
            
            searchInput.SendKeys(Keys.Enter);
            Console.WriteLine($"Searched for '{cardName}' via web interface.");
            
            var deckElements = _driver.FindElements(By.CssSelector(".deck-card, .deck-item, [data-testid='deck']"));
            Console.WriteLine($"Found {deckElements.Count} potential deck elements on the page.");
            
            var results = new List<MoxfieldDeckSearchResult>();
            
            foreach (var deckElement in deckElements.Take(20))
            {
                try
                {
                    var nameElement = deckElement.FindElement(By.CssSelector("h3, .deck-name, [data-testid='deck-name']"));
                    var linkElement = deckElement.FindElement(By.CssSelector("a"));
                    
                    var viewsText = "0";
                    var likesText = "0";
                    
                    try
                    {
                        var statsElements = deckElement.FindElements(By.CssSelector(".stat, .count, .views, .likes"));
                        foreach (var stat in statsElements)
                        {
                            var text = stat.Text.ToLower();
                            if (text.Contains("view")) viewsText = ExtractNumber(text);
                            if (text.Contains("like")) likesText = ExtractNumber(text);
                        }
                    }
                    catch { }
                    
                    var newResult = new MoxfieldDeckSearchResult
                    {
                        name = nameElement.Text,
                        views = int.TryParse(viewsText, out var views) ? views : 0,
                        likes = int.TryParse(likesText, out var likes) ? likes : 0,
                        link = linkElement.GetAttribute("href")
                    };
                    results.Add(newResult);
                    Console.WriteLine($"  -> Parsed deck: '{newResult.name}' (Views: {newResult.views}, Likes: {newResult.likes})");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("  -> Failed to parse a deck element.");
                }
            }
            
            Console.WriteLine($"Finished web interface search. Found {results.Count} decks.");
            return results;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SearchDecksViaWebInterface: {ex.Message}");
            return new List<MoxfieldDeckSearchResult>();
        }
    }

    private string ExtractNumber(string text)
    {
        var numbers = System.Text.RegularExpressions.Regex.Match(text, @"\d+");
        return numbers.Success ? numbers.Value : "0";
    }

    public void Dispose()
    {
        try
        {
            Console.WriteLine("Disposing ChromeDriver...");
            _driver?.Quit();
            _driver?.Dispose();
            Console.WriteLine("ChromeDriver disposed successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during ChromeDriver disposal: {ex.Message}");
        }
    }
}