using System.Net;

namespace UluBilge.Api.Website;

internal static class WebsitePages
{
    public static string Render(string path, PageLanguage language, WebsiteOptions options)
    {
        var title = path switch
        {
            "/support" => language.Text("Destek", "Support"),
            "/privacy" => language.Text("Gizlilik politikası", "Privacy policy"),
            "/terms" => language.Text("Kullanım koşulları", "Terms of service"),
            _ => language.Text("Raporlarınız. İzninizle.", "Your reports. With your permission.")
        };
        var description = path switch
        {
            "/support" => language.Text("Ulu Bilge bağlantısı, raporlar ve örnek yemek menüleri için yardım alın.", "Get help with your Ulu Bilge connection, reports and sample meal menus."),
            "/privacy" => language.Text("Ulu Bilge hesap, rapor ve bağlantı verilerinin nasıl işlendiğini öğrenin.", "Learn how Ulu Bilge processes account, report and connection data."),
            "/terms" => language.Text("Ulu Bilge hizmetinin kullanım kapsamı ve hesap sorumlulukları.", "The scope of the Ulu Bilge service and your account responsibilities."),
            _ => language.Text("İş verilerinize sorular sorun. Yetkiniz dahilindeki raporlara ve örnek yemek menülerine asistanınızdan ulaşın.", "Ask questions about your business data. Access reports within your permissions and sample meal menus from your assistant.")
        };
        var content = path switch
        {
            "/support" => Support(language, options),
            "/privacy" => Privacy(language, options),
            "/terms" => Terms(language, options),
            _ => Home(language)
        };
        return WebsitePageLayout.Render(path, language, title, description, content);
    }

