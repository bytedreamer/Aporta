using System.Collections.Generic;

namespace Aporta.Shared.Models;

public class PaginatedItemsDto<T>
{
    public IEnumerable<T> Items { get; init;  }
    public int TotalItems { get; init; }
    public int PageNumber { get;  init; }
    public int PageSize { get; init;  }
}