namespace InventoryAPI.Services.IServices
{
    public interface IFcmNotificationService
    {
        Task<bool> SendPushNotificationAsync(string fcmToken, string title, string body, Dictionary<string, string>? dataPayload = null);
    }
}