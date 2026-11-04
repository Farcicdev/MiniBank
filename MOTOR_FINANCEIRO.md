# Motor financeiro — primeira versão

Somente dinheiro fictício. A estrutura continua Controller → Service → DbContext.
Não há autenticação nem idempotência nesta versão.

## Endpoints

Depósito simulado em uma conta pública existente:

```http
POST /api/contas/11111111-1111-1111-1111-111111111111/depositos
Content-Type: application/json

{
  "valor": 100.00
}
```

Transferência entre contas públicas existentes:

```http
POST /api/transferencias
Content-Type: application/json

{
  "contaOrigemId": "11111111-1111-1111-1111-111111111111",
  "contaDestinoId": "22222222-2222-2222-2222-222222222222",
  "valor": 25.50
}
```

Substitua os IDs pelos retornados por `POST /api/contas`. Valores usam `decimal`
e `numeric(18,2)`, com máximo de `9999999999999999.99`. Valores com frações menores
que um centavo são rejeitados, não arredondados. Os saldos também respeitam esse limite.

Os dois endpoints retornam **200 OK** após o commit, com o ID da operação, tipo,
contas de débito/crédito, valor, data UTC e os dois lançamentos. São comandos
concluídos; não é fornecida uma Location para uma rota de consulta inexistente.

| Situação | HTTP |
| --- | --- |
| Valor inválido, ID vazio ou mesma conta | 400 |
| Conta inexistente ou tentativa de usar a compensação diretamente | 404 |
| Saldo insuficiente ou limite de saldo excedido | 409 |
| Falha interna de persistência/configuração | 500 com mensagem genérica |

O cliente não informa saldo, data, ID da operação, natureza dos lançamentos ou tipo
de operação. Erros internos são registrados no servidor, sem detalhes na resposta.

## Fluxo de transferência

```text
POST /api/transferencias
  → Controller recebe o record e chama o serviço
  → Serviço valida valor e identificadores
  → BEGIN READ COMMITTED
  → SELECT das duas contas ORDER BY Id FOR UPDATE
  → Verifica existência e saldo com os bloqueios adquiridos
  → Domínio cria operação + débito + crédito e aplica os lançamentos aos saldos
  → SaveChangesAsync persiste saldos, operação e lançamentos
  → COMMIT verifica constraints diferidas do ledger
  → Controller retorna 200 com o record de resposta

Erro antes do commit → descarte da transação → rollback de todas as alterações
```

O depósito segue o mesmo fluxo, debitando a compensação e creditando o cliente.
Todas as operações bloqueiam as contas na ordem de UUID definida pelo PostgreSQL.
Isso inclui a compensação: nenhuma operação usa uma ordem especial origem/destino.
Os bloqueios duram até o fim da transação. Uma transferência concorrente espera e
lê o saldo atualizado antes de decidir se pode gastar. Não há mutex em memória
nem confiança em uma consulta de saldo feita antes do bloqueio.

As consultas públicas de contas continuam com `AsNoTracking`. A consulta financeira
usa tracking porque as entidades serão modificadas. O DbContext é scoped, com uma
operação por requisição. A adição ao ChangeTracker é síncrona porque não faz I/O;
consultas, persistência, início, commit e descarte da transação são assíncronos.

## Ledger e saldo

Este é um subledger de natureza credora, não um plano contábil completo de um banco:

- Crédito aumenta o saldo da conta; débito diminui.
- Transferência de 25,50: débito 25,50 na origem e crédito 25,50 no destino.
- Depósito simulado de 100,00: débito 100,00 na compensação e crédito 100,00 no cliente.
- A compensação acumula saldo devedor, representado como saldo negativo nessa convenção.
  Apenas essa conta pode ficar negativa; ela não representa dinheiro real disponível.

A conta interna tem ID fixo `00000000-0000-0000-0000-000000000001`, número
`__COMPENSACAO_DEPOSITOS__` e é criada pela migration. Não aparece nos GETs públicos
nem pode ser escolhida como origem/destino pelos requests. Seu ID aparece no
lançamento de contrapartida da resposta de depósito para permitir rastreabilidade.

