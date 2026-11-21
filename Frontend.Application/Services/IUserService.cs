namespace UCR.EB.BioMicroscopeAdmin.Frontend.Application.Services;

public sealed record UserDto(string Id, string Email, IReadOnlyList<string> Roles, bool IsActive);

/// <summary>Reader account management, available only to administrators.</summary>
public interface IUserService
{
    Task<List<UserDto>> ListAsync();

    /// <exception cref="InvalidOperationException">The API rejected the data; the message explains why.</exception>
    Task CreateReaderAsync(string email, string password);

    Task DeactivateAsync(string id);

    Task ReactivateAsync(string id);
}
