using System.ComponentModel.DataAnnotations;

namespace IdentityService.DTOs
{
    public class RegisterRequest
    {
        [Required(ErrorMessage = "Kullanıcı adı gereklidir")]
        [StringLength(50, MinimumLength = 3, ErrorMessage = "Kullanıcı adı 3-50 karakter arasında olmalıdır")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "E-posta gereklidir")]
        [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Şifre gereklidir")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Şifre en az 8 karakter olmalıdır")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$",
            ErrorMessage = "Şifre en az 1 büyük harf, 1 küçük harf, 1 rakam ve 1 özel karakter içermelidir")]
        public string Password { get; set; } = string.Empty;

        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
    }

    public class LoginRequest
    {
        [Required(ErrorMessage = "Kullanıcı adı veya e-posta gereklidir")]
        public string UsernameOrEmail { get; set; } = string.Empty;

        [Required(ErrorMessage = "Şifre gereklidir")]
        public string Password { get; set; } = string.Empty;
    }

    public class AuthResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public UserResponse User { get; set; } = null!;
    }

    public class RefreshTokenRequest
    {
        [Required]
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class UserResponse
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? PhoneNumber { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
    }

    public class ChangePasswordRequest
    {
        [Required(ErrorMessage = "Mevcut şifre gereklidir")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Yeni şifre gereklidir")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "Şifre en az 8 karakter olmalıdır")]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$",
            ErrorMessage = "Şifre en az 1 büyük harf, 1 küçük harf, 1 rakam ve 1 özel karakter içermelidir")]
        public string NewPassword { get; set; } = string.Empty;
    }

    public class UpdateProfileRequest
    {
        [StringLength(50)]
        public string? FirstName { get; set; }

        [StringLength(50)]
        public string? LastName { get; set; }

        [Phone(ErrorMessage = "Geçerli bir telefon numarası giriniz")]
        public string? PhoneNumber { get; set; }
    }
}