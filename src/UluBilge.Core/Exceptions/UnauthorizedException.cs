namespace UluBilge.Core.Exceptions
{
    public sealed class UnauthorizedException(string message = "Kimlik doğrulama başarısız oldu") : Exception(message)
    {

    }
}
