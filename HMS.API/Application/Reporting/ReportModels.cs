using System;
using System.Collections.Generic;

namespace HMS.API.Application.Reporting
{
    public abstract class ReportDtoBase { }

    public class PagedReportResult<T> where T : ReportDtoBase
    {
        public IEnumerable<T> Items { get; set; } = Array.Empty<T>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
