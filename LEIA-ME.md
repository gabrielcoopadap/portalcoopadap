# AgroCert — Módulo **Certificação** (C# / ASP.NET Core 8)

Conversão dos módulos Java (Spring Boot) + React do AgroCert para C#, estruturada para virar o
módulo **Certificação** do Portal Coopadap — com as mesmas cores, fonte e menu do portal.

```
Atendimentos
 ├─ Meu Estoque
 ├─ ...
 ├─ Visita Técnica
 └─ Certificação            ← novo
     ├─ Gestão de Certificações   /certificacao
     ├─ Certificações Ativas      /certificacao/certificacoes
     ├─ Requisitos                /certificacao/requisitos
     ├─ Vistorias                 /certificacao/vistorias
     ├─ Ambiental                 /certificacao/ambiental
     ├─ Corredores Ecológicos     /certificacao/corredores
     └─ Fauna e Flora             /certificacao/fauna-flora
 └─ Clima e El Niño         /clima   ← abas: Previsão · El Niño/La Niña · Validação · Base de dados
```

---

## 1. Como rodar no Visual Studio (teste)

Pré-requisitos: **Visual Studio 2022 (17.8 ou mais novo)** com a carga "ASP.NET e desenvolvimento Web",
**PostgreSQL** (com pgAdmin) e internet na primeira compilação para baixar os pacotes NuGet.

1. No pgAdmin, crie o banco `agrocert` (ou deixe a primeira execução criar).
2. Guarde a conexão com a senha nos **Segredos do Usuário** (botão direito em *AgroCert.Portal.Teste* →
   *Gerenciar Segredos do Usuário*), nunca no `appsettings.json`:
   ```json
   { "ConnectionStrings:AgroCert": "Host=localhost;Port=5432;Database=agrocert;Username=postgres;Password=<senha>" }
   ```
3. **Só na primeira vez**: gere a migration inicial (cria as tabelas no schema `certificacao`). Na pasta
   do projeto, no PowerShell:
   ```
   powershell -ExecutionPolicy Bypass -File banco\gerar-migracao-inicial.ps1
   ```
   Isso cria `src/AgroCert.Infrastructure/Persistencia/Migrations`, que vai junto para o Git.
4. Abra `AgroCert.sln`, confirme **AgroCert.Portal.Teste** como projeto de inicialização e aperte **F5**.
   O navegador abre em `https://localhost:7180/certificacao`.

Ao subir, o sistema aplica as migrations pendentes e carrega os dados de exemplo (4 certificações,
74 requisitos SMETA com o guia "como cumprir" e o catálogo de fauna e flora).
Para zerar os dados, apague e recrie o banco `agrocert` no pgAdmin.

Endereços úteis com o sistema rodando:
- `https://localhost:7180/swagger`: documentação da API (só em Development).
- `https://localhost:7180/health`: responde `Healthy` quando o PostgreSQL está acessível.

Mudou uma entidade? Gere uma nova migration: `banco\nova-migracao.ps1 NomeDaMudanca`.

> O host de teste imita o layout do portal (menu verde, cabeçalho, “Bem-vindo(a) de volta”).
> Os outros itens do menu (Meu Estoque, Romaneio…) só compõem a tela.

### Fonte "Redonda"
O portal usa a fonte licenciada **Redonda**. Ela não vem no pacote: copie os 4 arquivos `.otf`
(`Redonda-light`, `-regular`, `-medium`, `-bold`) para
`src/AgroCert.Certificacao/wwwroot/fonts/`. Sem eles, as telas usam Roboto (mesmo fallback do portal).

### Credenciais das integrações (opcional para o teste)
As telas funcionam sem nada configurado; só os botões **Ver alertas** (MapBiomas), **Consultar NDVI**
(Copernicus) e o cadastro pelo **SICAR** mostram uma mensagem explicando o que falta.
No Visual Studio: botão direito em *AgroCert.Portal.Teste* → **Gerenciar Segredos do Usuário**:

