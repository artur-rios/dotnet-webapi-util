using ArturRios.Util.WebApi.Security.Records;
using FluentValidation;

namespace ArturRios.Util.WebApi.Security.Validation;

public class CredentialsValidator : AbstractValidator<Credentials>
{
    public CredentialsValidator()
    {
        RuleFor(authCredentials => authCredentials.Email).NotEmpty();
        RuleFor(authCredentials => authCredentials.Password).NotEmpty();
    }
}
