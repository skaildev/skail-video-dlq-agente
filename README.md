# Dead letter de agente: quando o LLM falha 5 vezes seguidas

Código de apoio do vídeo do canal da skail. É um agente que lê um pedido recebido por e-mail, extrai os itens com um LLM, confere estoque e regra de negócio, grava no ERP e responde o cliente. Ele roda no skail e serve para provocar os quatro tipos de falha do vídeo e ver cada um no Monitor.

O LLM, o estoque, o ERP e o e-mail são serviços simulados (`src/Simuladores`), para que cada falha aconteça na hora em que você quiser.

## O que tem aqui

- `src/AgenteDePedidos`: o workload `dlqagente:v1.0.0`, com duas functions.
  - `ProcessarPedido` trata cada tipo de falha do jeito que o vídeo mostra.
  - `ProcessarPedidoRetryPadrao` usa o retry padrão nos commands e lança exceção para falha de negócio. Serve para ver no Monitor o que vai parar na dead letter queue sem precisar. Só a function dela tem `retryCount: 3` em vez dos 15 padrão: com o padrão, as reentregas têm intervalo crescente e a execução leva horas para chegar na DLQ.
- `src/Simuladores`: API em `http://localhost:5080` com a caixa de entrada, o LLM, o estoque, a média do cliente, o ERP (idempotente pelo header `Idempotency-Key`), a fila de revisão e o e-mail.
- `scripts`: um script por bloco do vídeo, em PowerShell (`.ps1`) e bash (`.sh`), mais os scripts para disparar uma execução, trocar o modo dos simuladores e disparar a revisão.

## Commands do agente

| Command | O que faz | Tentativas |
|---|---|---|
| `LerPedido` | Lê o pedido na caixa de entrada | padrão (5) |
| `ExtrairItensComLlm` | Chama o LLM; a exceção chega na function na primeira falha | 0 |
| `ExtrairItensComLlmRetryPadrao` | Mesma chamada, com o retry padrão | padrão (5) |
| `ConsultarEstoque` | Confere se cada SKU existe e o saldo | padrão (5) |
| `ValidarPedido` | Devolve inválido para SKU inexistente ou quantidade acima de 10 vezes a média do cliente (a média simulada é 80 caixas, então o limite é 800) | padrão (5) |
| `ValidarPedidoComExcecao` | Lança exceção para SKU inexistente (antipadrão) | padrão (5) |
| `AvisarRevisao` | Coloca o pedido na fila de revisão | padrão (5) |
| `DevolverAoCliente` | Avisa o cliente que o pedido não foi processado | padrão (5) |
| `CriarPedidoNoErp` | Grava o pedido no ERP com o id do pedido como chave de idempotência | padrão (5) |
| `ResponderCliente` | Manda a confirmação por e-mail | padrão (5) |

## Pré-requisitos

- .NET 10 SDK.
- O pacote de build do skail (`Skail.Platform.Runtime.Build` 2.3.1) roda uma ferramenta feita para o .NET 8. Se a máquina tiver só o .NET 10, rode o build com `DOTNET_ROLL_FORWARD=Major`, ou instale o runtime do .NET 8.
- Conta no [dash.skail.dev](https://dash.skail.dev) com um environment e o workload `dlqagente` versão `1.0.0` criado em Workloads.

## Como rodar

1. Copie `.env.example` para `.env` e preencha `SKAIL_SIDECAR`, `SKAIL_NAMESPACE` e `SKAIL_KEY` com os valores da aba Conectar do environment.
2. Suba os simuladores num terminal:

   ```bash
   dotnet run --project src/Simuladores
   ```

3. Suba o agente em outro terminal, com as variáveis do `.env` carregadas:

   ```bash
   # bash
   set -a; source .env; set +a
   DOTNET_ROLL_FORWARD=Major dotnet run --project src/AgenteDePedidos
   ```

   ```powershell
   # PowerShell
   Get-Content .env | Where-Object { $_ -match '^\s*[^#].*=' } | ForEach-Object { $n, $v = $_ -split '=', 2; Set-Item "env:$($n.Trim())" $v.Trim() }
   $env:DOTNET_ROLL_FORWARD = "Major"
   dotnet run --project src/AgenteDePedidos
   ```

4. Rode o script do bloco que quiser ver, num terceiro terminal.

## Os quatro blocos do vídeo

| Bloco | Script | O que acontece |
|---|---|---|
| 1. Provedor do LLM fora do ar | `bloco1-provedor-fora` | O LLM responde 503. As execuções `ProcessarPedidoRetryPadrao` gastam as tentativas e vão para a dead letter queue. As `ProcessarPedido` ficam aguardando o delay de 2 minutos e concluem quando o LLM volta (`modo llm normal`). As que caíram na DLQ voltam com "Retentar tudo que casa com o filtro". |
| 2. Resposta inválida que se repete | `bloco2-resposta-invalida` | O LLM devolve o SKU 88412-X, que não existe. A versão com retry padrão repete a mesma exceção até cair na DLQ. A versão tratada fica aguardando o evento `PEDIDO_REVISADO`, que o `revisar` dispara. |
| 3. Falha depois da ação | `bloco3-falha-depois-da-acao` | O pedido é criado no ERP e o e-mail estoura o timeout. Depois de `modo email normal`, o Retentar continua de `ResponderCliente`, e o ERP continua com um pedido só. |
| 4. Sucesso errado | `bloco4-sucesso-errado` | O LLM lê 100 caixas como 1.000. A versão sem regra de negócio grava 1.000 no ERP e nada aparece na DLQ. A versão tratada barra na regra (1.000 passa do limite de 800) e espera a revisão. Os itens aprovados na revisão passam de novo pelo estoque e pela regra antes de ir para o ERP. |

Os scripts usam ids fixos (PED-2101, PED-2202 e assim por diante), e o ERP usa o id do pedido como chave de idempotência. Antes de repetir uma tomada, rode `./scripts/reset.sh` (ou `.\scripts\reset.ps1`) para zerar os simuladores.

Para conferir o lado de fora: `http://localhost:5080/erp/pedidos`, `/emails`, `/revisoes` e `/controle`.

## Modos dos simuladores

```bash
./scripts/modo.sh llm normal            # resposta correta
./scripts/modo.sh llm fora              # 503
./scripts/modo.sh llm sobrecarga        # 529
./scripts/modo.sh llm limite            # 429 com retry-after: 30
./scripts/modo.sh llm sku-invalido      # troca o primeiro SKU por 88412-X
./scripts/modo.sh llm quantidade-errada # multiplica a primeira quantidade por 10
./scripts/modo.sh email timeout         # o e-mail demora 15 s; o agente desiste em 5 s
./scripts/modo.sh email normal
```

## Documentação do skail

- [Retries e idempotência](https://docs.skail.dev/construa/conceitos/retries-e-idempotencia)
- [Esperar um evento com prazo](https://docs.skail.dev/construa/como-fazer/esperar-um-evento-com-prazo)
- [Monitor](https://docs.skail.dev/referencia/portal/monitor)
