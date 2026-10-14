using System.Text.Json.Serialization;

namespace AgroCert.Certificacao.Clima;

public record PrevisaoAnoDto(
    int AnoAlvo,
    int AnoReferencia,
    int Ciclo,
    bool CicloDisponivel,
    double PesoCiclo,
    double NivelIntervalo,
    string Metodo,
    string Resumo,
    EnsoAnoDto EnsoAno,
    List<AnoAnalogoDto> AnosAnalogos,
    List<PrevisaoMesDto> Meses,
    List<string> Avisos,
    string? Margem);

public record EnsoAnoDto(double OniMedio, string FasePredominante, string Descricao);

public record AnoAnalogoDto(int Ano, double Distancia, double OniMedio, string Descricao);

public record PrevisaoMesDto(
    int Mes,
    int Ano,
    EnsoMesDto Enso,
    FaixaDto Chuva,
    FaixaDto Maxima,
    FaixaDto Minima,
    FaixaDto TempAmbiente,
    FaixaDto UmidadeRelativa,
    List<RiscoDto> Riscos,
    string? Margem);

public record EnsoMesDto(
    double Oni,
    double Incerteza,
    string Fase,
    string Intensidade,
    string Descricao,
    string Origem,
    double ProbElNino,
    double ProbLaNina);

public record FaixaDto(
    double Min,
    double Valor,
    double Max,
    double Normal,
    double EfeitoEnso,
    double? EfeitoCiclo,
    double Anomalia,
    double? AnomaliaPct,
    double ProbAbaixoNormal,
    double ProbDentroNormal,
    double ProbAcimaNormal,
    double? Observado);

public record RiscoDto(string Tipo, string Nivel, double Probabilidade, string Mensagem);

public record SituacaoEnsoDto(
    AnoMesDto UltimoMes,
    double Oni,
    string Fase,
    string Intensidade,
    string Episodio,
    [property: JsonPropertyName("tendencia_3_meses")] double Tendencia3Meses,
    string Resumo,
    [property: JsonPropertyName("ultimos_12_meses")] List<OniObservadoDto> Ultimos12Meses,
    [property: JsonPropertyName("proximos_12_meses")] List<OniProjetadoDto> Proximos12Meses,
    string? Observacao);

public record AnoMesDto(int Ano, int Mes);

public record OniObservadoDto(int Ano, int Mes, double? Oni, string? Fase);

public record OniProjetadoDto(int Ano, int Mes, double Oni, double Incerteza, string Fase, string Descricao);

public record ImpactoEnsoDto(string Periodo, string ComoLer, List<ImpactoLinhaDto> Linhas);

public record ImpactoLinhaDto(
    int Mes,
    string Variavel,
    string Nome,
    double Normal,
    double TendenciaPorDecada,
    double ElNinoModerado,
    double ElNinoForte,
    double LaNinaModerada,
    double? ElNinoModeradoPct,
    double? LaNinaModeradaPct,
    [property: JsonPropertyName("t_el_nino")] double TElNino,
    [property: JsonPropertyName("t_la_nina")] double TLaNina,
    string EvidenciaElNino,
    string EvidenciaLaNina,
    int AnosElNino,
    int AnosLaNina,
    int N,
    [property: JsonPropertyName("r2")] double R2);

public record ValidacaoDto(
    string Metodo,
    int AntecedenciaOniMeses,
    string Anos,
    string ComoLer,
    Dictionary<string, ResumoValidacaoDto> PorVariavel,
    Dictionary<string, ValidacaoMesDto> PorMes,
    Dictionary<string, ResumoValidacaoDto> PorFase);

public record ResumoValidacaoDto(
    int Meses,
    double ErroMedioClimatologia,
    double ErroMedioMetodoAntigo,
    double ErroMedioEnsoObservado,
    double ErroMedioEnsoPrevisto,
    double GanhoVsClimatologiaPct,
    double GanhoPrevistoVsClimatologiaPct,
    double GanhoVsMetodoAntigoPct,
    [property: JsonPropertyName("cobertura_intervalo_80")] double CoberturaIntervalo80);

public record ValidacaoMesDto(double ErroClimatologia, double ErroEnso, double GanhoPct);

public record QualidadeDto(
    int Registros,
    string Periodo,
    int TotalProblemas,
    Dictionary<string, int> PorTipo,
    List<ProblemaDadoDto> Problemas);

public record ProblemaDadoDto(string Tipo, int Ano, int Mes, int? Id, string Detalhe);

public record CorrecaoEnsoDto(
    bool Simulacao,
    int RegistrosCorrigidos,
    int RegistrosACorrigir,
    List<AlteracaoEnsoDto> Alteracoes);

public record AlteracaoEnsoDto(int? Id, int Ano, int Mes, string Detalhe);

public record ParametrosPrevisao(int Ano, string? Oni, int Ciclo, double PesoCiclo, int Nivel);
