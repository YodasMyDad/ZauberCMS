﻿using MediatR;
using ZauberCMS.Core.Shared.Models;

namespace ZauberCMS.Core.Content.Commands;

public class DataGridContentCommand : BaseQueryContentCommand, IRequest<DataGridResult<Models.Content>>
{
    /// <summary>
    /// Gets how many items to skip. Related to paging and the current page. Usually used with the <see cref="M:System.Linq.Enumerable.Skip``1(System.Collections.Generic.IEnumerable{``0},System.Int32)" /> LINQ method.
    /// </summary>
    public int Skip { get; set; }

    /// <summary>
    /// Gets how many items to take. Related to paging and the current page size. Usually used with the <see cref="M:System.Linq.Enumerable.Take``1(System.Collections.Generic.IEnumerable{``0},System.Int32)" /> LINQ method.
    /// </summary>
    /// <value>The top.</value>
    public int Take { get; set; } = 20;

    /// <summary>Gets the sort expression as a string.</summary>
    public string? Order { get; set; }

    /// <summary>Gets the filter expression as a string.</summary>
    /// <value>The filter.</value>
    public string? Filter { get; set; }
}