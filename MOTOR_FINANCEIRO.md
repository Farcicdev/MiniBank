# Motor financeiro — primeira versão

Somente dinheiro fictício. A estrutura continua Controller → Service → DbContext.
Há idempotência nas transferências; depósitos continuam sem idempotência. Não há autenticação.

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
Idempotency-Key: transferencia-exemplo-001
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
  → Serviço valida chave (obrigatória, até 128 caracteres), valor e identificadores
  → Gera SHA-256 dos dados com cultura invariável
  → BEGIN READ COMMITTED
  → Bloqueio transacional pela chave (pg_advisory_xact_lock)
  → Consulta a chave: hash igual retorna operação anterior; diferente retorna 409
  → SELECT das duas contas ORDER BY Id FOR UPDATE
  → Verifica existência e saldo com os bloqueios adquiridos
  → Domínio cria operação + débito + crédito e aplica os lançamentos aos saldos
  → SaveChangesAsync persiste saldos, operação, lançamentos e registro de idempotência
  → COMMIT verifica constraints diferidas do ledger
  → Controller retorna 200 com o record de resposta

Erro antes do commit → descarte da transação → rollback de todas as alterações
```

O depósito segue o fluxo financeiro sem chave, hash ou bloqueio de idempotência,
debitando a compensação e creditando o cliente.
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

- Não foi criada uma suíte de testes nem executada validação de integração. Build não comprova
  execução dos triggers, rollback, migração de dados ou comportamento concorrente
  em PostgreSQL; esses cenários ainda precisam de validação manual em banco de desenvolvimento.
- Transferências exigem reutilizar a mesma chave e os mesmos dados quando a resposta
  se perde. Uma chave nova representa outra transferência. Depósitos ainda podem
  duplicar se repetidos. Não há retry automático de transações.
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


## Idempotência das transferências

A chave é global, sensível a maiúsculas e não é normalizada pelo serviço. Use um
UUID por intenção de transferência e preserve-o nas repetições. Ausência, vazio,
somente espaços ou mais de 128 caracteres retornam 400. O limite é validado no
serviço, sem alterar a coluna `text` nem as migrations aplicadas.

`IdempotenciaHash.Gerar` preserva a versão `transferencia:v1`, UUIDs no formato `D`
e valor `F2` com cultura invariável. `10`, `10.0` e `10.00` produzem o mesmo hash.
O serviço valida o dinheiro antes do hash para não aceitar arredondamento de
frações de centavo. A chave identifica a intenção; o hash identifica os dados.

A ordem é: **bloqueio da chave → contas em ordem de UUID → commit**.
`pg_advisory_xact_lock(hashtextextended(chave, 0))` funciona entre conexões e
instâncias da API no mesmo banco, mesmo antes de existir uma linha para a chave.
Ele é liberado ao encerrar a transação. Uma colisão do hash de 64 bits do bloqueio
apenas faz chaves distintas esperarem; a consulta continua comparando a chave
completa. Esse hash de bloqueio não substitui o SHA-256 do conteúdo.

O SELECT da chave é um comando separado, após adquirir o bloqueio. Em
`READ COMMITTED`, ele enxerga o commit de quem acabou de liberar o bloqueio.
Se a primeira tentativa desfez a transação, não há registro e a próxima pode
executar. Se confirmou, a próxima retorna a operação original com os lançamentos,
sem verificar novamente o saldo. Hash diferente retorna 409 antes de alterar contas.
Os lançamentos da resposta são ordenados por natureza, mantendo débito antes de crédito.

Todos os caminhos da API adquirem no máximo um bloqueio de chave e seguem a mesma
ordem de contas; nenhum adquire uma chave depois de bloquear contas. Isso evita
ciclos de espera entre esses fluxos. SQL externo que ignore esse protocolo pode
introduzir deadlocks. O índice UNIQUE continua obrigatório como proteção adicional.

O tratamento de `23505` é restrito a `IX_IdemPotencias_Chave`. Antes da consulta
de recuperação, o escopo de `ExecutarAsync` já descartou a transação inteira e
seus efeitos; o ChangeTracker é limpo. A leitura ocorre fora dessa transação e
compara o hash do resultado confirmado. Não há nova tentativa de movimentar saldo.
Outras violações de unicidade não são confundidas com repetição de transferência.
Se o registro vencedor não existir (por exemplo, exclusão externa), o erro original
é preservado; não se inventa um sucesso.

Até a última verificação antes do commit, o cancelamento pode desfazer a tentativa.
Iniciado o commit da transferência, usa-se `CancellationToken.None` para não
interrompê-lo por desconexão HTTP. Isso não impede falhas de rede com o banco ou
timeouts: o resultado pode ficar desconhecido ao cliente. Repita a mesma chave e
os mesmos dados para recuperar um eventual commit. Uma operação já confirmada
não é desfeita pelo cancelamento da resposta. Depósitos mantêm sua semântica anterior.

Somente sucessos são armazenados; recusas como saldo insuficiente não reservam a
chave. Não há expiração, escopo por usuário ou proteção contra exclusão administrativa
dos registros. A garantia depende de preservá-los e do uso deste protocolo por
todos os escritores. Não há nova migration nesta alteração.

### Validação manual

Veja `MiniBank.Api/MiniBank.Api.http` para criação de contas, depósito, primeira
transferência, repetição e conflito. Substitua os IDs pelas contas criadas e garanta
saldo suficiente. As duas primeiras transferências devem retornar 200 com o mesmo
ID, data e lançamentos; a terceira retorna 409. Confira os saldos antes e depois.
Teste também header ausente, vazio e maior que 128 caracteres (400).

Para duas requisições simultâneas, use Bash, com IDs reais e uma chave ainda não usada:

```bash
api=http://localhost:5032
origem=SUBSTITUA_PELO_UUID_DA_ORIGEM
destino=SUBSTITUA_PELO_UUID_DO_DESTINO
chave=$(cat /proc/sys/kernel/random/uuid)
corpo=$(printf '{"contaOrigemId":"%s","contaDestinoId":"%s","valor":10.00}' "$origem" "$destino")
for tentativa in 1 2; do
  curl --silent --show-error --write-out '\nHTTP %{http_code}\n' \
    -H 'Content-Type: application/json' -H "Idempotency-Key: $chave" \
    --data "$corpo" "$api/api/transferencias" > "/tmp/minibank-$chave-$tentativa.txt" &
