using Newtonsoft.Json;

namespace website.Services
{
    public interface ITurnstileService
    {
        Task<bool> VerifyAsync(string? token, string? remoteIp);
    }

    public class TurnstileService : ITurnstileService
    {
        private const string VerifyUrl = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<TurnstileService> _logger;

        public TurnstileService(HttpClient httpClient, IConfiguration configuration, ILogger<TurnstileService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> VerifyAsync(string? token, string? remoteIp)
        {
            var secret = _configuration["Turnstile:SecretKey"];
            if (string.IsNullOrWhiteSpace(secret))
            {
                _logger.LogWarning("Turnstile:SecretKey is not configured — bypassing verification.");
                return true;
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var form = new Dictionary<string, string>
            {
                ["secret"] = secret,
                ["response"] = token
            };
            if (!string.IsNullOrWhiteSpace(remoteIp))
            {
                form["remoteip"] = remoteIp;
            }

            try
            {
                using var content = new FormUrlEncodedContent(form);
                using var response = await _httpClient.PostAsync(VerifyUrl, content);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Turnstile verify HTTP {Status}: {Body}", (int)response.StatusCode, body);
                    return false;
                }

                var result = JsonConvert.DeserializeObject<TurnstileResponse>(body);
                if (result?.Success != true)
                {
                    _logger.LogInformation("Turnstile verification failed: {Errors}",
                        string.Join(",", result?.ErrorCodes ?? Array.Empty<string>()));
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Turnstile verification threw");
                return false;
            }
        }

        private sealed class TurnstileResponse
        {
            [JsonProperty("success")]
            public bool Success { get; set; }

            [JsonProperty("error-codes")]
            public string[]? ErrorCodes { get; set; }
        }
    }
}
