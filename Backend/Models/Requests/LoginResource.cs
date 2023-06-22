using Newtonsoft.Json;

namespace Backend.Models.Requests {
    public class LoginResource {
        public string Login { get; set; }
        public string ResourceName { get; set; }

        public void Deconstruct(out string login, out string resourceName) {
            login = Login;
            resourceName = ResourceName.ToUpper();
        }
        
        public override string ToString() {
            return JsonConvert.SerializeObject(this);
        }
    }
}
