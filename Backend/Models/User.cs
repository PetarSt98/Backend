using System.Collections.Generic;
using Newtonsoft.Json;

namespace Backend.Models {
    public class User {
        public string AccountName { get; }
        public string Name { get; }
        public string EmailAddress { get; }
        public List<string> Roles { get; } = new List<string>();
        public override string ToString() {
            return JsonConvert.SerializeObject(this);
        }
        public User(string accName, string name, string emailAddress) {
            AccountName = accName;
            Name = name;
            EmailAddress = emailAddress;
        }

        public User(List<string> roles) {
            AccountName = "Worker";
            Name = "Worker";
            EmailAddress = "";
            Roles.AddRange(roles);
        }

        public void AddRole(string role) {
            Roles.Add(role);
        }
    }
}