    private static string Home(PageLanguage l) => $$"""
        <section class="hero" aria-labelledby="home-title">
            <div class="poster">
                <p class="eyebrow">{{l.Text("İŞİNİZİN BİLGİSİ, SOHBETİNİZDE", "BUSINESS KNOWLEDGE, IN YOUR CONVERSATION")}}</p>
                <h1 id="home-title">{{l.Text("Raporlarınız.<br>İzninizle.", "Your reports.<br>With your<br>permission.")}}</h1>
                <p class="hero-copy">{{l.Text("İş verilerinize sorular sorun. Ulu Bilge hesabınızın erişebildiği bilgilere, kullandığınız yapay zekâ asistanından ulaşın.", "Ask questions about your business data. Reach the information available to your Ulu Bilge account through your AI assistant.")}}</p>
                <a class="button" href="{{l.Link("/support")}}#getting-started">{{l.Text("Nasıl başlarım?", "How do I get started?")}} <span aria-hidden="true">↗</span></a>
                <div class="poster-art" aria-hidden="true">
                    <svg viewBox="0 0 300 310" fill="none"><path d="M24 20h72v138c0 35 21 56 54 56s54-21 54-56V20h72v140c0 82-50 132-126 132S24 242 24 160V20Z" fill="#2944ee"/><path d="M204 20h72v72h-72V20Z" fill="#ff8fba"/></svg>
                </div>
            </div>
            <div class="conversation">
                <div class="conversation-header"><span class="status-dot" aria-hidden="true"></span> {{l.Text("Ulu Bilge ile bir sohbet", "A conversation with Ulu Bilge")}} <span class="sample-tag">{{l.Text("ÖRNEK", "EXAMPLE")}}</span></div>
                <p class="question">{{l.Text("1997 yılında en çok ciro sağlayan 10 ürünü göster.", "Show the 10 products with the highest revenue in 1997.")}}</p>
                <div class="answer">
                    <span class="mini-wordmark" aria-hidden="true">U.</span>
                    <div><h2>{{l.Text("Sorudan bilgiye.", "From question to insight.")}}</h2><p>{{l.Text("Ürün, müşteri ve çalışan bazında satışları inceleyin. Dönemi ve ölçütü belirtin; hesabınızın yetkisi kapsamındaki raporu isteyin.", "Explore sales by product, customer and employee. Specify the period and metric, then request a report within your account permissions.")}}</p></div>
                </div>
                <div class="report-illustration" aria-hidden="true"><span></span><span></span><span></span><span></span><span></span><span></span></div>
                <p class="illustration-note">{{l.Text("Görsel temsil · Gerçek rapor verisi içermez", "Illustration · Does not contain actual report data")}}</p>
                <p class="question second-question">{{l.Text("Bu haftanın örnek öğle yemeği menüsünü göster.", "Show this week's sample lunch menu.")}}</p>
                <p class="sample-note">{{l.Text("Günlük, haftalık veya aylık menüleri Türkçe ve İngilizce sorgulayın. Yemek menüleri şu anda örnek veridir.", "Ask for daily, weekly or monthly menus in Turkish and English. Meal menus currently use sample data.")}}</p>
            </div>
        </section>
        <section class="features" aria-label="{{l.Text("Neler yapabilirsiniz?", "What can you do?")}}">
            <article><span class="feature-number">01 /</span><h2>{{l.Text("İşinizi sorun.", "Ask about your business.")}}</h2><p>{{l.Text("Satışları karşılaştırın, ürünleri sıralayın, müşteri ve çalışan performansını inceleyin.", "Compare sales, rank products, and explore customer and employee performance.")}}</p></article>
            <article><span class="feature-number">02 /</span><h2>{{l.Text("Hesabınızla bağlanın.", "Connect your account.")}}</h2><p>{{l.Text("Bağlantıyı asistanınızdan başlatın. Ulu Bilge giriş ekranında uygulamayı ve istenen erişimi inceleyip onaylayın.", "Start the connection from your assistant. Review the app and requested access on the Ulu Bilge sign-in page, then approve.")}}</p></article>
            <article><span class="feature-number">03 /</span><h2>{{l.Text("İki dilde devam edin.", "Continue in either language.")}}</h2><p>{{l.Text("Türkçe veya İngilizce sorular sorun. Sayfanın üstünden dilinizi seçin; seçiminiz sonraki ziyaretlerde hatırlansın.", "Ask questions in Turkish or English. Choose your page language above; your preference is remembered for future visits.")}}</p></article>
        </section>
        <section id="mcp-server" class="mcp-connect" aria-labelledby="mcp-title">
            <div class="mcp-intro">
                <p class="eyebrow">{{l.Text("ASİSTANINIZA BAĞLAYIN", "CONNECT YOUR ASSISTANT")}}</p>
                <h2 id="mcp-title">{{l.Text("MCP sunucusu<br>olarak ekleyin.", "Add Ulu Bilge<br>as an MCP server.")}}</h2>
                <p>{{l.Text("Ulu Bilge'yi uzak MCP bağlantılarını ve OAuth girişini destekleyen asistanınıza ekleyin.", "Add Ulu Bilge to an assistant that supports remote MCP connections and OAuth sign-in.")}}</p>
                <ol class="mcp-quick-steps">
                    <li>{{l.Text("Asistanınızın bağlantılar bölümünden özel MCP sunucusu ekleyin; adını Ulu Bilge olarak yazın.", "Add a custom MCP server from your assistant's connections and name it Ulu Bilge.")}}</li>
                    <li>{{l.Text("MCP Server URL alanına bu bölümdeki adresi girin ve OAuth kimlik doğrulamasını seçin.", "Enter the address shown here in the MCP Server URL field and choose OAuth authentication.")}}</li>
                    <li>{{l.Text("Açılan Ulu Bilge ekranında giriş yapıp izin verin. Sohbette Ulu Bilge'yi seçerek sorularınızı sorun.", "Sign in and grant access on the Ulu Bilge screen. Select Ulu Bilge in your conversation and ask a question.")}}</li>
                </ol>
                <a class="mcp-guide-link" href="{{l.Link("/support")}}#getting-started">{{l.Text("ChatGPT ve Claude için kurulum adımları", "Setup steps for ChatGPT and Claude")}} <span aria-hidden="true">↗</span></a>
            </div>
            {{McpSettings(l)}}
        </section>
        <aside class="bottom-banner"><p>{{l.Text("Bir bağlantı. Daha anlaşılır bilgiler.", "One connection. Clearer insights.")}}</p><a href="{{l.Link("/support")}}">{{l.Text("Bağlantı rehberine gidin", "Open the connection guide")}} <span aria-hidden="true">↗</span></a></aside>
        """;

