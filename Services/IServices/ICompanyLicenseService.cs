namespace InventoryAPI.Services.IServices;

/// <summary>Valida el cupo y la vigencia antes de registrar nuevos usuarios.</summary>
public interface ICompanyLicenseService
{
    Task<(bool Allowed, string? Error)> CanCreateUserAsync(
        int companyId,
        CancellationToken cancellationToken = default);
}
