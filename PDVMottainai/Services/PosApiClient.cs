using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PDVMottainai.Services;

public sealed class PosApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public PosApiClient()
    {
        var baseUrl = Environment.GetEnvironmentVariable("POS_API_BASE_URL");
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = "http://127.0.0.1:5097/";

        _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl, UriKind.Absolute), Timeout = TimeSpan.FromSeconds(20) };
    }

    public PosSession? Session { get; private set; }
    private string? _firebaseIdToken;

    public async Task<PosAvailableAccess> AuthenticateFirebaseAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var firebaseApiKey = Environment.GetEnvironmentVariable("POS_FIREBASE_API_KEY");
        if (string.IsNullOrWhiteSpace(firebaseApiKey))
            throw new PosApiException("POS_FIREBASE_API_KEY is not configured.", HttpStatusCode.ServiceUnavailable);

        using var firebaseResponse = await _httpClient.PostAsJsonAsync(
            $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={Uri.EscapeDataString(firebaseApiKey)}",
            new FirebaseSignInRequest(email, password, true), JsonOptions, cancellationToken);
        var firebase = await ReadResponseAsync<FirebaseSignInResponse>(firebaseResponse, cancellationToken);
        _firebaseIdToken = firebase.IdToken;

        using var accessRequest = new HttpRequestMessage(HttpMethod.Get, "auth/available-access");
        accessRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", firebase.IdToken);
        using var accessResponse = await _httpClient.SendAsync(accessRequest, cancellationToken);
        return await ReadResponseAsync<PosAvailableAccess>(accessResponse, cancellationToken);
    }

    public async Task<PosSession> SelectTerminalAsync(string terminalCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_firebaseIdToken))
            throw new PosApiException("Authenticate before selecting a terminal.", HttpStatusCode.Unauthorized);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"auth/login?terminalCode={Uri.EscapeDataString(terminalCode)}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _firebaseIdToken);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var session = await ReadResponseAsync<PosSession>(response, cancellationToken);
        Session = session;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return session;
    }

    public Task<JsonElement> GetCurrentOperatorAsync(CancellationToken cancellationToken = default) =>
        GetAsync<JsonElement>("auth/me", cancellationToken);

    public Task<IReadOnlyList<PosProduct>> GetProductsAsync(string? search, CancellationToken cancellationToken = default) =>
        GetAsync<IReadOnlyList<PosProduct>>($"products?query={Uri.EscapeDataString(search ?? string.Empty)}", cancellationToken);

    public Task<PosProduct> GetProductAsync(int productId, CancellationToken cancellationToken = default) =>
        GetAsync<PosProduct>($"products/{productId}", cancellationToken);

    public Task<PosShift?> GetCurrentShiftAsync(CancellationToken cancellationToken = default) =>
        GetAsync<PosShift?>("shifts/current", cancellationToken);

    public Task<JsonElement> GetTerminalsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<JsonElement>("terminals", cancellationToken);

    public Task<JsonElement> GetCapabilitiesAsync(CancellationToken cancellationToken = default) =>
        GetAsync<JsonElement>("capabilities", cancellationToken);

    public Task<JsonElement> SyncOfflineCatalogAsync(CancellationToken cancellationToken = default) =>
        PostAsync<JsonElement>("offline-catalog/sync", new { }, cancellationToken);

    public Task<JsonElement> GetPromotionsAsync(CancellationToken cancellationToken = default) =>
        GetAsync<JsonElement>("promotions", cancellationToken);

    public Task<JsonElement> OpenShiftAsync(decimal openingAmount, CancellationToken cancellationToken = default) =>
        PostAsync<JsonElement>("shifts", new { openingAmount, observation = (string?)null, idempotencyKey = Guid.NewGuid() }, cancellationToken);

    public Task<JsonElement> GetShiftSummaryAsync(int shiftId, CancellationToken cancellationToken = default) =>
        GetAsync<JsonElement>($"shifts/{shiftId}/summary", cancellationToken);

    public Task<JsonElement> GetShiftAsync(int shiftId, CancellationToken cancellationToken = default) =>
        GetAsync<JsonElement>($"shifts/{shiftId}", cancellationToken);

    public Task<JsonElement> CloseShiftAsync(int shiftId, decimal closingAmount, CancellationToken cancellationToken = default) =>
        PostAsync<JsonElement>($"shifts/{shiftId}/closing", new { closingAmount, observation = (string?)null }, cancellationToken);

    public Task<JsonElement> CreateCashMovementAsync(int shiftId, decimal amount, string reason, bool withdrawal, CancellationToken cancellationToken = default) =>
        PostAsync<JsonElement>($"shifts/{shiftId}/{(withdrawal ? "cash-withdrawals" : "cash-deposits")}",
            new { amount, reason, observation = (string?)null, idempotencyKey = Guid.NewGuid() }, cancellationToken);

    public Task<JsonElement> CreateSaleAsync(int? customerId, CancellationToken cancellationToken = default) =>
        PostAsync<JsonElement>("sales", new { customerId, observation = (string?)null, idempotencyKey = Guid.NewGuid() }, cancellationToken);

    public Task<JsonElement> GetSalesAsync(CancellationToken cancellationToken = default) =>
        GetAsync<JsonElement>("sales", cancellationToken);

    public Task<JsonElement> GetCashMovementsAsync(int shiftId, CancellationToken cancellationToken = default) =>
        GetAsync<JsonElement>($"shifts/{shiftId}/cash-movements", cancellationToken);

    public Task<JsonElement> GetSaleAsync(int saleId, CancellationToken cancellationToken = default) =>
        GetAsync<JsonElement>($"sales/{saleId}", cancellationToken);

    public Task<JsonElement> GetSaleCustomerAsync(int saleId, CancellationToken cancellationToken = default) =>
        GetAsync<JsonElement>($"sales/{saleId}/customer", cancellationToken);

    public Task<JsonElement> AddSaleItemAsync(int saleId, int productId, decimal quantity, CancellationToken cancellationToken = default) =>
        PostAsync<JsonElement>($"sales/{saleId}/items", new { productId, quantity, idempotencyKey = Guid.NewGuid() }, cancellationToken);

    public Task<JsonElement> ChangeSaleItemAsync(int saleId, int itemId, decimal quantity, CancellationToken cancellationToken = default) =>
        SendAsync<JsonElement>(HttpMethod.Patch, $"sales/{saleId}/items/{itemId}", new { quantity, idempotencyKey = Guid.NewGuid() }, cancellationToken);

    public Task<JsonElement> SimulateSaleCompletionAsync(int saleId, CancellationToken cancellationToken = default) =>
        PostAsync<JsonElement>($"sales/{saleId}/completion-simulation", new { }, cancellationToken);

    public Task<JsonElement> SimulateSaleCompletionAsync(int saleId, object payments, CancellationToken cancellationToken = default) =>
        PostAsync<JsonElement>($"sales/{saleId}/completion-simulation/payments", payments, cancellationToken);

    public Task<JsonElement> CreateApprovalAsync(object request, CancellationToken cancellationToken = default) =>
        PostAsync<JsonElement>("approvals", request, cancellationToken);

    public Task<JsonElement> GetApprovalAsync(Guid requestId, CancellationToken cancellationToken = default) =>
        GetAsync<JsonElement>($"approvals/{requestId:D}", cancellationToken);

    public Task<JsonElement> SubmitManagerDecisionAsync(Guid requestId, bool? approved, string? observation, CancellationToken cancellationToken = default) =>
        PostAsync<JsonElement>($"approvals/{requestId:D}/manager-decision", new { approved, observation }, cancellationToken);

    public Task<JsonElement> SearchCustomerAsync(string accountToken, CancellationToken cancellationToken = default) =>
        PostAsync<JsonElement>("customers/search", new { accountToken }, cancellationToken);

    public Task<JsonElement> SearchCustomerByCpfAsync(string cpfToken, CancellationToken cancellationToken = default) =>
        PostAsync<JsonElement>("customers/search-by-cpf", new { cpfToken }, cancellationToken);

    public Task<JsonElement> CreateOfflineSessionAsync(string firebaseUid, string terminalCode, int validHours = 12, CancellationToken cancellationToken = default) =>
        PostAsync<JsonElement>("offline-sessions", new { firebaseUid, terminalCode, validHours }, cancellationToken);

    public async Task<PosSession> SignInOfflineAsync(string firebaseUid, string offlineGrant, int terminalId, CancellationToken cancellationToken = default)
    {
        var session = await PostAsync<PosSession>("auth/offline-login", new { firebaseUid, offlineGrant, terminalId }, cancellationToken);
        Session = session;
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return session;
    }

    public async Task RefreshSessionAsync(CancellationToken cancellationToken = default)
    {
        if (Session is null)
            throw new PosApiException("There is no active session to refresh.", HttpStatusCode.Unauthorized);
        Session = await PostAsync<PosSession>("auth/refresh", new { refreshToken = Session.RefreshToken }, cancellationToken);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Session.AccessToken);
    }

    public Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        if (Session is null)
            return Task.CompletedTask;
        return SignOutCoreAsync(cancellationToken);
    }

    private async Task SignOutCoreAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsync("auth/logout", null, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            Session = null;
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
        else
        {
            await EnsureSuccessAsync(response, cancellationToken);
        }
    }

    private Task<T> GetAsync<T>(string path, CancellationToken cancellationToken) => SendAsync<T>(HttpMethod.Get, path, null, cancellationToken);

    private Task<T> PostAsync<T>(string path, object body, CancellationToken cancellationToken) => SendAsync<T>(HttpMethod.Post, path, body, cancellationToken);

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: JsonOptions);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        return await ReadResponseAsync<T>(response, cancellationToken);
    }

    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
            await EnsureSuccessAsync(response, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NoContent)
            return default!;
        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return value is null ? throw new PosApiException("The POS API returned an empty response.", response.StatusCode) : value;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        string message;
        try
        {
            using var document = JsonDocument.Parse(body);
            message = document.RootElement.TryGetProperty("error", out var error) ? error.GetString() ?? body : body;
        }
        catch (JsonException)
        {
            message = body;
        }
        throw new PosApiException(string.IsNullOrWhiteSpace(message) ? response.ReasonPhrase ?? "Request failed." : message, response.StatusCode);
    }

    private sealed record FirebaseSignInRequest(string Email, string Password, bool ReturnSecureToken);

    private sealed record FirebaseSignInResponse(
        [property: JsonPropertyName("idToken")] string IdToken,
        [property: JsonPropertyName("localId")] string LocalId,
        [property: JsonPropertyName("refreshToken")] string RefreshToken);
}

