using ControlInventario.Shared.Models;
using ControlInventario.Shared.Models.DTO;
using InventoryAPI.Repositories.IRepositories;

namespace InventoryAPI.Services.IServices
{
    public interface IUserService : IWorkContainer<User>
    {
        Task<IEnumerable<UserDTO>> GetUsersDtoAsync();
        Task<UserDTO?> GetUserDtoByIdAsync(int id);
        Task<(bool Success, string Message)> UpdateUserAsync(int id, User userActualizado);
        Task<(bool Success, object Data, string Message)> CreateUserAsync(User user, string contentRootPath);
        Task<(bool Success, User? User, string Message, bool Requires2FA, bool RequirePasswordChange, bool AccountPending)> LoginAsync(LoginRequestDTO request);
        Task<(bool Success, User? User, string Message)> ChangeInitialPasswordAsync(int userId, string newPassword);
        Task<(bool Success, string Url, string Message)> UpdatePhotoAsync(int id, string base64Image, string contentRootPath);
        Task<(bool Success, string Secret, string QrUri)> Generate2FAAsync(int id);
        Task<bool> Enable2FAAsync(int id, string code);
        Task<bool> Disable2FAAsync(int id);
        Task<(bool Success, string Message)> ApproveEmployeeAsync(int id);
        Task<(bool Success, string Message)> TestEmailConnectionAsync(string email, string password);
    }
}