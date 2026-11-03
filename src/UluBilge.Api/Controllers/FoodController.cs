using Microsoft.AspNetCore.Mvc;
using UluBilge.Api.Authentication;
using UluBilge.Api.Models;
using UluBilge.App.Queries.FoodHandler;
using UluBilge.Core.Enums;

namespace UluBilge.Api.Controllers
{
    [RoleAuthorize(RoleEnum.Admin, RoleEnum.Food, RoleEnum.Report, RoleEnum.Region)]
    public sealed class FoodController : BaseController
    {
        [HttpGet]
        public async Task<ActionResult<BaseResponse<GetFoodsAsync.Result>>> GetFoodsAsync([FromQuery] GetFoodsAsync.Query query, CancellationToken ct)
        {
            var result = await AsyncQueryHandler<GetFoodsAsync.Query, GetFoodsAsync.Result>().HandleAsync(query, ct);
            return Ok(new BaseResponse<GetFoodsAsync.Result>(result));
        }
    }
}