    private static string Support(PageLanguage l, WebsiteOptions options) => $$"""
        <section class="page-heading pink"><p class="eyebrow">{{l.Text("BİRLİKTE ÇÖZELİM", "LET'S WORK IT OUT")}}</p><h1>{{l.Text("Size nasıl<br>yardımcı olalım?", "How can<br>we help?")}}</h1><p>{{l.Text("Bağlanın, sorun, devam edin. Başlamak için ihtiyacınız olanlar burada.", "Connect, ask, continue. Here is what you need to get started.")}}</p></section>
        <div class="content-grid">
            <aside class="sidebar"><p class="eyebrow">{{l.Text("BU SAYFADA", "ON THIS PAGE")}}</p><nav aria-label="{{l.Text("Destek konuları", "Support topics")}}"><a href="#getting-started">{{l.Text("MCP sunucusu ekleme", "Add an MCP server")}}</a><a href="#chatgpt">ChatGPT</a><a href="#claude">Claude</a><a href="#other-clients">{{l.Text("Diğer MCP istemcileri", "Other MCP clients")}}</a><a href="#questions">{{l.Text("Sık sorulan sorular", "Frequently asked questions")}}</a><a href="#contact">{{l.Text("İletişim", "Contact")}}</a></nav></aside>
            <div class="article-content">
                <section id="getting-started">
                    <h2>{{l.Text("MCP sunucusu olarak ekleyin.", "Add Ulu Bilge as an MCP server.")}}</h2>
                    <p>{{l.Text("MCP, asistanınızın Ulu Bilge araçlarına bağlanmasını sağlar. Kullandığınız uygulamada özel veya uzak MCP sunucusu ekleme ekranını açın ve aşağıdaki bilgileri kullanın.", "MCP lets your assistant connect to Ulu Bilge tools. Open the custom or remote MCP server setup in your app and use the following settings.")}}</p>
                    {{McpSettings(l)}}
                    <ol class="steps">
                        <li><h3>{{l.Text("Sunucuyu kaydedin.", "Save the server.")}}</h3><p>{{l.Text("Adı ve MCP Server URL adresini girin. Bağlantı türü sorulursa Streamable HTTP, kimlik doğrulama için OAuth seçin. Otomatik istemci kaydı (DCR) destekleyen bir istemci kullanın.", "Enter the name and MCP Server URL. If prompted for a transport, choose Streamable HTTP; choose OAuth for authentication. Use a client that supports automatic client registration (DCR).")}}</p></li>
                        <li><h3>{{l.Text("Hesabınıza giriş yapıp izin verin.", "Sign in and grant access.")}}</h3><p>{{l.Text("Açılan Ulu Bilge ekranında uygulama adını ve istenen erişimi inceleyin; hesabınızla giriş yapıp bağlantıyı onaylayın. Hesabınız yoksa kurum yöneticinizden erişim isteyin.", "Review the app name and requested access on the Ulu Bilge screen, then sign in and approve the connection. Ask your organization administrator for access if you need an account.")}}</p></li>
                        <li><h3>{{l.Text("Sohbette Ulu Bilge'yi etkinleştirin.", "Enable Ulu Bilge in your conversation.")}}</h3><p>{{l.Text("Asistanınızın eklenti veya araç seçicisinden Ulu Bilge'yi seçin. “Bu haftanın örnek öğle yemeği menüsünü göster” diye sorarak deneyin. Satış raporları için tarih aralığı ve ölçüt belirtin; rapor erişimi hesap rolünüze bağlıdır.", "Select Ulu Bilge in your assistant's plugin or tool picker. Try “Show this week's sample lunch menu.” For sales reports, specify a date range and metric; report access depends on your account role.")}}</p></li>
                    </ol>
                </section>
                <section id="chatgpt">
                    <h2>{{l.Text("ChatGPT'de ekleme", "Add to ChatGPT")}}</h2>
                    <ol class="steps">
                        <li><p>{{l.Text("ChatGPT'nin web sürümünde Plugins bölümünü açın. Artı düğmesinden “Add custom MCP server” seçeneğine girin.", "In ChatGPT on the web, open Plugins. Use the plus button to add a custom MCP server.")}}</p></li>
                        <li><p>{{l.Text("Adı Ulu Bilge olarak girin; sunucu adresini yukarıdaki MCP Server URL ile doldurun. OAuth kullanın ve istemci kaydı sorulursa DCR seçin.", "Name it Ulu Bilge and enter the MCP Server URL above. Use OAuth and choose DCR if asked how to register the client.")}}</p></li>
                        <li><p>{{l.Text("“Create as a plugin” ile oluşturun, eklentiyi yükleyin ve Ulu Bilge hesabınızı bağlayın. Sohbette @ yazarak Ulu Bilge'yi seçin.", "Create it as a plugin, install it and connect your Ulu Bilge account. Type @ in a conversation to select Ulu Bilge.")}}</p></li>
                    </ol>
                    <p class="guide-source"><a href="https://developers.openai.com/api/docs/guides/custom-mcp-server">{{l.Text("Resmi ChatGPT MCP rehberi", "Official ChatGPT MCP guide")}}</a></p>
                </section>
                <section id="claude">
                    <h2>{{l.Text("Claude'da ekleme", "Add to Claude")}}</h2>
                    <ol class="steps">
                        <li><p>{{l.Text("Claude veya Claude Desktop'ta Customize → Connectors bölümünü açın. Add → Custom → Web yoluyla uzak bir bağlantı ekleyin.", "In Claude or Claude Desktop, open Customize → Connectors. Add a remote connection through Add → Custom → Web.")}}</p></li>
                        <li><p>{{l.Text("Adı Ulu Bilge olarak yazın; sunucu adresine yukarıdaki MCP Server URL'yi girin. Algılanan OAuth ayarlarını kontrol edin. İstemci kaydı için “Register automatically” seçin.", "Name it Ulu Bilge and enter the MCP Server URL above. Check the detected OAuth settings. Choose automatic registration for the OAuth client.")}}</p></li>
                        <li><p>{{l.Text("Bağlantıyı ekleyip hesabınıza giriş yapın ve izin verin. Sohbetin + menüsündeki Connectors bölümünden Ulu Bilge'yi etkinleştirin. Kurumsal hesapta bağlantıyı önce yöneticinizin eklemesi gerekebilir.", "Add the connection, sign in and grant access. Enable Ulu Bilge from Connectors in the conversation's + menu. In an organization, an administrator may need to add the connection first.")}}</p></li>
                    </ol>
                    <p class="guide-source"><a href="https://support.claude.com/en/articles/11175166-get-started-with-custom-connectors-using-remote-mcp">{{l.Text("Resmi Claude MCP rehberi", "Official Claude MCP guide")}}</a></p>
                </section>
                <section id="other-clients">
                    <h2>{{l.Text("Diğer MCP istemcileri", "Other MCP clients")}}</h2>
                    <p>{{l.Text("Uzak Streamable HTTP bağlantısı, OAuth/PKCE ve dinamik istemci kaydı (DCR) destekleyen uygulamalarda aynı sunucu adresini kullanabilirsiniz. İstemci kapsam isterse mcp:tools girin; bağlantıyı otomatik yenilemek için offline_access kapsamını da ekleyin.", "Use the same server URL in apps supporting remote Streamable HTTP, OAuth/PKCE and dynamic client registration (DCR). If scopes are requested, enter mcp:tools; add offline_access to allow automatic session renewal.")}}</p>
                    <p>{{l.Text("Menü adları uygulamanızın sürümüne göre değişebilir. Ekleme seçeneği görünmüyorsa hesabınızın ve çalışma alanınızın bağlantı izinlerini kontrol edin. Kurulumun ardından asistanın araç listesinde Ulu Bilge araçlarının göründüğünü doğrulayın.", "Menu names may vary with your app version. If the option is missing, check your account and workspace connection permissions. After setup, confirm that Ulu Bilge tools appear in your assistant's tool list.")}}</p>
                </section>
                <section id="questions"><h2>{{l.Text("Sık sorulan sorular", "Frequently asked questions")}}</h2>
                    {{Faq(l.Text("MCP adresini tarayıcıda açınca neden giriş yapamıyorum?", "Why can I not sign in by opening the MCP URL in a browser?"), l.Text("MCP adresini asistanınızın MCP Server URL alanına girin. Bağlantıyı ve giriş yönlendirmesini asistanınız başlatır; ardından Ulu Bilge giriş ekranı açılır.", "Enter the MCP address in your assistant's MCP Server URL field. Your assistant starts the connection and sign-in redirect, then opens the Ulu Bilge sign-in screen."))}}
                    {{Faq(l.Text("Giriş yapamıyorum. Ne yapmalıyım?", "I cannot sign in. What should I do?"), l.Text("Kullanıcı adınızı ve parolanızı kontrol edin. Bağlantıyı uygulamanızdan yeniden başlatın. Hesabınız kilitli, devre dışı veya yetkisizse kurum yöneticinizle iletişime geçin. Çok sayıda deneme yaptıysanız bir süre bekleyip tekrar deneyin.", "Check your username and password. Restart the connection from your app. Contact your organization administrator if your account is locked, disabled or lacks permission. If you have made many attempts, wait before trying again."))}}
                    {{Faq(l.Text("Neden bazı raporlara erişemiyorum?", "Why can I not access some reports?"), l.Text("Kullanılabilir araçlar hesap rollerinize bağlıdır. Raporlar için Report veya Admin yetkisi gerekir. Yemek menüsü erişimi tek başına satış raporu erişimi sağlamaz.", "Available tools depend on your account roles. Reports require Report or Admin permission. Meal menu access alone does not grant access to sales reports."))}}
                    {{Faq(l.Text("Yemek menüleri gerçek mi?", "Are the meal menus real?"), l.Text("Şu anda yemek menüleri ve kalori bilgileri örnek veridir. Onaylanmış şirket menüsü veya beslenme önerisi olarak kullanılmamalıdır.", "Meal menus and calorie values currently use sample data. They should not be treated as confirmed company menus or dietary advice."))}}
                    {{Faq(l.Text("Bağlantıyı nasıl kaldırırım?", "How do I remove the connection?"), l.Text("Asistanınızın bağlantılar bölümünden Ulu Bilge'yi kaldırın. Sunucu tarafındaki oturumların da iptal edilmesi gerekiyorsa aşağıdaki destek adresine başvurun.", "Remove Ulu Bilge from your assistant's connections. Contact support below if server-side sessions also need to be revoked."))}}
                    {{Faq(l.Text("Türkçe ve İngilizce arasında nasıl geçerim?", "How do I switch between Turkish and English?"), l.Text("Sayfanın üstündeki Türkçe / English seçimini kullanın. Tercihiniz bir dil çerezinde bir yıl saklanır. Asistanınıza soruları istediğiniz dilde yazabilirsiniz.", "Use the Türkçe / English links at the top of the page. Your choice is stored in a language cookie for one year. You can ask your assistant questions in either language."))}}
                </section>
                <section id="contact" class="contact-box"><p class="eyebrow">{{l.Text("BİZE YAZIN", "GET IN TOUCH")}}</p><h2>{{l.Text("Birlikte bakalım.", "Let's take a look together.")}}</h2><p>{{l.Text("Yayıncı ve destek", "Publisher and support")}}: <strong>{{WebUtility.HtmlEncode(options.PublisherName)}}</strong></p>{{EmailLink(options, "contact-email")}}<p>{{l.Text("Sorunun oluştuğu zamanı, kullandığınız uygulamayı ve gördüğünüz hata mesajını paylaşın. Parola, erişim anahtarı veya hassas rapor verisi göndermeyin.", "Include the time of the issue, the app you use and the error message you saw. Do not send passwords, access tokens or sensitive report data.")}}</p></section>
            </div>
        </div>
        """;

