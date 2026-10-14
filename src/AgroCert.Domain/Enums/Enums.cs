namespace AgroCert.Domain.Enums;

public enum TipoVistoria
{
    MENSAL_CONSULTORIA,
    DIARIA_FUNCIONARIO
}

public enum GrauRisco
{
    CRITICO,
    ALTO,
    MEDIO,
    BAIXO
}

public enum StatusApontamento
{
    ABERTO,
    ENCERRADO
}

public enum StatusAlerta
{
    NECESSITA_VERIFICACAO,
    VERIFICADO_MANEJO,
    VERIFICADO_IRREGULAR,
    DESCARTADO
}

public enum Reino
{
    FAUNA,
    FLORA
}

public enum OrigemRegistro
{
    AVISTAMENTO,
    CAMERA,
    VESTIGIO,
    COLETA
}
