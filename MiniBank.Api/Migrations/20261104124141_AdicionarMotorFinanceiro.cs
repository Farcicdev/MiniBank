using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniBank.Api.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarMotorFinanceiro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Falhar antes da conversão evita arredondamento ou perda de valores existentes.
            // O lock protege também o intervalo entre esta verificação e o backfill.
            migrationBuilder.Sql("""
                LOCK TABLE "Contas" IN ACCESS EXCLUSIVE MODE;
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "Contas"
                        WHERE "Saldo" < 0 OR "Saldo" > 9999999999999999.99
                            OR "Saldo" <> round("Saldo", 2))
                        OR (SELECT COALESCE(sum("Saldo"), 0) FROM "Contas") > 9999999999999999.99 THEN
                        RAISE EXCEPTION 'Saldos existentes incompatíveis com numeric(18,2); nenhuma conversão será realizada.';
                    END IF;
                    IF EXISTS (SELECT 1 FROM "Contas"
                        WHERE "Id" = '00000000-0000-0000-0000-000000000001'
                            OR "Numero" = '__COMPENSACAO_DEPOSITOS__') THEN
                        RAISE EXCEPTION 'Identificador ou número reservado à compensação já está em uso.';
                    END IF;
                END $$;
                """);

            migrationBuilder.AlterColumn<decimal>(
                name: "Saldo",
                table: "Contas",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AddColumn<bool>(
                name: "EhCompensacao",
                table: "Contas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "OperacoesFinanceiras",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    ContaDebitoId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContaCreditoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    DataUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperacoesFinanceiras", x => x.Id);
                    table.CheckConstraint("CK_Operacoes_Compensacao", "\"ContaCreditoId\" <> '00000000-0000-0000-0000-000000000001'\nAND ((\"Tipo\" IN (1, 3) AND \"ContaDebitoId\" = '00000000-0000-0000-0000-000000000001')\n    OR (\"Tipo\" = 2 AND \"ContaDebitoId\" <> '00000000-0000-0000-0000-000000000001'))");
                    table.CheckConstraint("CK_Operacoes_Contas", "\"ContaDebitoId\" <> \"ContaCreditoId\"");
                    table.CheckConstraint("CK_Operacoes_Tipo", "\"Tipo\" IN (1, 2, 3)");
                    table.CheckConstraint("CK_Operacoes_Valor", "\"Valor\" > 0");
                    table.ForeignKey(
                        name: "FK_OperacoesFinanceiras_Contas_ContaCreditoId",
                        column: x => x.ContaCreditoId,
                        principalTable: "Contas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OperacoesFinanceiras_Contas_ContaDebitoId",
                        column: x => x.ContaDebitoId,
                        principalTable: "Contas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Lancamentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OperacaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Natureza = table.Column<int>(type: "integer", nullable: false),
                    Valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lancamentos", x => x.Id);
                    table.CheckConstraint("CK_Lancamentos_Natureza", "\"Natureza\" IN (1, 2)");
                    table.CheckConstraint("CK_Lancamentos_Valor", "\"Valor\" > 0");
                    table.ForeignKey(
                        name: "FK_Lancamentos_Contas_ContaId",
                        column: x => x.ContaId,
                        principalTable: "Contas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lancamentos_OperacoesFinanceiras_OperacaoId",
                        column: x => x.OperacaoId,
                        principalTable: "OperacoesFinanceiras",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Contas_SaldoETipo",
                table: "Contas",
                sql: "(\"EhCompensacao\" AND \"Id\" = '00000000-0000-0000-0000-000000000001'\n    AND \"Numero\" = '__COMPENSACAO_DEPOSITOS__' AND \"Saldo\" <= 0)\nOR (NOT \"EhCompensacao\" AND \"Id\" <> '00000000-0000-0000-0000-000000000001'\n    AND \"Saldo\" >= 0)");

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_ContaId",
                table: "Lancamentos",
                column: "ContaId");

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_OperacaoId_Natureza",
                table: "Lancamentos",
                columns: new[] { "OperacaoId", "Natureza" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperacoesFinanceiras_ContaCreditoId",
                table: "OperacoesFinanceiras",
                column: "ContaCreditoId");

            migrationBuilder.CreateIndex(
                name: "IX_OperacoesFinanceiras_ContaDebitoId",
                table: "OperacoesFinanceiras",
                column: "ContaDebitoId");

            migrationBuilder.Sql("""
                INSERT INTO "Contas" ("Id", "Numero", "Saldo", "EhCompensacao")
                SELECT '00000000-0000-0000-0000-000000000001', '__COMPENSACAO_DEPOSITOS__',
                    -COALESCE(sum("Saldo"), 0), true FROM "Contas";

                -- SaldoInicial identifica a abertura do ledger, não inventa um histórico anterior.
                DO $$
                DECLARE
                    conta record;
                    operacao_id uuid;
                BEGIN
                    FOR conta IN SELECT "Id", "Saldo" FROM "Contas" WHERE NOT "EhCompensacao" AND "Saldo" > 0 LOOP
                        operacao_id := gen_random_uuid();
                        INSERT INTO "OperacoesFinanceiras"
                            ("Id", "Tipo", "ContaDebitoId", "ContaCreditoId", "Valor", "DataUtc")
                        VALUES (operacao_id, 3, '00000000-0000-0000-0000-000000000001',
                            conta."Id", conta."Saldo", CURRENT_TIMESTAMP);

                        INSERT INTO "Lancamentos" ("Id", "OperacaoId", "ContaId", "Natureza", "Valor")
                        VALUES
                            (gen_random_uuid(), operacao_id, '00000000-0000-0000-0000-000000000001', 1, conta."Saldo"),
                            (gen_random_uuid(), operacao_id, conta."Id", 2, conta."Saldo");
                    END LOOP;
                END $$;

                CREATE FUNCTION minibank_impedir_alteracao_ledger() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION 'Operações e lançamentos são imutáveis.' USING ERRCODE = '23514';
                END $$;

                CREATE TRIGGER "TR_Operacoes_Imutaveis"
                    BEFORE UPDATE OR DELETE ON "OperacoesFinanceiras"
                    FOR EACH ROW EXECUTE FUNCTION minibank_impedir_alteracao_ledger();
                CREATE TRIGGER "TR_Lancamentos_Imutaveis"
                    BEFORE UPDATE OR DELETE ON "Lancamentos"
                    FOR EACH ROW EXECUTE FUNCTION minibank_impedir_alteracao_ledger();

                CREATE FUNCTION minibank_validar_partidas() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE
                    operacao_id uuid;
                    operacao "OperacoesFinanceiras"%ROWTYPE;
                    quantidade bigint;
                    debitos numeric;
                    creditos numeric;
                    contas_corretas boolean;
                BEGIN
                    IF TG_TABLE_NAME = 'OperacoesFinanceiras' THEN
                        operacao_id := NEW."Id";
                    ELSE
                        operacao_id := NEW."OperacaoId";
                    END IF;

                    SELECT * INTO STRICT operacao FROM "OperacoesFinanceiras" WHERE "Id" = operacao_id;
                    SELECT count(*),
                        COALESCE(sum("Valor") FILTER (WHERE "Natureza" = 1), 0),
                        COALESCE(sum("Valor") FILTER (WHERE "Natureza" = 2), 0),
                        bool_and(CASE "Natureza"
                            WHEN 1 THEN "ContaId" = operacao."ContaDebitoId"
                            WHEN 2 THEN "ContaId" = operacao."ContaCreditoId"
                            ELSE false END)
                    INTO quantidade, debitos, creditos, contas_corretas
                    FROM "Lancamentos" WHERE "OperacaoId" = operacao_id;

                    IF quantidade <> 2 OR debitos <> operacao."Valor" OR creditos <> operacao."Valor"
                        OR contas_corretas IS DISTINCT FROM true THEN
                        RAISE EXCEPTION 'A operação deve possuir débito e crédito correspondentes e equilibrados.'
                            USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END $$;

                CREATE CONSTRAINT TRIGGER "TR_Operacoes_Partidas"
                    AFTER INSERT ON "OperacoesFinanceiras" DEFERRABLE INITIALLY DEFERRED
                    FOR EACH ROW EXECUTE FUNCTION minibank_validar_partidas();
                CREATE CONSTRAINT TRIGGER "TR_Lancamentos_Partidas"
                    AFTER INSERT ON "Lancamentos" DEFERRABLE INITIALLY DEFERRED
                    FOR EACH ROW EXECUTE FUNCTION minibank_validar_partidas();

                -- Saldo é uma projeção do ledger. A conferência ocorre no commit,
                -- depois que EF salva tanto as contas quanto os lançamentos.
                CREATE FUNCTION minibank_validar_saldo_ledger() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE
                    conta_id uuid;
                    saldo_atual numeric;
                    saldo_ledger numeric;
                BEGIN
                    IF TG_TABLE_NAME = 'Contas' THEN
                        conta_id := NEW."Id";
                    ELSE
                        conta_id := NEW."ContaId";
                    END IF;

                    SELECT "Saldo" INTO saldo_atual FROM "Contas" WHERE "Id" = conta_id;
                    SELECT COALESCE(sum(CASE "Natureza" WHEN 2 THEN "Valor" ELSE -"Valor" END), 0)
                        INTO saldo_ledger FROM "Lancamentos" WHERE "ContaId" = conta_id;
                    IF saldo_atual IS DISTINCT FROM saldo_ledger THEN
                        RAISE EXCEPTION 'Saldo da conta diverge dos lançamentos.' USING ERRCODE = '23514';
                    END IF;
                    RETURN NEW;
                END $$;

                CREATE CONSTRAINT TRIGGER "TR_Contas_SaldoLedger"
                    AFTER INSERT OR UPDATE ON "Contas" DEFERRABLE INITIALLY DEFERRED
                    FOR EACH ROW EXECUTE FUNCTION minibank_validar_saldo_ledger();
                CREATE CONSTRAINT TRIGGER "TR_Lancamentos_SaldoLedger"
                    AFTER INSERT ON "Lancamentos" DEFERRABLE INITIALLY DEFERRED
                    FOR EACH ROW EXECUTE FUNCTION minibank_validar_saldo_ledger();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "Rollback automático desabilitado para preservar o histórico financeiro. Use uma migration corretiva.");
        }
    }
}
