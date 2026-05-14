using System.Collections.Generic;

namespace HMS.UI.Models.Reporting
{
    public class PagedReportResult<T>
    {
        public T[] Items { get; set; } = System.Array.Empty<T>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
