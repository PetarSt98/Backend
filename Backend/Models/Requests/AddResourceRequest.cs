using Newtonsoft.Json;

namespace Backend.Models.Requests {
    public class AddResourceRequest {
        public string Login { get; set; }
        public string ResourceName { get; set; }
        public string ResourceOwner { get; set; }

        public void Deconstruct(out string resourceName, out string resourceOwner, out string login) {
            login = Login.Trim();
            resourceName = ResourceName.ToUpper().Trim();
            resourceOwner = ResourceOwner;
        }
        
        public override string ToString() {
            return JsonConvert.SerializeObject(this);
        }
    }
}