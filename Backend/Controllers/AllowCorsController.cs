using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Cors;

namespace Backend.Controllers {

    [ApiController]
    [Route("api/[controller]")]
    [EnableCors("CorsPolicy")]
    public class AllowCorsController : ControllerBase {
    }
}
