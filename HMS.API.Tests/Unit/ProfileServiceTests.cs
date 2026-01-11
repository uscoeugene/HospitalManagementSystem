using System;
using System.Threading.Tasks;
using FluentAssertions;
using HMS.API.Application.Profile;
using HMS.API.Application.Profile.DTOs;
using HMS.API.Domain.Profile;
using HMS.API.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HMS.API.Tests.Unit
{
    public class ProfileServiceTests
    {
        private async Task<HmsDbContext> CreateInMemoryDb()
        {
            var options = new DbContextOptionsBuilder<HmsDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var db = new HmsDbContext(options);
            await db.Database.EnsureCreatedAsync();
            return db;
        }

        [Fact]
        public async Task CreateOrUpdate_Creates_New_Profile()
        {
            var db = await CreateInMemoryDb();
            var svc = new ProfileService(db);

            var userId = Guid.NewGuid();
            var req = new UpdateUserProfileRequest { FirstName = "John", LastName = "Doe", Email = "john@example.com" };

            var dto = await svc.CreateOrUpdateAsync(userId, req);

            dto.Should().NotBeNull();
            dto.UserId.Should().Be(userId);
            dto.FirstName.Should().Be("John");
            dto.Email.Should().Be("john@example.com");
        }

        [Fact]
        public async Task UpdateForUser_Updates_Existing_Profile()
        {
            var db = await CreateInMemoryDb();
            var svc = new ProfileService(db);

            var userId = Guid.NewGuid();
            var initial = new UserProfile { UserId = userId, FirstName = "Jane", LastName = "Smith" };
            db.UserProfiles.Add(initial);
            await db.SaveChangesAsync();

            var req = new UpdateUserProfileRequest { FirstName = "Janet", Email = "janet@example.com" };
            var updated = await svc.UpdateForUserAsync(userId, req);

            updated.Should().NotBeNull();
            updated!.FirstName.Should().Be("Janet");
            updated.Email.Should().Be("janet@example.com");
        }
    }
}