    private static string Privacy(PageLanguage l, WebsiteOptions options)
    {
        var sections = new (string Id, string Title, string Content)[]
        {
            ("publisher", l.Text("Yayıncı ve kapsam", "Publisher and scope"),
                $"<p>{l.Text("Ulu Bilge hizmetinin yayıncısı ve gizlilik iletişim kişisi", "The publisher and privacy contact for the Ulu Bilge service is")} <strong>{WebUtility.HtmlEncode(options.PublisherName)}</strong>. {l.Text("Bu politika, bu web sitesi ile Ulu Bilge hesap bağlantısı ve raporlama hizmetinin veri işleme akışını açıklar. Kurumunuzun kaynak verilerine ilişkin talepler için kurum yetkilinizle de iletişime geçebilirsiniz.", "This policy explains data processing by this website and the Ulu Bilge account connection and reporting service. You may also contact your organization administrator about source data controlled by your organization.")}</p>"),
            ("data", l.Text("İşlenen bilgiler ve amaçları", "Information processed and purposes"),
                l.Text("<p>Giriş sırasında kullanıcı adı ve parola kimlik doğrulaması için işlenir. Hesap kimliği, roller, uygulama kayıtları, verilen erişim kapsamı ve oturum/token kayıtları bağlantıyı kurmak, yetkileri denetlemek ve oturumları yönetmek için kullanılır.</p><p>Rapor isteğinizdeki filtreler ve sorgular, izin verilen veritabanı şeması üzerinden sonuç üretmek için işlenir. Sonuçlar iş verileri ile müşteri ve çalışan bilgileri içerebilir. Destek e-postalarındaki bilgiler talebinizi yanıtlamak için kullanılır.</p>",
                    "<p>Your username and password are processed during sign-in for authentication. Account identity, roles, app registrations, granted scopes and session/token records are used to establish connections, check permissions and manage sessions.</p><p>Filters and queries in report requests are processed against the permitted database schema to produce results. Results may include business data and customer or employee information. Information in support emails is used to respond to your request.</p>")),
            ("collection", l.Text("Toplama yöntemi ve hukuki sebepler", "Collection and legal grounds"),
                l.Text("<p>Bilgiler giriş formları, bağlı uygulamadan gelen istekler, kurumun kaynak veritabanı, teknik istek kayıtları ve sizin gönderdiğiniz destek e-postaları aracılığıyla elektronik ortamda elde edilir.</p><p>Talep ettiğiniz hizmetin sunulması için gerekli işlemler sözleşmenin kurulması veya ifası; hizmet güvenliğine ilişkin işlemler, temel hak ve özgürlüklerinize zarar vermemek kaydıyla meşru menfaat; mevzuatın gerektirdiği işlemler ise hukuki yükümlülüklerin yerine getirilmesi sebeplerine dayanır. Ayrı bir açık rıza gereken bir kullanım, o kullanım için alınacak rızaya tabidir.</p>",
                    "<p>Information is collected electronically through sign-in forms, connected app requests, your organization's source database, technical request records and support emails you send.</p><p>Processing necessary to deliver the service you request relies on establishing or performing a contract. Service security processing relies on legitimate interests where these do not harm your fundamental rights and freedoms. Processing required by law relies on legal obligations. Any use requiring separate consent is subject to consent for that use.</p>")),
            ("sharing", l.Text("Bağlı uygulamalar ve paylaşım", "Connected apps and sharing"),
                l.Text("<p>Hesabınızı bağladığınız uygulama, hesabınızın rolü ve verdiğiniz erişim kapsamında araç sonuçlarını alır. Bağlantı ve oturum işlemleri için gerekli bilgiler de ilgili istemciye iletilir. Kurumunuzun verilerini hangi uygulamada kullanacağınızı, o uygulamanın sağlayıcısını ve veri işleme koşullarını bağlantıdan önce inceleyin.</p><p>Ulu Bilge sunucusu rapor oluşturmak için kendi içinde ayrıca bir yapay zekâ modeli çalıştırmaz. Sonuçların bağlı asistan tarafından işlenmesi, saklanması ve varsa yurt dışına aktarılması o sağlayıcının koşullarına ve kullandığınız kurumsal yapılandırmaya bağlıdır. Destek e-postaları, e-posta sağlayıcısı üzerinden iletilir.</p>",
                    "<p>The app you connect receives tool results within your account role and the access you grant. Information needed for connection and session handling is also sent to that client. Before connecting, review the app, its provider and its data processing terms for your organization's data.</p><p>The Ulu Bilge server does not run a separate AI model internally to generate reports. Processing, retention and any international transfer of results by the connected assistant depend on that provider's terms and your organization's configuration. Support emails are delivered through the email provider.</p>")),
            ("retention", l.Text("Saklama ve teknik kayıtlar", "Retention and technical records"),
                l.Text("<p>Hesap, istemci ve token kayıtları hizmetin veritabanında tutulur; erişim süreleri sunucu ayarlarına göre sınırlandırılır. Bir token'ın süresinin dolması, ilgili veritabanı kaydının otomatik silindiği anlamına gelmez. Kurum verilerinin saklanması kaynak sistemin politikasına bağlıdır. Hesap ve destek kayıtlarının silinmesine ilişkin talepler için aşağıdaki iletişim adresini kullanın.</p><p>Mevcut performans tanılama sistemi istek adreslerini, başlıkları, küçük istek gövdelerini ve yanıtların ilk bölümünü kaydedebilir. Bu kayıtlar kimlik doğrulama veya rapor verisi içerebilir; uygulama belleğinde en fazla 24 saat tutulur ve yeniden başlatmada kaybolur. Kalıcı sunucu/barındırma günlüklerinin saklama koşulları ilgili işletim ortamına bağlıdır.</p>",
                    "<p>Account, client and token records are kept in the service database; access lifetimes are limited by server settings. Token expiration does not mean its database record is automatically deleted. Retention of organization data follows the source system's policy. Use the contact below to request deletion of account or support records.</p><p>The current performance diagnostics system may record request URLs, headers, small request bodies and the beginning of responses. These records may contain authentication or report data; they are held in application memory for up to 24 hours and disappear on restart. Retention of persistent server or hosting logs depends on the operating environment.</p>")),
            ("cookies", l.Text("Çerezler", "Cookies"),
                l.Text("<p>Türkçe/İngilizce seçiminiz UluBilge.Language adlı işlevsel çerezde bir yıl saklanır. Giriş formunda sahte istekleri önlemek için oturumluk güvenlik çerezi kullanılır. Bu sayfalar reklam veya pazarlama izleyicisi eklemez. Tarayıcınızdan çerezleri silebilirsiniz; güvenlik çerezini engellemek giriş işlemini etkileyebilir.</p>",
                    "<p>Your Turkish/English preference is stored for one year in the functional UluBilge.Language cookie. A session security cookie protects the sign-in form against forged requests. These pages do not add advertising or marketing trackers. You can delete cookies in your browser; blocking the security cookie may affect sign-in.</p>")),
            ("rights", l.Text("Haklarınız ve iletişim", "Your rights and contact"),
                $"<p>{l.Text("Uygulanabilir mevzuat kapsamında verilerinizin işlenip işlenmediğini ve amacını öğrenme, aktarıldığı tarafları öğrenme, düzeltme veya silme isteme, yalnızca otomatik analizle aleyhinize bir sonuç doğmasına itiraz etme ve hukuka aykırı işleme nedeniyle zararınızın giderilmesini talep etme haklarınız bulunabilir. Talebinizin kapsamını belirterek", "Under applicable law, you may have rights to learn whether and why your data is processed, identify recipients, request correction or deletion, object to adverse outcomes from solely automated analysis, and seek compensation for unlawful processing. Describe your request and contact")} {EmailLink(options)}. {l.Text("Talebi yerine getirmeden önce kimliğinizin doğrulanması istenebilir.", "Identity verification may be required before a request is fulfilled.")}</p><p><a href=\"https://www.kvkk.gov.tr/Icerik/2036/Ilgili-Kisinin-Haklari\">{l.Text("KVKK: İlgili kişinin hakları", "KVKK: Data subject rights")}</a></p>"),
            ("changes", l.Text("Politika değişiklikleri", "Policy changes"),
                l.Text("<p>Hizmetin veri işleme biçimi değiştiğinde bu sayfa güncellenir. Güncel metni ve aşağıdaki destek kanalını her zaman bu adreste bulabilirsiniz.</p>", "<p>This page is updated when the service's data processing changes. The current policy and support contact remain available at this address.</p>"))
        };
        return LegalPage(l, options, l.Text("VERİLERİNİZ HAKKINDA", "ABOUT YOUR DATA"), l.Text("Gizlilik<br>politikası.", "Privacy<br>policy."), l.Text("Hangi bilgiler işlenir, nasıl kullanılır, kime başvurabilirsiniz?", "What is processed, how is it used, and who can you contact?"), sections);
    }

