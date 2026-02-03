using MealPlanOrganizer.Mobile.Models;
using MealPlanOrganizer.Mobile.Services;

namespace MealPlanOrganizer.Mobile;

public partial class ExtractRecipePage : ContentPage
{
    private enum InputMode
    {
        Image,
        Url,
        Text
    }

    private readonly IRecipeService _recipeService;
    private InputMode _currentMode = InputMode.Image;
    private byte[]? _selectedImageBytes;
    private string? _selectedImageContentType;

    public ExtractRecipePage(IRecipeService recipeService)
    {
        InitializeComponent();
        _recipeService = recipeService;
        UpdateModeVisuals();
    }

    private void OnImageModeClicked(object? sender, EventArgs e)
    {
        _currentMode = InputMode.Image;
        UpdateModeVisuals();
        UpdateExtractButtonState();
    }

    private void OnUrlModeClicked(object? sender, EventArgs e)
    {
        _currentMode = InputMode.Url;
        UpdateModeVisuals();
        UpdateExtractButtonState();
    }

    private void OnTextModeClicked(object? sender, EventArgs e)
    {
        _currentMode = InputMode.Text;
        UpdateModeVisuals();
        UpdateExtractButtonState();
    }

    private void UpdateModeVisuals()
    {
        // Update button styles
        ImageModeButton.BackgroundColor = _currentMode == InputMode.Image 
            ? Color.FromArgb("#4CAF50") 
            : Color.FromArgb("#E0E0E0");
        ImageModeButton.TextColor = _currentMode == InputMode.Image 
            ? Colors.White 
            : Color.FromArgb("#333333");

        UrlModeButton.BackgroundColor = _currentMode == InputMode.Url 
            ? Color.FromArgb("#4CAF50") 
            : Color.FromArgb("#E0E0E0");
        UrlModeButton.TextColor = _currentMode == InputMode.Url 
            ? Colors.White 
            : Color.FromArgb("#333333");

        TextModeButton.BackgroundColor = _currentMode == InputMode.Text 
            ? Color.FromArgb("#4CAF50") 
            : Color.FromArgb("#E0E0E0");
        TextModeButton.TextColor = _currentMode == InputMode.Text 
            ? Colors.White 
            : Color.FromArgb("#333333");

        // Update section visibility
        ImageInputSection.IsVisible = _currentMode == InputMode.Image;
        UrlInputSection.IsVisible = _currentMode == InputMode.Url;
        TextInputSection.IsVisible = _currentMode == InputMode.Text;

        // Hide error when switching modes
        ErrorFrame.IsVisible = false;
    }

    private async void OnTakePhotoClicked(object? sender, EventArgs e)
    {
        try
        {
            ErrorFrame.IsVisible = false;

            if (!MediaPicker.Default.IsCaptureSupported)
            {
                ShowError("Camera is not supported on this device");
                return;
            }

            var photo = await MediaPicker.Default.CapturePhotoAsync();
            if (photo != null)
            {
                await ProcessSelectedImage(photo);
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
            ShowError($"Failed to take photo: {ex.Message}");
        }
    }

    private async void OnPickImageClicked(object? sender, EventArgs e)
    {
        try
        {
            ErrorFrame.IsVisible = false;

            var photo = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
            {
                Title = "Select a recipe image"
            });

            if (photo != null)
            {
                await ProcessSelectedImage(photo);
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
            ShowError($"Failed to select photo: {ex.Message}");
        }
    }

    private async Task ProcessSelectedImage(FileResult photo)
    {
        try
        {
            using var stream = await photo.OpenReadAsync();
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            var imageBytes = memoryStream.ToArray();

            // Compress if needed (max 4MB for API)
            const int maxSizeBytes = 4 * 1024 * 1024;
            if (imageBytes.Length > maxSizeBytes)
            {
                imageBytes = await CompressImageAsync(imageBytes);
            }

            _selectedImageBytes = imageBytes;
            _selectedImageContentType = GetContentType(photo.ContentType ?? photo.FileName);

            // Display preview
            SelectedImagePreview.Source = ImageSource.FromStream(() => new MemoryStream(_selectedImageBytes));
            ImagePreviewFrame.IsVisible = true;

            UpdateExtractButtonState();
        }
        catch (Exception ex)
        {
            ShowError($"Failed to process image: {ex.Message}");
        }
    }

    private static string GetContentType(string input)
    {
        var lower = input.ToLowerInvariant();
        if (lower.Contains("png")) return "image/png";
        if (lower.Contains("gif")) return "image/gif";
        if (lower.Contains("webp")) return "image/webp";
        return "image/jpeg"; // Default to JPEG
    }

    private static Task<byte[]> CompressImageAsync(byte[] imageBytes)
    {
        // Simple compression by re-encoding at lower quality
        // In a production app, you might use a library like SkiaSharp for better control
        // For now, just return original - real compression would require platform-specific code or SkiaSharp
        return Task.FromResult(imageBytes);
    }

    private void OnClearImageClicked(object? sender, EventArgs e)
    {
        _selectedImageBytes = null;
        _selectedImageContentType = null;
        SelectedImagePreview.Source = null;
        ImagePreviewFrame.IsVisible = false;
        UpdateExtractButtonState();
    }

    private void OnUrlTextChanged(object? sender, TextChangedEventArgs e)
    {
        ErrorFrame.IsVisible = false;
        UpdateExtractButtonState();
    }

    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        ErrorFrame.IsVisible = false;
        var length = TextEditor.Text?.Length ?? 0;
        CharacterCountLabel.Text = $"{length} characters (minimum 20)";
        CharacterCountLabel.TextColor = length >= 20 
            ? Color.FromArgb("#4CAF50") 
            : Color.FromArgb("#999999");
        UpdateExtractButtonState();
    }

