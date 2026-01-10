using System;
using Microsoft.AspNetCore.Authorization;

namespace HMS.API.Security
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true, Inherited = true)]
    public class HasPermissionAttribute : AuthorizeAttribute
    {
        public HasPermissionAttribute(string permission)
        {
            Policy = $"permission:{permission}";
        }
    }
}