    private static string Terms(PageLanguage l, WebsiteOptions options)
    {
        var sections = new (string Id, string Title, string Content)[]
        {
            ("service", l.Text("Hizmetin kapsamı", "Scope of the service"),
                $"<p>{l.Text("Ulu Bilge'nin yayıncısı", "Ulu Bilge is published by")} <strong>{WebUtility.HtmlEncode(options.PublisherName)}</strong>. {l.Text("Bu koşullar web sitesinin, hesap bağlantısının, raporlama araçlarının ve örnek yemek menülerinin kullanımını düzenler. Hizmeti kullanırken bu koşulları ve kurumunuzun veri erişim kurallarını dikkate almalısınız.", "These terms govern use of the website, account connection, reporting tools and sample meal menus. When using the service, observe these terms and your organization's data access rules.")}</p>"),
            ("account", l.Text("Hesap ve erişim", "Account and access"),
                l.Text("<p>Hizmeti yalnızca size tahsis edilen hesap ve izinlerle kullanın. Kullanıcı adı, parola ve erişim token'larınızı korumak; yetkisiz kullanım fark ettiğinizde kurum yöneticisine veya desteğe bildirmek sizin sorumluluğunuzdadır. Araçların kullanılabilirliği hesap rolüne, kurum yapılandırmasına ve bağlantıda verilen erişime bağlıdır.</p><p>Bağlantıyı kullandığınız uygulamadan başlatın, Ulu Bilge ekranında uygulamayı inceleyip izin verin. Bağlantıyı iptal etmek için uygulamanın bağlantılar bölümünü kullanın; sunucu oturumlarının iptali için destekle iletişim kurun.</p>",
                    "<p>Use the service only with the account and permissions assigned to you. You are responsible for protecting your username, password and access tokens, and for reporting unauthorized use to your administrator or support. Tool availability depends on your account role, organization configuration and granted access.</p><p>Start the connection in your app and review the app on the Ulu Bilge screen before granting access. Remove the connection in your app to disconnect; contact support to revoke server sessions.</p>")),
            ("use", l.Text("Uygun kullanım", "Acceptable use"),
                l.Text("<p>Yetkiniz dışındaki verilere erişmeye, güvenlik denetimlerini aşmaya, başkasının hesabını kullanmaya veya hizmetin çalışmasını aksatmaya çalışmayın. Kurumunuza ait raporları ve kişisel verileri yalnızca paylaşmaya yetkili olduğunuz uygulamalarda ve amaçlarla kullanın. İsteklerinizin ve verileri kullanma amacınızın hukuka ve kurum kurallarına uygunluğundan sorumlusunuz.</p>",
                    "<p>Do not attempt to access data beyond your permissions, bypass security checks, use someone else's account or disrupt the service. Use organization reports and personal data only in apps and for purposes you are authorized to use. You are responsible for ensuring your requests and use of data comply with law and organization rules.</p>")),
            ("reports", l.Text("Raporlar ve örnek içerik", "Reports and sample content"),
                l.Text("<p>Rapor sonuçları kaynak veriye, seçilen döneme, filtrelere ve sorgunun tanımına bağlıdır. Özellikle ciro, kâr ve net satış gibi ölçütlerin ne anlama geldiğini belirtin. Önemli iş kararlarından önce sonuçları kaynak kayıtlarla doğrulayın; hizmet kesintisiz erişim veya her soruya eksiksiz sonuç garantisi vermez.</p><p>Yemek menüleri ve kalori değerleri şu anda örnek veridir. Gerçek şirket menüsü, alerjen bilgisi veya beslenme tavsiyesi olarak değerlendirilmemelidir. Ana sayfadaki grafik görsel temsildir.</p>",
                    "<p>Report results depend on source data, the selected period, filters and query definitions. Clarify metrics such as revenue, profit and net sales. Verify results against source records before important business decisions; the service does not guarantee uninterrupted access or a complete answer to every question.</p><p>Meal menus and calorie values currently use sample data. They must not be treated as actual company menus, allergen information or dietary advice. The chart on the home page is illustrative.</p>")),
            ("third-parties", l.Text("Bağlı uygulamalar ve gizlilik", "Connected apps and privacy"),
                $"<p>{l.Text("Bağlantı kurduğunuz asistanın veya uygulamanın kendi koşulları ve gizlilik politikası ayrıca geçerlidir. Verileri ilgili uygulamaya aktarmaya yetkili olduğunuzdan emin olun. Ulu Bilge'nin veri işleme akışı", "Your connected assistant or app has its own terms and privacy policy. Make sure you are authorized to transfer data to that app. Ulu Bilge's data processing is described in the")} <a href=\"{l.Link("/privacy")}\">{l.Text("Gizlilik politikası", "Privacy policy")}</a> {l.Text("sayfasında açıklanır.", "page.")}</p>"),
            ("availability", l.Text("Değişiklikler ve erişimin sonlandırılması", "Changes and termination of access"),
                l.Text("<p>Hizmetin özellikleri bakım, güvenlik ve geliştirme ihtiyaçlarına göre değişebilir. Güvenliği tehdit eden kullanım, kurum yetkisinin kaldırılması veya bu koşullara aykırılık durumunda erişim sınırlandırılabilir ya da sonlandırılabilir. Kanunen vazgeçilemeyen haklarınız bu koşullarla sınırlandırılmaz. Güncel koşullar bu sayfada yayımlanır.</p>",
                    "<p>Features may change for maintenance, security or development needs. Access may be limited or terminated for use that threatens security, removal of organization permissions or violations of these terms. These terms do not limit rights that cannot legally be waived. Current terms are published on this page.</p>")),
            ("contact", l.Text("İletişim", "Contact"),
                $"<p>{l.Text("Bu koşullar, erişim sorunları ve hizmetle ilgili talepleriniz için", "For questions about these terms, access issues or the service, contact")} {EmailLink(options)} {l.Text("adresinden yayıncıya ulaşabilirsiniz.", "to reach the publisher.")}</p>")
        };
        return LegalPage(l, options, l.Text("ORTAK KULLANIM ÇERÇEVEMİZ", "OUR SHARED TERMS"), l.Text("Kullanım<br>koşulları.", "Terms of<br>service."), l.Text("Ulu Bilge ile çalışırken hizmetin kapsamını ve sorumluluklarınızı bilin.", "Understand the service and your responsibilities when working with Ulu Bilge."), sections);
    }

