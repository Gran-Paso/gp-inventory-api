using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using GPInventory.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace GPInventory.Infrastructure.Services;

/// <summary>
/// Thin wrapper around the Fintoc REST API v1.
/// Docs: https://docs.fintoc.com/reference
/// </summary>
public class FintocService : IFintocService
{
    private readonly HttpClient _http;
    private readonly ILogger<FintocService> _logger;
    private readonly string _secretKey;

    public FintocService(HttpClient http, IConfiguration config, ILogger<FintocService> logger)
    {
        _http = http;
        _logger = logger;
        _secretKey = config["Fintoc:SecretKey"]
            ?? throw new InvalidOperationException("Fintoc:SecretKey is not configured.");

        _http.BaseAddress = new Uri("https://api.fintoc.com/v1/");
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _secretKey);
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<IEnumerable<FintocMovementDto>> GetMovementsAsync(
        string linkToken,
        string accountId,
        DateTime since,
        DateTime until)
    {
        try
        {
            var sinceStr = since.ToString("yyyy-MM-dd");
            var untilStr = until.ToString("yyyy-MM-dd");

            // GET /v1/accounts/{account_id}/movements?link_token=...&since=...&until=...
            var url = $"accounts/{accountId}/movements" +
                      $"?link_token={Uri.EscapeDataString(linkToken)}" +
                      $"&since={sinceStr}&until={untilStr}";

            _logger.LogInformation("Fetching Fintoc movements for account {AccountId} from {Since} to {Until}",
                accountId, sinceStr, untilStr);

            var response = await _http.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("Fintoc API error {Status}: {Body}", response.StatusCode, body);
                throw new HttpRequestException($"Fintoc API returned {response.StatusCode}: {body}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var movements = JsonSerializer.Deserialize<List<FintocMovementResponse>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new List<FintocMovementResponse>();

            return movements.Select(m => new FintocMovementDto
            {
                Id          = m.Id ?? string.Empty,
                Amount      = Math.Abs(m.Amount),
                Description = m.Description,
                PostDate    = m.PostDate,
                Type        = m.Amount < 0 ? "debit" : "credit"
            }).ToList();
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching Fintoc movements");
            throw new ApplicationException("Error al obtener movimientos de Fintoc.", ex);
        }
    }

    public async Task<IEnumerable<FintocAccountDto>> GetAccountsAsync(string linkToken)
    {
        try
        {
            var url = $"links/{Uri.EscapeDataString(linkToken)}/accounts";
            _logger.LogInformation("Fetching Fintoc accounts for link_token {Token}", linkToken[..Math.Min(8, linkToken.Length)] + "...");

            var response = await _http.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("Fintoc accounts API error {Status}: {Body}", response.StatusCode, body);
                throw new HttpRequestException($"Fintoc API returned {response.StatusCode}: {body}");
            }

            var json = await response.Content.ReadAsStringAsync();
            var accounts = JsonSerializer.Deserialize<List<FintocAccountResponse>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new List<FintocAccountResponse>();

            return accounts.Select(a => new FintocAccountDto
            {
                Id     = a.Id ?? string.Empty,
                Name   = a.Name ?? string.Empty,
                Type   = a.Type,
                Number = a.Number
            }).ToList();
        }
        catch (HttpRequestException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error fetching Fintoc accounts");
            throw new ApplicationException("Error al obtener cuentas de Fintoc.", ex);
        }
    }

    // ─── Internal Fintoc JSON models ──────────────────────────────────────────

    private class FintocMovementResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        /// <summary>Negative = debit (egreso), Positive = credit (ingreso).</summary>
        [JsonPropertyName("amount")]
        public decimal Amount { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("post_date")]
        public DateTime PostDate { get; set; }
    }

    private class FintocAccountResponse
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("type")]
        public string? Type { get; set; }

        [JsonPropertyName("number")]
        public string? Number { get; set; }
    }
}
