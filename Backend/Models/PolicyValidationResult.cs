using Newtonsoft.Json;

namespace Backend.Models {
    public class PolicyValidationResult {
        public string Message { get; set; }
        public bool Valid { get; set; }

        public PolicyValidationResult(bool isValid) {
            Valid = isValid;
        }
        public PolicyValidationResult(bool isValid, string message) {
            Valid = isValid;
            Message = message;
        }
        public override string ToString() {
            return JsonConvert.SerializeObject(this);
        }
    }
}
