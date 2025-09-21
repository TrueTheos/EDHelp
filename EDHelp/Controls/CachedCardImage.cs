using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using EDHelp.Models;
using EDHelp.Services;

namespace EDHelp.Controls;

public class CachedCardImage : Image
{
    public static readonly StyledProperty<Card?> CardProperty =
        AvaloniaProperty.Register<CachedCardImage, Card?>(nameof(Card));

    public Card? Card
    {
        get => GetValue(CardProperty);
        set => SetValue(CardProperty, value);
    }

    private CardCacheService? _cardCacheService;

    public CachedCardImage()
    {
        PropertyChanged += async (sender, e) =>
        {
            if (e.Property == CardProperty)
            {
                await LoadCardImageAsync();
            }
        };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        
        _cardCacheService ??= new CardCacheService();
        
        _ = LoadCardImageAsync();
    }
    
    private async Task LoadCardImageAsync()
    {
        if (Card == null || _cardCacheService == null)
        {
            Source = null;
            return;
        }

        try
        {
            ShowPlaceholder();

            var cachedCard = await _cardCacheService.GetCardFromMemoryCache(Card);
            if (cachedCard?.imageData != null)
            {
                using var stream = new MemoryStream(cachedCard.imageData);
                var bitmap = new Bitmap(stream);
                Source = bitmap;
            }
            else
            {
                var newlyCachedCard = await _cardCacheService.GetCard(Card);
                if (newlyCachedCard?.imageData != null)
                {
                    using var stream = new MemoryStream(newlyCachedCard.imageData);
                    var bitmap = new Bitmap(stream);
                    Source = bitmap;
                }
                else
                {
                    ShowPlaceholder();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading card image for {Card.name}: {ex.Message}");
            ShowPlaceholder();
        }
    }

    private void ShowPlaceholder()
    {
        Source = CreatePlaceholderBitmap();
    }

    private Bitmap CreatePlaceholderBitmap()
    {
        const int width = 200;
        const int height = 280;
        
        var bitmap = new WriteableBitmap(new PixelSize(width, height), new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888);
        
        using var buffer = bitmap.Lock();
        unsafe
        {
            var ptr = (uint*)buffer.Address;
            var color = 0xFF3C3C3C;
            
            for (int i = 0; i < width * height; i++)
            {
                ptr[i] = color;
            }
        }
        
        return bitmap;
    }
}