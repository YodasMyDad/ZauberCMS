﻿using MediatR;
using ZauberCMS.Core.Membership.Models;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Commands;

public class SaveContentCommand : IRequest<HandlerResult<Models.Content>>
{
    public Models.Content? Content { get; set; }
    public List<Role> Roles { get; set; } = [];
    public bool UpdateContentRoles { get; set; }
    public bool ExcludePropertyData { get; set; }
    
    /// <summary>
    /// This saves the content as unpublished only and leaves the original content intact
    /// </summary>
    public bool SaveUnpublishedOnly { get; set; }
}