# MealPlanOrganizer Mobile App

.NET MAUI mobile application for iOS, Android, and Windows.

## Setup Instructions

### 1. Clone the Repository
```bash
git clone https://github.com/BShiffAVA/MealPlanOrganizer.git
cd MealPlanOrganizer/src/MealPlanOrganizer.Mobile
```

### 2. Configure Azure Function Key

The app requires an Azure Function key to communicate with the backend API. 

**Create `appsettings.local.json`:**
```bash
cp appsettings.json appsettings.local.json
```

**Edit `appsettings.local.json` and add your function key:**
```json
{
  "AzureFunctions": {
    "BaseUrl": "https://func-mealplan-organizer.azurewebsites.net/api",
    "FunctionKey": "YOUR_ACTUAL_FUNCTION_KEY_HERE"
  }
}
```

> **Note:** `appsettings.local.json` is gitignored and should **never** be committed to the repository.

**To get your function key:**
1. Go to [Azure Portal](https://portal.azure.com)
2. Navigate to your Function App (`func-mealplan-organizer`)
3. Go to **Functions** → **App keys**
4. Copy the default function key

### 3. Build and Run

**Windows:**
```bash
dotnet build -f net10.0-windows10.0.19041.0
dotnet run -f net10.0-windows10.0.19041.0
```

**Android (requires Android Studio and SDK):**
```bash
dotnet build -f net10.0-android
dotnet run -f net10.0-android
```

**iOS (requires Mac with Xcode):**
```bash
dotnet build -f net10.0-ios
dotnet run -f net10.0-ios
```

## Prerequisites

### All Platforms
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- .NET MAUI workload: `dotnet workload install maui`

### Android Development
- Android Studio with Android SDK
- Environment variables:
  - `ANDROID_HOME` → Android SDK path
  - `JAVA_HOME` → JDK 21 path (use Android Studio's bundled JDK)

### iOS Development (Mac only)
- Xcode 15+
- iOS 15+ SDK
- Apple Developer account for device deployment

## Project Structure

```
src/MealPlanOrganizer.Mobile/
├── Services/
│   ├── IRecipeService.cs       # Service interface
│   └── RecipeService.cs        # Azure Functions API client
├── MainPage.xaml               # Home screen UI
├── MainPage.xaml.cs            # Home screen logic
├── MauiProgram.cs              # App configuration and DI
├── appsettings.json            # Template configuration
└── appsettings.local.json      # Local secrets (gitignored)
```

## Configuration Files

| File | Purpose | Committed |
|------|---------|-----------|
| `appsettings.json` | Template with placeholder values | ✅ Yes |
| `appsettings.local.json` | Actual secrets for local development | ❌ No (gitignored) |

## Security

- **Never commit secrets** to the repository
- `appsettings.local.json` is automatically excluded via `.gitignore`
- Production apps should use Azure Key Vault (see [#31](https://github.com/BShiffAVA/MealPlanOrganizer/issues/31))

## Troubleshooting

### Error: "AzureFunctions:FunctionKey not configured"
- Ensure `appsettings.local.json` exists and contains the function key
- Check that the file is in the correct location (same directory as `.csproj`)

### Android Build Error: "Android SDK not found"
```bash
# Set environment variables
$env:ANDROID_HOME = "C:\Users\<username>\AppData\Local\Android\Sdk"
$env:JAVA_HOME = "C:\Program Files\Android\Android Studio\jbr"
```

### Windows Build: File locked error
- Close any running instances of the app
- Try: `taskkill /IM MealPlanOrganizer.Mobile.exe /F`

## Development

- **Hot Reload:** Not supported in VS Code, use Visual Studio 2022 for best MAUI experience
- **Debugging:** Use VS Code with C# Dev Kit or Visual Studio 2022
- **Device Testing:** Use Android emulator or physical device for mobile testing

## Related Documentation

- [PROJECT_SPEC.md](../../PROJECT_SPEC.md) - Full project specification
- [USER_STORIES_AND_REQUIREMENTS.md](../../USER_STORIES_AND_REQUIREMENTS.md) - User stories
- [Issue #31](https://github.com/BShiffAVA/MealPlanOrganizer/issues/31) - Key Vault implementation
