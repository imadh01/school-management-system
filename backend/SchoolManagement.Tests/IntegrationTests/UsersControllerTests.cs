using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SchoolManagement.Application.DTOs.Auth;
using SchoolManagement.Application.DTOs.Users;
using Xunit;

namespace SchoolManagement.Tests.IntegrationTests;

public class UsersControllerTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public UsersControllerTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest("admin", "Admin@123"));
        var result = await response.Content.ReadFromJsonAsync<AuthResult>();
        return result!.Token;
    }

    [Fact]
    public async Task CreateUser_WithoutToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync("/api/users",
            new CreateUserRequest("someone", "someone@test.com", "Passw0rd!", "Teacher"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateUser_WithValidTokenAndData_Returns201()
    {
        var token = await GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.PostAsJsonAsync("/api/users",
            new CreateUserRequest("newteacher", "newteacher@test.com", "Passw0rd!", "Teacher"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<UserResponse>();
        Assert.Equal("newteacher", user!.Username);
    }

    [Fact]
    public async Task CreateUser_DuplicateUsername_Returns409()
    {
        var token = await GetAdminTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // "admin" already exists via seed data
        var response = await _client.PostAsJsonAsync("/api/users",
            new CreateUserRequest("admin", "different@test.com", "Passw0rd!", "Teacher"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}