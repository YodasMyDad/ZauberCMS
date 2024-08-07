﻿using MediatR;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Membership.Commands;

public class SaveUserCommand : IRequest<HandlerResult<User>>
{
    public User? User { get; set; }
    public string? Password { get; set; }
    public List<string>? Roles { get; set; }
}