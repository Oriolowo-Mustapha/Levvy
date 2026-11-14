using System.Security.Cryptography;

namespace Levvy.Application.Common;

/// <summary>
/// Generates 7-character room codes in the XXX-XXXX format shared by
/// CreateSessionCommandHandler and ConnectOnlineClassCommandHandler.
/// Alphabet excludes visually ambiguous characters (no I, O, 0, 1).
/// </summary>
public static class SessionCodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string Generate()
    {
        Span<char> chars = stackalloc char[7];
        for (var i = 0; i < 7; i++)
        {
            if (i == 3)
            {
                chars[i] = '-';
                continue;
            }

            chars[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
        }

        return new string(chars);
    }
}
