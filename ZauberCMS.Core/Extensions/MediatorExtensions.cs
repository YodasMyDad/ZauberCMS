using System.Text.Json;
using MediatR;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Data.Commands;
using ZauberCMS.Core.Data.Models;
using ZauberCMS.Core.Media.Commands;
using ZauberCMS.Core.Membership.Commands;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Settings;

namespace ZauberCMS.Core.Extensions;

public static class MediatorExtensions
{
    /// <summary>
    /// Retrieves the global settings from the mediator.
    /// </summary>
    /// <param name="mediator">The mediator instance used to send the GetGlobalDataCommand.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an instance of GlobalSettings.</returns>
    public static async Task<GlobalSettings> GetGlobalSettings(this IMediator mediator)
    {
        var globalData = await mediator.Send(new GetGlobalDataCommand { Alias = Constants.GlobalSettings });
        if (globalData?.Data != null)
        {
            return globalData.GetValue<GlobalSettings>() ?? new GlobalSettings();
        }

        return new GlobalSettings();
    }

    /// <summary>
    /// Saves the global settings using the mediator.
    /// </summary>
    /// <param name="mediator">The mediator instance used to send the SaveGlobalDataCommand.</param>
    /// <param name="settings">The global settings to be saved.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains a boolean indicating the success of the save operation.</returns>
    public static async Task<bool> SaveGlobalSettings(this IMediator mediator, GlobalSettings settings)
    {
        var result = await mediator.Send(new SaveGlobalDataCommand
        {
            Alias = Constants.GlobalSettings,
            Data = JsonSerializer.Serialize(settings)
        });
        return result.Success;
    }

    /// <summary>
    /// Retrieves global data from the mediator using the specified alias.
    /// </summary>
    /// <param name="mediator">The mediator instance used to send the GetGlobalDataCommand.</param>
    /// <param name="alias">The alias associated with the global data to be retrieved.</param>
    /// <typeparam name="T">The type to which the global data should be converted.</typeparam>
    /// <returns>A task that represents the asynchronous operation. The task result contains an instance of the specified type.</returns>
    public static async Task<T?> GetGlobalData<T>(this IMediator mediator, string alias)
    {
        var globalData = await mediator.Send(new GetGlobalDataCommand { Alias = alias });
        if (globalData?.Data != null)
        {
            return globalData.GetValue<T>();
        }

        return default;
    }
    
    /// <summary>
    /// Gets the currently logged in user
    /// </summary>
    /// <param name="mediator"></param>
    /// <returns></returns>
    public static async Task<User?> GetCurrentUser(this IMediator mediator)
    {
        return await mediator.Send(new GetCurrentUserCommand());
    }

    /// <summary>
    /// Retrieves a user based on the specified ID.
    /// </summary>
    /// <param name="mediator">The mediator instance used to send the GetUserCommand.</param>
    /// <param name="id">The unique identifier of the user to retrieve. If null, no user will be returned.</param>
    /// <param name="cached">Indicates whether to use cached data. Default value is true.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an instance of User, or null if no user is found.</returns>
    public static async Task<User?> GetUser(this IMediator mediator, Guid? id, bool cached = true)
    {
        if (id != null)
        {
            return await mediator.Send(new GetUserCommand { Id = id.Value, Cached = cached});
        }

        return null;
    }

    /// <summary>
    /// Retrieves the media with the specified ID from the mediator.
    /// </summary>
    /// <param name="mediator">The mediator instance used to send the GetMediaCommand.</param>
    /// <param name="id">The ID of the media to retrieve.</param>
    /// <param name="cached">A boolean value indicating whether the cached version should be retrieved if available.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an instance of Media or null if the ID is null or the media is not found.</returns>
    public static async Task<Media.Models.Media?> GetMedia(this IMediator mediator, Guid? id, bool cached = true)
    {
        if (id != null)
        {
            return await mediator.Send(new GetMediaCommand { Id = id.Value, Cached = cached });
        }

        return null;
    }

    /// <summary>
    /// Retrieves content information based on the provided content ID.
    /// </summary>
    /// <param name="mediator">The mediator instance used to send the GetContentCommand.</param>
    /// <param name="id">The unique identifier of the content to retrieve.</param>
    /// <param name="cached">Indicates whether to use cached content data or not.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains an instance of Content if found; otherwise, null.</returns>
    public static async Task<Content.Models.Content?> GetContent(this IMediator mediator, Guid? id, bool cached = true)
    {
        if (id != null)
        {
            return await mediator.Send(new GetContentCommand { Id = id.Value, Cached = cached });
        }

        return null;
    }
}