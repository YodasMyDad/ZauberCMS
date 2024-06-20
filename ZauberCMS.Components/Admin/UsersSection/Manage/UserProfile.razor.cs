using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Radzen;
using ZauberCMS.Components.Admin.Layout;
using ZauberCMS.Core;
using ZauberCMS.Core.Data;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Membership.Commands;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Components.Admin.UsersSection.Manage
{
    [Authorize]
    public partial class UserProfile : ComponentBase
    {
        [Inject] public AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
        [Inject] public NotificationService NotificationService { get; set; } = default!;
        [Parameter] public CreateUpdateUserCommand CreateUpdateUserCommand { get; set; } = new();
        
        [Inject] public IServiceProvider ServiceProvider { get; set; } = default!;

        private User? CurrentUser { get; set; }
        private bool Loading { get; set; }
        private bool UserIsExternalLogin { get; set; }

        [CascadingParameter] protected SectionLayout? Layout { get; set; }
        
        protected override async Task OnInitializedAsync()
        {
            Loading = true;
            Layout?.SetSection(Constants.Sections.UsersSection);
            var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();

            using var scope = ServiceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
            
            await SetCuUserCommand(authState.User.GetUserId(), dbContext);

            // Check if this is a refresh and look for messages to display
            // This is shite, but it's a hack to get around RefreshSignInAsync
            bool refresh;
            NavigationManager.TryGetQueryString("refresh", out refresh);
            if (refresh)
            {
                var tempMessages = CurrentUser?.ExtendedData.GetTempUiMessages();
                var resultMessages = tempMessages as ResultMessage[] ?? tempMessages?.ToArray();
                if (resultMessages?.Any() == true)
                {
                    foreach (var message in resultMessages)
                    {
                        if (message.Message != null) NotificationService.ShowNotification(message.Message, NotificationSeverity.Info);
                    }
                }
            }

            Loading = false;
        }

        private async Task SetCuUserCommand(Guid userId, ZauberDbContext context)
        {
            CurrentUser = await context.Users.FirstOrDefaultAsync(x => x.Id == userId);
            if (CurrentUser != null)
            {
                var logins = context.UserLogins.AsNoTracking().Where(l => l.UserId == userId);
                CreateUpdateUserCommand.User = CurrentUser;
                UserIsExternalLogin = logins.Any();
            }
        }

        private async void HandleValidSubmit()
        {
            Loading = true;

            // Set email if external login as it's not shown on the page
            if (UserIsExternalLogin)
            {
                CreateUpdateUserCommand.User.Email = CurrentUser.Email;
            }

            using var scope = ServiceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ZauberDbContext>();
            var mediatr = scope.ServiceProvider.GetRequiredService<IMediator>();
            
            var result = await mediatr.Send(CreateUpdateUserCommand);

            // Yes this is really shit for
            // Cannot refresh RefreshSignInAsync in Blazor, so have to redirect to a razor page and then 
            // straight back.
            if (result.RefreshSignIn)
            {
                NavigationManager.NavigateTo(Constants.Urls.Account.RefreshSignIn, true);
            }
            else
            {
                await SetCuUserCommand(CurrentUser.Id, dbContext);

                if (result.Messages.Count > 0)
                {
                    foreach (var resultMessage in result.Messages)
                    {
                        if (resultMessage.Message != null) NotificationService.ShowNotification(resultMessage.Message, NotificationSeverity.Info);
                    }
                }
                else
                {
                    if (result.Success)
                    {
                        NotificationService.ShowSuccessNotification("Updated successfully");
                    }
                    else
                    {
                        NotificationService.ShowErrorNotification("There was an issue updating, please check the logs");
                    }
                }
            }

            Loading = false;
            await InvokeAsync(StateHasChanged);
        }
    }
}