    private void UpdateExtractButtonState()
    {
        bool canExtract = _currentMode switch
        {
            InputMode.Image => _selectedImageBytes != null && _selectedImageBytes.Length > 0,
            InputMode.Url => !string.IsNullOrWhiteSpace(UrlEntry.Text) && IsValidUrl(UrlEntry.Text),
            InputMode.Text => !string.IsNullOrWhiteSpace(TextEditor.Text) && TextEditor.Text.Length >= 20,
            _ => false
        };

        ExtractButton.IsEnabled = canExtract;
        ExtractButton.BackgroundColor = canExtract 
            ? Color.FromArgb("#4CAF50") 
            : Color.FromArgb("#CCCCCC");
    }

    private static bool IsValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
            && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }

    private async void OnExtractClicked(object? sender, EventArgs e)
    {
        if (!ExtractButton.IsEnabled) return;

        try
        {
            SetLoadingState(true);
            ErrorFrame.IsVisible = false;

            var request = BuildExtractionRequest();
            if (request == null)
            {
                ShowError("Invalid input. Please check your selection.");
                return;
            }

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

            // Navigate to preview page with extracted recipe
            await Shell.Current.GoToAsync(nameof(ExtractedRecipePreviewPage), new Dictionary<string, object>
            {
                { "ExtractedRecipe", response.ExtractedRecipe },
                { "Confidence", response.Confidence }
            });
        }
        catch (Exception ex)
        {
            ShowError($"An error occurred: {ex.Message}");
        }
        finally
        {
            SetLoadingState(false);
        }
    }

    private RecipeExtractionRequest? BuildExtractionRequest()
    {
        return _currentMode switch
        {
            InputMode.Image when _selectedImageBytes != null => new RecipeExtractionRequest
            {
                InputType = "image",
                ImageBase64 = Convert.ToBase64String(_selectedImageBytes),
                ImageContentType = _selectedImageContentType ?? "image/jpeg"
            },
            InputMode.Url when !string.IsNullOrWhiteSpace(UrlEntry.Text) => new RecipeExtractionRequest
            {
                InputType = "url",
                Url = UrlEntry.Text.Trim()
            },
            InputMode.Text when !string.IsNullOrWhiteSpace(TextEditor.Text) => new RecipeExtractionRequest
            {
                InputType = "text",
                Text = TextEditor.Text.Trim()
            },
            _ => null
        };
    }

    private void SetLoadingState(bool isLoading)
    {
        ExtractButton.IsVisible = !isLoading;
        LoadingSection.IsVisible = isLoading;
        
        // Disable input while loading
        ImageModeButton.IsEnabled = !isLoading;
        UrlModeButton.IsEnabled = !isLoading;
        TextModeButton.IsEnabled = !isLoading;
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorFrame.IsVisible = true;
    }
}
