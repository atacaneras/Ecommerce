namespace IdentityService.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public string Salt { get; set; } = string.Empty;
        public UserRole Role { get; set; } = UserRole.Customer;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? RefreshTokenExpiryTime { get; set; }

        // Profil
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
    }

    public enum UserRole
    {
        Customer,
        Admin,
        SuperAdmin
    }

    public class RefreshToken
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiryDate { get; set; }
        public bool IsRevoked { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedByIp { get; set; }

        public User User { get; set; } = null!;
    }

    public class AuditLog
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public string Action { get; set; } = string.Empty;
        public string? Details { get; set; }
        public string? IpAddress { get; set; }
        public DateTime CreatedAt { get; set; }

        public User? User { get; set; }
    }
}