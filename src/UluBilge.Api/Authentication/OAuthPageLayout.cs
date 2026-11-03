using System.Net;
using UluBilge.Api.Website;

namespace UluBilge.Api.Authentication
{
    internal static class OAuthPageLayout
    {
        public static string Render(string title, string posterHeading, string posterCopy, string content, PageLanguage language, string languageSwitcher, string additionalStyles = "")
        {
            return $$"""
                <!doctype html>
                <html lang="{{language.Code}}">
                <head>
                    <meta charset="utf-8">
                    <meta name="viewport" content="width=device-width, initial-scale=1">
                    <title>{{WebUtility.HtmlEncode(title)}}</title>
                    <link rel="icon" href="/favicon.ico">
                    <style>
                        :root {
                            color-scheme: light;
                            font-family: Arial, "Helvetica Neue", sans-serif;
                            color: #171e43;
                            background: #2944ee;
                            font-synthesis: none;
                            --ink: #171e43;
                            --blue: #2944ee;
                            --lime: #e9ff64;
                            --pink: #ffd3e5;
                            --paper: #fffdf7;
                        }

                        * { box-sizing: border-box; }
                        body { margin: 0; }
                        h1, h2, p { margin: 0; }
                        button, input { font: inherit; }
                        svg { display: block; flex-shrink: 0; }

                        .page {
                            display: flex;
                            flex-direction: column;
                            width: 100%;
                            max-width: 1224px;
                            min-height: 100vh;
                            min-height: 100svh;
                            margin: 0 auto;
                            padding: 0 32px 32px;
                        }

                        .masthead {
                            display: flex;
                            align-items: center;
                            justify-content: space-between;
                            gap: 24px;
                            flex-wrap: wrap;
                            padding: 24px 0;
                            color: #fff;
                        }
                        .wordmark { color: inherit; font-size: 25px; font-weight: 700; letter-spacing: -1.2px; text-decoration: none; }
                        .wordmark span { color: var(--pink); }
                        .page-caption { font-size: 12px; letter-spacing: .1px; }
                        .masthead-tools { display: flex; align-items: center; flex-wrap: wrap; justify-content: flex-end; gap: 20px; margin-left: auto; }
                        .language-picker { display: flex; gap: 4px; padding: 4px; border: 1px solid #a7b4ff; border-radius: 3px; font-size: 12px; }
                        .language-picker a { padding: 7px 10px; border-radius: 1px; color: white; text-decoration: none; }
                        .language-picker a[aria-current] { color: var(--ink); background: var(--lime); }
                        .site-footer { display: flex; flex-wrap: wrap; justify-content: space-between; gap: 16px; padding-top: 24px; color: white; font-size: 11px; line-height: 1.7; }
                        .site-footer nav { display: flex; flex-wrap: wrap; gap: 20px; }
                        .site-footer a { color: inherit; text-underline-offset: 4px; }
                        .masthead a:focus-visible, .site-footer a:focus-visible { outline: 3px solid var(--lime); outline-offset: 4px; }
                        .skip-link { position: absolute; z-index: 10; top: -100px; left: 24px; padding: 12px; color: var(--ink); background: var(--lime); }
                        .skip-link:focus { top: 10px; }

                        main {
                            display: grid;
                            grid-template-columns: minmax(0, 1.08fr) minmax(0, 1fr);
                            flex: 1;
                            width: 100%;
                            border: 2px solid var(--ink);
                            border-radius: 3px;
                            overflow: hidden;
                            background: var(--paper);
                        }

                        .poster {
                            position: relative;
                            min-height: 590px;
                            padding: 36px;
                            overflow: hidden;
                            border-right: 2px solid var(--ink);
                            background: var(--lime);
                        }
                        .poster-label { font-size: 13px; font-weight: 700; }
                        .poster h2 {
                            position: relative;
                            z-index: 1;
                            margin-top: 32px;
                            font-size: clamp(48px, 5.8vw, 76px);
                            font-weight: 700;
                            line-height: 1.02;
                            letter-spacing: -.065em;
                        }
                        .poster-copy {
                            position: relative;
                            z-index: 1;
                            max-width: 285px;
                            margin-top: 24px;
                            font-size: 14px;
                            line-height: 1.7;
                        }
                        .poster-art {
                            position: absolute;
                            right: 24px;
                            bottom: -40px;
                            width: 310px;
                            transform: rotate(-12deg);
                        }
                        .poster-art svg { width: 100%; height: auto; }

                        .sign-in {
                            display: flex;
                            flex-direction: column;
                            justify-content: center;
                            min-width: 0;
                            padding: 32px 36px;
                        }
                        h1 { font-size: 29px; font-weight: 700; line-height: 1.2; letter-spacing: -1px; }
                        .description { margin-top: 8px; color: #53576d; font-size: 13px; line-height: 1.6; }

                        .request {
                            margin: 22px -36px 24px;
                            padding: 15px 36px;
                            border-top: 1px solid var(--ink);
                            border-bottom: 1px solid var(--ink);
                            background: var(--pink);
                        }
                        .client-name { font-size: 14px; font-weight: 700; line-height: 1.5; overflow-wrap: anywhere; }
                        .scope { margin-top: 3px; font-size: 13px; line-height: 1.6; }
                        .duration { margin-top: 9px; color: #4b4058; font-size: 11px; line-height: 1.6; }

                        .field + .field { margin-top: 18px; }
                        label { display: block; margin-bottom: 8px; font-size: 13px; font-weight: 700; }
                        input {
                            display: block;
                            width: 100%;
                            min-height: 48px;
                            padding: 12px 14px;
                            border: 1px solid #7f8192;
                            border-radius: 2px;
                            color: var(--ink);
                            background: #fff;
                            font-size: 16px;
                        }
                        input:hover { border-color: var(--ink); }
                        input:focus-visible { border-color: var(--blue); outline: 3px solid var(--blue); outline-offset: 2px; }

                        .error {
                            margin-bottom: 22px;
                            padding: 12px 14px;
                            border: 1px solid #a0302b;
                            border-radius: 2px;
                            color: #8d2724;
                            background: #fff0e9;
                            font-size: 13px;
                            line-height: 1.6;
                            overflow-wrap: anywhere;
                        }

                        .actions {
                            display: grid;
                            grid-template-columns: minmax(0, 1fr) auto;
                            align-items: center;
                            gap: 16px;
                            margin-top: 26px;
                        }
                        button {
                            display: flex;
                            align-items: center;
                            justify-content: center;
                            gap: 12px;
                            min-height: 50px;
                            padding: 12px 16px;
                            border: 1px solid var(--ink);
                            border-radius: 2px;
                            font-size: 13px;
                            font-weight: 700;
                            line-height: 1.5;
                            cursor: pointer;
                        }
                        button:focus-visible { outline: 3px solid var(--blue); outline-offset: 3px; }
                        .allow { justify-content: space-between; color: var(--ink); background: var(--lime); }
                        .allow:hover { background: #d9ef4e; }
                        .deny {
                            min-width: 48px;
                            padding-right: 0;
                            padding-left: 0;
                            border-color: transparent;
                            color: #464b64;
                            background: transparent;
                            font-weight: 400;
                            text-decoration: underline;
                            text-underline-offset: 4px;
                        }
                        .deny:hover { color: var(--blue); }
                        .form-note { margin-top: 18px; color: #53576d; font-size: 11px; line-height: 1.65; }

                        @media (min-width: 801px) and (max-width: 980px) {
                            .page { padding-right: 24px; padding-left: 24px; }
                            .poster { padding: 28px; }
                            .poster h2 { font-size: 54px; }
                            .poster-art { right: 0; width: 275px; }
                            .sign-in { padding-right: 28px; padding-left: 28px; }
                            .request { margin-right: -28px; margin-left: -28px; padding-right: 28px; padding-left: 28px; }
                            .actions { gap: 12px; }
                            button { gap: 8px; padding-right: 12px; padding-left: 12px; }
                        }

                        @media (max-width: 800px) {
                            .page { max-width: 660px; padding: 0 24px 24px; }
                            .masthead { padding: 22px 0; }
                            main { display: block; flex: none; }
                            .poster { min-height: 220px; padding: 28px; border-right: 0; border-bottom: 2px solid var(--ink); }
                            .poster h2 { margin-top: 20px; font-size: 50px; }
                            .poster-copy { display: none; }
                            .poster-art { right: 22px; bottom: -60px; width: 220px; }
                            .sign-in { padding: 30px 28px; }
                            .request { margin-right: -28px; margin-left: -28px; padding-right: 28px; padding-left: 28px; }
                        }

                        @media (max-width: 640px) {
                            .poster-art { display: none; }
                        }

                        @media (max-width: 540px) {
                            .page { padding: 0 16px 20px; }
                            .masthead { padding: 19px 0; }
                            .wordmark { font-size: 23px; }
                            .page-caption { font-size: 11px; }
                            .masthead-tools { gap: 12px; }
                            .masthead-tools .page-caption { display: none; }
                            .language-picker { font-size: 11px; }
                            .language-picker a { padding: 6px 8px; }
                            .poster { min-height: 0; padding: 22px 24px; }
                            .poster-label { font-size: 12px; }
                            .poster h2 { margin-top: 16px; font-size: 39px; line-height: 1; letter-spacing: -.06em; }
                            .sign-in { padding: 26px 24px; }
                            h1 { font-size: 27px; }
                            .request { margin: 20px -24px 22px; padding: 13px 24px; }
                            .actions { gap: 12px; margin-top: 24px; }
                            button { gap: 8px; padding-right: 12px; padding-left: 12px; font-size: 12px; }
                            .form-note { margin-top: 15px; }
                        }

                        @media (forced-colors: active) {
                            input, button, .error { border: 1px solid ButtonText; }
                        }
                        {{additionalStyles}}
                    </style>
                </head>
                <body>
                    <a class="skip-link" href="#main">{{language.Text("İçeriğe geç", "Skip to content")}}</a>
                    <div class="page">
                        <header class="masthead">
                            <a class="wordmark" href="{{language.Link("/")}}">Ulu Bilge<span aria-hidden="true">.</span></a>
                            <div class="masthead-tools">
                                <p class="page-caption">{{language.Text("Raporlama / Hesap bağlantısı", "Reporting / Account connection")}}</p>
                                {{languageSwitcher}}
                            </div>
                        </header>

                        <main id="main">
                            <section class="poster" aria-labelledby="poster-title">
                                <p class="poster-label">{{language.Text("Ulu Bilge Uygulama bağlantısı", "Ulu Bilge App connection")}}</p>
                                <h2 id="poster-title">{{posterHeading}}</h2>
                                <p class="poster-copy">{{WebUtility.HtmlEncode(posterCopy)}}</p>
                                <div class="poster-art" aria-hidden="true">
                                    <svg viewBox="0 0 300 310" fill="none">
                                        <path d="M24 20h72v138c0 35 21 56 54 56s54-21 54-56V20h72v140c0 82-50 132-126 132S24 242 24 160V20Z" fill="#2944ee" />
                                        <path d="M204 20h72v72h-72V20Z" fill="#ff8fba" />
                                    </svg>
                                </div>
                            </section>

                            {{content}}
                        </main>
                        {{language.Footer()}}
                    </div>
                </body>
                </html>
                """;
        }
    }
}
