using Microsoft.AspNetCore.Components;

namespace ZauberCMS.Web.TestData;

public interface IContentProperty
{
    string Value { get; set; }
    EventCallback<string> ValueChanged { get; set; }
}