using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HMS.API.Application.Auth.DTOs
{
    public class CreateRoleRequest
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;
    }

    public class RoleResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public IEnumerable<string> Permissions { get; set; } = Array.Empty<string>();
    }

    public class AddPermissionRequest
    {
        [Required]
        [MaxLength(200)]
        public string Code { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;
    }
}