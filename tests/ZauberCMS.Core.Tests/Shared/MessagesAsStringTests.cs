using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Tests.Shared;

/// <summary>
/// ZA-283: with several messages, <c>MessagesAsString</c> gave only the success ones, so a refusal for more than
/// one reason (ASP.NET Identity's password rules, say) showed as an empty alert in "Your account" and as an empty
/// notification elsewhere.
/// </summary>
public class MessagesAsStringTests
{
    [Fact]
    public void Several_refusals_are_all_shown_one_per_line_as_written()
    {
        List<ResultMessage> messages =
        [
            new("Passwords must be at least 8 characters.", ResultMessageType.Error),
            new("Passwords must have at least one digit ('0'-'9').", ResultMessageType.Error)
        ];

        Assert.Equal("Passwords must be at least 8 characters." + Environment.NewLine +
                     "Passwords must have at least one digit ('0'-'9').", messages.MessagesAsString());
    }

    [Fact]
    public void One_message_is_itself_and_none_is_empty()
    {
        Assert.Equal("Saved", new List<ResultMessage> { new("Saved", ResultMessageType.Success) }.MessagesAsString());
        Assert.Equal(string.Empty, new List<ResultMessage>().MessagesAsString());
    }
}
