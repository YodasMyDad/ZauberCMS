using System.Text.Json;
using MediatR;
using ZauberCMS.Core.Data.Commands;
using ZauberCMS.Core.Data.Models;
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
}