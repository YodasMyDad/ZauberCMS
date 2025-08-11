using System.Text.Json;
using ZauberCMS.Core.Content.Interfaces;
using ZauberCMS.Core.Content.Parameters;
using ZauberCMS.Core.Data.Interfaces;
using ZauberCMS.Core.Data.Parameters;
using ZauberCMS.Core.Media.Interfaces;
using ZauberCMS.Core.Media.Parameters;
using ZauberCMS.Core.Membership.Interfaces;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Membership.Parameters;
using ZauberCMS.Core.Settings;

namespace ZauberCMS.Core.Extensions;

public static class ServiceExtensions
{
    /// <summary>
    /// Retrieves the global settings from the mediator.
    /// </summary>
    /// <param name="dataService">The dataService instance</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an instance of GlobalSettings.</returns>
    public static async Task<GlobalSettings> GetGlobalSettings(this IDataService dataService)
    {
        var globalData = await dataService.GetGlobalDataAsync(new GetGlobalDataParameters { Alias = Constants.GlobalSettings });
        if (globalData?.Data != null)
        {
            return globalData.GetValue<GlobalSettings>() ?? new GlobalSettings();
        }

        return new GlobalSettings();
    }

    /// <summary>
    /// Saves the global settings using the mediator.
    /// </summary>
    /// <param name="dataService">The dataService instance.</param>
    /// <param name="settings">The global settings to be saved.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating the success of the save operation.</returns>
    public static async Task<bool> SaveGlobalSettings(this IDataService dataService, GlobalSettings settings)
    {
        var result = await dataService.SaveGlobalDataAsync(new SaveGlobalDataParameters
        {
            Alias = Constants.GlobalSettings,
            Data = JsonSerializer.Serialize(settings)
        });
        return result.Success;
    }

    /// <summary>
    /// Retrieves global data from the mediator using the specified alias.
    /// </summary>
    /// <param name="dataService">The dataService instance used to send the GetGlobalDataCommand.</param>
    /// <param name="alias">The alias associated with the global data to be retrieved.</param>
    /// <typeparam name="T">The type to which the global data should be converted.</typeparam>
    /// <returns>A task that represents the asynchronous operation. The task result contains an instance of the specified type.</returns>
    public static async Task<T?> GetGlobalData<T>(this IDataService dataService, string alias)
    {
        var globalData = await dataService.GetGlobalDataAsync(new GetGlobalDataParameters { Alias = alias });
        if (globalData?.Data != null)
        {
            return globalData.GetValue<T>();
        }

        return default;
    }
    
    /// <summary>
    /// Gets the currently logged in user
    /// </summary>
    /// <param name="membershipService"></param>
    /// <returns></returns>
    public static async Task<User?> GetCurrentUser(this IMembershipService membershipService)
    {
        return await membershipService.GetCurrentUserAsync();
    }

    /// <summary>
    /// Retrieves a user based on the specified ID.
    /// </summary>
    /// <param name="membershipService">The membershipService instance used.</param>
    /// <param name="id">The unique identifier of the user to retrieve. If null, no user will be returned.</param>
    /// <param name="cached">Indicates whether to use cached data. Default value is true.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an instance of User, or null if no user is found.</returns>
    public static async Task<User?> GetUser(this IMembershipService membershipService, Guid? id, bool cached = true)
    {
        if (id != null)
        {
            return await membershipService.GetUserAsync(new GetUserParameters { Id = id.Value, Cached = cached});
        }

        return null;
    }

    /// <summary>
    /// Retrieves the media with the specified ID from the mediator.
    /// </summary>
    /// <param name="mediaService">The mediaService instance used.</param>
    /// <param name="id">The ID of the media to retrieve.</param>
    /// <param name="cached">A boolean value indicating whether the cached version should be retrieved if available.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an instance of Media or null if the ID is null or the media is not found.</returns>
    public static async Task<Media.Models.Media?> GetMedia(this IMediaService mediaService, Guid? id, bool cached = true)
    {
        if (id != null)
        {
            return await mediaService.GetMediaAsync(new GetMediaParameters { Id = id.Value, Cached = cached });
        }

        return null;
    }

    /// <summary>
    /// Retrieves content information based on the provided content ID.
    /// </summary>
    /// <param name="contentService">The content service</param>
    /// <param name="id">The unique identifier of the content to retrieve.</param>
    /// <param name="cached">Indicates whether to use cached content data or not.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an instance of Content if found; otherwise, null.</returns>
    public static async Task<Content.Models.Content?> GetContent(this IContentService contentService, Guid? id, bool cached = true)
    {
        if (id != null)
        {
            return await contentService.GetContentAsync(new GetContentParameters { Id = id.Value, Cached = cached });
        }

        return null;
    }
}