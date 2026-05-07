---
description: For any changes to the Admin UI or UX
globs:
alwaysApply: true
---

# ZauberCMS Admin UI/UX Developer Guide

## Overview

ZauberCMS Admin section provides a comprehensive content management interface built with **Blazor Server** and extensively uses **[Radzen Blazor](https://blazor.radzen.com/)** components for consistent UI/UX. The Admin interface is organized into sections (Content, Media, Settings, Users, Structure) with a plugin-based architecture that allows dynamic component loading.

## Architecture

### Core Components

The Admin section follows a consistent architectural pattern:

- **`SectionLayout.razor`** - Main layout component for all admin pages
- **Section-based navigation** - Each section (Content, Media, Settings, etc.) has its own navigation tree
- **Plugin architecture** - Uses `ExtensionManager` for dynamic component discovery
- **Tree-based navigation** - Hierarchical navigation for content organization

### Layout Structure

```12:40:ZauberCMS.Components/Admin/Layout/SectionLayout.razor
<RadzenComponents />
<CascadingBlazoredModal>
    <RadzenLayout>
        <CascadingValue Value="@this">
            <RadzenHeader class="rz-background-color-primary-darker flex justify-between items-center">
                <RadzenStack Orientation="Orientation.Horizontal" AlignItems="AlignItems.Center" Gap="5"
                             Style="height: 100%;">
                    <RadzenSidebarToggle Click="@(() => ToggleSideBar())"/>
                    <ZauberCMS.Components.Shared.ZauberLogo Styles="height: 24px; colour: #fff; fill: #fff;" />
                    <SectionLinks CurrentSection="@(SectionAlias)"/>
                </RadzenStack>
                <RadzenStack Orientation="Orientation.Horizontal" AlignItems="AlignItems.Center" Gap="5"
                             Style="height: 100%;">
                    <UserProfile/>
                </RadzenStack>
            </RadzenHeader>
            @if (!SectionAlias.IsNullOrWhiteSpace())
            {
                <RadzenSidebar @bind-Expanded="@Expanded">
                    <SectionTrees SectionAlias="@(SectionAlias)"/>
                </RadzenSidebar>
            }
            <RadzenBody>
                @Body
            </RadzenBody>
        </CascadingValue>
    </RadzenLayout>
</CascadingBlazoredModal>
```

## Radzen Blazor Integration

### Core Components Used

ZauberCMS Admin extensively uses Radzen Blazor components for consistent styling and functionality:

#### Layout Components
- `RadzenLayout` - Main layout container
- `RadzenHeader` - Top navigation bar
- `RadzenSidebar` - Left navigation panel
- `RadzenBody` - Main content area
- `RadzenSidebarToggle` - Sidebar toggle button

#### Form Components
- `RadzenTextBox` - Text input fields
- `RadzenTextArea` - Multi-line text input
- `RadzenNumeric` - Numeric input
- `RadzenSwitch` - Toggle switches
- `RadzenCheckBox` - Checkboxes
- `RadzenDropDown` - Dropdown selections

#### Data Display
- `RadzenDataGrid` - Data tables with sorting/filtering
- `RadzenTabs` - Tabbed interfaces
- `RadzenStack` - Flexible layout containers
- `RadzenCard` - Content containers

#### Interactive Components
- `RadzenButton` - Action buttons
- `RadzenIcon` - Material Design icons
- `RadzenTree` - Hierarchical tree navigation
- `RadzenListBox` - List selections

### Radzen Component Best Practices

1. **Consistent Styling**: Use Radzen's built-in CSS classes like `rz-background-color-primary-darker`
2. **Responsive Design**: Leverage Radzen's responsive utilities
3. **Theme Integration**: Use Radzen's theme variables for consistent colors
4. **Accessibility**: Always include appropriate ARIA labels

## Navigation Patterns

### Section Navigation

Each section implements the `ISection` interface:

```14:25:ZauberCMS.Components/Admin/ContentSection/ContentIndex.razor
@code {
    private const string Url = Urls.AdminContentBaseUrl; // So we only have on string
    public string Name => "Content";
    public string Alias => Constants.Sections.ContentSection;
    public string IndexUrl => Url;
    public int SortOrder => 0;
    
    [CascadingParameter] protected SectionLayout? Layout { get; set; }

    protected override void OnInitialized()
    {
        Layout?.SetSection(Alias);
    }
}
```

### Tree Navigation

Tree components implement `ISectionNav` interface:

```10:21:ZauberCMS.Components/Admin/ContentSection/Navigation/ContentSectionTree.razor
<ContentTree @ref="ContentTree"
            Data="@ContentItems"
            OnChange="OnChange" 
            @bind-Value="@Selection" />

@code {
    public int SortOrder => 0;
    public string SectionNavGroupAlias => Constants.Sections.SectionNavGroups.ContentNavGroup;
    
    [CascadingParameter] public IModalService ModalService { get; set; } = null!;
    
    [Inject] public AppState AppState { get; set; } = null!;
    [Inject] public TreeState TreeState { get; set; } = null!;
```

### Navigation Groups

Sections are organized into navigation groups:

```6:12:ZauberCMS.Components/Admin/ContentSection/Navigation/ContentNavGroup.cs
public class ContentNavGroup : ISectionNavGroup
{
    public string Heading => "Content";
    public string Alias => Constants.Sections.SectionNavGroups.ContentNavGroup;
    public int SortOrder => 0;
    public string SectionAlias => Constants.Sections.ContentSection;
    public List<ISectionNavGroupAction> Actions => [];
}
```

## Component Patterns

### Editor Components

Editors follow a consistent pattern with tabs and property editing:

```26:46:ZauberCMS.Components/Admin/ContentSection/ContentEditor.razor
@if (ContentType != null && Content != null)
{
    <EditForm @ref="@ContentForm" Model="@Content">

        <RadzenRow class="rz-pb-2">
            <RadzenTextBox Style="width: 100%;" Name="Name" Placeholder="Name" Value="@Content!.Name"
                           ValueChanged="@((string name) => { Content.Name = name; })" aria-label="Name"/>
            <RadzenStack Orientation="Orientation.Horizontal" JustifyContent="JustifyContent.Left"
                         AlignItems="AlignItems.Center" Gap="0.2rem">
                <RadzenIcon Icon="schedule" Style="line-height: 20px; height: 20px; font-size: 20px;"
                            IconStyle="IconStyle.Light"/>
                <RadzenText TextStyle="TextStyle.Caption" Style="margin: 0; padding: 0;">Last
                    Updated: @Content.DateUpdated.Humanize()</RadzenText>
                @if (Content.UnpublishedContent != null)
                {
                    <RadzenText TextStyle="TextStyle.Caption" Style="margin: 0; padding: 0; color: #e85d5d">
                        (Contains Unpublished Changes)
                    </RadzenText>
                }
            </RadzenStack>
        </RadzenRow>

        <RadzenTabs @ref="Tabs" RenderMode="TabRenderMode.Client" Change="@(i => TabChange(i))">
```

### Property Editors

Property editors use a consistent layout with left column for labels, center for controls:

```70:82:ZauberCMS.Components/Admin/ContentSection/ContentEditor.razor
<EditorRow FullWidth="@(property.FullWidth)">
    <LeftColumn>
        <PropertyInfo Name="@property.Name" Alias="@property.Alias"
                      Description="@property.Description"/>
    </LeftColumn>
    <CentreColumn>
        <DynamicContentProperty
            ComponentType="@contentPropertyComponent"
            Settings="@property.Settings"
            Content="@Content"
            Value="@contentValue.Value"
            ValueChanged="@(value => UpdateProperty(contentValue.ContentTypePropertyId, value))"/>
    </CentreColumn>
</EditorRow>
```

### Dialog Components

Dialogs use consistent patterns for CRUD operations:

```25:41:ZauberCMS.Components/Admin/ContentSection/Dialogs/CopyContent.razor
@code {
    [Parameter, EditorRequired] public Content Content { get; set; } = null!;
    [Parameter, EditorRequired] public CopyContentParameters CopyContentCommand { get; set; } = null!;
    [CascadingParameter] BlazoredModalInstance BlazoredModal { get; set; } = null!;
    
    private void OnValueChangedHandler(object value)
    {
        if (value is Content content)
        {
            CopyContentCommand.CopyTo = content.Id;
        }
    }
    
    private async Task Save()
    {
        await BlazoredModal.CloseAsync(ModalResult.Ok(CopyContentCommand));
    }
}
```

## Data Grid Patterns

Data grids follow consistent patterns for filtering, sorting, and pagination:

```5:40:ZauberCMS.Components/Admin/ContentSection/ContentListView.razor
<RadzenDataGrid AllowFiltering="true"
                FilterPopupRenderMode="PopupRenderMode.Initial"
                FilterCaseSensitivity="FilterCaseSensitivity.CaseInsensitive"
                AllowPaging="true"
                PageSize="@AmountPerPage"
                AllowSorting="true"
                LoadData="@LoadAllUserData"
                IsLoading="@IsLoading"
                Count="@AllUserCount"
                Data="@AllUserContents"
                SelectionMode="DataGridSelectionMode.Single"
                RowSelect="@((Content value) => OnRowSelect(value))"
                PagerHorizontalAlign="HorizontalAlign.Center">
    @*@bind-Value="@SelectedContent"*@
    <Columns>
        <RadzenDataGridColumn Property="Name" Title="Name"/>
        <RadzenDataGridColumn Property="DateUpdated" Title="Last Updated">
            <Template Context="data">
                @data.DateUpdated.Humanize()
            </Template>
        </RadzenDataGridColumn>
        <RadzenDataGridColumn Property="LastUpdatedBy" Title="Updated By">
            <Template Context="data">
                <RadzenGravatar Email="@data.LastUpdatedBy?.Email" Style="width: 25px; height: 25px;"/>
            </Template>
        </RadzenDataGridColumn>
        <RadzenDataGridColumn Property="Id" Title="Delete">
            <Template Context="data">
                <RadzenButton Variant="Variant.Text" Size="ButtonSize.Small" Text="Delete" Click="@(() => Delete(data.Id))"/>
            </Template>
        </RadzenDataGridColumn>
    </Columns>
</RadzenDataGrid>
```

## State Management

ZauberCMS uses two main state management services to coordinate UI updates and maintain consistency across the admin interface.

### AppState Service

The `AppState` service provides a centralized event-driven system for notifying components when data changes occur. It handles notifications for Content, ContentTypes, Users, and Media.

#### Key Events

- **OnContentChanged/OnContentSaved/OnContentDeleted**: Content-related events
- **OnContentTypeChanged/OnContentTypeSaved/OnContentTypeDeleted**: ContentType-related events  
- **OnUserChanged/OnUserSaved/OnUserDeleted**: User-related events
- **OnMediaChanged/OnMediaSaved/OnMediaDeleted**: Media-related events

#### Usage Pattern

```csharp
@code {
    [Inject] public AppState AppState { get; set; } = null!;
    
    protected override void OnInitialized()
    {
        // Subscribe to events
        AppState.OnContentChanged += HandleContentChanged;
        AppState.OnContentDeleted += HandleContentDeleted;
    }
    
    // Weak event handlers run on the publisher's thread (whatever thread the
    // service that called AppState.NotifyXxx happened to be on). Anything that
    // touches render state — including StateHasChanged — must be marshalled
    // onto the component's Dispatcher via InvokeAsync, otherwise Blazor throws
    // "The current thread is not associated with the Dispatcher."
    private Task HandleContentChanged(Content.Models.Content? content, string username)
        => InvokeAsync(async () =>
        {
            // Refresh data, update UI, etc.
            await RefreshData();
            StateHasChanged();
        });

    private Task HandleContentDeleted(Content.Models.Content? content, string username)
        => InvokeAsync(() =>
        {
            // Handle deletion - remove from lists, navigate away, etc.
            if (content?.Id == CurrentContentId)
            {
                NavigationManager.NavigateTo("/admin/content");
            }
            StateHasChanged();
            return Task.CompletedTask;
        });
    
    private async Task SaveContent()
    {
        // Save your content
        var savedContent = await ContentService.SaveAsync(content);
        
        // Notify all subscribers about the change
        await AppState.NotifyContentSaved(savedContent, CurrentUser.Email);
    }
    
    public void Dispose()
    {
        // Always unsubscribe to prevent memory leaks
        AppState.OnContentChanged -= HandleContentChanged;
        AppState.OnContentDeleted -= HandleContentDeleted;
    }
}
```

#### Notification Methods

When you make changes to data, always notify other components:

```csharp
// For saves (triggers both Saved and Changed events)
await AppState.NotifyContentSaved(content, username);
await AppState.NotifyContentTypeSaved(contentType, username);
await AppState.NotifyUserSaved(user, username);
await AppState.NotifyMediaSaved(media, username);

// For deletions (triggers both Deleted and Changed events)
await AppState.NotifyContentDeleted(content, username);
await AppState.NotifyContentTypeDeleted(contentType, username);
await AppState.NotifyUserDeleted(user, username);
await AppState.NotifyMediaDeleted(media, username);

// For general changes
await AppState.NotifyContentChanged(content, username);
await AppState.NotifyContentTypeChanged(contentType, username);
await AppState.NotifyUserChanged(user, username);
await AppState.NotifyMediaChanged(media, username);
```

### TreeState Service

The `TreeState` service manages tree navigation state, including expanded/collapsed nodes and selected values. It's essential for maintaining tree state across navigation and updates.

#### Key Features

- **Node Expansion**: Track which tree nodes are expanded/collapsed
- **Selection Management**: Manage currently selected tree items
- **Children Cache**: Cache whether nodes have children for performance
- **Section Tracking**: Track current admin section

#### Usage Pattern

```csharp
@code {
    [Inject] public TreeState TreeState { get; set; } = null!;
    
    protected override void OnInitialized()
    {
        // Subscribe to tree value changes
        TreeState.OnTreeValueChanged += HandleTreeValueChanged;
        
        // Set current section
        TreeState.CurrentSection = Constants.Sections.ContentSection;
    }
    
    private void HandleTreeValueChanged(object? value)
    {
        if (value is Content.Models.Content content)
        {
            SelectedContent = content;
            NavigationManager.NavigateTo($"/admin/content/edit/{content.Id}");
        }
        StateHasChanged();
    }
    
    private void OnNodeExpanded(Guid nodeId)
    {
        TreeState.NodeExpanded(nodeId);
    }
    
    private void OnNodeCollapsed(Guid nodeId)
    {
        TreeState.NodeCollapsed(nodeId);
    }
    
    private bool IsNodeExpanded(Guid nodeId)
    {
        return TreeState.IsNodeExpanded(nodeId);
    }
    
    // Set tree selection
    private void SelectNode(Content.Models.Content content)
    {
        TreeState.TreeValue = content;
    }
    
    // Cache children information for performance
    private void SetNodeHasChildren(Guid nodeId, bool hasChildren)
    {
        TreeState.SetChildren(nodeId, hasChildren);
    }
    
    // Clear cache when content structure changes
    private void ClearChildrenCache(Guid? contentId = null)
    {
        TreeState.ClearChildCache(contentId); // null clears all
    }
    
    public void Dispose()
    {
        TreeState.OnTreeValueChanged -= HandleTreeValueChanged;
    }
}
```

#### Tree Integration Example

```csharp
<RadzenTree @ref="ContentTreeRef" Data="@TreeData" @bind-Value="@TreeState.TreeValue"
            Expand="@OnExpand" Collapse="@OnCollapse">
    <RadzenTreeLevel>
        <Template Context="data">
            @if (data is Content.Models.Content content)
            {
                <RadzenIcon Icon="@GetContentIcon(content)" />
                @content.Name
            }
        </Template>
    </RadzenTreeLevel>
</RadzenTree>

@code {
    private async Task OnExpand(TreeExpandEventArgs args)
    {
        if (args.Value is Content.Models.Content content)
        {
            TreeState.NodeExpanded(content.Id);
            
            // Load children if not already loaded
            if (!TreeState.HasChildren(content.Id))
            {
                var children = await LoadChildren(content.Id);
                TreeState.SetChildren(content.Id, children.Any());
            }
        }
    }
    
    private void OnCollapse(TreeExpandEventArgs args)
    {
        if (args.Value is Content.Models.Content content)
        {
            TreeState.NodeCollapsed(content.Id);
        }
    }
}
```

## Modal Dialog Management

ZauberCMS uses Blazored.Modal for dialog management. All dialogs follow a consistent pattern using cascading parameters and result handling.

### Basic Dialog Pattern

Every dialog component should include the `BlazoredModalInstance` cascading parameter:

```csharp
@code {
    [CascadingParameter] BlazoredModalInstance BlazoredModal { get; set; } = null!;
    
    // Your dialog parameters
    [Parameter, EditorRequired] public Guid ContentId { get; set; }
    [Parameter] public string? Title { get; set; }
    
    private async Task Save()
    {
        try
        {
            // Perform your save operation
            var result = await SaveData();
            
            // Close dialog with success result
            await BlazoredModal.CloseAsync(ModalResult.Ok(result));
        }
        catch (Exception ex)
        {
            // Handle errors - you can either show error in dialog or close with error
            await BlazoredModal.CloseAsync(ModalResult.Cancel());
            // Or show error notification and keep dialog open
        }
    }
    
    private async Task Cancel()
    {
        await BlazoredModal.CloseAsync(ModalResult.Cancel());
    }
}
```

### Opening Dialogs

Use the `IModalService` to open dialogs.

#### Side panel dialogs (preferred)

The standard pattern is to open dialogs as a side panel using `OpenSidePanel<TComponent>()` and await the result's `Confirmed` flag:

```csharp
@code {
    [Inject] public IModalService DialogService { get; set; } = null!;

    private async Task CreateLanguageDictionary()
    {
        var dialog = DialogService.OpenSidePanel<SaveLanguageDictionary>("Create Language Dictionary", 
            new Dictionary<string, object>
            {
                {nameof(SaveLanguageDictionary.LanguageDictionary), new LanguageDictionary()}
            });
        var result = await dialog.Result;
        if (result is { Confirmed: true })
        {
            await LoadData(new LoadDataArgs { Top = AmountPerPage });
        }
    }
}
```

#### Standard modal dialogs

You can still open centered modals when appropriate:

```csharp
@code {
    [CascadingParameter] public IModalService ModalService { get; set; } = null!;
    
    private async Task OpenRestrictAccessDialog(Guid contentId)
    {
        var parameters = new ModalParameters()
            .Add(nameof(RestrictAccess.ContentId), contentId);
            
        var options = new ModalOptions
        {
            Size = ModalSize.Large,
            DisableBackgroundCancel = true
        };
        
        var modal = ModalService.Show<RestrictAccess>("Restrict Access", parameters, options);
        var result = await modal.Result;
        
        if (result.Confirmed)
        {
            var data = result.Data;
            if (data is (bool hadRoles, List<Role> selectedRoles))
            {
                await ProcessRoleRestrictions(contentId, hadRoles, selectedRoles);
                await AppState.NotifyContentChanged(content, CurrentUser.Email);
            }
        }
    }
}
```

### OpenSidePanel defaults

`OpenSidePanel<T>()` is an extension on `IModalService` that sets consistent side-panel options by default (large size, custom position with the `side-panel` class) and maps a `Dictionary<string, object>` to `ModalParameters`:

```9:27:ZauberCMS.Core/Extensions/DialogExtensions.cs
public static IModalReference OpenSidePanel<T>(this IModalService dialogService, string title, Dictionary<string, object>? parameters = null) where T : ComponentBase
{
    var modalParameters = new ModalParameters();
    if (parameters != null)
    {
        foreach (var p in parameters)
        {
            modalParameters.Add(p.Key, p.Value);
        }
        
    }
    var options = new ModalOptions
    { 
        Size = ModalSize.Large,
        Position = ModalPosition.Custom,
        PositionCustomClass = "side-panel"
    };
    return dialogService.Show<T>(title, modalParameters, options);
}
```

### Dialog Return Values

Dialogs can return different types of data:

```csharp
// Simple confirmation
await BlazoredModal.CloseAsync(ModalResult.Ok());

// Return single value
await BlazoredModal.CloseAsync(ModalResult.Ok(selectedItem));

// Return multiple values using tuples
await BlazoredModal.CloseAsync(ModalResult.Ok((hadPreviousData, selectedItems, additionalInfo)));

// Return complex objects
var result = new DialogResult 
{ 
    Success = true, 
    Data = complexObject,
    Message = "Operation completed"
};
await BlazoredModal.CloseAsync(ModalResult.Ok(result));

// Cancel/Error
await BlazoredModal.CloseAsync(ModalResult.Cancel());
```

### Complete Dialog Example

```csharp
// RestrictAccess.razor
<div class="font-medium mb-1">Pick the roles who have access to this page</div>

<RadzenPickList @bind-Source="@AllRoles" @bind-Target="@SelectedRoles" 
                Style="height:500px; width:100%;"
                TextProperty="@nameof(Role.Name)">
    <SourceHeader>Available Roles:</SourceHeader>
    <TargetHeader>Selected Roles:</TargetHeader>
</RadzenPickList>

<RadzenStack Orientation="Orientation.Horizontal" JustifyContent="JustifyContent.Right" 
             AlignItems="AlignItems.Center" class="rz-mt-3">
    <RadzenButton ButtonStyle="ButtonStyle.Light" Text="Cancel" Click="Cancel"/>
    <RadzenButton ButtonStyle="ButtonStyle.Success" Icon="save" Text="Save" Click="Save"/>
</RadzenStack>

@code {
    [CascadingParameter] BlazoredModalInstance BlazoredModal { get; set; } = null!;
    [Parameter, EditorRequired] public Guid ContentId { get; set; }
    
    [Inject] public IContentService ContentService { get; set; } = null!;
    [Inject] public IMembershipService MembershipService { get; set; } = null!;
    
    private IEnumerable<Role> AllRoles { get; set; } = [];
    private IEnumerable<Role>? SelectedRoles { get; set; } = [];
    private bool AlreadyHadContentRoles { get; set; }
    
    protected override async Task OnInitializedAsync()
    {
        var content = await ContentService.GetContentAsync(new GetContentParameters 
        { 
            Id = ContentId, 
            IncludeContentRoles = true 
        });
        
        if (content != null)
        {
            var allRoles = await MembershipService.QueryRolesAsync(new QueryRolesParameters 
            { 
                AmountPerPage = 250, 
                OrderBy = GetRolesOrderBy.Name
            });
            
            AllRoles = allRoles.Items;
            SelectedRoles = content.ContentRoles.Select(x => x.Role);
            
            var selectedRoles = SelectedRoles as Role[] ?? SelectedRoles.ToArray();
            if (selectedRoles.Any())
            {
                AlreadyHadContentRoles = true;
                AllRoles = AllRoles.Except(selectedRoles, new ITreeEqualityComparer<Role>()).ToList();
            }
        }
    }

    private async Task Save()
    {
        var result = (AlreadyHadContentRoles, SelectedRoles?.ToList() ?? []);
        await BlazoredModal.CloseAsync(ModalResult.Ok(result));
    }
    
    private async Task Cancel()
    {
        await BlazoredModal.CloseAsync(ModalResult.Cancel());
    }
}
```

## Best Practices

### Component Structure

1. **Consistent Naming**: Use PascalCase for component names, follow existing patterns
2. **Parameter Validation**: Use `[Parameter, EditorRequired]` for required parameters
3. **Error Handling**: Always include try-catch blocks and user-friendly error messages
4. **Loading States**: Show loading indicators for async operations

### Layout Consistency

1. **Use SectionLayout**: All admin pages should inherit from `SectionLayout`
2. **Consistent Spacing**: Use Radzen's gap utilities and spacing classes
3. **Responsive Design**: Ensure components work on mobile and desktop
4. **Accessibility**: Include proper ARIA labels and keyboard navigation

### State Management Best Practices

1. **Always Notify**: Use AppState notifications when data changes
2. **Subscribe/Unsubscribe**: Always unsubscribe from events in Dispose() to prevent memory leaks
3. **Tree State**: Use TreeState for navigation state management
4. **Local State**: Keep component-specific state in the component
5. **State Synchronization**: Ensure state changes trigger appropriate UI updates

### Code Organization

1. **Separation of Concerns**: Keep business logic in services, UI logic in components
2. **Reusable Components**: Create reusable components for common patterns
3. **Consistent Imports**: Use `@using` directives consistently across files
4. **Documentation**: Include XML comments for public methods and properties

## Adding New Admin Features

### Step 1: Create Section Structure

1. Create a new folder under `ZauberCMS.Components/Admin/`
2. Implement `ISection` interface for the main index page
3. Create navigation group implementing `ISectionNavGroup`
4. Create tree navigation implementing `ISectionNav`

### Step 2: Implement Core Components

```csharp
// Example: NewFeatureIndex.razor
@attribute [Route(Urls.AdminNewFeatureBaseUrl)]
@layout SectionLayout
@implements ZauberCMS.Core.Sections.Interfaces.ISection

<PageTitle>@Name</PageTitle>

<SectionDashboards SectionAlias="@(Alias)"/>

@code {
    private const string Url = Urls.AdminNewFeatureBaseUrl;
    public string Name => "New Feature";
    public string Alias => Constants.Sections.NewFeatureSection;
    public string IndexUrl => Url;
    public int SortOrder => 10;
    
    [CascadingParameter] protected SectionLayout? Layout { get; set; }

    protected override void OnInitialized()
    {
        Layout?.SetSection(Alias);
    }
}
```

### Step 3: Add Navigation Components

```csharp
// Navigation/NewFeatureNavGroup.cs
public class NewFeatureNavGroup : ISectionNavGroup
{
    public string Heading => "New Feature";
    public string Alias => Constants.Sections.SectionNavGroups.NewFeatureNavGroup;
    public int SortOrder => 10;
    public string SectionAlias => Constants.Sections.NewFeatureSection;
    public List<ISectionNavGroupAction> Actions => [];
}
```

### Step 4: Create Editor Components

```csharp
// NewFeatureEditor.razor
@attribute [Route($"{Urls.AdminNewFeatureUpdate}/{{Id:guid}}")]
@layout SectionLayout

<PageTitle>Edit New Feature</PageTitle>

<RadzenPanel Class="rz-mx-auto">
    <EditForm Model="NewFeature" OnSubmit="Save">
        <RadzenRow class="rz-pb-2">
            <RadzenTextBox Style="width: 100%;" Name="Name" Placeholder="Name" 
                           @bind-Value="NewFeature.Name" aria-label="Name"/>
        </RadzenRow>
        
        <RadzenStack Orientation="Orientation.Horizontal" JustifyContent="JustifyContent.Right"
                     AlignItems="AlignItems.Center">
            <RadzenButton ButtonType="ButtonType.Submit" ButtonStyle="ButtonStyle.Success" 
                          Icon="save" Text="Save"/>
        </RadzenStack>
    </EditForm>
</RadzenPanel>

@code {
    [Parameter] public Guid Id { get; set; }
    
    [Inject] public NotificationService NotificationService { get; set; } = null!;
    
    [CascadingParameter] protected SectionLayout? Layout { get; set; }
    
    private NewFeature NewFeature { get; set; } = new();
    
    protected override async Task OnInitializedAsync()
    {
        Layout?.SetSection(Constants.Sections.NewFeatureSection);
        // Load data here
    }
    
    private async Task Save()
    {
        // Save logic here
        NotificationService.Notify(new NotificationMessage { 
            Severity = NotificationSeverity.Success, 
            Summary = "Saved", 
            Duration = 4000 
        });
    }
}
```

## Testing Guidelines

1. **Unit Tests**: Test component logic in isolation
2. **Integration Tests**: Test component interaction with services
3. **UI Tests**: Test user interactions and workflows
4. **Accessibility Testing**: Ensure WCAG compliance
5. **Cross-browser Testing**: Test in supported browsers

## Resources

- [Radzen Blazor Documentation](https://blazor.radzen.com/)
- [Radzen Blazor Components](https://blazor.radzen.com/components)
- [Blazor Documentation](https://learn.microsoft.com/en-us/aspnet/core/blazor/)
- [Material Design Icons](https://fonts.google.com/icons)

## Migration Notes

When updating existing components:

1. **Preserve existing functionality** while adding new features
2. **Maintain backward compatibility** with existing APIs
3. **Update tests** to cover new functionality
4. **Document breaking changes** clearly
5. **Follow established patterns** for consistency

This guide provides the foundation for maintaining consistency and quality in ZauberCMS Admin development while leveraging the power of Radzen Blazor components.