```json
{
  "AgroCert:Ambiental:MapBiomas:Email": "seu-email",
  "AgroCert:Ambiental:MapBiomas:Senha": "sua-senha",
  "AgroCert:Ambiental:Copernicus:ClientId": "...",
  "AgroCert:Ambiental:Copernicus:ClientSecret": "..."
}
```
Enquanto não houver acesso ao SICAR, cadastre a propriedade marcando *“vou colar a geometria
manualmente”* (aceita Polygon, MultiPolygon, Feature ou FeatureCollection; a área é calculada).

---

## 2. Estrutura da solução

```
AgroCert.sln
└─ src/
   ├─ AgroCert.Domain           Entidades e enums (Certificacao, Requisito, Vistoria, Apontamento,
   │                            Propriedade, AlertaDesmatamento, LeituraNdvi) — sem dependências
   ├─ AgroCert.Application      Regras de negócio: serviços, DTOs, motor de conformidade,
   │                            GeoUtil (GeoJSON/WKT, área, interseção) e interfaces
   ├─ AgroCert.Infrastructure   EF Core + PostgreSQL (migrations), repositórios, carga inicial,
   │                            clientes SICAR / MapBiomas / Copernicus, PDFs (MigraDoc) e Excel
   ├─ AgroCert.Certificacao     O MÓDULO: Razor Class Library com as telas (Area "Certificacao"),
   │                            API REST (/api/...), CSS/JS, ícones, Leaflet e o item de menu
   └─ AgroCert.Portal.Teste     Host só para teste (layout do portal + Program.cs)
tests/
   └─ AgroCert.Testes           Testes xUnit das regras críticas (dotnet test)
banco/                          Scripts para gerar migrations
agrocert-web/                   Telas em Angular (biblioteca "agrocert" + app de teste "portal-demo")
.github/workflows/ci.yml        Build e testes automáticos a cada push (GitHub Actions)
```

Correspondência com o projeto Java:

| Java (Spring)                              | C# (.NET 8)                                              |
|--------------------------------------------|----------------------------------------------------------|
| `model/*.java` (JPA)                        | `AgroCert.Domain/Entidades`                              |
| `repository/*Repository` (Spring Data)      | `Infrastructure/Persistencia/Repositorios` (EF Core)     |
| `service/*Service`                          | `Application/Servicos`                                   |
| `dto/*` (records)                           | `Application/Dtos`                                       |
| `controller/*Controller` (`/api/...`)       | `Certificacao/Controllers/Api` (mesmas rotas e JSON)     |
| `config/*Seeder` (CommandLineRunner)        | `Infrastructure/Persistencia/Seed/InicializadorBanco`    |
| `application.properties`                    | `appsettings.json` → seção `AgroCert`                    |
| iText 7 (PDF)                               | PDFsharp/MigraDoc (licença MIT)                          |
| ExcelJS no front (lista mestra)             | Gerador .xlsx no servidor (sem dependência)              |
| PostGIS `ST_Intersects` + JTS               | `GeoUtil` em memória (funciona em qualquer banco)        |
| Telas React                                 | Views Razor em `Areas/Certificacao/Views`                |

---

## 3. Integração com o portal (pauta para a reunião com a Viceri)

O front do portal é **Angular** (Angular Material) e o login é pelo **Microsoft Entra ID**.
O módulo foi preparado para três caminhos — a escolha depende de como o portal está estruturado:

**A) Módulo como sub-aplicação** (mais rápido): publicar o `AgroCert.Portal.Teste` (ou um host
mínimo) em `https://portal.coopadap.com.br/certificacao`, no mesmo Entra ID. O menu do Angular só
aponta para essas rotas. As telas já estão no visual do portal.

**B) Telas em Angular + esta API**: a Viceri refaz as telas no Angular consumindo a API REST
(mesmas rotas do Java): `/api/certificacoes`, `/api/requisitos`, `/api/vistorias`, `/api/ambiental`, `/api/fauna-flora`.
PDFs: `/api/vistorias/{id}/relatorio/pdf`, `/api/ambiental/propriedades/{id}/relatorio.pdf`.
Excel: `/api/requisitos/lista-mestra.xlsx?certificacao=GRASP`.

