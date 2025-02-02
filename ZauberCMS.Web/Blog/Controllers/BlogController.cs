using MediatR;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ZauberCMS.Core.Content.Commands;
using ZauberCMS.Core.Extensions;
using ZauberCMS.Core.Settings;
using ZauberCMS.Routing.Controllers;
using ZauberCMS.Web.Blog.Models;

namespace ZauberCMS.Web.Blog.Controllers;

public class BlogController(ILogger<BlogController> logger, IOptions<ZauberSettings> options, IMediator mediator) 
    : ZauberRenderController(logger, options)
{
    public async Task<IActionResult> Blog(int? p = null)
    {
        var viewModel = new BlogViewModel(CurrentPage!);
        
        viewModel.HeaderImage = await viewModel.GetMedia("HeaderImage", mediator, "/assets/img/about-bg.jpg");
        
        p ??= 0;
        var amountPerPage = viewModel.GetValue<int>("AmountPerPage");
        var queryContentCommand = new QueryContentCommand
        {
            OrderBy = GetContentsOrderBy.DateCreated,
            AmountPerPage = amountPerPage,
            WhereClause = content => content.ParentId == viewModel.Id,
            PageIndex = p.Value
        };
        viewModel.BlogPosts = await mediator.Send(queryContentCommand);
        
        return CurrentView(viewModel);
    }
}