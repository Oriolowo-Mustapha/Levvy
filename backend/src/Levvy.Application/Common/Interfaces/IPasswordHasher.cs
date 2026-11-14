namespace Levvy.Application.Common.Interfaces;

/// <summary>PBKDF2 password hashing abstraction for local email/password auth.</summary>
public interface IPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string passwordHash);
}
