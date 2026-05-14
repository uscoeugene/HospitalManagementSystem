using System;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Common
{
    public class AppSetting : BaseEntity
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