`OperacaoFinanceira` cria exatamente dois lançamentos de mesmo valor e os aplica
às contas por métodos de domínio; `Saldo` continua com setter privado. O registro
dos lançamentos não é um log independente da atualização do saldo.

O PostgreSQL reforça os invariantes:

- FKs com exclusão restrita; valores positivos; naturezas e tipos válidos;
  contas diferentes e compensação compatível com o tipo de operação.
- Índice UNIQUE por operação/natureza: no máximo um débito e um crédito.
- Constraint triggers `DEFERRABLE INITIALLY DEFERRED`: no commit, exigem dois
  lançamentos, contas correspondentes e débito = crédito = valor da operação.
- Outra constraint diferida exige `Saldo = SUM(créditos) - SUM(débitos)` por conta.
  Atualizar apenas o saldo ou apenas o ledger não pode confirmar.
- Triggers impedem UPDATE e DELETE em operações e lançamentos. Correções futuras
  devem usar novos lançamentos de estorno, funcionalidade ainda não implementada.

Assim, a soma dos saldos, incluindo a compensação, permanece zero. A data UTC fica
na operação e é compartilhada pelos lançamentos através da FK, sem duplicação.

## Migration e dados anteriores

`20261104124141_AdicionarMotorFinanceiro` foi criada; as migrations antigas foram
preservadas. Ela **não foi aplicada ao banco** durante esta implementação.

A migration obtém lock exclusivo em `Contas` e verifica os dados antes de converter
o saldo para `numeric(18,2)`. Saldo negativo, fração de centavo, excesso de capacidade
(inclusive no total para compensação) ou conflito com o identificador/número interno
interrompe a migration; ela não corrige nem arredonda esses dados silenciosamente.

Para cada saldo positivo anterior, cria uma operação `SaldoInicial`, creditando o
cliente e debitando a compensação sem alterar o saldo do cliente. A data é a da
abertura do ledger: não representa a data histórica de recebimento daqueles fundos.
Contas zeradas não precisam de lançamento de abertura.

A aplicação manual deve ocorrer em uma janela de manutenção, após revisão do SQL
e dos dados. A migration é transacional e requer PostgreSQL com `gen_random_uuid()`
nativo (13+). O método `Down` bloqueia rollback automático para não apagar o
histórico financeiro; correções exigem uma nova migration. `EnsureCreated` não
substitui as migrations, pois não instala os triggers e a conta de compensação.

## Limitações e próxima etapa manual

- Não foram implementados nem executados testes automatizados. Build não comprova
  execução dos triggers, rollback, migração de dados ou comportamento concorrente
  em PostgreSQL; esses cenários ainda precisam de validação manual em banco de desenvolvimento.
- Não há idempotência. Repetir um POST pode repetir a movimentação. Uma perda de
  conexão/resposta durante o commit pode deixar o resultado desconhecido ao cliente;
  não repetir automaticamente. Não há retry automático de transações.
- A compensação única serializa depósitos. A conferência de saldo soma o histórico
  da conta no commit; existem índices, mas o custo cresce com o volume de lançamentos.
- A ordem de locks evita ciclos entre estes fluxos. SQL externo, futuras rotinas
  com outra ordem ou mudanças de esquema podem introduzir contenção/deadlocks.
- Ainda não há extrato/consulta por operação, estorno, paginação do ledger, múltiplas
  moedas, integração financeira real ou autenticação. A API é para ambiente de aprendizado.
- Os triggers não substituem permissões de banco: um administrador pode desabilitá-los
  ou truncar tabelas. A gestão de permissões não foi implementada nesta etapa.

Na validação manual futura, conferir depósito e transferência válidos, entradas
inválidas, conta inexistente, saldo insuficiente, rollback e duas transferências
simultâneas cujo total exceda o saldo. Também conferir transferências em sentidos
opostos, saldo agregado zero e rejeição de escritas que desequilibrem o ledger.

Referências: [bloqueios no PostgreSQL](https://www.postgresql.org/docs/current/explicit-locking.html)
e [constraint triggers](https://www.postgresql.org/docs/16/sql-createtrigger.html).
