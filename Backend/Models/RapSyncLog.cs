using Newtonsoft.Json;

namespace Backend.Models {
    public class RapSyncLog {
        public string RapName { get; set; }
        public bool Success { get; set; }
        public string[] SyncResources { get; set; }
        public override string ToString() {
            return JsonConvert.SerializeObject(this);
        }
    }
}
