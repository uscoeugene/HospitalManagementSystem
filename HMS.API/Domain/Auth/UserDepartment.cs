using System;
using HMS.API.Domain.Common;

namespace HMS.API.Domain.Auth
{
    public class UserDepartment : BaseEntity
    {
        public Guid UserId { get; set; }
        public User User { get; set; } = null!;

        public Guid DepartmentId { get; set; }
        public HMS.API.Domain.Organization.Department Department { get; set; } = null!;
    }
}
