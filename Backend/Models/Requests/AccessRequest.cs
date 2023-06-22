using Newtonsoft.Json;

namespace Backend.Models.Requests {
    public class AccessRequest {
        public string UserName { get; set; }
        public string RapOwner { get; set; }
        public string ResourceName { get; set; }

        public void Deconstruct(out string userName, out string rapOwner, out string resourceName) {
            userName = UserName;
            rapOwner = RapOwner;
            resourceName = ResourceName;
        }
        
        public override string ToString() {
            return JsonConvert.SerializeObject(this);
        }
    }
}
