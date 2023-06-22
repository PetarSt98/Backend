using Newtonsoft.Json;

namespace Backend.Models {
    public class RequestValidationResult {
        public bool IsValid { get; set; }
        public string ValidationMessage { get; set; }

        public RequestValidationResult(bool isValid) {
            IsValid = isValid;
            ValidationMessage = "";
        }

        public RequestValidationResult(bool isValid, string validationMessage) {
            IsValid = isValid;
            ValidationMessage = validationMessage;
        }

        public RequestValidationResult(string validationMessage) {
            IsValid = false;
            ValidationMessage = validationMessage;
        }

        public override string ToString() {
            return JsonConvert.SerializeObject(this);
        }
    }
}
