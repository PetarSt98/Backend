using Newtonsoft.Json;
using System.Threading.Tasks;

namespace Backend.ExchangeTokenService
{
    public class TokenResponse
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }

        // Add any other properties here that are in the server's response
    }
    public class TokenService : ITokenService
    {
        private readonly HttpClient _httpClient;
        private string _token;

        public TokenService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<string> GetTokenAsync()
        {
            if (!string.IsNullOrEmpty(_token))
            {
                return _token;
            }

            var parameters = new Dictionary<string, string>
        {
            {"grant_type", "client_credentials"},
            {"client_id", "yourClientId"},
            {"client_secret", "yourClientSecret"},
            {"audience", "yourTargetApplication"}
        };

            var response = await _httpClient.PostAsync("https://auth.cern.ch/auth/realms/cern/api-access/token", new FormUrlEncodedContent(parameters));

            if (!response.IsSuccessStatusCode)
            {
                // Handle error - log this or throw an exception
            }

            var content = await response.Content.ReadAsStringAsync();
            var tokenResponse = JsonConvert.DeserializeObject<TokenResponse>(content); // TokenResponse is a class you define to match the structure of the response from the server.
            _token = tokenResponse.AccessToken; // Where AccessToken is a property of your TokenResponse class

            return _token;
        }
    }
}