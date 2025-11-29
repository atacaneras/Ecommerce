using System.Security.Cryptography;
using System.Text;

namespace IdentityService.Security
{
    public interface IPasswordHasher
    {
        (string hash, string salt) HashPassword(string password);
        bool VerifyPassword(string password, string hash, string salt);
    }

    public class PasswordHasher : IPasswordHasher
    {
        private const int SaltSize = 32; // 256 bits
        private const int HashSize = 32; // 256 bits
        private const int Iterations = 100000; // PBKDF2

        public (string hash, string salt) HashPassword(string password)
        {
            using var rng = RandomNumberGenerator.Create();
            var saltBytes = new byte[SaltSize];
            rng.GetBytes(saltBytes);
            var hashBytes = HashPasswordWithSalt(password, saltBytes);
            return (
                Convert.ToBase64String(hashBytes),
                Convert.ToBase64String(saltBytes)
            );
        }

        public bool VerifyPassword(string password, string hash, string salt)
        {
            try
            {
                var saltBytes = Convert.FromBase64String(salt);
                var hashBytes = Convert.FromBase64String(hash);

                var computedHash = HashPasswordWithSalt(password, saltBytes);

                return CryptographicOperations.FixedTimeEquals(hashBytes, computedHash);
            }
            catch
            {
                return false;
            }
        }

        private byte[] HashPasswordWithSalt(string password, byte[] salt)
        {
            using var pbkdf2 = new Rfc2898DeriveBytes(
                password,
                salt,
                Iterations,
                HashAlgorithmName.SHA256
            );

            return pbkdf2.GetBytes(HashSize);
        }
    }
}