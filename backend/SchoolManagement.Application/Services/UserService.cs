using SchoolManagement.Application.DTOs.Users;
using SchoolManagement.Application.Interfaces;
using SchoolManagement.Domain.Entities;
using SchoolManagement.Domain.Exceptions;

namespace SchoolManagement.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IPasswordHasher _passwordHasher;

    public UserService(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<UserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        if (await _userRepository.ExistsByUsernameAsync(request.Username, cancellationToken))
            throw new ConflictException($"Username '{request.Username}' is already taken.");

        if (await _userRepository.ExistsByEmailAsync(request.Email, cancellationToken))
            throw new ConflictException($"Email '{request.Email}' is already registered.");

        var role = await _roleRepository.GetByNameAsync(request.RoleName, cancellationToken)
            ?? throw new NotFoundException($"Role '{request.RoleName}' does not exist.");

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            Status = "Active",
        };
        user.UserRoles.Add(new UserRole { Role = role, AssignedAt = DateTime.UtcNow });

        await _userRepository.AddAsync(user, cancellationToken);

        return new UserResponse(user.Id, user.Username, user.Email, user.Status, new[] { role.Name });
    }
}