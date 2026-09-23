using System.Net;
using System.Net.Http.Json;
using SchoolManagement.Application.DTOs.Auth;
using Xunit;

namespace SchoolManagement.Tests.IntegrationTests;

public class AuthControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_WithSeededAdminCredentials_Returns200AndToken()
    {
        // Relies on the same seeded admin account (username "admin",
        // password "Admin@123") that the migration's HasData creates.
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("admin", "Admin@123"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AuthResult>();
        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result!.Token));
        Assert.Contains("Admin", result.Roles);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("admin", "WrongPassword1!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithEmptyBody_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("", ""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}