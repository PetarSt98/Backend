using System.Collections.Generic;

namespace NetCoreOidcExample.Models {
    public class User {
        public string AccountName { get; set; }
        public string Name { get; set; }
        public List<string> Roles { get; set; }
    }
}