**C) Backend do portal em ASP.NET Core MVC**: referenciar `AgroCert.Certificacao` e adicionar:

```csharp
builder.Services.AddModuloCertificacao(builder.Configuration);
...
app.UseModuloCertificacao();                 // fotos em /uploads
await app.InicializarModuloCertificacaoAsync();
```
e no layout: `<partial name="_MenuCertificacao" />` logo depois de "Visita Técnica".

Item de menu para o Angular (mesmo formato do `menu` do portal, depois de `VISITS_HISTORY`):

```ts
{
  name: "Certificação", icon: "workspace_premium", expanded: false, showArrowIcon: true,
  children: [
    { name: "Gestão de Certificações", match: "/certificacao",              icon: "dashboard" },
    { name: "Certificações Ativas",    match: "/certificacao/certificacoes", icon: "verified" },
    { name: "Requisitos",              match: "/certificacao/requisitos",    icon: "checklist" },
    { name: "Vistorias",               match: "/certificacao/vistorias",     icon: "fact_check" },
    { name: "Ambiental",               match: "/certificacao/ambiental",     icon: "eco" },
    { name: "Fauna e Flora",           match: "/certificacao/fauna-flora",   icon: "forest" },
  ],
},
{ name: "Clima e El Niño", match: "/clima", icon: "thermostat" },
```

### Login (Microsoft Entra ID)
No `appsettings.json` do host: preencher `AzureAd` (TenantId, ClientId), `Autenticacao:Habilitada = true`
e `AgroCert:Acesso:ExigirLogin = true`. Opcional: `AgroCert:Acesso:Perfis = ["Certificacao"]` para liberar
só quem tiver esse *App Role*. Quem tiver o App Role **Consultor** abre vistorias mensais por padrão.

### Banco de dados
- **PostgreSQL**, num banco só do AgroCert. As tabelas ficam no schema `certificacao`.
- A estrutura é controlada por **EF Core Migrations** (`Persistencia/Migrations`). Na subida, o sistema
  aplica as pendentes (`AgroCert:Banco:AplicarMigracoes`). Em produção, se preferir aplicar à mão, desligue
  essa opção e gere o script: `dotnet ef migrations script -p src/AgroCert.Infrastructure -s src/AgroCert.Portal.Teste`.
- No servidor, a conexão vai na variável de ambiente `ConnectionStrings__AgroCert` (ou AWS Secrets Manager).
- Datas são gravadas como `timestamp without time zone`. Os nomes de colunas seguem o C#
  (ex.: `"CarCodigo"`): em SQL manual use aspas.

### Arquivos (fotos e imagens de satélite)
Ficam em `AgroCert:Uploads:Diretorio` e são servidos em `/uploads`. Na AWS com mais de uma instância,
implemente `IArmazenamentoArquivos` com S3 e registre no lugar de `ArmazenamentoLocal`.
As fontes dos PDFs (Liberation Sans, OFL) vão junto no build — funciona em Windows e Linux.

### Fauna e Flora (módulo novo)
- **Espécies** (catálogo: nome popular/científico, fauna/flora, grupo, status de conservação, foto) e
  **registros de ocorrência** (avistamento, câmera de monitoramento, vestígio ou coleta; data/hora,
  lat/lon, área, foto). Tabelas `especies` e `registros_biodiversidade`.
- A área de preservação do painel soma **Reserva Legal + APP** das propriedades do módulo Ambiental,
  e o mapa mostra esses polígonos.
- Na primeira execução é carregado um **catálogo de exemplo** (31 espécies do Cerrado) com registros
  e a propriedade "Fazenda Demonstração". Para começar sem exemplo: `AgroCert:Banco:CarregarDadosIniciais = false`.
- As fotos de espécies não vêm no pacote: envie fotos próprias (ou com licença) pelo catálogo.
  Sem foto, o card mostra o ícone do grupo na cor do grupo.
- Latitude/longitude podem vir do clique no mapa, do GPS do celular ("Usar minha localização") ou digitadas.

### Corredores Ecológicos (Certificação → Corredores Ecológicos)

