using ArturRios.Util.WebApi.Security.Records;
using FluentValidation;

namespace ArturRios.Util.WebApi.Security.Validation;

public class CredentialsValidator : AbstractValidator<Credentials>
{
    public CredentialsValidator()
    {
        RuleFor(credentials => credentials.Email).NotEmpty().EmailAddress();
        RuleFor(credentials => credentials.Password).NotEmpty().MinimumLength(8);
    }
}
