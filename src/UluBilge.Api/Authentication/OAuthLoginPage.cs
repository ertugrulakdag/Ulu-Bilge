using System.Net;
using UluBilge.Api.Website;

namespace UluBilge.Api.Authentication
{
    internal static class OAuthLoginPage
    {
        public static string Render(string clientName, bool allowOfflineAccess, int refreshTokenLifetimeDays, string hiddenFields, PageLanguage language, string languageSwitcher, string? error = null)
        {
            var accessDuration = allowOfflineAccess
                ? language.Text($"En fazla {refreshTokenLifetimeDays} gün boyunca otomatik yenilenebilir.", $"May renew automatically for up to {refreshTokenLifetimeDays} days.")
                : language.Text("Kısa süreli erişim. Otomatik yenilenmez.", "Short-term access. Does not renew automatically.");
            var errorMessage = string.IsNullOrEmpty(error)
                ? ""
                : $"<div class=\"error\" id=\"login-error\" role=\"alert\">{WebUtility.HtmlEncode(error)}</div>";

            var content = $$"""
                <section class="sign-in" aria-labelledby="login-title">
                    <h1 id="login-title">{{language.Text("Giriş yapın", "Sign in")}}</h1>
                    <p class="description">{{language.Text("Ulu Bilge hesabınızla devam edin.", "Continue with your Ulu Bilge account.")}}</p>

                    <div class="request" id="access-summary">
                        <p class="client-name">{{WebUtility.HtmlEncode(clientName)}}</p>
                        <p class="scope">{{language.Text("Yetkiniz dahilindeki raporlara ve menülere erişmek için izin istiyor.", "Requests access to reports and menus within your permissions.")}}</p>
                        <p class="duration">{{accessDuration}}</p>
                    </div>

                    {{errorMessage}}
                    <form method="post" action="/oauth/login" aria-describedby="access-summary{{(string.IsNullOrEmpty(error) ? "" : " login-error")}}">
                        {{hiddenFields}}
                        <div class="field">
                            <label for="userName">{{language.Text("Kullanıcı adı", "Username")}}</label>
                            <input id="userName" name="userName" maxlength="64" autocomplete="username" autocapitalize="none" spellcheck="false" required>
                        </div>
                        <div class="field">
                            <label for="password">{{language.Text("Parola", "Password")}}</label>
                            <input id="password" type="password" name="password" maxlength="128" autocomplete="current-password" required>
                        </div>
                        <div class="actions">
                            <button class="allow" type="submit" name="decision" value="allow">
                                {{language.Text("Giriş yap ve izin ver", "Sign in and allow")}}
                                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" aria-hidden="true">
                                    <path d="M4 12h15m-6-6 6 6-6 6" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" />
                                </svg>
                            </button>
                            <button class="deny" type="submit" name="decision" value="deny" formnovalidate>{{language.Text("İptal et", "Cancel")}}</button>
                        </div>
                        <p class="form-note">{{language.Text("Giriş yaptığınızda, bu uygulamaya yukarıdaki erişim için izin vermiş olursunuz.", "By signing in, you grant this app the access described above.")}}</p>
                    </form>
                </section>
                """;

            return OAuthPageLayout.Render(
                language.Text("Ulu Bilge Hesap bağlantısı", "Ulu Bilge Account connection"),
                language.Text("Raporlarınız.<br>İzninizle.", "Your reports.<br>With your<br>permission."),
                language.Text("Ulu Bilge hesabınızı kullandığınız uygulamaya bağlayın.", "Connect your Ulu Bilge account to the app you use."),
                content, language, languageSwitcher);
        }
    }
}
