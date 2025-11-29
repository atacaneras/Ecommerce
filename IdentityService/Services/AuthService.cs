using IdentityService.Data;
using IdentityService.DTOs;
using IdentityService.Models;
using IdentityService.Security;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Services
{
    public interface IAuthService
    {
        Task<AuthResponse?> RegisterAsync(RegisterRequest request, string? ipAddress);
        Task<AuthResponse?> LoginAsync(LoginRequest request, string? ipAddress);
        Task<AuthResponse?> RefreshTokenAsync(string refreshToken, string? ipAddress);
        Task<bool> RevokeTokenAsync(string refreshToken);
        Task<UserResponse?> GetUserByIdAsync(int userId);
        Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest request);
        Task<bool> UpdateProfileAsync(int userId, UpdateProfileRequest request);
    }

    public class AuthService : IAuthService
    {
        private readonly IdentityDbContext _context;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IJwtTokenGenerator _tokenGenerator;
        private readonly ILogger<AuthService> _logger;
        private readonly IConfiguration _configuration;

        public AuthService(
            IdentityDbContext context,
            IPasswordHasher passwordHasher,
            IJwtTokenGenerator tokenGenerator,
            ILogger<AuthService> logger,
            IConfiguration configuration)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _tokenGenerator = tokenGenerator;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<AuthResponse?> RegisterAsync(RegisterRequest request, string? ipAddress)
        {
            try
            {
                // Check if username already exists
                if (await _context.Users.AnyAsync(u => u.Username == request.Username))
                {
                    _logger.LogWarning("Registration failed: Username {Username} already exists", request.Username);
                    return null;
                }

                // Check if email already exists
                if (await _context.Users.AnyAsync(u => u.Email == request.Email))
                {
                    _logger.LogWarning("Registration failed: Email {Email} already exists", request.Email);
                    return null;
                }

                // Hash password
                var (hash, salt) = _passwordHasher.HashPassword(request.Password);

                // Create user
                var user = new User
                {
                    Username = request.Username,
                    Email = request.Email,
                    PasswordHash = hash,
                    Salt = salt,
                    Role = UserRole.Customer,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow,
                    FirstName = request.FirstName,
                    LastName = request.LastName,
                    PhoneNumber = request.PhoneNumber
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                // Log audit
                await LogAuditAsync(user.Id, "User Registered", $"New user registered: {user.Username}", ipAddress);

                // Generate tokens
                var accessToken = _tokenGenerator.GenerateAccessToken(user);
                var refreshToken = _tokenGenerator.GenerateRefreshToken();

                // Save refresh token
                await SaveRefreshTokenAsync(user.Id, refreshToken, ipAddress);

                _logger.LogInformation("User registered successfully: {Username}", user.Username);

                return new AuthResponse
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(int.Parse(_configuration["JwtSettings:AccessTokenExpiryMinutes"] ?? "60")),
                    User = MapToUserResponse(user)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration for user {Username}", request.Username);
                return null;
            }
        }

        public async Task<AuthResponse?> LoginAsync(LoginRequest request, string? ipAddress)
        {
            try
            {
                // Find user by username or email
                var user = await _context.Users
                    .FirstOrDefaultAsync(u =>
                        (u.Username == request.UsernameOrEmail || u.Email == request.UsernameOrEmail)
                        && u.IsActive);

                if (user == null)
                {
                    _logger.LogWarning("Login failed: User not found {UsernameOrEmail}", request.UsernameOrEmail);
                    await LogAuditAsync(null, "Failed Login Attempt", $"User not found: {request.UsernameOrEmail}", ipAddress);
                    return null;
                }

                // Verify password
                if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash, user.Salt))
                {
                    _logger.LogWarning("Login failed: Invalid password for user {Username}", user.Username);
                    await LogAuditAsync(user.Id, "Failed Login Attempt", "Invalid password", ipAddress);
                    return null;
                }

                // Update last login
                user.LastLoginAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                // Generate tokens
                var accessToken = _tokenGenerator.GenerateAccessToken(user);
                var refreshToken = _tokenGenerator.GenerateRefreshToken();

                // Save refresh token
                await SaveRefreshTokenAsync(user.Id, refreshToken, ipAddress);

                // Log audit
                await LogAuditAsync(user.Id, "User Login", "Successful login", ipAddress);

                _logger.LogInformation("User logged in successfully: {Username}", user.Username);

                return new AuthResponse
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(int.Parse(_configuration["JwtSettings:AccessTokenExpiryMinutes"] ?? "60")),
                    User = MapToUserResponse(user)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for user {UsernameOrEmail}", request.UsernameOrEmail);
                return null;
            }
        }

        public async Task<AuthResponse?> RefreshTokenAsync(string refreshToken, string? ipAddress)
        {
            try
            {
                var storedToken = await _context.RefreshTokens
                    .Include(rt => rt.User)
                    .FirstOrDefaultAsync(rt => rt.Token == refreshToken && !rt.IsRevoked);

                if (storedToken == null || storedToken.ExpiryDate <= DateTime.UtcNow)
                {
                    _logger.LogWarning("Refresh token is invalid or expired");
                    return null;
                }

                // Generate new tokens
                var user = storedToken.User;
                var accessToken = _tokenGenerator.GenerateAccessToken(user);
                var newRefreshToken = _tokenGenerator.GenerateRefreshToken();

                // Revoke old refresh token
                storedToken.IsRevoked = true;

                // Save new refresh token
                await SaveRefreshTokenAsync(user.Id, newRefreshToken, ipAddress);

                await _context.SaveChangesAsync();

                _logger.LogInformation("Token refreshed for user: {Username}", user.Username);

                return new AuthResponse
                {
                    AccessToken = accessToken,
                    RefreshToken = newRefreshToken,
                    ExpiresAt = DateTime.UtcNow.AddMinutes(int.Parse(_configuration["JwtSettings:AccessTokenExpiryMinutes"] ?? "60")),
                    User = MapToUserResponse(user)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing token");
                return null;
            }
        }

        public async Task<bool> RevokeTokenAsync(string refreshToken)
        {
            try
            {
                var storedToken = await _context.RefreshTokens
                    .FirstOrDefaultAsync(rt => rt.Token == refreshToken);

                if (storedToken == null)
                    return false;

                storedToken.IsRevoked = true;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Refresh token revoked for user {UserId}", storedToken.UserId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking token");
                return false;
            }
        }

        public async Task<UserResponse?> GetUserByIdAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            return user != null ? MapToUserResponse(user) : null;
        }

        public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest request)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return false;

                // Verify current password
                if (!_passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash, user.Salt))
                {
                    _logger.LogWarning("Password change failed: Invalid current password for user {UserId}", userId);
                    return false;
                }

                // Hash new password
                var (hash, salt) = _passwordHasher.HashPassword(request.NewPassword);
                user.PasswordHash = hash;
                user.Salt = salt;

                await _context.SaveChangesAsync();
                await LogAuditAsync(userId, "Password Changed", "User changed password", null);

                _logger.LogInformation("Password changed successfully for user {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> UpdateProfileAsync(int userId, UpdateProfileRequest request)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return false;

                user.FirstName = request.FirstName ?? user.FirstName;
                user.LastName = request.LastName ?? user.LastName;
                user.PhoneNumber = request.PhoneNumber ?? user.PhoneNumber;

                await _context.SaveChangesAsync();
                await LogAuditAsync(userId, "Profile Updated", "User updated profile", null);

                _logger.LogInformation("Profile updated for user {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile for user {UserId}", userId);
                return false;
            }
        }

        private async Task SaveRefreshTokenAsync(int userId, string token, string? ipAddress)
        {
            var refreshToken = new RefreshToken
            {
                UserId = userId,
                Token = token,
                ExpiryDate = DateTime.UtcNow.AddDays(int.Parse(_configuration["JwtSettings:RefreshTokenExpiryDays"] ?? "7")),
                IsRevoked = false,
                CreatedAt = DateTime.UtcNow,
                CreatedByIp = ipAddress
            };

            _context.RefreshTokens.Add(refreshToken);
            await _context.SaveChangesAsync();
        }

        private async Task LogAuditAsync(int? userId, string action, string? details, string? ipAddress)
        {
            var auditLog = new AuditLog
            {
                UserId = userId,
                Action = action,
                Details = details,
                IpAddress = ipAddress,
                CreatedAt = DateTime.UtcNow
            };

            _context.AuditLogs.Add(auditLog);
            await _context.SaveChangesAsync();
        }

        private UserResponse MapToUserResponse(User user)
        {
            return new UserResponse
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                Role = user.Role.ToString(),
                FirstName = user.FirstName,
                LastName = user.LastName,
                PhoneNumber = user.PhoneNumber,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt
            };
        }
    }
}