    private static string LegalPage(PageLanguage l, WebsiteOptions options, string eyebrow, string heading, string description, (string Id, string Title, string Content)[] sections) => $$"""
        <section class="page-heading"><p class="eyebrow">{{eyebrow}}</p><h1>{{heading}}</h1><p>{{description}}</p><p class="updated">{{l.Text("Son güncelleme: 3 Kasım 2026", "Last updated: 3 November 2026")}}</p></section>
        <div class="content-grid">
            <aside class="sidebar"><p class="eyebrow">{{l.Text("İÇİNDEKİLER", "CONTENTS")}}</p><nav aria-label="{{l.Text("İçindekiler", "Contents")}}">{{string.Join("", sections.Select(section => $"<a href=\"#{section.Id}\">{section.Title}</a>"))}}</nav><div class="publisher"><p>{{l.Text("Yayıncı", "Publisher")}}</p><strong>{{WebUtility.HtmlEncode(options.PublisherName)}}</strong>{{EmailLink(options)}}</div></aside>
            <article class="article-content">{{string.Join("", sections.Select((section, index) => $"<section id=\"{section.Id}\"><p class=\"section-number\">{index + 1:00} /</p><h2>{section.Title}</h2>{section.Content}</section>"))}}</article>
        </div>
        """;

    private static string Faq(string question, string answer) => $"<details><summary>{question}</summary><p>{answer}</p></details>";

