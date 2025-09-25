---
description: Guide for developing new content editors
globs:
alwaysApply: true
---

[8 tools called]

Based on my analysis of the ZauberCMS editor system, here's comprehensive documentation for new developers:

# ZauberCMS Editor System - Developer Guide

Welcome to the ZauberCMS Editor System! This guide will walk you through creating custom content property editors that can be used throughout the CMS. The editor system is built around three key concepts: **Editors**, **Settings**, and **Models**, all working together through well-defined interfaces.

## 🏗️ System Architecture

The ZauberCMS editor system consists of:

- **Editors** (`ZauberCMS.Components/Editors/`): The actual UI components users interact with
- **Settings** (`ZauberCMS.Components/Editors/Settings/`): Configuration panels for editors  
- **Models** (`ZauberCMS.Components/Editors/Models/`): Data structures for settings
- **Dialogs** (`ZauberCMS.Components/Editors/Dialogs/`): Modal dialogs for complex interactions

All editors must implement the `IContentProperty` interface, and settings components implement `IContentPropertySettings<T>`.

## 📋 Core Interfaces

### IContentProperty

Every editor must implement this interface:

```csharp
public interface IContentProperty
{
    string Name { get; }                    // Display name
    string Alias { get; }                   // Unique identifier (e.g., "ZauberCMS.MyEditor")
    string Description { get; }             // Help text
    string Icon { get; }                    // Material Design icon name
    
    Type? SettingsComponent { get; set; }   // Associated settings component
    
    string? Value { get; set; }             // Current editor value
    EventCallback<string> ValueChanged { get; set; }  // Value change handler
    string? Settings { get; set; }          // Serialized settings JSON
    
    Models.Content? Content { get; set; }   // Current content context
    IModalService? ModalService { get; set; } // For opening dialogs
    
    List<string> Scripts { get; set; }      // Additional JavaScript files
    List<string> Styles { get; set; }       // Additional CSS files
    bool FullWidth { get; set; }           // Whether editor needs full width
}
```

### IContentPropertySettings<T>

Settings components implement this generic interface:

```csharp
public interface IContentPropertySettings<T>
{
    EventCallback<string> ValueChanged { get; set; }  // Settings change handler
    string? SettingsModel { get; set; }               // Serialized settings JSON
    T Settings { get; set; }                         // Strongly-typed settings object
}
```

## 🚀 Creating Your First Editor

Let's create a simple "Color Picker" editor. Start by creating the main editor component:

### Step 1: Create the Editor Component

```csharp
// ZauberCMS.Components/Editors/SimpleColorPickerProperty.razor
@implements ZauberCMS.Core.Content.Interfaces.IContentProperty

<RadzenColorPicker @bind-Value="@Value" ShowHSV="true" ShowRGBA="false" 
                   Change="@(args => OnValueChanged(args))" />

@code {
    // Required IContentProperty members
    public string Name { get; set; } = "Simple Color Picker";
    public string Alias { get; set; } = "ZauberCMS.SimpleColorPicker";
    public string Description { get; set; } = "Pick a color using the color picker";
    public string Icon { get; set; } = "palette";
    public Type? SettingsComponent { get; set; } = typeof(SimpleColorPickerSettings);
    
    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string> ValueChanged { get; set; }
    [Parameter] public string? Settings { get; set; }
    [Parameter] public Models.Content? Content { get; set; }
    [CascadingParameter] public IModalService? ModalService { get; set; }
    
    public List<string> Scripts { get; set; } = [];
    public List<string> Styles { get; set; } = [];
    public bool FullWidth { get; set; }
    
    // Handle value changes
    private async Task OnValueChanged(string newValue)
    {
        Value = newValue;
        await ValueChanged.InvokeAsync(Value);
    }
}
```

### Step 2: Create Settings Model

Create a model to hold your editor's configuration:

```csharp
// ZauberCMS.Components/Editors/Models/SimpleColorPickerSettingsModel.cs
namespace ZauberCMS.Components.Editors.Models;

public class SimpleColorPickerSettingsModel
{
    public bool ShowHexValue { get; set; } = true;
    public string DefaultColor { get; set; } = "#007bff";
    public bool AllowTransparent { get; set; } = false;
}
```