public sealed record PosSession(
    [property: JsonPropertyName("accessToken")] string AccessToken,
    [property: JsonPropertyName("refreshToken")] string RefreshToken,
    [property: JsonPropertyName("offlineGrant")] string? OfflineGrant,
    [property: JsonPropertyName("operatorProfile")] PosOperator? OperatorProfile,
    [property: JsonPropertyName("permissions")] JsonElement Permissions);

public sealed record PosOperator(
    [property: JsonPropertyName("id")] int EmployeeId,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("storeId")] int StoreId,
    [property: JsonPropertyName("storeName")] string StoreName,
    [property: JsonPropertyName("terminalId")] int TerminalId);

public sealed record PosAvailableAccess(
    [property: JsonPropertyName("employeeId")] int EmployeeId,
    [property: JsonPropertyName("stores")] IReadOnlyList<PosStoreAccess> Stores,
    [property: JsonPropertyName("terminals")] IReadOnlyList<PosTerminalAccess> Terminals);

public sealed record PosStoreAccess(
    [property: JsonPropertyName("storeId")] int StoreId,
    [property: JsonPropertyName("name")] string Name);

public sealed record PosTerminalAccess(
    [property: JsonPropertyName("terminalId")] int TerminalId,
    [property: JsonPropertyName("terminalCode")] string TerminalCode,
    [property: JsonPropertyName("name")] string Name);

public sealed record PosProduct(
    [property: JsonPropertyName("productId")] int ProductId,
    [property: JsonPropertyName("sku")] string Sku,
    [property: JsonPropertyName("barcode")] string? Barcode,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("unitMeasure")] string UnitMeasure,
    [property: JsonPropertyName("normalPrice")] decimal NormalPrice,
    [property: JsonPropertyName("promotionalPrice")] decimal? PromotionalPrice,
    [property: JsonPropertyName("promotionId")] int? PromotionId,
    [property: JsonPropertyName("active")] bool Active,
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("cachedAt")] DateTimeOffset CachedAt);

public sealed class PosShift
{
    [JsonPropertyName("shiftId")] public int ShiftId { get; init; }
    [JsonPropertyName("status")] public string Status { get; init; } = string.Empty;
    [JsonPropertyName("openedAt")] public DateTimeOffset OpenedAt { get; init; }
    [JsonPropertyName("openingAmount")] public decimal OpeningAmount { get; init; }
}

internal sealed class PosApiException(string message, HttpStatusCode statusCode) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}