done
wait
cat "/tmp/minibank-$chave-1.txt" "/tmp/minibank-$chave-2.txt"
```

Espere dois 200, uma única operação e variação de apenas 10,00 em cada saldo.
Com outra chave nova, repita em paralelo usando valores distintos: espere um 200
e um 409, sem pressupor qual chega primeiro. Teste também mesmas contas com chaves
diferentes e transferências em sentidos opostos para verificar os bloqueios de contas.
Para forçar espera observável, uma sessão SQL de desenvolvimento pode manter aberto
`BEGIN; SELECT pg_advisory_xact_lock(hashtextextended('chave-do-teste', 0));` enquanto
as duas chamadas usam essa chave; `ROLLBACK;` libera o bloqueio sem alterar dados.

A concorrência, o fallback UNIQUE e a perda de conexão durante o commit precisam
ser validados em PostgreSQL real: os exemplos não são evidência de execução.
A implementação não promete ausência de deadlocks com escritores externos nem
disponibilidade ilimitada sob contenção.

Referências técnicas: [advisory locks e ordem de bloqueios](https://www.postgresql.org/docs/17/explicit-locking.html),
[visibilidade em READ COMMITTED](https://www.postgresql.org/docs/17/transaction-iso.html)
e [transações do EF Core](https://learn.microsoft.com/en-us/ef/core/saving/transactions).


Validação executada nesta alteração: `dotnet build` passou sem erros, com aviso
`NU1900` por indisponibilidade da consulta de vulnerabilidades do NuGet, inclusive
na tentativa fora do sandbox. Uma verificação temporária fora do repositório usou
o próprio `IdempotenciaHash.cs`: 16 combinações de `pt-BR`, `en-US`, `de-DE`, `tr-TR`
e `10m`, `10.0m`, `10.00m`, `10.000m` produziram o mesmo hash; inverter contas ou
alterar o valor para `10.01m` produziu hash diferente. Nenhum dado foi alterado.
