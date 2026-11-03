using UluBilge.Api.Website;

namespace UluBilge.Api.Authentication
{
    internal static class OAuthSuccessPage
    {
        public static string Render(PageLanguage language)
        {
            var content = $$"""
                <section class="sign-in success" aria-labelledby="success-title">
                    <div class="success-mark" aria-hidden="true">
                        <svg width="38" height="38" viewBox="0 0 48 48" fill="none">
                            <path d="m12 24 8 8 16-17" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round" />
                        </svg>
                    </div>
                    <p class="success-eyebrow">{{language.Text("HER ŞEY HAZIR", "ALL SET")}}</p>
                    <h1 id="success-title">{{language.Text("Giriş başarılı.", "Sign-in successful.")}}</h1>
                    <p class="description">{{language.Text("Ulu Bilge hesabınız başarıyla bağlandı.", "Your Ulu Bilge account is now connected.")}}</p>

                    <div class="usage-guide" aria-label="{{language.Text("Kullanım bilgisi", "Usage guide")}}">
                        <p>{{language.Text("Bağlantıyı başlattığınız uygulamadaki sohbete dönün ve öğrenmek istediğinizi yazın. Yetkiniz dahilindeki satış raporlarını sorgulayabilir, örnek öğle yemeği menüsünü görüntüleyebilirsiniz.", "Return to the conversation in the app where you started the connection and ask your question. You can query sales reports within your permissions and view sample lunch menus.")}}</p>
                        <p class="usage-label">{{language.Text("Örnek sorular", "Example questions")}}</p>
                        <ul class="usage-examples">
                            <li>{{language.Text("“1997 yılında en çok ciro sağlayan 10 ürünü göster.”", "“Show the 10 products with the highest revenue in 1997.”")}}</li>
                            <li>{{language.Text("“Bu haftanın örnek öğle yemeği menüsünü göster.”", "“Show this week's sample lunch menu.”")}}</li>
                        </ul>
                        <p>{{language.Text("Raporlarda yıl veya tarih aralığını belirtmeniz, istediğiniz sonuca ulaşmayı kolaylaştırır.", "Include a year or date range in report questions to get the result you need.")}}</p>
                    </div>

                    <div class="request">
                        <p class="client-name">{{language.Text("Bağlantı tamamlandı", "Connection complete")}}</p>
                        <p class="scope">{{language.Text("Yapay zeka asistanınıza dönerek devam edebilirsiniz.", "Return to your AI assistant to continue.")}}</p>
                    </div>
                </section>
                """;

            const string styles = """
                .success-mark {
                    display: grid;
                    place-items: center;
                    width: 72px;
                    height: 72px;
                    margin-bottom: 28px;
                    border: 2px solid var(--ink);
                    border-radius: 50%;
                    color: var(--ink);
                    background: var(--lime);
                }
                .success-eyebrow { margin-bottom: 10px; color: #53576d; font-size: 11px; font-weight: 700; letter-spacing: 1.3px; }
                .success h1 { font-size: clamp(29px, 3vw, 38px); letter-spacing: -1.4px; }
                .success .description { max-width: 340px; font-size: 14px; }
                .usage-guide { margin-top: 20px; color: #53576d; font-size: 13px; line-height: 1.7; overflow-wrap: anywhere; }
                .usage-label { margin-top: 16px; color: var(--ink); font-size: 12px; font-weight: 700; }
                .usage-examples { display: grid; gap: 10px; margin: 10px 0 14px; padding: 0; list-style: none; }
                .usage-examples li { padding-left: 12px; border-left: 2px solid var(--blue); color: var(--ink); }
                .success .request { margin-top: 28px; margin-bottom: 28px; }
                @media (max-width: 540px) {
                    .success-mark { width: 60px; height: 60px; margin-bottom: 22px; }
                    .success .request { margin-top: 22px; margin-bottom: 24px; }
                }
                @media (max-width: 360px) {
                    .success { padding-right: 20px; padding-left: 20px; }
                    .success .request { margin-right: -20px; margin-left: -20px; padding-right: 20px; padding-left: 20px; }
                    .masthead { gap: 12px; }
                    .page-caption { max-width: 120px; text-align: right; line-height: 1.5; }
                }
                @media (forced-colors: active) {
                    .success-mark { border-color: CanvasText; }
                }
                """;

            return OAuthPageLayout.Render(
                language.Text("Ulu Bilge Bağlantı başarılı", "Ulu Bilge Connection successful"),
                language.Text("Bağlantınız.<br>Hazır.", "Connected.<br>Ready."),
                language.Text("Hesabınız bağlandı. Ulu Bilge raporlarınızla kaldığınız yerden devam edin.", "Your account is connected. Pick up where you left off with your Ulu Bilge reports."),
                content,
                language,
                language.Switcher("/oauth/success"),
                styles);
        }
    }
}
