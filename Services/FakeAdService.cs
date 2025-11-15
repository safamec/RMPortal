using System;
using System.Collections.Generic;
using System.Linq;

namespace RMPortal.Services
{
    public record FakeUser(string Sam, string DisplayName, string Email, string Department, string ManagerSam, string[] Groups);

    public interface IFakeAdService
    {
        FakeUser? GetUser(string sam);
        IEnumerable<FakeUser> GetAllUsers();
        string? GetManagerSam(string sam);
        IEnumerable<FakeUser> GetUsersInGroup(string group);
        IEnumerable<string> GetGroupsForUser(string sam);
    }

    public class FakeAdService : IFakeAdService
    {
        private readonly List<FakeUser> _users = new()
        {
            new FakeUser("safa","safa Ahmed","safaa3568@gmail.com","HR","bob",   new[] { "RM_Requesters" }),
            new FakeUser("bob","Bob Saleh","safaa3568@gmail.com","HR","carol",       new[] { "RM_LineManagers" }),
            new FakeUser("dave","Dave Ali","dave@local.test","IT","",           new[] { "RM_ITAdmins" })
        };

        public FakeUser? GetUser(string sam)
            => _users.FirstOrDefault(u => u.Sam.Equals(sam, StringComparison.OrdinalIgnoreCase));

        public IEnumerable<FakeUser> GetAllUsers() => _users;

        public string? GetManagerSam(string sam) => GetUser(sam)?.ManagerSam;

        public IEnumerable<FakeUser> GetUsersInGroup(string group)
            => _users.Where(u => u.Groups.Contains(group));

        public IEnumerable<string> GetGroupsForUser(string sam)
            => GetUser(sam)?.Groups ?? Enumerable.Empty<string>();
    }
}
