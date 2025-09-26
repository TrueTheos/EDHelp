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

public class MoxfieldService : IDisposable, IMoxfieldService
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
        _options.AddArgument("--disable-blink-features=AutomationControlled");
        _options.AddExcludedArgument("enable-automation");
        _options.AddAdditionalOption("useAutomationExtension", false);
        
        _options.AddArgument("--no-sandbox");
        _options.AddArgument("--disable-web-security");
        _options.AddArgument("--disable-features=VizDisplayCompositor");
        _options.AddArgument("--disable-extensions");
        _options.AddArgument("--disable-plugins");
        _options.AddArgument("--disable-images");
        _options.AddArgument("--disable-gpu");
        _options.AddArgument("--disable-dev-shm-usage");
        _options.AddArgument("--disable-software-rasterizer");
        
        _options.AddArgument("--headless=new");
        _options.AddArgument("--window-size=1920,1080");
        
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
        
        _options.AddUserProfilePreference("profile.default_content_setting_values.notifications", 2);
        _options.AddUserProfilePreference("profile.default_content_settings.popups", 0);
        _options.AddUserProfilePreference("profile.managed_default_content_settings.images", 2);
        
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
        
        _options.AddUserProfilePreference("profile.default_content_setting_values.media_stream", 2);
        
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
            return;
        }

        try
        {
            var service = ChromeDriverService.CreateDefaultService();
            service.HideCommandPromptWindow = true;
            service.SuppressInitialDiagnosticInformation = true;
            
            _driver = new ChromeDriver(service, _options);
            
            _driver.Manage().Timeouts().PageLoad = TimeSpan.FromSeconds(30);
            _driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(10);
            
            await HideAutomationSignals();
            
            _driver.Navigate().GoToUrl("https://www.moxfield.com/");
            
            await WaitForCloudflareChallenge();
            
            _isInitialized = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error initializing ChromeDriver: {ex.Message}");
            throw;
        }
    }

    private async Task HideAutomationSignals()
    {
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
                Console.WriteLine($"Script failed: {script}");
            }
        }
    }

    private async Task WaitForCloudflareChallenge()
    {
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
                    return;
                }
                
                if (hasChallenge)
                {
                    await Task.Delay(1000);
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
    }

    public async Task<List<List<string>>> ExportTopDecksForCommander(string cardName, int topCount = 5)
    {
        try
        {
            await InitializeDriver();
        
            var cardId = await SearchForCard(cardName);
            if (string.IsNullOrEmpty(cardId))
            {
                Console.WriteLine($"Card ID not found for '{cardName}'. Aborting export.");
                return new List<List<string>>();
            }
        
            var deckResults = await SearchForDecks(cardId, cardName);
            if (deckResults.Count == 0)
            {
                Console.WriteLine("No decks found for this commander.");
                return new List<List<string>>();
            }
        
            var topDecks = deckResults.Take(topCount).ToList();
            var exportedDecks = new List<List<string>>();
        
            foreach (var deck in topDecks)
            {
                var deckCardNames = await ExportDeckCardNames(deck);
                if (deckCardNames != null && deckCardNames.Count > 0)
                {
                    exportedDecks.Add(deckCardNames);
                }
            
                await Task.Delay(new Random().Next(1000, 2000));
            }
        
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
        
            string exportUrl = $"https://api2.moxfield.com/v3/decks/all/{deckId}";
        
            _driver.Navigate().GoToUrl(exportUrl);
        
            var jsonContent = await ExtractJsonFromPage();
        
            if (string.IsNullOrEmpty(jsonContent))
            {
                return null;
            }
        
            var cardNames = ExtractCardNamesFromDeckJson(jsonContent);
        
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
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing deck JSON: {ex.Message}");
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
                    return deckId;
                }
            }
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting Deck ID: {ex.Message}");
            return null;
        }
    }

    private async Task<string> SearchForCard(string cardName)
    {
        try
        {
            await Task.Delay(new Random().Next(1000, 2000));
            
            string encodedCardName = HttpUtility.UrlEncode(cardName);
            string cardSearchUrl = $"https://api2.moxfield.com/v2/cards/search?q={encodedCardName}&page=1";
            
            _driver.Navigate().GoToUrl(cardSearchUrl);
            
            var jsonContent = await ExtractJsonFromPage();
            
            if (string.IsNullOrEmpty(jsonContent))
            {
                return null;
            }
            
            var cardSearchResult = JsonConvert.DeserializeObject<MoxfieldCardSearchResponse>(jsonContent);
            
            if (cardSearchResult?.data == null || cardSearchResult.data.Count == 0)
            {
                return null;
            }
            
            var foundCard = cardSearchResult.data[0];
            
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
            await Task.Delay(new Random().Next(1500, 2500));
            
            string deckSearchUrl = $"https://api2.moxfield.com/v2/decks/search?pageNumber=1&pageSize=64&sortType=views&sortDirection=descending&fmt=commander&commanderCardId={cardId}";
            
            _driver.Navigate().GoToUrl(deckSearchUrl);
            
            var jsonContent = await ExtractJsonFromPage();
            
            if (string.IsNullOrEmpty(jsonContent))
            {
                return new List<MoxfieldDeckSearchResult>();
            }
            
            var deckSearchResult = JsonConvert.DeserializeObject<MoxfieldDeckSearchResponse>(jsonContent);
            
            var results = new List<MoxfieldDeckSearchResult>();
            
            if (deckSearchResult?.data != null)
            {
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
                        return jsonString;
                    }
                }
            }
            catch { }
            
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in ExtractJsonFromPage: {ex.Message}");
            return null;
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
            _driver?.Quit();
            _driver?.Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during ChromeDriver disposal: {ex.Message}");
        }
    }
}