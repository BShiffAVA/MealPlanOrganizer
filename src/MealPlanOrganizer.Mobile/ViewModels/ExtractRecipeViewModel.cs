using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MealPlanOrganizer.Mobile.Models;
using MealPlanOrganizer.Mobile.Services;
using Microsoft.Extensions.Logging;

namespace MealPlanOrganizer.Mobile.ViewModels;

/// <summary>
/// ViewModel for the Extract Recipe page (AI-powered recipe import).
/// </summary>
public partial class ExtractRecipeViewModel : ObservableObject
{
    private readonly IRecipeService _recipeService;
    private readonly ILogger<ExtractRecipeViewModel> _logger;

    public enum InputMode
    {
        Image,
        Url,
        Text
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsImageMode))]
    [NotifyPropertyChangedFor(nameof(IsUrlMode))]
    [NotifyPropertyChangedFor(nameof(IsTextMode))]
    [NotifyPropertyChangedFor(nameof(ImageModeBackgroundColor))]
    [NotifyPropertyChangedFor(nameof(UrlModeBackgroundColor))]
    [NotifyPropertyChangedFor(nameof(TextModeBackgroundColor))]
    [NotifyPropertyChangedFor(nameof(ImageModeTextColor))]
    [NotifyPropertyChangedFor(nameof(UrlModeTextColor))]
    [NotifyPropertyChangedFor(nameof(TextModeTextColor))]
    [NotifyPropertyChangedFor(nameof(CanExtract))]
    private InputMode _currentMode = InputMode.Image;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedImage))]
    [NotifyPropertyChangedFor(nameof(CanExtract))]
    private byte[]? _selectedImageBytes;

    [ObservableProperty]
    private string? _selectedImageContentType;

    [ObservableProperty]
    private ImageSource? _selectedImagePreview;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanExtract))]
    private string? _urlText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CharacterCountText))]
    [NotifyPropertyChangedFor(nameof(CharacterCountColor))]
    [NotifyPropertyChangedFor(nameof(CanExtract))]
    private string? _pastedText;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _hasError;

    [ObservableProperty]
    private string? _errorMessage;

    public bool IsImageMode => CurrentMode == InputMode.Image;
    public bool IsUrlMode => CurrentMode == InputMode.Url;
    public bool IsTextMode => CurrentMode == InputMode.Text;

    public bool HasSelectedImage => SelectedImageBytes != null && SelectedImageBytes.Length > 0;

    public string ImageModeBackgroundColor => CurrentMode == InputMode.Image ? "#4CAF50" : "#E0E0E0";
    public string UrlModeBackgroundColor => CurrentMode == InputMode.Url ? "#4CAF50" : "#E0E0E0";
    public string TextModeBackgroundColor => CurrentMode == InputMode.Text ? "#4CAF50" : "#E0E0E0";
    public string ImageModeTextColor => CurrentMode == InputMode.Image ? "White" : "#333333";
    public string UrlModeTextColor => CurrentMode == InputMode.Url ? "White" : "#333333";
    public string TextModeTextColor => CurrentMode == InputMode.Text ? "White" : "#333333";

    public string CharacterCountText
    {
        get
        {
            var length = PastedText?.Length ?? 0;
            return $"{length} characters (minimum 20)";
        }
    }

    public string CharacterCountColor => (PastedText?.Length ?? 0) >= 20 ? "#4CAF50" : "#999999";

    public bool CanExtract => CurrentMode switch
    {
        InputMode.Image => SelectedImageBytes != null && SelectedImageBytes.Length > 0,
        InputMode.Url => !string.IsNullOrWhiteSpace(UrlText) && IsValidUrl(UrlText),
        InputMode.Text => !string.IsNullOrWhiteSpace(PastedText) && PastedText.Length >= 20,
        _ => false
    };

    public string ExtractButtonBackgroundColor => CanExtract ? "#4CAF50" : "#CCCCCC";

    public ExtractRecipeViewModel(
        IRecipeService recipeService,
        ILogger<ExtractRecipeViewModel> logger)
    {
        _recipeService = recipeService;
        _logger = logger;
    }

    partial void OnCurrentModeChanged(InputMode value)
    {
        HasError = false;
        OnPropertyChanged(nameof(ExtractButtonBackgroundColor));
    }

    partial void OnSelectedImageBytesChanged(byte[]? value)
    {
        OnPropertyChanged(nameof(ExtractButtonBackgroundColor));
    }

    partial void OnUrlTextChanged(string? value)
    {
        HasError = false;
        OnPropertyChanged(nameof(ExtractButtonBackgroundColor));
    }

    partial void OnPastedTextChanged(string? value)
    {
        HasError = false;
        OnPropertyChanged(nameof(ExtractButtonBackgroundColor));
    }

    [RelayCommand]
    private void SelectImageMode()
    {
        CurrentMode = InputMode.Image;
    }

    [RelayCommand]
    private void SelectUrlMode()
    {
        CurrentMode = InputMode.Url;
    }

    [RelayCommand]
    private void SelectTextMode()
    {
        CurrentMode = InputMode.Text;
    }

    [RelayCommand]
    private async Task TakePhotoAsync()
    {
        try
        {
            HasError = false;

            if (!MediaPicker.Default.IsCaptureSupported)
            {
                ShowError("Camera is not supported on this device");
                return;
            }

            var photo = await MediaPicker.Default.CapturePhotoAsync();
            if (photo != null)
            {
                await ProcessSelectedImageAsync(photo);
            }
        }
        catch (FeatureNotSupportedException)
        {
            ShowError("Camera is not supported on this device");
        }
        catch (PermissionException)
        {
            ShowError("Camera permission was not granted. Please enable it in Settings.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to take photo");
            ShowError($"Failed to take photo: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task PickPhotoAsync()
    {
        try
        {
            HasError = false;

            var photos = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions
            {
                Title = "Select a recipe image"
            });

            var photo = photos?.FirstOrDefault();
            if (photo != null)
            {
                await ProcessSelectedImageAsync(photo);
            }
        }
        catch (FeatureNotSupportedException)
        {
            ShowError("Photo selection is not supported on this device");
        }
        catch (PermissionException)
        {
            ShowError("Photo library permission was not granted. Please enable it in Settings.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to select photo");
            ShowError($"Failed to select photo: {ex.Message}");
        }
    }

    private async Task ProcessSelectedImageAsync(FileResult photo)
    {
        try
        {
            using var stream = await photo.OpenReadAsync();
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            var imageBytes = memoryStream.ToArray();

            // TODO: Compress if needed (max 4MB for API)
            const int maxSizeBytes = 4 * 1024 * 1024;
            if (imageBytes.Length > maxSizeBytes)
            {
                // In production, use SkiaSharp for compression
                _logger.LogWarning("Image is larger than 4MB: {Size} bytes", imageBytes.Length);
            }

            SelectedImageBytes = imageBytes;
            SelectedImageContentType = GetContentType(photo.ContentType ?? photo.FileName);
            SelectedImagePreview = ImageSource.FromStream(() => new MemoryStream(SelectedImageBytes));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process image");
            ShowError($"Failed to process image: {ex.Message}");
        }
    }

    private static string GetContentType(string input)
    {
        var lower = input.ToLowerInvariant();
        if (lower.Contains("png")) return "image/png";
        if (lower.Contains("gif")) return "image/gif";
        if (lower.Contains("webp")) return "image/webp";
        return "image/jpeg";
    }

    [RelayCommand]
    private void ClearImage()
    {
        SelectedImageBytes = null;
        SelectedImageContentType = null;
        SelectedImagePreview = null;
    }

    [RelayCommand]
    private async Task ExtractAsync()
    {
        if (!CanExtract) return;

        try
        {
            IsLoading = true;
            HasError = false;

            var request = BuildExtractionRequest();
            if (request == null)
            {
                ShowError("Invalid input. Please check your selection.");
                return;
            }

            _logger.LogInformation("Extracting recipe from {InputType}", request.InputType);
            var response = await _recipeService.ExtractRecipeAsync(request);

            if (response == null)
            {
                ShowError("Failed to connect to the server. Please try again.");
                return;
            }

            if (!response.Success || response.ExtractedRecipe == null)
            {
                ShowError(response.ErrorMessage ?? "Failed to extract recipe. Please try a different input.");
                return;
            }

            _logger.LogInformation("Recipe extracted successfully: {Name}", response.ExtractedRecipe.Name);

            // Navigate to preview page with extracted recipe
            await Shell.Current.GoToAsync(nameof(ExtractedRecipePreviewPage), new Dictionary<string, object>
            {
                { "ExtractedRecipe", response.ExtractedRecipe },
                { "Confidence", response.Confidence }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract recipe");
            ShowError($"An error occurred: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    private RecipeExtractionRequest? BuildExtractionRequest()
    {
        return CurrentMode switch
        {
            InputMode.Image when SelectedImageBytes != null => new RecipeExtractionRequest
            {
                InputType = "image",
                ImageBase64 = Convert.ToBase64String(SelectedImageBytes),
                ImageContentType = SelectedImageContentType ?? "image/jpeg"
            },
            InputMode.Url when !string.IsNullOrWhiteSpace(UrlText) => new RecipeExtractionRequest
            {
                InputType = "url",
                Url = UrlText.Trim()
            },
            InputMode.Text when !string.IsNullOrWhiteSpace(PastedText) => new RecipeExtractionRequest
            {
                InputType = "text",
                Text = PastedText.Trim()
            },
            _ => null
        };
    }

    private static bool IsValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
            && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }

    private void ShowError(string message)
    {
        ErrorMessage = message;
        HasError = true;
    }
}
