using System.ComponentModel.DataAnnotations;
using Adesha.Application.Configuration;
using Microsoft.Extensions.Options;

namespace Adesha.Api.Configuration;

/// <summary>
/// Startup validation for AdeshaOptions. DataAnnotations validation is shallow, so nested
/// JwtOptions are validated explicitly here. Failure prevents boot (fail fast).
/// </summary>
public sealed class AdeshaOptionsValidator : IValidateOptions<AdeshaOptions>
{
    public ValidateOptionsResult Validate(string? name, AdeshaOptions options)
    {
        var failures = new List<string>();
        ValidateObject(options, failures, nameof(AdeshaOptions));
        ValidateObject(options.Jwt, failures, $"{nameof(AdeshaOptions)}.{nameof(AdeshaOptions.Jwt)}");

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static void ValidateObject(object instance, List<string> failures, string prefix)
    {
        var results = new List<ValidationResult>();
        if (!Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true))
        {
            failures.AddRange(results.Select(r => $"{prefix}: {r.ErrorMessage}"));
        }
    }
}