### Step 3: Create Settings Component

Create the settings UI that allows content editors to configure your editor:

```csharp
// ZauberCMS.Components/Editors/Settings/SimpleColorPickerSettings.razor
@using ZauberCMS.Components.Editors.Models
@implements ZauberCMS.Core.Content.Interfaces.IContentPropertySettings<SimpleColorPickerSettingsModel>

<EditorRow>
    <LeftColumn>
        <PropertyInfo Name="Show Hex Value" Description="Display the selected color's hex value"/>
    </LeftColumn>
    <CentreColumn>
        <RadzenSwitch @bind-Value="@Settings.ShowHexValue" />
    </CentreColumn>
</EditorRow>

<EditorRow>
    <LeftColumn>
        <PropertyInfo Name="Default Color" Description="The default color when no value is set"/>
    </LeftColumn>
    <CentreColumn>
        <RadzenColorPicker @bind-Value="@Settings.DefaultColor" />
    </CentreColumn>
</EditorRow>

<EditorRow>
    <LeftColumn>
        <PropertyInfo Name="Allow Transparent" Description="Allow selection of transparent color"/>
    </LeftColumn>
    <CentreColumn>
        <RadzenSwitch @bind-Value="@Settings.AllowTransparent" />
    </CentreColumn>
</EditorRow>

<RadzenStack Orientation="Orientation.Horizontal" JustifyContent="JustifyContent.Right" 
             AlignItems="AlignItems.Center" class="rz-mt-3">
    <RadzenButton ButtonType="ButtonType.Submit" ButtonStyle="ButtonStyle.Success" 
                  Text="Store Settings" Click="Save"/>
</RadzenStack>

@code {
    [Parameter] public string? SettingsModel { get; set; }
    [Parameter] public EventCallback<string> ValueChanged { get; set; }
    public SimpleColorPickerSettingsModel Settings { get; set; } = new();

    protected override void OnInitialized()
    {
        Settings = this.GetPropertySettings();
    }

    private async Task Save()
    {
        await this.SavePropertySettings(Settings);
    }
}
```

### Step 4: Update the Editor to Use Settings

Now modify your editor to use the settings:

```csharp
// Update your editor to use settings
@code {
    // ... existing properties ...
    
    private SimpleColorPickerSettingsModel SettingsModel { get; set; } = new();
    
    protected override void OnInitialized()
    {
        SettingsModel = Settings.FromJson<SimpleColorPickerSettingsModel>();
    }
    
    // You can now use SettingsModel.ShowHexValue, etc. in your UI
}
```

## 🎨 Advanced Editor Patterns

### Using Dialogs for Complex Interactions

Editors can open modal dialogs for complex interactions. Here's an example:

```csharp
// ZauberCMS.Components/Editors/AdvancedColorPickerProperty.razor
@implements ZauberCMS.Core.Content.Interfaces.IContentProperty

@if (!string.IsNullOrEmpty(Value))
{
    <div style="background-color: @Value; width: 50px; height: 50px; border-radius: 4px; border: 1px solid #ccc;"></div>
}

<RadzenButton Click="@OpenColorDialog" Text="Choose Color" />

@code {
    // ... other properties ...
    
    private async Task OpenColorDialog()
    {
        if (ModalService != null)
        {
            var dialog = ModalService.OpenSidePanel<ColorPickerDialog>("Choose Color", 
                new Dictionary<string, object>
                {
                    { "CurrentColor", Value },
                    { "Settings", SettingsModel }
                });
            
            var result = await dialog.Result;
            if (result.Confirmed && result.Data is string selectedColor)
            {
                Value = selectedColor;
                await ValueChanged.InvokeAsync(Value);
            }
        }
    }
}
```

### Creating the Dialog Component

