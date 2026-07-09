using ArturRios.Util.WebApi.Security.Records;
using FluentValidation;

namespace ArturRios.Util.WebApi.Security.Validation;

/// <summary>FluentValidation validator for <see cref="Credentials"/>: requires a valid email and a password of at least 8 characters.</summary>
public class CredentialsValidator : AbstractValidator<Credentials>
{
    /// <summary>Defines the validation rules for <see cref="Credentials"/>.</summary>
    public CredentialsValidator()
    {
        RuleFor(credentials => credentials.Email).NotEmpty().EmailAddress();
        RuleFor(credentials => credentials.Password).NotEmpty().MinimumLength(8);
    }
}
