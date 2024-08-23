using MediatR;
using ZauberCMS.Core.Languages.Models;

namespace ZauberCMS.Core.Languages.Commands;

public class GetCachedAllLanguageDictionariesCommand : IRequest<Dictionary<string, Dictionary<string, string>>>
{
    
}