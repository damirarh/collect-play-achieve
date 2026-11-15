using Microsoft.AspNetCore.Identity;
using static System.Environment;

namespace CollectPlayAchieve.Api.Features.Auth;

internal class IdentityException : Exception
{
    private readonly List<IdentityError> _errors = [];

    public IdentityException() { }

    public IdentityException(string message)
        : base(message) { }

    public IdentityException(string message, Exception innerException)
        : base(message, innerException) { }

    public IdentityException(string message, IEnumerable<IdentityError> errors)
        : base(message)
    {
        _errors.AddRange(errors);
    }

    public override string Message =>
        base.Message
        + (
            _errors.Count != 0
                ? $"{NewLine}Errors:{NewLine}{string.Join($"{NewLine}", _errors.Select(e => $"{e.Code}: {e.Description}"))}"
                : string.Empty
        );
}