Mostra as matas interligadas nas fazendas dos cooperados: corredores (cada um com sua cor e tamanho),
fragmentos isolados, pontos de ligação a restaurar, situação de cada fazenda/Reserva Legal e a comparação 2008 → 2025.
A tela só lê o resultado gerado por duas ferramentas de linha de comando (pasta `ferramentas`):

```
dados-corredores\
  Car_Coopadap.xlsx                 planilha dos cooperados (aba "Clientes x CAR")
  MG_<Município>_<TEMA>.gpkg        GeoPackages do SICAR (APP, RESERVA_LEGAL, VEGETACAO_NATIVA, AREA_CONSOLIDADA, AREA_IMOVEL)
  mapbiomas_regiao_<ano>.tif        MapBiomas recortado (gerado pelo RecortarMapBiomas)
  resultado\                        gerado pelo ProcessarCorredores (a tela lê daqui)
```

```
dotnet run -c Release --project ferramentas\RecortarMapBiomas -- dados-corredores      (só se trocar os mapas do Brasil)
dotnet run -c Release --project ferramentas\ProcessarCorredores -- dados-corredores    (~1-2 min)
```

Camada **"O que a lei exige recompor"** (hachurada): APP marcada "a recompor" no CAR e Reserva Legal sem vegetação
nativa no último ano do MapBiomas. Pontos de ligação que passam por essas áreas sobem na prioridade (marcados em azul).
**Relatório por fazenda** (ícone de PDF na tabela ou no balão da fazenda): página A4 com mapa, situação da RL, o que a lei
pede recompor, trechos a restaurar (A, B, C, com mudas estimadas) e a mudança desde 2008 — "Gerar PDF" → "Salvar como PDF".

Rode o ProcessarCorredores de novo sempre que mudar a planilha, baixar mais municípios do SICAR ou um ano novo do MapBiomas.
A pasta do resultado é configurável: `"AgroCert": { "Corredores": { "Pasta": "../../dados-corredores/resultado" } }`
(caminho relativo à pasta do projeto do portal, ou absoluto).

### Relatório de conformidade ambiental (Ambiental → imóvel → "Relatório de conformidade")

Página A4 para imprimir/salvar em PDF com: parecer por critério (Rainforest — sem conversão desde 01/01/2014;
ATITUDES — supressão após 22/07/2008 só com autorização e nenhuma após 31/12/2020; fora de UC e de zona de
amortecimento; alertas do MapBiomas Alerta), mapa do que mudou, vegetação nativa por área (imóvel, RL, APP),
cada área convertida com imagens antes × depois (MapBiomas de cada ano e, com o Copernicus configurado,
Sentinel-2 cor real e NDVI de 2016, 2020 e da última seca), UCs próximas e NDVI mensal.

Dados que precisam estar nas pastas:
```
dados-corredores\mapbiomas_regiao_<ano>.tif   2008, 2014, 2020 e o mais recente (recorte com o RecortarMapBiomas,
                                               ou o arquivo do Brasil inteiro com o ano no nome)
dados-ambiental\ucs\                          shapefile das unidades de conservação (o .zip do CNUC/MMA serve;
                                               arquivos com "amortec" no nome = zonas de amortecimento)
```
Sem o mapa de 2014 ou de 2020 o relatório usa o ano anterior mais próximo e marca o critério como "Verificar".
Configuração: `AgroCert:Ambiental:CoberturaMapBiomas:Pasta`, `AgroCert:Ambiental:UnidadesConservacao:Pasta`
e `RaioAmortecimentoM` (padrão 3.000 m, CONAMA 428/2010). As imagens Sentinel-2 ficam guardadas em `uploads/conformidade`.

### Clima e El Niño (integração com a api-clima)
As telas ficam em `/clima` e buscam os dados na **api-clima** (NestJS, pasta `src/api-clima`). Quem chama a API
é o servidor do portal, então o navegador não precisa acessar a porta 3030 (sem CORS).

- **Previsão do ano**: chuva e temperaturas mês a mês com faixa min–máx, efeito do El Niño/La Niña, riscos
  (seca, excesso de chuva, calor, geada) e anos parecidos. Aceita o ONI oficial da NOAA/IRI no campo "ONI oficial".
