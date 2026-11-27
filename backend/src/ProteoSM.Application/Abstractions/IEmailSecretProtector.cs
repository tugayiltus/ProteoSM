namespace ProteoSM.Application.Abstractions;

/// <summary>Protects the outgoing mail password at rest with the shared key ring, so the API and the Worker can both read it.</summary>
public interface IEmailSecretProtector
{
    string Protect(string secret);

    string Unprotect(string protectedSecret);
}
