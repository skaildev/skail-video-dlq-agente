namespace AgenteDePedidos;

// Toda classe de exceção que cruza um command precisa do construtor (string message):
// é com ele que o runtime reconstrói a exceção a partir do histórico na retomada.

/// <summary>O provedor do LLM respondeu 429, 503 ou 529. Vale esperar e tentar de novo.</summary>
public class ProvedorIndisponivelException : Exception
{
    public ProvedorIndisponivelException(string message) : base(message) { }
}

/// <summary>Usada só no antipadrão: falha de negócio lançada como exceção.</summary>
public class PedidoInvalidoException : Exception
{
    public PedidoInvalidoException(string message) : base(message) { }
}
