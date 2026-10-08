namespace Salas_Elvis.Services;

public class ReservaException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
