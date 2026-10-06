using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectFlow.Api.Controllers;
using ProjectFlow.Application.Abstractions.Persistence;
using ProjectFlow.Domain.Common;

namespace ProjectFlow.Api.IntegrationTests.ErrorHandling;

/// <summary>Test-only endpoints that produce every kind of failure, registered just for <see cref="ErrorResponseTests"/>.</summary>
[AllowAnonymous]
[Route("api/error-probe")]
public sealed class ErrorProbeController : ApiControllerBase
{
    [HttpGet("result/{type}")]
    public IActionResult FromResult(ErrorType type) => ErrorResult(new Error($"Probe.{type}", $"Probe {type} error.", type));

    [HttpGet("validation-exception")]
    public IActionResult ThrowValidation() =>
        throw new ValidationException([new ValidationFailure("Name", "Name is required."), new ValidationFailure("Name", "Name is too short.")]);

    [HttpGet("concurrency-exception")]
    public IActionResult ThrowConcurrency() => throw new ConcurrencyConflictException(new InvalidOperationException("probe"));

    [HttpGet("unique-exception")]
    public IActionResult ThrowUnique() => throw new UniqueConstraintViolationException("ix_probe", new InvalidOperationException("probe"));

    [HttpGet("unhandled-exception")]
    public IActionResult ThrowUnhandled() => throw new InvalidOperationException("Sensitive internal detail that must not leak");
}
