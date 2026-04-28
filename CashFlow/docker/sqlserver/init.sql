-- init.sql
-- Executado uma vez na inicialização do container SQL Server
-- Cria os dois bancos se ainda não existirem

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'cashflow_lancamentos')
BEGIN
    CREATE DATABASE cashflow_lancamentos;
    PRINT 'Banco cashflow_lancamentos criado.';
END

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'cashflow_consolidado')
BEGIN
    CREATE DATABASE cashflow_consolidado;
    PRINT 'Banco cashflow_consolidado criado.';
END

-- Garante que o usuário SA tem acesso a ambos os bancos
ALTER SERVER ROLE sysadmin ADD MEMBER sa;
GO
