namespace AgroCert.Domain.Excecoes;

public class RegraNegocioException : Exception
{
    public RegraNegocioException(string mensagem) : base(mensagem) { }
}

public class NaoEncontradoException : RegraNegocioException
{
    public NaoEncontradoException(string mensagem) : base(mensagem) { }
}
