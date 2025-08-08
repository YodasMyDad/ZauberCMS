using System;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Media.Parameters;

public class DeleteMediaParameters
{
    public Guid MediaId { get; set; }
    public bool DeleteFile { get; set; }
}