namespace ArturRios.Util.WebApi.Security.Records;

public record Credentials(string Email, string Password)
{
    public Credentials() : this(string.Empty, string.Empty) { }
}
