using System.Collections.Generic;

namespace Aporta.Shared.Models;

public class PaginatedItemsDto<T>
{
    public IEnumerable<T> Items { get; init;  }
    public int TotalItems { get; init; }
    
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public int PageNumber { get;  init; }
    
    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public int PageSize { get; init;  }
}