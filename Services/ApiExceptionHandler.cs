using Grpc.Core;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Salas_Elvis.Services;

public class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var status = exception is ReservaException error ? error.StatusCode
            : exception is RpcException ? 503 : 500;
        if (status >= 500) logger.LogError(exception, "Falha ao processar reserva.");
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = status == 409 ? "Conflito de reserva" : "Não foi possível processar a solicitação",
            Detail = exception is ReservaException ? exception.Message
                : status == 503 ? "Não foi possível acessar o Firestore. Verifique a configuração e tente novamente."
                : "Ocorreu um erro interno.",
            Instance = context.Request.Path
        }, cancellationToken);
        return true;
    }
}
