namespace AgenteDePedidos;

// Tipos que passam entre a function e os commands. Tudo vira JSON no histórico
// da execução, por isso só tipos simples: string, int, bool e listas.

public record PedidoRecebido(string PedidoId, string Cliente, string Texto);

public record ItemPedido(string Sku, int Quantidade);

public record SaldoEstoque(string Sku, bool Existe, int Saldo);

public record Validacao(bool Valido, string Motivo);

public record Revisao(bool Aprovado, List<ItemPedido>? Itens, string Revisor);

public static class Eventos
{
    // Disparado pela pessoa que revisa o pedido: POST /api/v1/fire/PEDIDO_REVISADO/{pedidoId}
    public const string PedidoRevisado = "PEDIDO_REVISADO";
}