```csharp
// ZauberCMS.Components/Editors/Dialogs/ColorPickerDialog.razor
@inject NotificationService NotificationService

<RadzenColorPicker @bind-Value="@SelectedColor" ShowHSV="true" ShowRGBA="true" />

<RadzenStack Orientation="Orientation.Horizontal" JustifyContent="JustifyContent.Right" 
             AlignItems="AlignItems.Center" class="rz-mt-3">
    <RadzenButton Text="Cancel" Click="Cancel" />
    <RadzenButton ButtonStyle="ButtonStyle.Success" Text="Select" Click="Confirm" />
</RadzenStack>

@code {
    [CascadingParameter] BlazoredModalInstance Modal { get; set; } = null!;
    
    [Parameter] public string? CurrentColor { get; set; }
    [Parameter] public SimpleColorPickerSettingsModel? Settings { get; set; }
    
    private string SelectedColor { get; set; } = "#007bff";
    
    protected override void OnInitialized()
    {
        SelectedColor = CurrentColor ?? Settings?.DefaultColor ?? "#007bff";
    }
    
    private async Task Confirm()
    {
        await Modal.CloseAsync(ModalResult.Ok(SelectedColor));
    }
    
    private async Task Cancel()
    {
        await Modal.CloseAsync(ModalResult.Cancel());
    }
}
```

### Handling Additional Scripts and Styles

Some editors need custom JavaScript or CSS:

```csharp
@code {
    public List<string> Scripts { get; set; } = [
        "_content/MyCustomLibrary/custom-script.js"
    ];
    
    public List<string> Styles { get; set; } = [
        "_content/MyCustomLibrary/custom-styles.css"
    ];
}
```

## 🔧 Best Practices

### Editor Development

1. **Always implement IContentProperty** - This ensures your editor integrates properly
2. **Use descriptive aliases** - Follow the pattern `ZauberCMS.YourEditorName`
3. **Provide meaningful icons** - Use Material Design icon names
4. **Handle null/empty values gracefully** - Editors should work even without initial values
5. **Use async operations properly** - Always await when calling ValueChanged

### Settings Development

1. **Keep settings simple** - Complex settings confuse content editors
2. **Provide sensible defaults** - Settings should work out of the box
3. **Use appropriate controls** - Match the data type to the control (numeric for numbers, switches for booleans)
4. **Validate settings** - Ensure required fields are filled

### Model Design

1. **Use descriptive property names** - Self-documenting code
2. **Include XML comments** - Help other developers understand your settings
3. **Use nullable types appropriately** - Make optional settings nullable
4. **Consider serialization** - Ensure your models serialize/deserialize properly

## 📚 Common Patterns and Examples

### Conditional UI Based on Settings

```csharp
@if (SettingsModel.ShowAdvancedOptions)
{
    <RadzenTextBox @bind-Value="@AdvancedValue" Placeholder="Advanced setting" />
}
```

### Integration with Content Context

```csharp
protected override async Task OnInitializedAsync()
{
    await base.OnInitializedAsync();
    
    // Access current content information
    if (Content != null)
    {
        // Do something based on content type, etc.
    }
}
```

### Custom Validation

```csharp
private async Task OnValueChanged(string newValue)
{
    // Custom validation logic
    if (IsValidValue(newValue))
    {
        Value = newValue;
        await ValueChanged.InvokeAsync(Value);
    }
    else
    {
        // Show error notification
        NotificationService.Notify(new NotificationMessage
        {
            Severity = NotificationSeverity.Error,
            Summary = "Invalid Value",
            Detail = "The entered value is not valid."
        });
    }
}
```

## 🚀 Registration and Discovery

Once you've created your editor, it's automatically discovered by the system through the `IContentProperty` interface. Content editors can select your editor from the property type dropdown in the content type editor.

## 🛠️ Available Helper Methods

Your editor components inherit helpful extension methods:

### Settings Management
- `this.GetPropertySettings<T>()` - Deserialize settings JSON to your model
- `this.SavePropertySettings<T>(T settings)` - Serialize and save settings

### JSON Extensions
- `Settings.FromJson<T>(json)` - Deserialize JSON settings
- `settings.ToJson()` - Serialize settings to JSON

## 🎯 Next Steps

1. **Study existing editors** - Look at `TextProperty.razor`, `RichTextEditorProperty.razor`, etc.
2. **Start simple** - Create a basic editor first, then add settings
3. **Test thoroughly** - Test with different content types and settings combinations
4. **Consider performance** - Avoid heavy operations in frequently-called methods

Remember, editors should be intuitive for content editors while providing the flexibility content authors need. Happy coding! 🎨