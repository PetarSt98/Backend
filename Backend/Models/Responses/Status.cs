using Newtonsoft.Json;

namespace Backend.Models.Responses {
    public class Status {
        public bool Success { get; set; }
        public string Message { get; set; }

        public Status(bool success) {
            Success = success;
        }

        public Status(bool success, string msg) {
            Success = success;
            Message = msg;
        }

        public override string ToString() {
            return JsonConvert.SerializeObject(this);
        }
    }
}
