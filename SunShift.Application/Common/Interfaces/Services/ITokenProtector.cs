namespace SunShift.Application.Common.Interfaces.Services;

public interface ITokenProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}
