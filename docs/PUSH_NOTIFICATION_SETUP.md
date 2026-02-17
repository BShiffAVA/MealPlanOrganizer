# Push Notification Setup Guide

This guide covers setting up push notifications for both Windows (WNS) and Android (FCM).

## Part 1: Windows Push Notifications (WNS)

### Step 1: Create Store Association (Required for WNS)

1. **Create a Microsoft Partner Center Account** (if you don't have one)
   - Go to https://partner.microsoft.com/dashboard
   - Sign in with your Microsoft account
   - Register as an app developer ($19 one-time fee for individuals)

2. **Reserve Your App Name**
   - In Partner Center, go to **Apps and games** > **New product** > **MSIX or PWA app**
   - Enter app name: `MealPlanOrganizer` (or similar)
   - Click **Reserve product name**

3. **Get Your Package Identity**
   - After reserving, go to **Product identity** in your app's dashboard
   - Note these values:
     - **Package/Identity/Name** (e.g., `12345CompanyName.MealPlanOrganizer`)
     - **Package/Identity/Publisher** (e.g., `CN=XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX`)
     - **Package/Properties/PublisherDisplayName**

### Step 2: Update Package.appxmanifest

Replace the Identity section in `Platforms/Windows/Package.appxmanifest`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Package
  xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
  xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
  xmlns:mp="http://schemas.microsoft.com/appx/2014/phone/manifest"
  xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
  IgnorableNamespaces="uap rescap">

  <!-- Replace with your Partner Center values -->
  <Identity 
    Name="YOUR_PACKAGE_IDENTITY_NAME" 
    Publisher="CN=YOUR_PUBLISHER_ID" 
    Version="1.0.0.0" />

  <mp:PhoneIdentity PhoneProductId="3A50E355-A4CF-41DD-BB13-8F44088ACC18" PhonePublisherId="00000000-0000-0000-0000-000000000000"/>

  <Properties>
    <DisplayName>Meal Plan Organizer</DisplayName>
    <PublisherDisplayName>YOUR_PUBLISHER_DISPLAY_NAME</PublisherDisplayName>
    <Logo>$placeholder$.png</Logo>
  </Properties>

  <!-- ... rest of manifest ... -->
</Package>
```

### Step 3: Enable MSIX Packaging

In `MealPlanOrganizer.Mobile.csproj`, change the WindowsPackageType:

```xml
<!-- Change from None to MSIX -->
<WindowsPackageType>MSIX</WindowsPackageType>
```

### Step 4: Get WNS Credentials

1. In Partner Center, go to your app > **Product management** > **WNS/MPNS**
2. Click **App Registration Portal** link
3. Note your:
   - **Application (client) ID** (also called Package SID)
   - **Application Secret** (generate one if needed)

### Step 5: Configure Azure Notification Hub for WNS

1. In Azure Portal, go to your Notification Hub
2. Navigate to **Settings** > **Windows (WNS)**
3. Enter:
   - **Package SID**: Your app's Package SID from Partner Center
   - **Security Key**: Your application secret
4. Click **Save**

---

## Part 2: Android Push Notifications (FCM)

### Step 1: Create Firebase Project

1. Go to https://console.firebase.google.com/
2. Click **Add project**
3. Name it `MealPlanOrganizer` (or similar)
4. Follow the wizard (you can disable Google Analytics if not needed)

### Step 2: Add Android App to Firebase

1. In Firebase Console, click **Add app** > **Android**
2. Enter your package name: `com.companyname.mealplanorganizer.mobile`
3. (Optional) Add app nickname and SHA-1 certificate
4. Click **Register app**

### Step 3: Download google-services.json

1. Download `google-services.json` from Firebase Console
2. Place it in `Platforms/Android/` folder
3. Update `.csproj` to include it:

```xml
<ItemGroup Condition="$(TargetFramework.Contains('-android'))">
    <GoogleServicesJson Include="Platforms\Android\google-services.json" />
</ItemGroup>
```

### Step 4: Add Firebase NuGet Package

Add to your `.csproj`:

```xml
<ItemGroup Condition="$(TargetFramework.Contains('-android'))">
    <PackageReference Include="Xamarin.Firebase.Messaging" Version="124.0.4" />
    <PackageReference Include="Xamarin.Google.Dagger" Version="2.52.0.2" />
</ItemGroup>
```

### Step 5: Update AndroidManifest.xml

Add FCM permissions and service:

```xml
<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android">
    <application android:allowBackup="true" android:icon="@mipmap/appicon" android:roundIcon="@mipmap/appicon_round" android:supportsRtl="true">
        
        <!-- FCM Service -->
        <service android:name=".MealPlanFirebaseMessagingService"
                 android:exported="false">
            <intent-filter>
                <action android:name="com.google.firebase.MESSAGING_EVENT" />
            </intent-filter>
        </service>
        
    </application>
    
    <uses-permission android:name="android.permission.ACCESS_NETWORK_STATE" />
    <uses-permission android:name="android.permission.INTERNET" />
    <uses-permission android:name="android.permission.CAMERA" />
    <uses-permission android:name="android.permission.READ_MEDIA_IMAGES" />
    <uses-permission android:name="android.permission.READ_EXTERNAL_STORAGE" android:maxSdkVersion="32" />
    <uses-permission android:name="android.permission.POST_NOTIFICATIONS" />
    
    <queries>
        <intent>
            <action android:name="android.intent.action.VIEW" />
            <category android:name="android.intent.category.BROWSABLE" />
            <data android:scheme="https" />
        </intent>
    </queries>
</manifest>
```

### Step 6: Get FCM Server Key

1. In Firebase Console, go to **Project settings** > **Cloud Messaging**
2. If you see "Cloud Messaging API (Legacy)" is disabled, enable it
3. Copy the **Server key** (you'll need this for Azure Notification Hub)

**Note**: Google is deprecating legacy FCM keys. For production, use FCM v1 API with service account credentials.

### Step 7: Configure Azure Notification Hub for FCM

1. In Azure Portal, go to your Notification Hub
2. Navigate to **Settings** > **Google (GCM/FCM)**
3. Enter your **FCM Server Key**
4. Click **Save**

---

## Part 3: Local Testing Without Store Identity

If you want to test notifications without a Store identity:

### Option A: Use Azure Notification Hub REST API Testing

You can send test notifications directly via Azure Notification Hub without the mobile app:

```bash
# Get your Notification Hub connection string from Azure Portal
# Settings > Access Policies > DefaultFullSharedAccessSignature

# Send a test notification (use Postman or curl)
```

### Option B: Use Local Toast Notifications (Windows)

For Windows, you can test the notification UI without WNS by sending local toasts. Create a test method:

```csharp
// In your Windows platform code
using Microsoft.Toolkit.Uwp.Notifications;

public void SendLocalTestNotification()
{
    new ToastContentBuilder()
        .AddText("Test Notification")
        .AddText("This is a local test notification")
        .Show();
}
```

### Option C: Android Emulator with FCM

Android emulators with Google Play Services can receive FCM notifications. Just ensure you:
1. Use an emulator with Google APIs/Play Store
2. Sign in with a Google account on the emulator

---

## Summary Checklist

### Windows
- [ ] Create Partner Center account
- [ ] Reserve app name and get Package Identity
- [ ] Update `Package.appxmanifest` with real identity
- [ ] Change `WindowsPackageType` to `MSIX` in `.csproj`
- [ ] Configure WNS in Partner Center
- [ ] Add WNS credentials to Azure Notification Hub

### Android
- [ ] Create Firebase project
- [ ] Add Android app with correct package name
- [ ] Download and add `google-services.json`
- [ ] Add Firebase NuGet packages
- [ ] Update `AndroidManifest.xml` with FCM service
- [ ] Add FCM server key to Azure Notification Hub

---

## Environment-Specific Configuration

Update your `local.settings.json` (Functions) with Notification Hub credentials:

```json
{
    "Values": {
        "NotificationHub__ConnectionString": "Endpoint=sb://your-namespace.servicebus.windows.net/;SharedAccessKeyName=DefaultFullSharedAccessSignature;SharedAccessKey=YOUR_KEY",
        "NotificationHub__HubName": "your-hub-name"
    }
}
```

Once both platforms are configured, the app will automatically register with the Notification Hub on login, and you can send cross-platform notifications.
