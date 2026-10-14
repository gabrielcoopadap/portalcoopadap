# AgroCert — telas em Angular

Telas do AgroCert (Certificação, Ambiental, Corredores Ecológicos, Fauna e Flora e Clima) em **Angular**, para entrar no
Portal Coopadap. O backend em C# (.NET 8) continua o mesmo, agora só como **API** (`/api/...`).

```
agrocert-web/
├── projects/agrocert/      ← a biblioteca (é isto que vai para o portal)
│   └── src/lib/
│       ├── agrocert.routes.ts   rotas CERTIFICACAO_ROUTES e CLIMA_ROUTES (todas lazy)
│       ├── config.ts            provideAgroCert({ apiUrl })
│       ├── api/                 cliente da API (AgroCertApi) e modelos
│       ├── layout/              casca das telas + itens de menu (<ac-menu-certificacao>, <ac-menu-clima>)
│       ├── compartilhado/       modal, avisos, pílulas, tooltip, imagens com token
│       ├── util/                rótulos, cores e formatação pt-BR
│       ├── estilos/             CSS do módulo (tudo prefixado: .cert-modulo / .clima-modulo)
│       └── telas/               uma pasta por área (painel, certificacoes, requisitos, vistorias,
│                                ambiental, corredores, fauna-flora, clima)
└── projects/portal-demo/   ← app que imita o portal, só para testar
```

Requisitos: Node 18.19+ (ou 20/22) e Angular 18 ou mais novo no portal.

## Rodar no computador (para testar)

1. Suba o backend: no Visual Studio, rode o `AgroCert.Portal.Teste` (ele responde em `https://localhost:7180`
   ou `http://localhost:5080`, conforme o perfil).
2. O proxy (`projects/portal-demo/proxy.conf.json`) aponta para `https://localhost:7180`; ajuste se o seu for outro.
3. Nesta pasta:
   ```
   npm install
   npx ng serve portal-demo
   ```
4. Abra http://localhost:4200/certificacao e http://localhost:4200/clima

O `ng serve` repassa `/api` e `/uploads` para o backend (proxy), então não precisa de CORS em desenvolvimento.

## Integrar no portal (Viceris)

### 1. Trazer o código

Escolha uma das formas:

- **Como pacote:** `npx ng build agrocert` gera `dist/agrocert`; `cd dist/agrocert && npm pack` gera
  `agrocert-0.0.1.tgz`, que o portal instala com `npm install ./agrocert-0.0.1.tgz`.
- **Como código-fonte:** copiar `projects/agrocert` para dentro do workspace do portal e apontar o `paths` do
  `tsconfig.json` (`"agrocert": ["projects/agrocert/src/public-api.ts"]`).

Dependência extra: `npm install leaflet@^1.9.4` (mapas).

### 2. angular.json do portal (styles)

```json
"styles": [
  "node_modules/leaflet/dist/leaflet.css",
  "node_modules/material-icons/iconfont/material-icons.css",
  ...
]
```

Ícones: o módulo usa **Material Icons** (`<span class="material-icons">`). Se o portal já carrega a fonte, não precisa
adicionar. A fonte do texto segue a do portal (Redonda), com Roboto como reserva.

### 3. app.config.ts do portal

```ts
import { provideAgroCert } from 'agrocert';

providers: [
  provideHttpClient(withInterceptorsFromDi()),   // o MSAL coloca o token do Entra ID
  provideAgroCert({ apiUrl: '' }),               // '' = API no mesmo domínio (rota /api no load balancer)
]
```

No `MsalInterceptor`, inclua a API do AgroCert no `protectedResourceMap`
(ex.: `['/api/', ['api://<id-do-app-agrocert>/acesso']]`). Todas as chamadas do módulo passam pelo `HttpClient`,
inclusive downloads de Excel/PDF e imagens protegidas, então o token vai sempre junto.

### 4. Rotas

```ts
{ path: 'certificacao', loadChildren: () => import('agrocert').then(m => m.CERTIFICACAO_ROUTES), canActivate: [MsalGuard] },
{ path: 'clima',        loadChildren: () => import('agrocert').then(m => m.CLIMA_ROUTES),        canActivate: [MsalGuard] },
```

Se montar em outro caminho, informe em `provideAgroCert({ rotaCertificacao: '/outro', rotaClima: '/outro-clima' })`.

### 5. Menu

Use os componentes prontos (mesmas classes do menu do portal):

```html
<ac-menu-certificacao></ac-menu-certificacao>
<ac-menu-clima></ac-menu-clima>
```

ou monte o próprio menu com `ITENS_MENU_CERTIFICACAO`.

## Backend (API) em produção

No `appsettings` (ou variáveis de ambiente) do serviço AgroCert:

```json
"Autenticacao": { "Habilitada": true, "Modo": "Api" },
"AzureAd": { "Instance": "https://login.microsoftonline.com/", "TenantId": "...", "ClientId": "...", "Audience": "api://..." },
"AgroCert": { "Acesso": { "ExigirLogin": true } }
```

- `Modo: "Api"` = o backend aceita o token (Bearer) que o Angular envia. Com `Modo: "Telas"` ele continua servindo as
  telas Razor antigas com login próprio.
- Com o Angular e a API no mesmo domínio não há CORS. Se ficarem em domínios diferentes, liste a origem do portal em
  `AgroCert:Cors:Origens`.
- `/uploads` (fotos) é servido sem login, com nomes aleatórios (GUID).

## O que mudou em relação às telas Razor

Mesmo conteúdo, textos e visual. Diferenças pequenas:

- Mensagens de sucesso/erro aparecem no topo da tela, sem recarregar a página.
- PDFs (vistoria, ficha ambiental) abrem numa aba nova a partir do Angular, com o token.
- Relatórios para imprimir (conformidade e corredores) imprimem só o relatório, sem o menu do portal (botão "Gerar PDF").