    private static string McpSettings(PageLanguage l) => $$"""
        <dl class="mcp-settings">
            <div><dt>{{l.Text("Sunucu adı", "Server name")}}</dt><dd>Ulu Bilge</dd></div>
            <div><dt>MCP Server URL</dt><dd><code>https://ulubilge.uretimsistemi.com/mcp</code></dd></div>
            <div><dt>{{l.Text("Bağlantı türü", "Transport")}}</dt><dd>Streamable HTTP</dd></div>
            <div><dt>{{l.Text("Kimlik doğrulama", "Authentication")}}</dt><dd>OAuth 2.0 / PKCE</dd></div>
            <div><dt>{{l.Text("İstemci kaydı", "Client registration")}}</dt><dd>{{l.Text("Otomatik (DCR)", "Automatic (DCR)")}}</dd></div>
        </dl>
        """;

    private static string EmailLink(WebsiteOptions options, string cssClass = "")
    {
        // Cloudflare must preserve these public links: the page CSP blocks its email decoding script.
        // https://developers.cloudflare.com/waf/tools/scrape-shield/email-address-obfuscation/
        var classAttribute = cssClass.Length == 0 ? "" : $" class=\"{WebUtility.HtmlEncode(cssClass)}\"";
        var href = WebUtility.HtmlEncode("mailto:" + options.SupportEmail);
        return $"<!--email_off--><a{classAttribute} href=\"{href}\">{WebUtility.HtmlEncode(options.SupportEmail)}</a><!--/email_off-->";
    }
}
