using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using UluBilge.App.Authentication;
using UluBilge.Core.Commands;
using UluBilge.Core.Enums;
using UluBilge.Core.Exceptions;
using UluBilge.Data.Abstract;

namespace UluBilge.App.Commands.AuthHandler
{
    public static class RefreshTokenAsync
    {
        public sealed class Command : ICommand<LoginAsync.Result?>
        {
            [Required, StringLength(43, MinimumLength = 43), RegularExpression("^[A-Za-z0-9_-]{43}$")]
            public string RefreshToken { get; init; } = string.Empty;

            [JsonIgnore]
            public OAuthTokenContext? OAuth { get; init; }
        }

        public sealed class InvalidScopeException() : ValidationException("Izin verilen kapsam genisletilemez.") { }

        public sealed class Handler(IUserTokenRepository tokens, IUserRepository users, AuthTokenService tokenService, TimeProvider clock) : IAsyncCommandHandler<Command, LoginAsync.Result?>
        {
            private static readonly RoleEnum KnownRoles = Enum.GetValues<RoleEnum>().Aggregate(RoleEnum.None, (mask, role) => mask | role);

            public async Task<LoginAsync.Result?> HandleAsync(Command command, CancellationToken ct = default)
            {
                var token = await tokens.GetByTokenHashForUpdateAsync(AuthTokenService.HashRefreshToken(command.RefreshToken), ct);
                if (token is null)
                {
                    throw new CustomException("Yenileme tokenı bulunamadı veya geçersiz. Lütfen yeniden giriş yapın.");
                }

                // REST, baska OAuth istemcisi veya baska kaynak tokeni tuketilemez/iptal edilemez.
                var oauth = command.OAuth;
                if (oauth is null)
                {
                    if (token.OAuthClientId is not null || token.OAuthResource is not null || token.OAuthScope is not null)
                    {
                        throw new CustomException("Bu yenileme tokenı OAuth bağlantısına aittir; standart oturum yenilemede kullanılamaz.");
                    }
                }
                else if (token.OAuthClientId != oauth.ClientId || token.OAuthResource != oauth.Resource || token.OAuthScope is null || !token.SecurityStamp.HasValue)
                {
                    throw new CustomException("Yenileme tokenı bu uygulama veya kaynak için geçerli değil ya da yetkilendirme bilgileri eksik. Lütfen yeniden giriş yapın.");
                }

                var now = clock.GetUtcNow().UtcDateTime;
                if (token.ConsumedAtUtc.HasValue || token.RevokedAtUtc.HasValue)
                {
                    await tokens.RevokeFamilyAsync(token.FamilyId, now, ct);
                    throw new CommitException(token.ConsumedAtUtc.HasValue
                        ? "Yenileme tokenı daha önce kullanılmış. Güvenliğiniz için bu oturumun tüm yenileme tokenları iptal edildi. Lütfen yeniden giriş yapın."
                        : "Bu oturumun yenileme tokenı iptal edilmiş. Lütfen yeniden giriş yapın.");
                }

                if (token.ExpiresAtUtc <= now)
                {
                    throw new CustomException("Yenileme tokenının süresi dolmuş. Lütfen yeniden giriş yapın.");
                }

                if (oauth is not null)
                {
                    var scopes = (oauth.Scope ?? token.OAuthScope!).Split(' ', StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray();
                    if (scopes.Except(token.OAuthScope!.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Any())
                    {
                        throw new InvalidScopeException();
                    }
                    oauth = oauth with { Scope = string.Join(' ', scopes) };
                }

                var user = await users.GetByUserIdAsync(token.UserId, ct);
                if (user is null || !user.IsActive || user.LockoutEndUtc > now || (user.RoleEnum & ~KnownRoles) != 0
                    || token.SecurityStamp.HasValue && token.SecurityStamp != user.SecurityStamp
                    || oauth is not null && !user.RoleEnum.HasFlag(RoleEnum.Admin) && !user.RoleEnum.HasFlag(RoleEnum.Report)
                    && !user.RoleEnum.HasFlag(RoleEnum.Food) && !user.RoleEnum.HasFlag(RoleEnum.Region))
                {
                    await tokens.RevokeFamilyAsync(token.FamilyId, now, ct);
                    var message = user switch
                    {
                        null => "Yenileme tokenının bağlı olduğu kullanıcı hesabı bulunamadı.",
                        { IsActive: false } => "Kullanıcı hesabınız devre dışı olduğu için oturum yenilenemiyor.",
                        _ when user.LockoutEndUtc > now => "Hesabınız geçici olarak kilitli. Kilit süresi dolduktan sonra yeniden giriş yapın.",
                        _ when (user.RoleEnum & ~KnownRoles) != 0 => "Hesabınıza tanımlanan roller geçersiz olduğu için oturum yenilenemiyor.",
                        _ when token.SecurityStamp.HasValue && token.SecurityStamp != user.SecurityStamp => "Hesabınızın güvenlik bilgileri değişmiş. Lütfen yeniden giriş yapın.",
                        _ => "Hesabınızın MCP bağlantısını yenileme yetkisi bulunmuyor."
                    };
                    throw new CommitException(message);
                }

                // Yenileme ilk girisin son gecerlilik zamanini uzatmaz.
                var issued = await tokenService.IssueAsync(user, token.FamilyId, now, token.ExpiresAtUtc, ct, oauth);
                await tokens.ConsumeAsync(token.Id, issued.TokenId, now, ct);
                if (issued.TokenId is null)
                {
                    await tokens.RevokeFamilyAsync(token.FamilyId, now, ct);
                }
                return issued.Result;
            }
        }
    }
}