- **El Niño / La Niña**: situação atual do ONI, projeção de 12 meses e o mapa de impacto de cada fase nesta região.
- **Validação do modelo**: quanto o ENSO reduz o erro em relação à média e à regra antiga.
- **Base de dados**: problemas na tabela `dados_climaticos` e botão para simular/aplicar a correção do ENSO.

Para testar: suba a API (`npm run start:dev` em `src/api-clima`, porta 3030) e rode o portal (F5).
Se a API estiver fora do ar, a tela explica o que fazer. Configuração no `appsettings.json`:

```json
"Clima": { "ApiUrl": "http://localhost:3030", "TimeoutSegundos": 60, "Acesso": { "ExigirLogin": false, "Perfis": [] } }
```

No portal real: `builder.Services.AddModuloClima(builder.Configuration);` e `<partial name="_MenuClima" />` no layout.
Na AWS, `Clima:ApiUrl` aponta para o endereço interno da api-clima.

### Qualidade
- Testes: `dotnet test` (175 casos: conformidade por certificação, desmatamento por data de corte,
  geometria, MapBiomas, NDVI, clima).
- CI: `.github/workflows/ci.yml` compila o C#, roda os testes e compila o Angular a cada push.

---

## 4. O que mudou em relação ao Java/React

- **Gestão de Certificações** (antigo Painel): os indicadores eram fixos (`mockData.js`); agora são calculados.
  O card "Status de Treinamentos" virou **Pendências de Vistoria**, porque o módulo de Treinamentos não veio
  no pacote. Os cards usavam campos inexistentes (`ncs`, `auditoria`, `auditor`) — corrigido.
- **Código da vistoria** (`VIST-001`…): o Java usava a contagem de registros e repetia código depois de uma
  exclusão (quebrando o índice único). Agora usa o maior número + 1.
- **Usuário**: a tabela `usuarios` do AgroCert foi trocada pelos dados do login do portal (Entra ID).
- **Excluir requisito** não apaga apontamentos de vistoria vinculados; só desfaz o vínculo.
- **Propriedade com geometria manual**: a área é calculada a partir do polígono.
- **Uploads** só aceitam imagens (JPG, PNG, GIF, WEBP) de até 15 MB.
- `requisitos-seed.json` (formato antigo) não é mais usado; a carga vem de `requisitos-consolidados.json`.

---

## 5. Roteiro de teste

1. **Gestão de Certificações**: 4 certificações, indicadores e ações prioritárias.
2. **Certificações Ativas**: cadastrar uma nova, abrir, editar categorias, excluir.
3. **Requisitos**: filtrar por certificação/status, editar (clique na linha), incluir referências, baixar a lista mestra (.xlsx).
4. **Vistorias**: abrir vistoria → registrar apontamentos com fotos → encerrar com evidência → **Relatório PDF** → Pendências.
5. **Ambiental**: cadastrar propriedade colando GeoJSON → ver mapa/camadas → (com credenciais) Ver alertas / Consultar NDVI → **Gerar relatório**.
6. **Fauna e Flora**: painel com mapa (aba Fauna/Flora, grupos, áreas), destaques, câmeras, gráficos →
   clicar no mapa → "Novo registro" com foto → catálogo de espécies (nova/editar/excluir) → lista de registros.
7. **Clima e El Niño** (com a api-clima rodando): previsão do ano → mudar o ano / informar ONI → abas
   El Niño/La Niña, Validação e Base de dados (simular e aplicar a correção do ENSO).
8. **Corredores Ecológicos**: trocar ano / "Comparar 2008 → 2025", vegetação e distância → clicar numa mata e numa fazenda →
   abas Por fazenda (ordenar, buscar, baixar CSV), Onde restaurar (clicar numa ligação) e Corredores →
   ícone de PDF numa fazenda → "Gerar PDF".
9. **Relatório de conformidade**: Ambiental → abrir um imóvel → "Relatório de conformidade" → conferir parecer,
   áreas convertidas (antes × depois) e UCs → "Gerar PDF".
