// D1: Updated to handle the new AuthResult shape (+ MustChangePassword).

using System.Net;
using System.Net.Http.Json;
using SchoolManagement.Application.DTOs.Auth;
using Xunit;

namespace SchoolManagement.Tests.IntegrationTests;

[Collection("LegacyApi")]
public class AuthControllerTests
{
    private readonly HttpClient _client;

    public AuthControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_WithSeededAdminCredentials_Returns200AndToken()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest("admin", "Admin@123"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AuthResult>();
        Assert.NotNull(result);
        Assert.False(string.IsNullOrEmpty(result!.Token));
        Assert.Contains("Admin", result.Roles);
        Assert.False(result.MustChangePassword);
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