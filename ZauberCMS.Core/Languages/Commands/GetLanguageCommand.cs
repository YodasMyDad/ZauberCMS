using MediatR;
using ZauberCMS.Core.Languages.Models;

namespace ZauberCMS.Core.Languages.Commands;

public class GetLanguageCommand : IRequest<Language>
{
    public Guid? Id { get; set; }
    public bool AsNoTracking { get; set; }
    public string? LanguageIsoCode { get; set; } 
}