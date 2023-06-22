using Newtonsoft.Json;

namespace Backend.Models.Requests {
    public class LoginResourceState : LoginResource {
        public bool Invalid { get; set; }
        public override string ToString() {
            return JsonConvert.SerializeObject(this);
        }
    }
}
