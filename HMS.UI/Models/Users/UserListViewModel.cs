using System;
using System.Collections.Generic;

namespace HMS.UI.Models.Users
{
    public class UserListViewModel
    {
        public List<UserListItemViewModel> Items { get; set; } = new List<UserListItemViewModel>();
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public int TotalCount { get; set; } = 0;
        public string? Search { get; set; }
    }
}
