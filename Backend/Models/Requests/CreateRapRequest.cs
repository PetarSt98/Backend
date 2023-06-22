using System.Collections.Generic;
using Newtonsoft.Json;

namespace Backend.Models.Requests {
    public class CreateRapRequest {
        public string Login { get; set; }
        public string ResourceOwner { get; set; }
        public List<string> Resources { get; set; }

        public override string ToString() {
            return JsonConvert.SerializeObject(this);
        }
    }
}
