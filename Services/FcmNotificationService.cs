using FirebaseAdmin.Messaging;
using InventoryAPI.Services.IServices;

namespace InventoryAPI.Services
{
    public class FcmNotificationService : IFcmNotificationService
    {
        public async Task<bool> SendPushNotificationAsync(string fcmToken, string title, string body, Dictionary<string, string>? dataPayload = null)
        {
            if (string.IsNullOrWhiteSpace(fcmToken)) return false;

            try
            {
                var message = new Message()
                {
                    Token = fcmToken,
                    Notification = new Notification
                    {
                        Title = title,
                        Body = body
                    },
                    Data = dataPayload ?? []
                };

                string response = await FirebaseMessaging.DefaultInstance.SendAsync(message);

                return true;
            }
            catch (Exception ex)
            {
                throw new Exception($"Queja de Google Firebase: {ex.Message}");
            }
        }
    }
}