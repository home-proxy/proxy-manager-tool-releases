namespace NetAgent.ProxyManager.Core.Interfaces;

public interface ICredentialProtector
{
    string Protect(string plaintext);
    string Unprotect(string protectedValue);
}
