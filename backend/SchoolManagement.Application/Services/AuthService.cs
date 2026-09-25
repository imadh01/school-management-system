using SchoolManagement.Application.DTOs.Auth;
using SchoolManagement.Application.Interfaces;

namespace SchoolManagement.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _tokenGenerator;

    public AuthService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator tokenGenerator)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<AuthResult?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByUsernameOrEmailAsync(request.UsernameOrEmail, cancellationToken);

        if (user is null || user.Status != "Active")
            return null;

        if (!_passwordHasher.VerifyPassword(user.PasswordHash, request.Password))
            return null;

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToList();
        var permissions = await _userRepository.GetPermissionNamesAsync(user.Id, cancellationToken);
        var (token, expiresAtUtc) = _tokenGenerator.GenerateToken(user, roles, permissions);

        return new AuthResult(user.Id, user.Username, user.Email, roles, token, expiresAtUtc);
    }
}