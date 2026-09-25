using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Portfolio.TaskFlowApi.Api.Contracts.Auth;
using Portfolio.TaskFlowApi.Api.Contracts.Projects;
using Portfolio.TaskFlowApi.Api.Contracts.Tasks;
using Portfolio.TaskFlowApi.Infrastructure.Persistence;
using DomainTaskStatus = Portfolio.TaskFlowApi.Core.Domain.TaskStatus;

namespace Portfolio.TaskFlowApi.Tests;

public sealed class AuthenticationAndOwnershipTests :
    IClassFixture<ApiWebApplicationFactory>,
    IAsyncLifetime,
    IDisposable
{
    private const string TestOnlyPassword = "test-only-password-123";
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly ApiWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthenticationAndOwnershipTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task RegisterCreatesSafeNormalizedUser()
    {
        var response = await RegisterAsync(_client, "  New.User@Example.Test  ");
        var user = await ReadAsync<UserResponse>(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("new.user@example.test", user.Email);
        Assert.Equal(DateTimeKind.Utc, user.CreatedAtUtc.Kind);
    }

    [Fact]
    public async Task RegisterRejectsCaseInsensitiveDuplicateEmail()
    {
        await RegisterAsync(_client, "duplicate@example.test");

        var response = await RegisterAsync(_client, "DUPLICATE@EXAMPLE.TEST");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task RegisterRejectsInvalidEmail()
    {
        var response = await RegisterAsync(_client, "not-an-email");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterRejectsShortPassword()
    {
        var response = await RegisterAsync(_client, "short@example.test", "short");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterRejectsPasswordAboveMaximumLength()
    {
        var response = await RegisterAsync(_client, "long@example.test", new string('p', 129));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterResponseNeverContainsPasswordHash()
    {
        var response = await RegisterAsync(_client, "safe-register@example.test");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RegistrationStoresStandardHashInsteadOfPlaintextPassword()
    {
        await RegisterAsync(_client, "hashed@example.test");

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TaskFlowDbContext>();
        var user = Assert.Single(dbContext.Users);

        Assert.NotEqual(TestOnlyPassword, user.PasswordHash);
        Assert.NotEmpty(user.PasswordHash);
    }

    [Fact]
    public async Task LoginReturnsJwtAndSafeUser()
    {
        await RegisterAsync(_client, "login@example.test");

        var response = await LoginAsync(_client, "login@example.test");
        var auth = await ReadAsync<AuthResponse>(response);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty(auth.AccessToken);
        Assert.True(auth.ExpiresAtUtc > DateTime.UtcNow);
        Assert.Equal("login@example.test", auth.User.Email);
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoginWithWrongPasswordReturnsUnauthorized()
    {
        await RegisterAsync(_client, "wrong-password@example.test");

        var response = await LoginAsync(_client, "wrong-password@example.test", "incorrect-test-password");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task LoginWithUnknownEmailReturnsUnauthorized()
    {
        var response = await LoginAsync(_client, "unknown@example.test");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InvalidCredentialsUseSameSafeMessage()
    {
        await RegisterAsync(_client, "known@example.test");

        var wrongPassword = await LoginAsync(_client, "known@example.test", "incorrect-test-password");
        var unknownEmail = await LoginAsync(_client, "unknown@example.test");
        var wrongProblem = await ReadProblemDetailAsync(wrongPassword);
        var unknownProblem = await ReadProblemDetailAsync(unknownEmail);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(wrongProblem, unknownProblem);
    }

    [Fact]
    public async Task JwtContainsSubjectEmailIssuedAndExpiryClaims()
    {
        var auth = await RegisterLoginAndAuthorizeAsync(_client, "claims@example.test");

        var token = new JwtSecurityTokenHandler().ReadJwtToken(auth.AccessToken);

        Assert.Equal(auth.User.Id.ToString(), token.Subject);
        Assert.Equal("claims@example.test", token.Claims.Single(claim => claim.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Contains(token.Claims, claim => claim.Type == JwtRegisteredClaimNames.Iat);
        Assert.True(token.ValidTo > DateTime.UtcNow);
    }

    [Fact]
    public async Task ProtectedEndpointWithoutTokenReturnsProblemDetailsUnauthorized()
    {
        var response = await _client.GetAsync("/api/projects");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ProtectedEndpointWithMalformedTokenReturnsUnauthorized()
    {
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-valid-jwt");

        var response = await _client.GetAsync("/api/projects");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpointWithExpiredTokenReturnsUnauthorized()
    {
        var expiredAtUtc = DateTime.UtcNow.AddMinutes(-2);
        var expiredToken = new JwtSecurityToken(
            issuer: "Portfolio.TaskFlowApi.Tests",
            audience: "Portfolio.TaskFlowApi.Tests.Client",
            claims: [new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString())],
            notBefore: expiredAtUtc.AddMinutes(-5),
            expires: expiredAtUtc,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ApiWebApplicationFactory.TestOnlySigningKey)),
                SecurityAlgorithms.HmacSha256));

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            new JwtSecurityTokenHandler().WriteToken(expiredToken));

        var response = await _client.GetAsync("/api/projects");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ValidTokenAllowsProtectedEndpointAccess()
    {
        await RegisterLoginAndAuthorizeAsync(_client, "authorized@example.test");

        var response = await _client.GetAsync("/api/projects");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task TestDatabaseHasNoPendingMigrations()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TaskFlowDbContext>();

        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();

        Assert.Empty(pendingMigrations);
    }

    [Fact]
    public async Task OpenApiDocumentIncludesBearerSecurityForProtectedEndpoints()
    {
        var response = await _client.GetAsync("/swagger/v1/swagger.json");
        var document = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"Bearer\"", document, StringComparison.Ordinal);
        Assert.Contains("\"bearer\"", document, StringComparison.Ordinal);
        Assert.Contains("\"security\"", document, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProjectListContainsOnlyCurrentUsersProjects()
    {
        using var userA = await CreateAuthenticatedClientAsync("owner-a@example.test");
        using var userB = await CreateAuthenticatedClientAsync("owner-b@example.test");
        await CreateProjectAsync(userA, "A project");
        await CreateProjectAsync(userB, "B project");

        var projects = await userA.GetFromJsonAsync<List<ProjectResponse>>("/api/projects", JsonOptions);

        var project = Assert.Single(projects!);
        Assert.Equal("A project", project.Name);
    }

    [Fact]
    public async Task OtherUserCannotGetProject()
    {
        var resources = await CreateOwnedProjectAsync();
        using var userA = resources.Owner;
        using var userB = resources.Other;

        var response = await userB.GetAsync($"/api/projects/{resources.Project.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OtherUserCannotUpdateProject()
    {
        var resources = await CreateOwnedProjectAsync();
        using var userA = resources.Owner;
        using var userB = resources.Other;

        var response = await userB.PutAsJsonAsync(
            $"/api/projects/{resources.Project.Id}",
            new UpdateProjectRequest { Name = "Hijacked" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var unchanged = await userA.GetFromJsonAsync<ProjectResponse>($"/api/projects/{resources.Project.Id}", JsonOptions);
        Assert.Equal("Owned project", unchanged?.Name);
    }

    [Fact]
    public async Task OtherUserCannotDeleteProject()
    {
        var resources = await CreateOwnedProjectAsync();
        using var userA = resources.Owner;
        using var userB = resources.Other;

        var response = await userB.DeleteAsync($"/api/projects/{resources.Project.Id}");
        var ownerResponse = await userA.GetAsync($"/api/projects/{resources.Project.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ownerResponse.StatusCode);
    }

    [Fact]
    public async Task OtherUserCannotCreateTaskInProject()
    {
        var resources = await CreateOwnedProjectAsync();
        using var userA = resources.Owner;
        using var userB = resources.Other;

        var response = await userB.PostAsJsonAsync(
            $"/api/projects/{resources.Project.Id}/tasks",
            new CreateTaskRequest { Title = "Unauthorized task" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OtherUserCannotGetTask()
    {
        var resources = await CreateOwnedTaskAsync();
        using var userA = resources.Owner;
        using var userB = resources.Other;

        var response = await userB.GetAsync($"/api/tasks/{resources.Task.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OtherUserCannotUpdateTask()
    {
        var resources = await CreateOwnedTaskAsync();
        using var userA = resources.Owner;
        using var userB = resources.Other;

        var response = await userB.PutAsJsonAsync(
            $"/api/tasks/{resources.Task.Id}",
            new UpdateTaskRequest { Title = "Hijacked" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OtherUserCannotDeleteTask()
    {
        var resources = await CreateOwnedTaskAsync();
        using var userA = resources.Owner;
        using var userB = resources.Other;

        var response = await userB.DeleteAsync($"/api/tasks/{resources.Task.Id}");
        var ownerResponse = await userA.GetAsync($"/api/tasks/{resources.Task.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ownerResponse.StatusCode);
    }

    [Fact]
    public async Task OtherUserCannotPatchTaskStatus()
    {
        var resources = await CreateOwnedTaskAsync();
        using var userA = resources.Owner;
        using var userB = resources.Other;

        var response = await userB.PatchAsJsonAsync(
            $"/api/tasks/{resources.Task.Id}/status",
            new UpdateTaskStatusRequest { Status = DomainTaskStatus.InProgress },
            JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<(HttpClient Owner, HttpClient Other, ProjectResponse Project)> CreateOwnedProjectAsync()
    {
        var owner = await CreateAuthenticatedClientAsync($"owner-{Guid.NewGuid():N}@example.test");
        var other = await CreateAuthenticatedClientAsync($"other-{Guid.NewGuid():N}@example.test");
        var project = await CreateProjectAsync(owner, "Owned project");
        return (owner, other, project);
    }

    private async Task<(HttpClient Owner, HttpClient Other, TaskResponse Task)> CreateOwnedTaskAsync()
    {
        var resources = await CreateOwnedProjectAsync();
        var response = await resources.Owner.PostAsJsonAsync(
            $"/api/projects/{resources.Project.Id}/tasks",
            new CreateTaskRequest { Title = "Owned task" },
            JsonOptions);
        response.EnsureSuccessStatusCode();
        var task = await ReadAsync<TaskResponse>(response);
        return (resources.Owner, resources.Other, task);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync(string email)
    {
        var client = _factory.CreateClient();
        await RegisterLoginAndAuthorizeAsync(client, email);
        return client;
    }

    private static async Task<ProjectResponse> CreateProjectAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync(
            "/api/projects",
            new CreateProjectRequest { Name = name },
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return await ReadAsync<ProjectResponse>(response);
    }

    private static async Task<AuthResponse> RegisterLoginAndAuthorizeAsync(HttpClient client, string email)
    {
        var registerResponse = await RegisterAsync(client, email);
        registerResponse.EnsureSuccessStatusCode();
        var loginResponse = await LoginAsync(client, email);
        loginResponse.EnsureSuccessStatusCode();
        var auth = await ReadAsync<AuthResponse>(loginResponse);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return auth;
    }

    private static Task<HttpResponseMessage> RegisterAsync(
        HttpClient client,
        string email,
        string password = TestOnlyPassword) =>
        client.PostAsJsonAsync("/api/auth/register", new RegisterRequest { Email = email, Password = password }, JsonOptions);

    private static Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string email,
        string password = TestOnlyPassword) =>
        client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password }, JsonOptions);

    private static async Task<string?> ReadProblemDetailAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("detail").GetString();
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<T>(JsonOptions)
        ?? throw new InvalidOperationException($"Response did not contain a {typeof(T).Name} body.");

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        return options;
    }
}
