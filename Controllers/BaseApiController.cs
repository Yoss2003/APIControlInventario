using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InventoryAPI.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public abstract class BaseApiController : ControllerBase
    {
        protected int ObtenerEmpresaSegura()
        {
            var companyClaim = User.Claims.FirstOrDefault(c => c.Type == "CompanyId");

            if (companyClaim != null && int.TryParse(companyClaim.Value, out int companyId))
            {
                return companyId;
            }

            return 1;
        }

        protected int ObtenerUsuarioSeguro()
        {
            var userClaim = User.Claims.FirstOrDefault(c => c.Type == "UserId");
            return userClaim != null ? int.Parse(userClaim.Value) : 0;
        }
    }
}